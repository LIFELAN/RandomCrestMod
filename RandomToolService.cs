using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TeamCherry.SharedUtils;
using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// Random tool / random spell pools.
///
/// <para><b>Tools</b> are the throwable <see cref="ToolItemType.Red"/> items. <b>Spells</b> are the
/// six <see cref="ToolItemType.Skill"/> silk skills. Both are cast through the same QuickCast path
/// (<c>HeroController.GetWillThrowTool</c> -&gt; <c>ToolItemManager.GetBoundAttackTool</c>), so the
/// substitution happens there. The pools deliberately include tools/spells the player has not
/// obtained yet.</para>
///
/// <para>Normal Red tools share a single <see cref="_usesLeft"/> counter that is reset at every
/// bench (and on save load). A few tools are special-cased: <c>Extractor</c> (Needle Phial),
/// <c>Silk Snare</c> (Snare Setter) and <c>Rosary Cannon</c> are excluded from the pool, and
/// <c>Lightning Rod</c> (Voltvessels) rolls between its two vanilla forms on every pick.</para>
///
/// <para>Everything here is gated by <see cref="RandomToolsActive"/> / <see cref="RandomSpellsActive"/>
/// so that nothing (counts, refills, forms) leaks onto other crests.</para>
/// </summary>
internal static class RandomToolService
{
    /// <summary>
    /// Internal names excluded from the pool because they do not work when thrown at random:
    /// Extractor (Needle Phial), Silk Snare (Snare Setter) and Rosary Cannon (its usage differs and
    /// it misfires under rapid tool use).
    /// </summary>
    private static readonly string[] DefaultExcluded = { "Extractor", "Silk Snare", "Rosary Cannon" };

    private const string ToggleToolName = "Lightning Rod";

    /// <summary>PlayerData bool that selects the thrown (bola) form of Voltvessels.</summary>
    private const string ToggleStateField = "LightningToolToggle";

    private static readonly List<ToolItem> RedPool = new();
    private static readonly List<ToolItem> SkillPool = new();
    private static readonly HashSet<string> Excluded = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Original replenish resources, stashed while a bench refill runs for free.</summary>
    private static readonly Dictionary<ToolItem, ToolItem.ReplenishResources> SavedResources = new();

    private static bool _poolsBuilt;
    private static bool _initialized;
    private static int _usesLeft;

    // Voltvessels' form lives in PlayerData, so we remember the player's own value and put it back
    // as soon as the throw is over (or the crest is unequipped) to keep the save untouched.
    private static bool _toggleTracked;
    private static bool _toggleOriginal;
    private static bool _togglePendingRestore;
    private static float _toggleSetTime;

    private static FieldInfo? _replenishResourceField;

    private static ToolItem? _spoofTool;
    private static AttackToolBinding _spoofBinding;
    private static bool _picking;

    internal static AttackToolBinding SpoofBinding => _spoofBinding;

    /// <summary>
    /// True only between <c>HeroController.GetWillThrowTool</c> enter/exit. The tool lookup is also
    /// called from reporting code, and re-rolling there would waste picks and desync the HUD.
    /// </summary>
    internal static bool IsPicking => _picking;

    internal static void BeginPick() => _picking = true;

    internal static void EndPick() => _picking = false;

    internal static bool RandomToolsEnabled => RandomCrestModPlugin.EnableRandomTools;

    /// <summary>True only while the mod crest is equipped (or the gate is disabled).</summary>
    internal static bool GateOpen =>
        !RandomCrestModPlugin.OnlyOnRandomCrest || CrestService.IsRandomCrestEquipped();

    /// <summary>Feature on AND the random crest is equipped: substitutions / blocking / icons apply.</summary>
    internal static bool RandomToolsActive => RandomToolsEnabled && GateOpen;

    internal static bool RandomSpellsActive => RandomCrestModPlugin.EnableRandomSpells && GateOpen;

    internal static int UsesPerBench => Mathf.Max(1, RandomCrestModPlugin.ToolUsesPerBench.Value);

    /// <summary>
    /// Called every frame. Refills the shared counter the first time the mod crest becomes active
    /// (the bench refill covers later rests).
    /// </summary>
    internal static void Tick()
    {
        if (!RandomToolsActive)
        {
            _initialized = false;
            RestoreToggleState();
            return;
        }

        // A pick that never turned into a throw (e.g. the shared budget ran out) leaves the toggle
        // flipped; put it back once the throw window is safely over.
        if (_togglePendingRestore && !_picking && Time.time - _toggleSetTime > 0.5f)
        {
            RestoreToggleState();
        }

        if (!_initialized)
        {
            _initialized = true;
            ResetUses();
        }
    }

    /// <summary>Builds the Red/Skill pools from every tool in the game (locked ones included).</summary>
    internal static void EnsurePools()
    {
        if (_poolsBuilt && RedPool.Count > 0)
        {
            return;
        }

        if (ManagerSingleton<ToolItemManager>.UnsafeInstance == null)
        {
            return;
        }

        Excluded.Clear();
        foreach (var name in DefaultExcluded)
        {
            Excluded.Add(name);
        }

        RedPool.Clear();
        SkillPool.Clear();

        foreach (var tool in ToolItemManager.GetAllTools())
        {
            if (tool == null || Excluded.Contains(tool.name))
            {
                continue;
            }

            switch (tool.Type)
            {
                case ToolItemType.Red:
                    RedPool.Add(tool);
                    break;
                case ToolItemType.Skill:
                    SkillPool.Add(tool);
                    break;
            }
        }

        _poolsBuilt = true;
        RandomCrestModPlugin.Log($"[RandomTool] pools built: red={RedPool.Count}, skill={SkillPool.Count}");
    }

    /// <summary>
    /// Picks the stand-in tool for a throw. Normal Red picks mirror the shared counter onto the
    /// chosen tool so the vanilla empty check lines up with the shared budget.
    /// </summary>
    internal static ToolItem? PickRedForUse()
    {
        EnsurePools();
        var pick = Pick(RedPool);
        if (pick == null)
        {
            return null;
        }

        SetAmount(pick, _usesLeft);

        if (IsToggleTool(pick))
        {
            RollToggleState();
        }

        return pick;
    }

    internal static ToolItem? PickSkill()
    {
        EnsurePools();
        return Pick(SkillPool);
    }

    private static ToolItem? Pick(List<ToolItem> pool)
    {
        return pool.Count == 0 ? null : pool[UnityEngine.Random.Range(0, pool.Count)];
    }

    internal static bool IsRedPoolTool(ToolItem? tool)
    {
        return tool != null && RedPool.Contains(tool);
    }

    /// <summary>True for pool tools that follow the shared use counter.</summary>
    internal static bool IsSharedCounterTool(ToolItem? tool)
    {
        return IsRedPoolTool(tool);
    }

    private static bool IsToggleTool(ToolItem? tool)
    {
        return tool != null && string.Equals(tool.name, ToggleToolName, StringComparison.Ordinal);
    }

    internal static bool IsSpoofed(ToolItem? tool)
    {
        return tool != null && ReferenceEquals(tool, _spoofTool);
    }

    internal static void SetSpoof(ToolItem tool, AttackToolBinding binding)
    {
        _spoofTool = tool;
        _spoofBinding = binding;
    }

    /// <summary>True when a random Red throw must be refused because the shared budget is spent.</summary>
    internal static bool IsOutOfUses(ToolItem? tool)
    {
        return RandomToolsActive && _usesLeft <= 0 && IsRedPoolTool(tool);
    }

    /// <summary>Called at every bench (and when the mod crest first becomes active).</summary>
    internal static void ResetUses()
    {
        if (!RandomToolsActive)
        {
            return;
        }

        EnsurePools();
        _usesLeft = UsesPerBench;
        ApplyUsesToAllRedTools();
    }

    /// <summary>
    /// Drives the shared budget down after a throw. The vanilla code already decremented the thrown
    /// tool (unless it is a custom-usage tool), so mirror that back onto the whole pool.
    /// </summary>
    internal static void ConsumeUse(ToolItem usedTool)
    {
        if (!RandomToolsActive || usedTool == null || usedTool.Type != ToolItemType.Red)
        {
            return;
        }

        var after = usedTool.SavedData.AmountLeft;
        _usesLeft = after < _usesLeft ? after : _usesLeft - 1;
        if (_usesLeft < 0)
        {
            _usesLeft = 0;
        }

        ApplyUsesToAllRedTools();
        ToolItemManager.ReportAllBoundAttackToolsUpdated();
    }

    /// <summary>
    /// Temporarily clears the pool tools' replenish resource so the bench refill is free. Restored
    /// by <see cref="EndFreeRefill"/> so tool refills on other crests still cost shards.
    /// </summary>
    internal static void BeginFreeRefill()
    {
        if (!RandomToolsActive)
        {
            return;
        }

        EnsurePools();
        _replenishResourceField ??= AccessTools.Field(typeof(ToolItem), "replenishResource");
        if (_replenishResourceField == null)
        {
            return;
        }

        SavedResources.Clear();
        foreach (var tool in RedPool)
        {
            try
            {
                var original = (ToolItem.ReplenishResources)_replenishResourceField.GetValue(tool);
                SavedResources[tool] = original;
                _replenishResourceField.SetValue(tool, ToolItem.ReplenishResources.None);
            }
            catch (Exception e)
            {
                RandomCrestModPlugin.LogError("[RandomTool] BeginFreeRefill failed: " + e.Message);
            }
        }
    }

    internal static void EndFreeRefill()
    {
        if (SavedResources.Count == 0 || _replenishResourceField == null)
        {
            return;
        }

        foreach (var pair in SavedResources)
        {
            try
            {
                _replenishResourceField.SetValue(pair.Key, pair.Value);
            }
            catch (Exception e)
            {
                RandomCrestModPlugin.LogError("[RandomTool] EndFreeRefill failed: " + e.Message);
            }
        }

        SavedResources.Clear();
    }

    /// <summary>Called once per loaded save: rebuild the pools so the next active frame refills them.</summary>
    internal static void OnSaveLoaded()
    {
        // The game swapped in a fresh PlayerData; forget the previous save's Voltvessels snapshot.
        _toggleTracked = false;
        _togglePendingRestore = false;
        _poolsBuilt = false;
        _initialized = false;
        EnsurePools();
    }

    private static void ApplyUsesToAllRedTools()
    {
        foreach (var tool in RedPool)
        {
            SetAmount(tool, _usesLeft);
        }
    }

    /// <summary>
    /// Rolls Voltvessels between its two vanilla forms (thrown bola / staked spear) for this pick.
    /// The form is a persisted PlayerData bool, so the player's own value is snapshotted the first
    /// time we touch it and restored afterwards - the save is left exactly as the player had it.
    /// </summary>
    private static void RollToggleState()
    {
        try
        {
            var playerData = PlayerData.instance;
            if (playerData == null)
            {
                return;
            }

            var current = playerData.GetVariable<bool>(ToggleStateField);
            if (!_toggleTracked)
            {
                _toggleTracked = true;
                _toggleOriginal = current;
            }

            var desired = UnityEngine.Random.value < 0.5f;
            if (current != desired)
            {
                playerData.SetVariable(ToggleStateField, desired);
            }

            _togglePendingRestore = true;
            _toggleSetTime = Time.time;
            RandomCrestModPlugin.Log("[RandomTool] Voltvessels rolled to " + (desired ? "thrown" : "staked") + " form.");
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[RandomTool] RollToggleState failed: " + e.Message);
        }
    }

    /// <summary>Puts the player's own Voltvessels form back; safe to call when nothing was changed.</summary>
    private static void RestoreToggleState()
    {
        if (!_toggleTracked)
        {
            return;
        }

        try
        {
            var playerData = PlayerData.instance;
            if (playerData == null)
            {
                // PlayerData is momentarily unavailable (scene load); retry on a later Tick.
                return;
            }

            if (playerData.GetVariable<bool>(ToggleStateField) != _toggleOriginal)
            {
                playerData.SetVariable(ToggleStateField, _toggleOriginal);
            }

            _toggleTracked = false;
            _togglePendingRestore = false;
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[RandomTool] RestoreToggleState failed: " + e.Message);
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
