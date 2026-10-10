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
/// The random moveset pool (every crest's config group except Cloakless), plus the sprint/skid
/// predicates that every config swap has to respect.
/// </summary>
internal static class ConfigPool
{
    private static readonly List<HeroController.ConfigGroup> Pool = new();
    private static readonly List<HeroController.ConfigGroup> Filtered = new();

    private static HeroController.ConfigGroup[]? _cachedConfigs;
    private static FieldInfo? _configsField;

    /// <summary>Random pick from every usable group, ignoring direction.</summary>
    internal static HeroController.ConfigGroup? PickAny(HeroController hero)
    {
        var pool = Get(hero);
        return pool.Count == 0 ? null : pool[UnityEngine.Random.Range(0, pool.Count)];
    }

    /// <summary>Random pick among groups that can perform the requested slash.</summary>
    internal static HeroController.ConfigGroup? PickForSlash(HeroController hero, AttackDirection dir, bool wallSliding)
    {
        Filtered.Clear();
        foreach (var group in Get(hero))
        {
            if (IsUsable(group, dir, wallSliding))
            {
                Filtered.Add(group);
            }
        }

        return Filtered.Count == 0 ? null : Filtered[UnityEngine.Random.Range(0, Filtered.Count)];
    }

    /// <summary>Random pick among groups that have a charge slash (蓄力斩).</summary>
    internal static HeroController.ConfigGroup? PickForChargeSlash(HeroController hero)
    {
        Filtered.Clear();
        foreach (var group in Get(hero))
        {
            if (group.Config != null && group.ChargeSlash != null)
            {
                Filtered.Add(group);
            }
        }

        return Filtered.Count == 0 ? null : Filtered[UnityEngine.Random.Range(0, Filtered.Count)];
    }

    /// <summary>The group that belongs to <paramref name="crest"/>, matched by config reference then by name.</summary>
    internal static HeroController.ConfigGroup? Find(HeroController hero, ToolCrest crest)
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
            RandomCrestModPlugin.LogError("[ConfigPool] Find failed: " + e.Message);
        }

        return null;
    }

    /// <summary>True while the hero is sprinting, dashing, or still in the Sprint FSM's skid / wall-run.</summary>
    internal static bool IsSprintOrSkid(HeroController? hero)
    {
        if (hero == null)
        {
            return false;
        }

        if (IsSprintOrDash(hero.cState))
        {
            return true;
        }

        var state = hero.sprintFSM != null ? hero.sprintFSM.ActiveStateName : null;
        return !string.IsNullOrEmpty(state) && state != "Idle";
    }

    internal static bool IsSprintOrDash(HeroControllerStates cs)
    {
        return cs.isSprinting
            || cs.dashing
            || cs.airDashing
            || cs.backDashing
            || cs.superDashing
            || cs.shadowDashing;
    }

    /// <summary>The Sprint FSM's own state, read directly so it works even when the cState flags lag.</summary>
    internal static bool SprintFsmBusy(HeroController hero)
    {
        var state = hero.sprintFSM != null ? hero.sprintFSM.ActiveStateName : null;
        return !string.IsNullOrEmpty(state) && state != "Idle";
    }

    internal static PlayMakerFSM? LocateFsm(HeroController hero, string name)
    {
        try
        {
            return FSMUtility.LocateFSM(hero.gameObject, name);
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError($"[ConfigPool] could not locate FSM '{name}': " + e.Message);
            return null;
        }
    }

    private static List<HeroController.ConfigGroup> Get(HeroController hero)
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
            // Gameplay settings not ready yet; the pool is rebuilt on a later call.
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

    private static bool IsUsable(HeroController.ConfigGroup group, AttackDirection dir, bool wallSliding)
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
}
