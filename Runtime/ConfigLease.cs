using System;
using System.Reflection;
using GlobalSettings;
using HarmonyLib;
using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// Who currently holds the hero's crest config. Higher values preempt lower ones; a lower owner
/// can never take the config from a higher one.
/// </summary>
internal enum ConfigOwner
{
    None = 0,
    Dash = 1,
    Attack = 2,
    NailArt = 3,
    Bind = 4,
    Taunt = 5,
}

/// <summary>
/// The single writer of the hero's random crest config.
///
/// <para>Replaces the old set of independent flags (attack / nail art / bind / dash / taunt / pending /
/// spoof). There is exactly one owner at a time, and the spoof (<see cref="SpoofCrest"/>) is derived
/// from that owner, so the two can never disagree.</para>
///
/// <para>Each feature decides when it lets go by calling <see cref="Release"/> with its own owner. A
/// release by a feature that no longer owns the config is a no-op, so an owner that was preempted
/// cannot restore the config out from under its replacement.</para>
/// </summary>
internal static class ConfigLease
{
    private static MethodInfo? _setConfigGroup;
    private static MethodInfo? _updateConfig;

    private static ConfigOwner _owner;
    private static HeroController.ConfigGroup? _original;
    private static ToolCrest? _spoofCrest;
    private static float _since;
    private static bool _quiet;
    private static bool _restoring;

    internal static ConfigOwner Owner => _owner;

    internal static bool IsHeld => _owner != ConfigOwner.None;

    internal static bool IsOwnedBy(ConfigOwner owner) => _owner == owner;

    /// <summary>Crest the spoof reports as equipped. Null for the default (Hunter) moveset.</summary>
    internal static ToolCrest? SpoofCrest => _spoofCrest;

    /// <summary>Seconds since the current owner took the config.</summary>
    internal static float HeldFor => Time.time - _since;

    /// <summary>True while a swap is in progress without firing "HC CONFIG UPDATED".</summary>
    internal static bool Quiet => _quiet;

    /// <summary>True while the restore call runs. Attack hooks ignore pending requests during it.</summary>
    internal static bool Restoring => _restoring;

    /// <summary>
    /// Installs <paramref name="group"/> for <paramref name="owner"/>. Returns false when a higher
    /// owner holds the config. <paramref name="quiet"/> keeps the Sprint FSM running (see DEV_NOTES).
    /// </summary>
    internal static bool Acquire(HeroController hero, ConfigOwner owner, HeroController.ConfigGroup group, bool quiet, bool force = false)
    {
        if (!force && owner < _owner)
        {
            return false;
        }

        // Never let a swap fire "HC CONFIG UPDATED" while the Sprint FSM would cancellably react to it
        // (sprint / nail charge / transition). See ConfigPool.ShouldSwapQuietly.
        if (!quiet && ConfigPool.ShouldSwapQuietly(hero))
        {
            DiagForceQuiet(owner, "acquire", hero);
            quiet = true;
        }

        var original = _owner == ConfigOwner.None ? hero.CurrentConfigGroup : _original;

        // Replacing a moveset that is already installed: put the real one back first. Installing the new
        // group on top of it leaves the old group's crest root active too (witch and beast at once), and
        // the new root can then fail to take effect. The restore is quiet so the Sprint / Bind FSM keeps running.
        if (_owner != ConfigOwner.None)
        {
            RandomCrestModPlugin.Log($"[ConfigLease] {_owner} -> {owner} (replacing, quiet={quiet}).");
            _quiet = true;
            try
            {
                Restore(hero);
            }
            finally
            {
                _quiet = false;
            }
        }

        _quiet = quiet;
        try
        {
            SetConfigGroup(hero, group);
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[ConfigLease] swap failed: " + e.Message);
            ClearState();
            Restore(hero);
            return false;
        }
        finally
        {
            _quiet = false;
        }

        // The player's own crest objects must stay alive while the random moveset is installed.
        if (original != null && original.ActiveRoot != null && original != group)
        {
            original.ActiveRoot.SetActive(true);
        }

        hero.cState.altAttack = false;

        _original = original;
        _owner = owner;
        _since = Time.time;
        _spoofCrest = ResolveCrest(group.Config);
        return true;
    }

    /// <summary>Gives the config back to the real crest, but only if <paramref name="owner"/> still holds it.</summary>
    internal static void Release(HeroController? hero, ConfigOwner owner)
    {
        if (_owner != owner)
        {
            return;
        }

        RandomCrestModPlugin.Log($"[ConfigLease] release {owner}.");
        ClearState();

        // Restoring the real crest fires "HC CONFIG UPDATED", which globally cancels the Sprint FSM.
        // While the hero is sprinting / sliding / wall-running / mantling that cancellation strands the
        // hero: control stays off and the velocity is kept, so she is flung out of the scene (the
        // long-standing "nailed to the wall then flies out" bug). The same event also runs the Sprint
        // FSM's "Reset Nail Charge State", which clears a held nail charge on a scene transition.
        // Swallow the event for that restore; the real moveset is still put back.
        var forceQuiet = ConfigPool.ShouldSwapQuietly(hero);
        if (forceQuiet && !_quiet && hero != null)
        {
            DiagForceQuiet(owner, "release", hero);
        }

        var wasQuiet = _quiet;
        _quiet = wasQuiet || forceQuiet;
        try
        {
            Restore(hero);
        }
        finally
        {
            _quiet = wasQuiet;
        }
    }

    /// <summary>Drops whatever is held, for death / respawn / scene transitions / shutdown.</summary>
    internal static void Abort(HeroController? hero)
    {
        if (!IsHeld)
        {
            return;
        }

        RandomCrestModPlugin.Log($"[ConfigLease] abort {_owner}.");
        ClearState();

        // Abort runs on death / respawn / scene change. Those states re-init the hero's FSMs on their
        // own, so a "HC CONFIG UPDATED" here is never needed and only risks cancelling the Sprint FSM
        // and clearing a held nail charge. Always restore silently.
        var wasQuiet = _quiet;
        _quiet = true;
        try
        {
            Restore(hero);
        }
        finally
        {
            _quiet = wasQuiet;
        }
    }

    /// <summary>Diagnostic for forced-quiet swaps; remove once the fix is verified.</summary>
    private static void DiagForceQuiet(ConfigOwner owner, string phase, HeroController hero)
    {
        var cs = hero.cState;
        RandomCrestModPlugin.LogDiag(
            $"[ConfigLease] forcing quiet {phase} ({owner}) "
            + $"nailCharging={(cs != null && cs.nailCharging)} "
            + $"transitioning={(cs != null && cs.transitioning)} "
            + $"sprint={ConfigPool.IsSprintOrSkid(hero)}.");
    }

    private static void ClearState()
    {
        _owner = ConfigOwner.None;
        _original = null;
        _spoofCrest = null;
    }

    private static void Restore(HeroController? hero)
    {
        if (hero == null)
        {
            return;
        }

        // State is already cleared above, so even if the game throws here nothing stays spoofed.
        _restoring = true;
        try
        {
            UpdateConfig(hero);
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[ConfigLease] restore failed: " + e.Message);
        }
        finally
        {
            _restoring = false;
        }

        hero.cState.altAttack = false;
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
            // Gameplay settings not ready: treat as the default moveset.
        }

        return null;
    }

    private static bool Matches(ToolCrest? crest, HeroControllerConfig config)
    {
        return crest != null && ReferenceEquals(crest.HeroConfig, config);
    }
}
