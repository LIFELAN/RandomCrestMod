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
    private static bool _dashActive;
    private static float _dashLastActive;
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

            // Sprint / dash / sprint-attack / air-dash are all driven by the "Sprint" FSM, which
            // grabs its attack object while it runs. Swapping the crest underneath it strands that
            // FSM (control stays relinquished and gravity stays off), so leave those alone.
            if (IsSprintOrDash(hero.cState))
            {
                return;
            }

            var group = PickGroup(hero, _pendingDir, _pendingWallSlide);
            if (group == null)
            {
                return;
            }

            ApplyGroup(hero, group);

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

        // Swapping the config fires "HC CONFIG UPDATED", which globally cancels the Sprint FSM.
        // Never do that while the player is sprinting / dashing.
        if (IsSprintOrDash(hero.cState))
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
            ApplyGroup(hero, group);

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
        // player is already sprinting. Postpone until the sprint ends (Tick handles it).
        if (IsSprintOrDash(hero.cState))
        {
            return;
        }

        Restore(hero);
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
                IsSpoofing = false;
                SpoofCrest = null;
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

                // Never swap the config back while sprinting / dashing; wait for the sprint to end.
                var sprinting = IsSprintOrDash(cs);
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

            // If the player starts sprinting / dashing while a random attack is still installed,
            // put the real crest back before the Sprint FSM picks up its attack object, otherwise
            // the FSM and the active config disagree and the dash state can strand.
            if (IsSprintOrDash(cs) && !crestBusy)
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
        if (!_active && !_nailArtActive && !_bindActive && !_dashActive)
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
        _dashActive = false;
        RandomCrestModPlugin.Log($"Random attack/charge/bind/dash restored after {(Time.time - _activateTime):F2}s.");
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
        if (!_dashActive || !RandomCrestModPlugin.EnableRandomAttacks)
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
