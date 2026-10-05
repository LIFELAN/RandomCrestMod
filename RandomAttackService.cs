using System;
using System.Collections.Generic;
using System.Reflection;
using GlobalEnums;
using GlobalSettings;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// Swaps the hero's active <see cref="HeroController.ConfigGroup"/> to a random crest right
/// before an attack is executed, then restores the equipped crest's own group once the attack
/// has finished. The direction the player pressed is preserved.
///
/// <para>Two things make this safe:</para>
/// <list type="bullet">
/// <item>The shared <c>cState.altAttack</c> flag is cleared on every swap. Otherwise a crest that
/// has an alternate slash can make the next crest look up a <c>null</c> alternate (for example a
/// down-spike <c>currentDownspike</c>), which throws and leaves the down-spike state (and its
/// disabled gravity) stuck forever.</item>
/// <item>The crest that was active before the swap keeps its <c>ActiveRoot</c> enabled, so any
/// object or FSM bound to it is never deactivated mid-use.</item>
/// </list>
/// </summary>
internal static class RandomAttackService
{
    private static readonly List<HeroController.ConfigGroup> Pool = new();
    private static readonly List<HeroController.ConfigGroup> Filtered = new();

    private static HeroController.ConfigGroup[]? _cachedConfigs;
    private static FieldInfo? _configsField;
    private static MethodInfo? _setConfigGroup;
    private static MethodInfo? _updateConfig;

    private static bool _pending;
    private static AttackDirection _pendingDir;
    private static bool _pendingWallSlide;
    private static bool _active;
    private static bool _nailArtActive;
    private static bool _bindActive;
    private static bool _bindWasBinding;
    private static bool _bindCancelSent;
    private static PlayMakerFSM? _bindFsm;
    private static bool _dashActive;
    private static float _dashLastActive;
    private static bool _tauntHold;
    private static bool _suppressConfigUpdated;
    private static PlayMakerFSM? _nailArtsFsm;

    private static HeroController.ConfigGroup? _originalGroup;
    private static float _activateTime;
    private static float _minHold;

    /// <summary>
    /// While <see cref="IsSpoofing"/> is true the <see cref="ToolCrest.IsEquipped"/> getter is
    /// forced to report only <see cref="SpoofCrest"/> as equipped (or nothing at all when the
    /// random pick is the default/Hunter moveset). PlayMaker's <c>CheckIfCrestEquipped</c> and the
    /// hero code both read that property, so the FSM branches match the config group we installed.
    /// </summary>
    internal static bool IsSpoofing { get; private set; }

    internal static ToolCrest? SpoofCrest { get; private set; }

    /// <summary>
    /// While true, the "HC CONFIG UPDATED" event is swallowed. Dash/sprint randomization swaps the
    /// config without letting the Sprint FSM globally cancel itself (which would strand the sprint).
    /// </summary>
    internal static bool SuppressConfigUpdated => _suppressConfigUpdated;

    /// <summary>Called from the <c>HeroController.Attack</c> prefix.</summary>
    internal static void RequestRandom(AttackDirection direction, bool wallSliding)
    {
        _pending = true;
        _pendingDir = direction;
        _pendingWallSlide = wallSliding;
    }

    /// <summary>
    /// Called from the <c>HeroController.UpdateConfig</c> postfix. Attack() calls UpdateConfig()
    /// just before it reads the config, so this is the correct moment to substitute the group.
    /// </summary>
    internal static void ApplyIfRequested(HeroController hero)
    {
        if (!_pending)
        {
            return;
        }

        _pending = false;

        try
        {
            if (!RandomCrestModPlugin.EnableRandomAttacks || hero == null)
            {
                return;
            }

            if (RandomCrestModPlugin.OnlyOnRandomCrest && !CrestService.IsRandomCrestEquipped())
            {
                return;
            }

            // Sprint / dash / air-dash are driven by the "Sprint" FSM. We used to skip random
            // attacks entirely here, which meant a normal / up / down slash during (or just out
            // of) a sprint never randomised. Swap *quietly* instead: firing "HC CONFIG UPDATED"
            // would globally cancel the Sprint FSM, but a quiet swap leaves it running (same
            // trick as the dash re-roll).
            var group = PickGroup(hero, _pendingDir, _pendingWallSlide);
            if (group == null)
            {
                return;
            }

            ApplyGroup(hero, group, quiet: IsSprintOrSkid(hero));

            _active = true;
            _activateTime = Time.time;
            _minHold = Mathf.Max(0.05f, group.Config != null ? group.Config.AttackDuration : 0.35f);

            var crestName = SpoofCrest != null ? SpoofCrest.name : "Default(Hunter)";
            RandomCrestModPlugin.Log(
                $"Random attack -> config='{(group.Config != null ? group.Config.name : "?")}' crest='{crestName}' dir={_pendingDir} (count {hero.cState.attackCount}).");
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("RandomAttackService.ApplyIfRequested failed: " + e);
        }
    }

    /// <summary>
    /// Called when a charge slash (蓄力斩) is about to fire. The Nail Arts FSM fetches its
    /// ChargeSlash object via GetHeroAttackObject and branches on CheckIfCrestEquipped, so the
    /// same random config + crest spoof applies here.
    /// </summary>
    internal static void RequestNailArt(HeroController hero)
    {
        if (_active || _nailArtActive)
        {
            return;
        }

        try
        {
            if (!RandomCrestModPlugin.EnableRandomAttacks || hero == null)
            {
                return;
            }

            if (RandomCrestModPlugin.OnlyOnRandomCrest && !CrestService.IsRandomCrestEquipped())
            {
                return;
            }

            var group = PickGroupForNailArt(hero);
            if (group == null)
            {
                return;
            }

            ApplyGroup(hero, group);
            _nailArtActive = true;
            _activateTime = Time.time;
            _minHold = 0.3f;

            var crestName = SpoofCrest != null ? SpoofCrest.name : "Default(Hunter)";
            RandomCrestModPlugin.Log($"Random charge slash -> config='{(group.Config != null ? group.Config.name : "?")}' crest='{crestName}'.");
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("RandomAttackService.RequestNailArt failed: " + e);
        }
    }

    /// <summary>
    /// Applies a random crest for a bind (缚丝). Unlike attacks, a bind also needs the crest's
    /// animation library and ActiveRoot (Beast's rage bind plays crest-specific clips), so this
    /// performs the full config swap, not just the IsEquipped spoof.
    /// </summary>
    internal static void ApplyForBind(HeroController hero)
    {
        if (_active || _nailArtActive || _bindActive || hero == null)
        {
            return;
        }

        // Random binds are only allowed while the mod's own crest is equipped (unless the player
        // turned the restriction off).
        if (RandomCrestModPlugin.OnlyOnRandomCrest && !CrestService.IsRandomCrestEquipped())
        {
            return;
        }

        try
        {
            var pool = GetPool(hero);
            if (pool.Count == 0)
            {
                return;
            }

            var group = pool[UnityEngine.Random.Range(0, pool.Count)];

            // Swapping the config normally fires "HC CONFIG UPDATED", which globally cancels the
            // Sprint FSM; during a sprint / post-release skid that strands the hero. Do a quiet swap
            // there so the Sprint FSM keeps running. The restore is postponed in Tick until the FSM
            // reaches Idle. (Trade-off: a bind started mid-skid may miss its crest bind state.)
            var quiet = IsSprintOrSkid(hero);
            ApplyGroup(hero, group, quiet);

            _bindActive = true;
            _bindWasBinding = false;
            _activateTime = Time.time;
            _minHold = 0.15f;

            var crestName = SpoofCrest != null ? SpoofCrest.name : "Default(Hunter)";
            RandomCrestModPlugin.Log($"Random bind -> config='{(group.Config != null ? group.Config.name : "?")}' crest='{crestName}'.");
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("RandomAttackService.ApplyForBind failed: " + e);
        }
    }

    internal static void EndBind(HeroController hero)
    {
        if (!_bindActive)
        {
            return;
        }

        // Restoring the config fires "HC CONFIG UPDATED", which would cancel the Sprint FSM if the
        // player is already sprinting / sliding. Postpone until the sprint ends (Tick handles it).
        if (IsSprintOrSkid(hero))
        {
            return;
        }

        Restore(hero);
    }

    /// <summary>
    /// Holds a crest's config group for the duration of a taunt (the Beast taunt's unique visual
    /// lives under the Warrior crest root, which only this swap enables). Quiet swap so the Sprint
    /// FSM is not cancelled - a taunt can only start on the ground. Released by
    /// <see cref="ReleaseTauntHold"/> when the taunt FSM returns to Idle.
    /// </summary>
    internal static void ApplyTauntHold(ToolCrest? crest)
    {
        if (crest == null)
        {
            return;
        }

        // Never clobber an attack / bind / dash that owns the config right now; a taunt cannot
        // legitimately start while one of those is running anyway.
        if (_active || _nailArtActive || _bindActive || _dashActive)
        {
            return;
        }

        try
        {
            var hero = HeroController.instance;
            if (hero == null)
            {
                return;
            }

            var group = FindGroup(hero, crest);
            if (group == null)
            {
                return;
            }

            ApplyGroup(hero, group, quiet: true);
            _tauntHold = true;
            _active = false;
            _nailArtActive = false;
            _bindActive = false;
            _activateTime = Time.time;

            RandomCrestModPlugin.Log($"Random taunt hold -> config='{(group.Config != null ? group.Config.name : "?")}'.");
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("RandomAttackService.ApplyTauntHold failed: " + e);
        }
    }

    /// <summary>Releases a crest config held for a taunt, restoring the real equipped crest.</summary>
    internal static void ReleaseTauntHold()
    {
        if (!_tauntHold)
        {
            return;
        }

        try
        {
            var hero = HeroController.instance;
            if (hero != null)
            {
                // Restore clears _tauntHold itself (it is part of its guard).
                Restore(hero);
                return;
            }
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("RandomAttackService.ReleaseTauntHold failed: " + e);
        }

        // No hero to restore onto (or restore threw): drop the hold so it cannot linger.
        _tauntHold = false;
    }

    /// <summary>
    /// True while the game's Bind FSM is in the Spell (Shaman) air-dive states. Used by the
    /// surface-water safety net: the reject branch only nudges the hero up once, but the Shaman
    /// Fall state keeps re-applying its own downward velocity, so without this the hero tunnels
    /// through the water and out of the scene.
    /// </summary>
    internal static bool IsShamanAirBind(HeroController hero)
    {
        try
        {
            _bindFsm ??= FSMUtility.LocateFSM(hero.gameObject, "Bind");
            var state = _bindFsm != null ? _bindFsm.ActiveStateName : null;
            return state == "Shaman Air" || state == "Shaman Fall";
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// While true, the Spell (Shaman) crest is reported as equipped so
    /// <c>SurfaceWaterRegion.OnTriggerEnter2D</c> takes its water-entry path instead of the reject
    /// branch. Set only for the duration of that call.
    /// </summary>
    internal static bool ForceSpellCrestForWater { get; set; }

    /// <summary>Cancels the game's Bind FSM (used to end a random bind stuck in water).</summary>
    private static void CancelBindFsm(HeroController hero)
    {
        try
        {
            _bindFsm ??= FSMUtility.LocateFSM(hero.gameObject, "Bind");
            if (_bindFsm != null && _bindFsm.ActiveStateName != "Idle")
            {
                _bindFsm.SendEvent("CANCEL");
            }
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("RandomAttackService.CancelBindFsm failed: " + e);
        }
    }

    /// <summary>Restores the equipped crest's config once the attack animation is done.</summary>
    internal static void Tick()
    {
        try
        {
            var hero = HeroController.instance;
            if (hero == null)
            {
                _active = false;
                _nailArtActive = false;
                _bindActive = false;
                _dashActive = false;
                _tauntHold = false;
                IsSpoofing = false;
                SpoofCrest = null;
                return;
            }

            // A taunt owns the crest config for its whole duration; only bail out on death / scene
            // transitions (the taunt service releases it when the FSM returns to Idle).
            if (_tauntHold)
            {
                var cs0 = hero.cState;
                if (cs0.dead || cs0.hazardDeath || cs0.hazardRespawning || cs0.transitioning)
                {
                    ReleaseTauntHold();
                }

                return;
            }

            TickDash(hero);

            if (!_active && !_nailArtActive && !_bindActive)
            {
                return;
            }

            var cs = hero.cState;

            // Never leave the crest spoof active through a death / respawn / scene transition.
            if (cs.dead || cs.hazardDeath || cs.hazardRespawning || cs.transitioning)
            {
                Restore(hero);
                return;
            }

            var elapsed = Time.time - _activateTime;

            if (_bindActive)
            {
                if (cs.isBinding)
                {
                    _bindWasBinding = true;
                }

                // The Spell (Shaman) air bind is allowed into surface water by the game, but the
                // vanilla bind cancels on entering it. Our swapped config misses that branch, so
                // cancel it ourselves - otherwise the hero sinks while stuck in the bind pose.
                if (cs.swimming && !_bindCancelSent)
                {
                    _bindCancelSent = true;
                    CancelBindFsm(hero);
                }

                // Never swap the config back while sprinting / dashing / skidding; wait for the
                // Sprint FSM to reach Idle (restoring fires "HC CONFIG UPDATED", which would
                // globally cancel the still-running Sprint FSM and strand the hero).
                var sprinting = IsSprintOrSkid(hero);
                if (!sprinting && ((_bindWasBinding && !cs.isBinding && elapsed >= _minHold) || elapsed > 30f))
                {
                    Restore(hero);
                }

                return;
            }

            if (_nailArtActive)
            {
                var nailFsm = GetNailArtsFsm(hero);
                var nailState = nailFsm != null ? nailFsm.ActiveStateName : null;
                var nailBusy = !string.IsNullOrEmpty(nailState) && nailState != "Inactive";

                if ((!nailBusy && elapsed >= _minHold) || elapsed > 4f)
                {
                    Restore(hero);
                }

                return;
            }

            // The crest-specific custom down slashes (Reaper/Witch/Toolmaster/Shaman) are driven by
            // the "Crest Attacks" FSM and can outlast Config.AttackDuration. That FSM sits in
            // "Idle" between attacks, so keep holding the random crest until it goes back to Idle.
            var crestFsm = hero.crestAttacksFSM;
            var crestState = crestFsm != null ? crestFsm.ActiveStateName : null;
            var crestBusy = !string.IsNullOrEmpty(crestState) && crestState != "Idle";

            // A sprint that we already randomised (quiet swap) owns the spoof now: keep it until
            // the Sprint FSM goes Idle, because restoring fires "HC CONFIG UPDATED" which would
            // cancel the still-running FSM and strand the hero. 5s timeout as a safety net.
            if (_dashActive && IsSprintOrSkid(hero) && elapsed < 5f)
            {
                return;
            }

            // If the player starts a *fresh* sprint / dash while a random attack is still
            // installed, put the real crest back before the Sprint FSM picks up its attack
            // object, otherwise the FSM and the active config disagree and the dash state can
            // strand. (When _dashActive already owns the sprint this is unnecessary and we would
            // be cancelling that very sprint.)
            if (!_dashActive && IsSprintOrDash(cs) && !crestBusy)
            {
                Restore(hero);
                return;
            }

            var busy = cs.attacking
                || cs.downAttacking
                || cs.upAttacking
                || cs.downSpikeAntic
                || cs.downSpiking
                || cs.downSpikeBouncing
                || cs.downSpikeRecovery
                || cs.isToolThrowing
                || IsSprintOrDash(cs)
                || crestBusy;

            if ((!busy && elapsed >= _minHold) || elapsed > 4f)
            {
                Restore(hero);
            }
        }
        catch (Exception e)
        {
            _active = false;
            RandomCrestModPlugin.LogError("RandomAttackService.Tick failed: " + e);
        }
    }

    private static void ApplyGroup(HeroController hero, HeroController.ConfigGroup group, bool quiet = false)
    {
        // Keep the first (player's) group as the restore target across repeated dash re-rolls.
        if (_originalGroup == null)
        {
            _originalGroup = hero.CurrentConfigGroup;
        }

        var previousSuppress = _suppressConfigUpdated;
        _suppressConfigUpdated = quiet;
        try
        {
            SetConfigGroup(hero, group);
        }
        finally
        {
            _suppressConfigUpdated = previousSuppress;
        }

        // Keep the player's own crest objects alive; never let the swap disable them.
        if (_originalGroup != null && _originalGroup.ActiveRoot != null && _originalGroup != group)
        {
            _originalGroup.ActiveRoot.SetActive(true);
        }

        // Avoid stale alternate-slot state leaking across crests (null alternate / null downspike).
        hero.cState.altAttack = false;

        // Make every crest check agree with the config group we just installed. Without this the
        // Sprint / Crest Attacks FSMs keep branching on the player's real crest while the hitbox
        // and animation come from a different one, which can strand the attack state.
        SpoofCrest = ResolveCrest(group.Config);
        IsSpoofing = true;
    }

    private static void Restore(HeroController hero)
    {
        if (!_active && !_nailArtActive && !_bindActive && !_dashActive && !_tauntHold)
        {
            return;
        }

        UpdateConfig(hero);
        hero.cState.altAttack = false;
        IsSpoofing = false;
        SpoofCrest = null;
        _originalGroup = null;
        _active = false;
        _nailArtActive = false;
        _bindActive = false;
        _bindWasBinding = false;
        _bindCancelSent = false;
        _dashActive = false;
        _tauntHold = false;
        RandomCrestModPlugin.Log($"Random attack/charge/bind/dash/taunt restored after {(Time.time - _activateTime):F2}s.");
    }

    private static HeroController.ConfigGroup? FindGroup(HeroController hero, ToolCrest crest)
    {
        try
        {
            var configs = GetConfigs(hero);
            if (configs == null)
            {
                return null;
            }

            var heroConfig = crest.HeroConfig;
            foreach (var group in configs)
            {
                if (group == null || group.Config == null)
                {
                    continue;
                }

                if (ReferenceEquals(group.Config, heroConfig)
                    || (heroConfig != null && group.Config.name == heroConfig.name))
                {
                    return group;
                }
            }
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("RandomAttackService.FindGroup failed: " + e.Message);
        }

        return null;
    }

    // ------------------------------------------------------------------ dash / sprint attacks

    private static void TickDash(HeroController hero)
    {
        if (!RandomCrestModPlugin.EnableRandomAttacks)
        {
            if (_dashActive)
            {
                Restore(hero);
            }

            return;
        }

        var cs = hero.cState;
        var sprintFsm = hero.sprintFSM;
        var sprintState = sprintFsm != null ? sprintFsm.ActiveStateName : null;

        // "isSprinting" goes false for a moment in the middle of a dash attack, so also treat the
        // Sprint FSM being out of its resting Idle state as "still sprinting".
        var sprintActive = cs.isSprinting
            || (!string.IsNullOrEmpty(sprintState) && sprintState != "Idle");

        if (!_dashActive)
        {
            if (_active || _nailArtActive || _bindActive)
            {
                return;
            }

            if (RandomCrestModPlugin.OnlyOnRandomCrest && !CrestService.IsRandomCrestEquipped())
            {
                return;
            }

            // Randomize the whole sprint so the Sprint FSM's cached attack objects and crest
            // branch all line up, instead of swapping mid-dash (which strands it). Only start on a
            // real sprint input; the hold below keeps it alive through the dash-attack blip.
            if (cs.isSprinting)
            {
                ApplyForDash(hero);
            }

            return;
        }

        if (sprintActive)
        {
            _dashLastActive = Time.time;
            return;
        }

        // A bind / attack now owns the spoof. Let its own logic release it: restoring here would
        // clear the crest spoof before the bind's later crest checks run (e.g. BindCompleted for
        // Beast rage / Reaper mode), and can also drop the Spell (Shaman) spoof before the water
        // region sees it, letting an air bind tunnel through surface water.
        if (_active || _nailArtActive || _bindActive)
        {
            return;
        }

        // Give the Sprint FSM a short grace period before putting the real crest back.
        if (Time.time - _dashLastActive > 0.25f || Time.time - _activateTime > 10f)
        {
            Restore(hero);
        }
    }

    private static void ApplyForDash(HeroController hero)
    {
        try
        {
            var pool = GetPool(hero);
            if (pool.Count == 0)
            {
                return;
            }

            var group = pool[UnityEngine.Random.Range(0, pool.Count)];

            // Quiet swap: install the config without firing "HC CONFIG UPDATED", so the Sprint FSM
            // is not globally cancelled, then repoint its cached dash objects at the new crest.
            ApplyGroup(hero, group, quiet: true);
            SetDashStabVariables(hero, group);

            _dashActive = true;
            _activateTime = Time.time;
            _dashLastActive = Time.time;

            var crestName = SpoofCrest != null ? SpoofCrest.name : "Default(Hunter)";
            RandomCrestModPlugin.Log($"Random dash -> config='{(group.Config != null ? group.Config.name : "?")}' crest='{crestName}'.");
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("RandomAttackService.ApplyForDash failed: " + e);
        }
    }

    /// <summary>
    /// Called from the <c>HeroController.IncrementAttackCounter</c> postfix. When a dash attack
    /// starts during a sprint, re-roll the crest for that attack (the quiet swap keeps the Sprint
    /// FSM running).
    /// </summary>
    internal static void OnAttackCounterForDash()
    {
        if (!_dashActive || _pending || !RandomCrestModPlugin.EnableRandomAttacks)
        {
            return;
        }

        try
        {
            var hero = HeroController.instance;
            if (hero == null)
            {
                return;
            }

            if (RandomCrestModPlugin.OnlyOnRandomCrest && !CrestService.IsRandomCrestEquipped())
            {
                return;
            }

            var pool = GetPool(hero);
            if (pool.Count == 0)
            {
                return;
            }

            var group = pool[UnityEngine.Random.Range(0, pool.Count)];
            ApplyGroup(hero, group, quiet: true);
            SetDashStabVariables(hero, group);
            _dashLastActive = Time.time;

            var crestName = SpoofCrest != null ? SpoofCrest.name : "Default(Hunter)";
            RandomCrestModPlugin.Log($"Random dash -> config='{(group.Config != null ? group.Config.name : "?")}' crest='{crestName}' (re-roll).");
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("RandomAttackService.OnAttackCounterForDash failed: " + e);
        }
    }

    private static void SetDashStabVariables(HeroController hero, HeroController.ConfigGroup group)
    {
        var fsm = hero.sprintFSM;
        if (fsm == null)
        {
            return;
        }

        try
        {
            var dash = fsm.FsmVariables.FindFsmGameObject("Dash Stab");
            if (dash != null)
            {
                dash.Value = group.DashStab;
            }

            var dashAlt = fsm.FsmVariables.FindFsmGameObject("Dash Stab Alt");
            if (dashAlt != null)
            {
                dashAlt.Value = group.DashStabAlt;
            }

            var dashCrt = fsm.FsmVariables.FindFsmGameObject("Dash Stab Crt");
            if (dashCrt != null)
            {
                dashCrt.Value = group.DashStab;
            }

            // These are normally re-read by the "Cancel All" state (via GetHeroConfigVariable).
            // Because the dash swap is quiet, refresh them by hand so the Single/Multiple branch,
            // speed and duration match the random crest.
            if (group.Config != null)
            {
                var attackSpeed = fsm.FsmVariables.FindFsmFloat("Attack Speed");
                if (attackSpeed != null)
                {
                    attackSpeed.Value = group.Config.DashStabSpeed;
                }

                var attackTime = fsm.FsmVariables.FindFsmFloat("Attack Time");
                if (attackTime != null)
                {
                    attackTime.Value = group.Config.DashStabTime;
                }

                var attackSteps = fsm.FsmVariables.FindFsmInt("Attack Steps");
                if (attackSteps != null)
                {
                    attackSteps.Value = group.Config.DashStabSteps;
                }
            }
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("SetDashStabVariables failed: " + e.Message);
        }
    }

    private static HeroController.ConfigGroup? PickGroupForNailArt(HeroController hero)
    {
        var pool = GetPool(hero);
        if (pool.Count == 0)
        {
            return null;
        }

        Filtered.Clear();
        foreach (var group in pool)
        {
            if (group.Config != null && group.ChargeSlash != null)
            {
                Filtered.Add(group);
            }
        }

        if (Filtered.Count == 0)
        {
            return null;
        }

        return Filtered[UnityEngine.Random.Range(0, Filtered.Count)];
    }

    private static PlayMakerFSM? GetNailArtsFsm(HeroController hero)
    {
        if (_nailArtsFsm != null)
        {
            return _nailArtsFsm;
        }

        try
        {
            _nailArtsFsm = FSMUtility.LocateFSM(hero.gameObject, "Nail Arts");
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("Could not locate Nail Arts FSM: " + e.Message);
        }

        return _nailArtsFsm;
    }

    private static bool IsSprintOrDash(HeroControllerStates cs)
    {
        return cs.isSprinting
            || cs.dashing
            || cs.airDashing
            || cs.backDashing
            || cs.superDashing
            || cs.shadowDashing;
    }

    /// <summary>
    /// True while the hero is sprinting / dashing, or still in the Sprint FSM's post-release skid
    /// or wall-run states. After the sprint button is released the cState flags are already clear,
    /// but the Sprint FSM stays out of Idle while the hero slides. Swapping the crest config then
    /// fires "HC CONFIG UPDATED" and cancels the Sprint FSM from under itself, stranding the hero
    /// (sliding with no control), so every config swap has to wait for the FSM to reach Idle.
    /// </summary>
    private static bool IsSprintOrSkid(HeroController hero)
    {
        if (hero == null)
        {
            return false;
        }

        if (IsSprintOrDash(hero.cState))
        {
            return true;
        }

        var fsm = hero.sprintFSM;
        var state = fsm != null ? fsm.ActiveStateName : null;
        return !string.IsNullOrEmpty(state) && state != "Idle";
    }

    private static ToolCrest? ResolveCrest(HeroControllerConfig? config)
    {
        if (config == null)
        {
            return null;
        }

        try
        {
            if (Matches(Gameplay.ReaperCrest, config)) return Gameplay.ReaperCrest;
            if (Matches(Gameplay.WandererCrest, config)) return Gameplay.WandererCrest;
            if (Matches(Gameplay.WarriorCrest, config)) return Gameplay.WarriorCrest;
            if (Matches(Gameplay.ToolmasterCrest, config)) return Gameplay.ToolmasterCrest;
            if (Matches(Gameplay.WitchCrest, config)) return Gameplay.WitchCrest;
            if (Matches(Gameplay.SpellCrest, config)) return Gameplay.SpellCrest;
        }
        catch
        {
            // Gameplay settings not ready; treat as the default moveset.
        }

        return null;
    }

    private static bool Matches(ToolCrest? crest, HeroControllerConfig config)
    {
        return crest != null && ReferenceEquals(crest.HeroConfig, config);
    }

    private static HeroController.ConfigGroup? PickGroup(HeroController hero, AttackDirection dir, bool wallSliding)
    {
        var pool = GetPool(hero);
        if (pool.Count == 0)
        {
            return null;
        }

        Filtered.Clear();
        foreach (var group in pool)
        {
            if (IsGroupUsable(group, dir, wallSliding))
            {
                Filtered.Add(group);
            }
        }

        if (Filtered.Count == 0)
        {
            return null;
        }

        return Filtered[UnityEngine.Random.Range(0, Filtered.Count)];
    }

    private static bool IsGroupUsable(HeroController.ConfigGroup group, AttackDirection dir, bool wallSliding)
    {
        if (group == null || group.Config == null)
        {
            return false;
        }

        if (wallSliding)
        {
            return group.WallSlash != null;
        }

        switch (dir)
        {
            case AttackDirection.normal:
                return group.NormalSlash != null;

            case AttackDirection.upward:
                return group.UpSlash != null;

            case AttackDirection.downward:
                switch (group.Config.DownSlashType)
                {
                    case HeroControllerConfig.DownSlashTypes.DownSpike:
                        return group.Downspike != null;

                    case HeroControllerConfig.DownSlashTypes.Slash:
                        return group.DownSlash != null;

                    case HeroControllerConfig.DownSlashTypes.Custom:
                        return true;

                    default:
                        return false;
                }

            default:
                return true;
        }
    }

    private static List<HeroController.ConfigGroup> GetPool(HeroController hero)
    {
        var configs = GetConfigs(hero);
        if (configs == null || configs.Length == 0)
        {
            return Pool;
        }

        if (ReferenceEquals(configs, _cachedConfigs) && Pool.Count > 0)
        {
            return Pool;
        }

        _cachedConfigs = configs;
        Pool.Clear();

        HeroControllerConfig? cloakless = null;
        try
        {
            if (Gameplay.CloaklessCrest != null)
            {
                cloakless = Gameplay.CloaklessCrest.HeroConfig;
            }
        }
        catch
        {
            // Gameplay settings not ready yet; the pool will be rebuilt on a later attack.
        }

        foreach (var group in configs)
        {
            if (group == null || group.Config == null)
            {
                continue;
            }

            if (cloakless != null && ReferenceEquals(group.Config, cloakless))
            {
                continue;
            }

            Pool.Add(group);
        }

        RandomCrestModPlugin.Log($"Random attack pool built with {Pool.Count} crest movesets.");
        return Pool;
    }

    private static HeroController.ConfigGroup[]? GetConfigs(HeroController hero)
    {
        _configsField ??= AccessTools.Field(typeof(HeroController), "configs");
        return _configsField?.GetValue(hero) as HeroController.ConfigGroup[];
    }

    private static void SetConfigGroup(HeroController hero, HeroController.ConfigGroup group)
    {
        _setConfigGroup ??= AccessTools.Method(typeof(HeroController), "SetConfigGroup");
        _setConfigGroup?.Invoke(hero, new object?[] { group, null });
    }

    private static void UpdateConfig(HeroController hero)
    {
        _updateConfig ??= AccessTools.Method(typeof(HeroController), "UpdateConfig");
        _updateConfig?.Invoke(hero, null);
    }
}
