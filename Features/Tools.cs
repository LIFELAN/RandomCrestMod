using System;
using System.Collections.Generic;
using System.Reflection;
using GlobalSettings;
using HarmonyLib;
using RandomCrestMod.Core;
using TeamCherry.SharedUtils;
using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// Entry point for the random tool feature. The four parts it coordinates are split by
/// responsibility: <see cref="ToolPool"/> (what can be picked), <see cref="ToolBudget"/> (the single
/// shared use counter), <see cref="ThrowBarrage"/> (multi-throw) and <see cref="VoltToggle"/>
/// (Voltvessels' form).
/// </summary>
internal static class Tools
{
    internal static void Tick(HeroController? hero)
    {
        if (!Gate.Tools)
        {
            ToolBudget.Invalidate();
            ThrowBarrage.End(hero);
            VoltToggle.Restore();
            return;
        }

        ThrowBarrage.Tick(hero);
        VoltToggle.Tick();
        ToolBudget.Tick();
    }

    internal static void OnSaveLoaded()
    {
        ToolBudget.Invalidate();
        ThrowBarrage.End(null);
        VoltToggle.OnSaveLoaded();
        ToolPool.EnsureBuilt();
    }
}

/// <summary>Builds and answers the pools of red tools and silk skills, and remembers the current spoof.</summary>
internal static class ToolPool
{
    private const string RosaryCannonName = "Rosary Cannon";
    private const string ToggleToolName = "Lightning Rod";

    /// <summary>Quest tools that keep their vanilla behaviour when equipped on the Chaos crest.</summary>
    private static readonly string[] VanillaEquipTools = { "Extractor" };

    /// <summary>Tools that never count toward the free-throw chance.</summary>
    private static readonly string[] FreeThrowExcluded = { "Extractor", "Silk Snare" };

    private static readonly List<ToolItem> RedPool = new();
    private static readonly List<ToolItem> SkillPool = new();
    private static readonly List<ToolItem> ThrowablePool = new();

    private static bool _built;
    private static bool _picking;
    private static ToolItem? _spoofTool;
    private static AttackToolBinding _spoofBinding;

    internal static ToolItem? RosaryCannon { get; private set; }

    internal static IReadOnlyList<ToolItem> RedTools => RedPool;

    internal static ToolItem? SpoofTool => _spoofTool;

    internal static AttackToolBinding SpoofBinding => _spoofBinding;

    /// <summary>True only inside <c>GetWillThrowTool</c>. Reporting code must keep seeing the real tool.</summary>
    internal static bool IsPicking => _picking;

    internal static void BeginPick() => _picking = true;

    internal static void EndPick() => _picking = false;

    internal static void EnsureBuilt()
    {
        if (_built && RedPool.Count > 0)
        {
            return;
        }

        if (ManagerSingleton<ToolItemManager>.UnsafeInstance == null)
        {
            return;
        }

        RedPool.Clear();
        SkillPool.Clear();
        RosaryCannon = null;

        foreach (var tool in ToolItemManager.GetAllTools())
        {
            if (tool == null)
            {
                continue;
            }

            switch (tool.Type)
            {
                case ToolItemType.Red:
                    RedPool.Add(tool);
                    if (string.Equals(tool.name, RosaryCannonName, StringComparison.Ordinal))
                    {
                        RosaryCannon = tool;
                    }

                    break;

                case ToolItemType.Skill:
                    SkillPool.Add(tool);
                    break;
            }
        }

        _built = true;
        RandomCrestModPlugin.Log($"[RandomTool] pools built: red={RedPool.Count}, skill={SkillPool.Count}.");
    }

    internal static void Invalidate()
    {
        _built = false;
    }

    internal static bool IsRed(ToolItem? tool) => tool != null && RedPool.Contains(tool);

    internal static bool IsVanillaEquip(ToolItem? tool)
    {
        return tool != null && Array.Exists(VanillaEquipTools, name => string.Equals(tool.name, name, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool IsFreeThrowExcluded(ToolItem tool)
    {
        return Array.Exists(FreeThrowExcluded, name => string.Equals(tool.name, name, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool IsSpoofed(ToolItem? tool) => tool != null && ReferenceEquals(tool, _spoofTool);

    internal static void SetSpoof(ToolItem tool, AttackToolBinding binding)
    {
        _spoofTool = tool;
        _spoofBinding = binding;
    }

    internal static void ClearSpoof()
    {
        _spoofTool = null;
    }

    /// <summary>
    /// A random red tool for this throw. <paramref name="requireThrowPrefab"/> restricts the pick to tools
    /// that spawn a projectile, so a chained barrage throw can never break the chain.
    /// </summary>
    internal static ToolItem? PickRed(bool requireThrowPrefab)
    {
        EnsureBuilt();

        ToolItem? pick;
        if (requireThrowPrefab)
        {
            ThrowablePool.Clear();
            foreach (var tool in RedPool)
            {
                if (HasThrowPrefab(tool))
                {
                    ThrowablePool.Add(tool);
                }
            }

            pick = Pick(ThrowablePool);
        }
        else
        {
            pick = Pick(RedPool);
        }

        if (pick == null)
        {
            return null;
        }

        ToolBudget.Mirror(pick);

        if (pick.name == ToggleToolName)
        {
            VoltToggle.Roll();
        }

        return pick;
    }

    internal static ToolItem? PickSkill()
    {
        EnsureBuilt();
        return Pick(SkillPool);
    }

    private static ToolItem? Pick(List<ToolItem> pool)
    {
        return pool.Count == 0 ? null : pool[UnityEngine.Random.Range(0, pool.Count)];
    }

    private static bool HasThrowPrefab(ToolItem tool)
    {
        try
        {
            return tool.Usage.ThrowPrefab != null;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// The shared use budget for every red tool. This is the only writer of the per-tool
/// <c>AmountLeft</c> values: every change goes through <see cref="SyncAll"/>, so the counter cannot be
/// half-updated. The game still decrements the tool it threw; <see cref="Consume"/> reconciles that.
/// </summary>
internal static class ToolBudget
{
    private static readonly Dictionary<ToolItem, ToolItem.ReplenishResources> SavedResources = new();
    private static FieldInfo? _replenishField;
    private static int _left;
    private static bool _initialized;

    internal static int Left => _left;

    internal static int Capacity => Rules.UsesPerBench(RandomCrestModPlugin.ToolUsesPerBench.Value, PouchLevel);

    internal static int PouchLevel
    {
        get
        {
            try
            {
                return PlayerData.instance != null ? Math.Max(0, PlayerData.instance.ToolPouchUpgrades) : 0;
            }
            catch
            {
                return 0;
            }
        }
    }

    internal static bool IsOutOfUses(ToolItem? tool) => Gate.Tools && _left <= 0 && ToolPool.IsRed(tool);

    /// <summary>Called every frame while the feature is live. Refills once when the feature first becomes live.</summary>
    internal static void Tick()
    {
        if (!_initialized)
        {
            _initialized = true;
            Reset();
        }

        // The Rosary Cannon spends its own saved ammo, so keep it at capacity for random picks.
        if (ToolPool.RosaryCannon != null)
        {
            SetAmount(ToolPool.RosaryCannon, Capacity);
        }
    }

    internal static void Invalidate()
    {
        _initialized = false;
    }

    /// <summary>Refills the shared budget. Called at every bench rest.</summary>
    internal static void Reset()
    {
        ToolPool.EnsureBuilt();
        _left = Capacity;
        SyncAll();
    }

    /// <summary>Called after a red tool was thrown. Reconciles the game's decrement with the shared budget.</summary>
    internal static void Consume(ToolItem used)
    {
        if (!Gate.Tools || used.Type != ToolItemType.Red)
        {
            return;
        }

        if (RollFreeThrow())
        {
            SyncAll();
            ToolItemManager.ReportAllBoundAttackToolsUpdated();
            return;
        }

        var after = used.SavedData.AmountLeft;
        _left = Math.Max(0, Math.Min(after, _left - 1));

        SyncAll();
        ToolItemManager.ReportAllBoundAttackToolsUpdated();
    }

    /// <summary>Writes the shared count to every red tool. The only place amounts are written.</summary>
    internal static void SyncAll()
    {
        foreach (var tool in ToolPool.RedTools)
        {
            SetAmount(tool, _left);
        }
    }

    /// <summary>Gives a picked tool the shared count, so the vanilla empty check agrees with the budget.</summary>
    internal static void Mirror(ToolItem tool)
    {
        SetAmount(tool, _left);
    }

    /// <summary>
    /// Temporarily clears the replenish cost of red tools so a bench refill is free. Restored by
    /// <see cref="EndFreeRefill"/>, which the replenish patch also runs from a finalizer.
    /// </summary>
    internal static void BeginFreeRefill()
    {
        if (!Gate.Tools || SavedResources.Count > 0)
        {
            return;
        }

        ToolPool.EnsureBuilt();
        _replenishField ??= AccessTools.Field(typeof(ToolItem), "replenishResource");
        if (_replenishField == null)
        {
            return;
        }

        foreach (var tool in ToolPool.RedTools)
        {
            try
            {
                SavedResources[tool] = (ToolItem.ReplenishResources)_replenishField.GetValue(tool);
                _replenishField.SetValue(tool, ToolItem.ReplenishResources.None);
            }
            catch (Exception e)
            {
                RandomCrestModPlugin.LogError("[RandomTool] BeginFreeRefill failed: " + e.Message);
            }
        }
    }

    internal static void EndFreeRefill()
    {
        if (SavedResources.Count == 0 || _replenishField == null)
        {
            return;
        }

        foreach (var pair in SavedResources)
        {
            try
            {
                _replenishField.SetValue(pair.Key, pair.Value);
            }
            catch (Exception e)
            {
                RandomCrestModPlugin.LogError("[RandomTool] EndFreeRefill failed: " + e.Message);
            }
        }

        SavedResources.Clear();
    }

    private static bool RollFreeThrow()
    {
        var chance = Rules.FreeThrowChance(CollectedRedTools(), CurveclawUpgraded());
        return chance > 0f && UnityEngine.Random.value < chance;
    }

    /// <summary>Collected red tools, counting an upgrade line once (Curve Claws / Curvesickle), quest tools excluded.</summary>
    private static int CollectedRedTools()
    {
        var keys = new HashSet<SavedItem>();
        try
        {
            foreach (var tool in ToolItemManager.GetAllTools())
            {
                if (tool == null || tool.Type != ToolItemType.Red || ToolPool.IsFreeThrowExcluded(tool))
                {
                    continue;
                }

                try
                {
                    if (tool.SavedData.IsUnlocked)
                    {
                        keys.Add(tool.CountKey);
                    }
                }
                catch
                {
                    // PlayerData momentarily unavailable: skip this tool.
                }
            }
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[RandomTool] collected tool count failed: " + e.Message);
        }

        return keys.Count;
    }

    private static bool CurveclawUpgraded()
    {
        try
        {
            var upgraded = Gameplay.CurveclawUpgradedTool;
            return upgraded != null && upgraded.SavedData.IsUnlocked;
        }
        catch
        {
            return false;
        }
    }

    private static void SetAmount(ToolItem tool, int amount)
    {
        try
        {
            var data = tool.SavedData;
            if (data.AmountLeft == amount)
            {
                return;
            }

            data.AmountLeft = amount;
            tool.SavedData = data;
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[RandomTool] SetAmount failed: " + e.Message);
        }
    }
}

/// <summary>
/// Multi-throw barrage. Reuses the game's own <c>queuedAutoThrowTool</c> loop (it already waits for the
/// throw animation) and re-rolls a fresh tool for each chained throw.
/// </summary>
internal static class ThrowBarrage
{
    private static AccessTools.FieldRef<HeroController, ToolItem>? _willThrowRef;
    private static AccessTools.FieldRef<HeroController, bool>? _queuedAutoThrowRef;

    private static int _remaining;
    private static AttackToolBinding _binding;
    private static bool _armed;
    private static ToolItem? _candidate;
    private static bool _consumed;

    internal static bool Armed => _armed;

    /// <summary>From the <c>ThrowTool</c> prefix: remember the tool about to be thrown.</summary>
    internal static void BeforeThrow(HeroController hero)
    {
        _consumed = false;
        _candidate = GetWillThrow(hero);
    }

    /// <summary>From the <c>DidUseAttackTool</c> postfix: a tool really left the hero.</summary>
    internal static void NotifyConsumed()
    {
        _consumed = true;
    }

    /// <summary>From the <c>ThrowTool</c> postfix: arm the barrage or chain the next throw.</summary>
    internal static void AfterThrow(HeroController hero, bool isAutoThrow)
    {
        try
        {
            if (!Gate.Tools)
            {
                return;
            }

            // Only a spawned projectile (ThrowPrefab path) sets isToolThrowing. FSM-event tools never arm.
            if (!_consumed || !hero.cState.isToolThrowing)
            {
                if (isAutoThrow)
                {
                    End(hero);
                }

                return;
            }

            if (_candidate == null || _candidate.Type != ToolItemType.Red)
            {
                End(hero);
                return;
            }

            if (!isAutoThrow)
            {
                _remaining = Rules.ExtraThrows(StatueHeld, ToolBudget.PouchLevel, QuickSlingEquipped);
                _binding = ToolPool.SpoofBinding;
                _armed = _remaining > 0;
                RandomCrestModPlugin.Log($"[RandomTool] barrage armed: +{_remaining} extra.");
            }
            else
            {
                _remaining--;
            }

            if (_remaining > 0 && ToolBudget.Left > 0)
            {
                var next = ToolPool.PickRed(requireThrowPrefab: true);
                if (next != null)
                {
                    SetWillThrow(hero, next);
                    ToolPool.SetSpoof(next, _binding);
                    SetQueuedAutoThrow(hero, true);
                    return;
                }
            }

            End(hero);
        }
        catch (Exception e)
        {
            End(hero);
            RandomCrestModPlugin.LogError("[RandomTool] barrage failed: " + e.Message);
        }
    }

    /// <summary>Per-frame check: stops a barrage whose conditions no longer hold, so it cannot fire later.</summary>
    internal static void Tick(HeroController? hero)
    {
        if (!_armed)
        {
            return;
        }

        var cs = hero != null ? hero.cState : null;
        if (hero == null || cs == null || cs.dead || cs.hazardRespawning || cs.transitioning
            || ToolBudget.Left <= 0 || !StatueHeld)
        {
            End(hero);
        }
    }

    /// <summary>Stops any barrage and clears the game's queued throw, but only if we owned one.</summary>
    internal static void End(HeroController? hero)
    {
        var hadChain = _armed || _remaining > 0;
        _armed = false;
        _remaining = 0;

        if (hero == null || !hadChain)
        {
            return;
        }

        try
        {
            SetQueuedAutoThrow(hero, false);
            SetWillThrow(hero, null);
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[RandomTool] EndChain failed: " + e.Message);
        }
    }

    private static bool StatueHeld
    {
        get
        {
            try
            {
                var pd = PlayerData.instance;
                return pd != null && pd.Collectables.GetData("Fixer Idol").Amount > 0;
            }
            catch
            {
                return false;
            }
        }
    }

    private static bool QuickSlingEquipped
    {
        get
        {
            try
            {
                return Gameplay.QuickSlingTool != null && Gameplay.QuickSlingTool.Status.IsEquipped;
            }
            catch
            {
                return false;
            }
        }
    }

    private static ToolItem? GetWillThrow(HeroController hero)
    {
        _willThrowRef ??= AccessTools.FieldRefAccess<HeroController, ToolItem>("willThrowTool");
        try
        {
            return _willThrowRef(hero);
        }
        catch
        {
            return null;
        }
    }

    private static void SetWillThrow(HeroController hero, ToolItem? tool)
    {
        _willThrowRef ??= AccessTools.FieldRefAccess<HeroController, ToolItem>("willThrowTool");
        _willThrowRef(hero) = tool!;
    }

    private static void SetQueuedAutoThrow(HeroController hero, bool value)
    {
        _queuedAutoThrowRef ??= AccessTools.FieldRefAccess<HeroController, bool>("queuedAutoThrowTool");
        _queuedAutoThrowRef(hero) = value;
    }
}

/// <summary>
/// Voltvessels (Lightning Rod) has two vanilla forms, stored as a PlayerData bool. Each random pick rolls
/// one, and the player's own value is put back afterwards so the save is untouched.
/// </summary>
internal static class VoltToggle
{
    private const string ToggleStateField = "LightningToolToggle";
    private const float RestoreDelay = 0.5f;

    private static bool _tracked;
    private static bool _original;
    private static bool _pendingRestore;
    private static float _setTime;

    internal static void Roll()
    {
        try
        {
            var playerData = PlayerData.instance;
            if (playerData == null)
            {
                return;
            }

            var current = playerData.GetVariable<bool>(ToggleStateField);
            if (!_tracked)
            {
                _tracked = true;
                _original = current;
            }

            var desired = UnityEngine.Random.value < 0.5f;
            if (current != desired)
            {
                playerData.SetVariable(ToggleStateField, desired);
            }

            _pendingRestore = true;
            _setTime = Time.time;
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[RandomTool] Voltvessels roll failed: " + e.Message);
        }
    }

    internal static void Tick()
    {
        if (_pendingRestore && !ToolPool.IsPicking && Time.time - _setTime > RestoreDelay)
        {
            Restore();
        }
    }

    internal static void Restore()
    {
        if (!_tracked)
        {
            return;
        }

        try
        {
            var playerData = PlayerData.instance;
            if (playerData == null)
            {
                return;
            }

            if (playerData.GetVariable<bool>(ToggleStateField) != _original)
            {
                playerData.SetVariable(ToggleStateField, _original);
            }

            _tracked = false;
            _pendingRestore = false;
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[RandomTool] Voltvessels restore failed: " + e.Message);
        }
    }

    internal static void OnSaveLoaded()
    {
        _tracked = false;
        _pendingRestore = false;
    }
}
