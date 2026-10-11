using System;
using GlobalEnums;
using GlobalSettings;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// Random normal / up / down / wall slashes, charge slashes (蓄力斩) and sprint-dash stabs.
///
/// <para>Each path takes the config through <see cref="ConfigLease"/> and only releases it when its own
/// owner is still the holder. The old design tracked the same lifetimes with separate flags; here a
/// request is only valid in the frame it was made, so a stale request cannot re-apply later.</para>
/// </summary>
internal static class Attacks
{
    private const float AttackTimeout = 4f;
    private const float NailArtMinHold = 0.3f;
    private const float NailArtTimeout = 4f;
    private const float DashGrace = 0.25f;
    private const float DashMaxHold = 10f;
    private const string DashStartState = "Start Attack";
    private const string CrestAttacksFsm = "Crest Attacks";

    private static int _requestFrame = -1;
    private static AttackDirection _requestDir;
    private static bool _requestWall;

    private static float _attackMinHold;
    private static bool _attackQuiet;
    private static float _dashLastActive;
    private static PlayMakerFSM? _nailArtsFsm;

    /// <summary>Called from the <c>Attack</c> prefix. The config is applied in <see cref="OnUpdateConfig"/>.</summary>
    internal static void RequestAttack(AttackDirection dir, bool wallSliding)
    {
        _requestFrame = Time.frameCount;
        _requestDir = dir;
        _requestWall = wallSliding;
    }

    /// <summary>
    /// Called from the <c>UpdateConfig</c> postfix. <c>Attack()</c> calls <c>UpdateConfig()</c> right
    /// before it reads the config, so this is the moment to substitute the group. A request only counts
    /// in the frame it was made, and never while our own restore is running.
    /// </summary>
    internal static void OnUpdateConfig(HeroController hero)
    {
        if (_requestFrame != Time.frameCount || ConfigLease.Restoring)
        {
            return;
        }

        _requestFrame = -1;

        if (!Gate.Attacks)
        {
            return;
        }

        try
        {
            var group = ConfigPool.PickForSlash(hero, _requestDir, _requestWall);
            if (group == null)
            {
                return;
            }

            var quiet = ConfigPool.IsSprintOrSkid(hero);

            // Old behaviour: a normal / charge attack always uses its own rolled moveset. Take the config
            // even if a bind or taunt still holds it, so an attack right after a bind is never stuck on
            // the real (Hunter) moveset.
            if (!ConfigLease.Acquire(hero, ConfigOwner.Attack, group, quiet, force: true))
            {
                return;
            }

            _attackQuiet = quiet;
            _attackMinHold = Mathf.Max(0.05f, group.Config != null ? group.Config.AttackDuration : 0.35f);
            RandomCrestModPlugin.Log($"Random attack -> '{NameOf(group)}' dir={_requestDir}.");
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[Attacks] apply failed: " + e);
        }
    }

    /// <summary>Called from the <c>CanNailArt</c> postfix when the charge slash is confirmed.</summary>
    internal static void OnNailArtConfirmed(HeroController hero)
    {
        if (!Gate.Attacks || ConfigLease.IsOwnedBy(ConfigOwner.NailArt))
        {
            return;
        }

        try
        {
            var group = ConfigPool.PickForChargeSlash(hero);
            if (group != null)
            {
                ConfigLease.Acquire(hero, ConfigOwner.NailArt, group, quiet: false);
                RandomCrestModPlugin.Log($"Random charge slash -> '{NameOf(group)}'.");
            }
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[Attacks] charge slash failed: " + e);
        }
    }

    /// <summary>Called from the <c>IncrementAttackCounter</c> postfix: each dash stab re-rolls its moveset.</summary>
    internal static void OnDashStabStarted(HeroController hero)
    {
        if (_requestFrame == Time.frameCount || !RandomCrestModPlugin.EnableRandomAttacks)
        {
            return;
        }

        var fromDashAttack = hero.sprintFSM != null && hero.sprintFSM.ActiveStateName == DashStartState;
        var owner = ConfigLease.Owner;
        if (owner != ConfigOwner.Dash && !(owner == ConfigOwner.None && fromDashAttack))
        {
            return;
        }

        if (!Gate.Attacks)
        {
            return;
        }

        try
        {
            var group = ConfigPool.PickAny(hero);
            if (group == null || !ConfigLease.Acquire(hero, ConfigOwner.Dash, group, quiet: true))
            {
                return;
            }

            SetDashStabVariables(hero, group);
            _dashLastActive = Time.time;
            RandomCrestModPlugin.Log($"Random dash stab -> '{NameOf(group)}' (re-roll).");
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[Attacks] dash stab failed: " + e);
        }
    }

    /// <summary>Per-frame driver. Only the owner's own release rules run.</summary>
    internal static void Tick(HeroController? hero)
    {
        if (hero == null)
        {
            return;
        }

        switch (ConfigLease.Owner)
        {
            case ConfigOwner.Attack:
                TickAttack(hero);
                break;

            case ConfigOwner.NailArt:
                TickNailArt(hero);
                break;

            case ConfigOwner.Dash:
                TickDash(hero);
                break;

            case ConfigOwner.None:
                TryStartSprintMoveset(hero);
                break;
        }
    }

    private static void TickAttack(HeroController hero)
    {
        var cs = hero.cState;
        var crestBusy = IsCrestAttackBusy(hero);
        var elapsed = ConfigLease.HeldFor;

        // An attack installed quietly during a sprint must not restore the real crest mid-sprint
        // ("HC CONFIG UPDATED" would cancel the Sprint FSM). It waits for the sprint to settle.
        if (_attackQuiet)
        {
            if (ConfigPool.IsSprintOrSkid(hero) && elapsed < AttackTimeout)
            {
                return;
            }
        }
        else if (ConfigPool.IsSprintOrDash(cs) && !crestBusy)
        {
            // A fresh sprint starts while a normal attack holds the moveset: give the real crest back first.
            ConfigLease.Release(hero, ConfigOwner.Attack);
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
            || ConfigPool.IsSprintOrSkid(hero)
            || crestBusy;

        if ((!busy && elapsed >= _attackMinHold) || elapsed > AttackTimeout)
        {
            ConfigLease.Release(hero, ConfigOwner.Attack);
        }
    }

    private static void TickNailArt(HeroController hero)
    {
        var fsm = GetNailArtsFsm(hero);
        var state = fsm != null ? fsm.ActiveStateName : null;
        var busy = !string.IsNullOrEmpty(state) && state != "Inactive";
        var elapsed = ConfigLease.HeldFor;

        if ((!busy && elapsed >= NailArtMinHold) || elapsed > NailArtTimeout)
        {
            ConfigLease.Release(hero, ConfigOwner.NailArt);
        }
    }

    private static void TickDash(HeroController hero)
    {
        // The Sprint FSM keeps the moveset alive until it is back in Idle (restoring earlier would fire
        // "HC CONFIG UPDATED" and strand the sprint).
        if (hero.cState.isSprinting || ConfigPool.SprintFsmBusy(hero))
        {
            _dashLastActive = Time.time;
            return;
        }

        if (Time.time - _dashLastActive > DashGrace || ConfigLease.HeldFor > DashMaxHold)
        {
            ConfigLease.Release(hero, ConfigOwner.Dash);
        }
    }

    private static void TryStartSprintMoveset(HeroController hero)
    {
        if (!hero.cState.isSprinting || !Gate.Attacks)
        {
            return;
        }

        var group = ConfigPool.PickAny(hero);
        if (group == null || !ConfigLease.Acquire(hero, ConfigOwner.Dash, group, quiet: true))
        {
            return;
        }

        SetDashStabVariables(hero, group);
        _dashLastActive = Time.time;
        RandomCrestModPlugin.Log($"Random dash -> '{NameOf(group)}'.");
    }

    /// <summary>
    /// The crest-specific down slashes are driven by the "Crest Attacks" FSM, which can outlast the
    /// attack's own duration. Keep holding the moveset until it returns to Idle.
    /// </summary>
    private static bool IsCrestAttackBusy(HeroController hero)
    {
        var fsm = hero.crestAttacksFSM;
        var state = fsm != null ? fsm.ActiveStateName : null;
        return !string.IsNullOrEmpty(state) && state != "Idle";
    }

    /// <summary>
    /// The Sprint FSM's cached dash objects and attack numbers are normally refreshed by its own
    /// "Cancel All" state. A quiet swap skips that, so refresh them by hand.
    /// </summary>
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

            if (group.Config == null)
            {
                return;
            }

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
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[Attacks] SetDashStabVariables failed: " + e.Message);
        }
    }

    private static PlayMakerFSM? GetNailArtsFsm(HeroController hero)
    {
        _nailArtsFsm ??= ConfigPool.LocateFsm(hero, "Nail Arts");
        return _nailArtsFsm;
    }

    private static string NameOf(HeroController.ConfigGroup group)
    {
        return group.Config != null ? group.Config.name : "?";
    }
}

/// <summary>Harmony hooks feeding the attack paths. Every hook only forwards to <see cref="Attacks"/>.</summary>
[HarmonyPatch]
internal static class AttackPatches
{
    [HarmonyPatch(typeof(HeroController), "Attack", new[] { typeof(AttackDirection) })]
    [HarmonyPrefix]
    private static void Attack_Prefix(HeroController __instance, AttackDirection attackDir)
    {
        var cs = __instance.cState;
        Attacks.RequestAttack(attackDir, cs.wallSliding || cs.wallScrambling);
    }

    [HarmonyPatch(typeof(HeroController), "UpdateConfig")]
    [HarmonyPostfix]
    private static void UpdateConfig_Postfix(HeroController __instance)
    {
        Attacks.OnUpdateConfig(__instance);
    }

    [HarmonyPatch(typeof(HeroController), nameof(HeroController.CanNailArt))]
    [HarmonyPostfix]
    private static void CanNailArt_Postfix(HeroController __instance, ref bool __result)
    {
        if (__result)
        {
            Attacks.OnNailArtConfirmed(__instance);
        }
    }

    [HarmonyPatch(typeof(HeroController), nameof(HeroController.IncrementAttackCounter))]
    [HarmonyPostfix]
    private static void IncrementAttackCounter_Postfix(HeroController __instance)
    {
        Attacks.OnDashStabStarted(__instance);
    }
}
