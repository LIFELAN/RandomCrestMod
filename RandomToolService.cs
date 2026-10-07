using System;
using System.Collections.Generic;
using System.Reflection;
using GlobalSettings;
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
/// <c>Silk Snare</c> (Snare Setter), <c>Rosary Cannon</c> and <c>Screw Attack</c> (Delver's Drill)
/// are excluded from the pool, and
/// <c>Lightning Rod</c> (Voltvessels) rolls between its two vanilla forms on every pick.</para>
///
/// <para>Everything here is gated by <see cref="RandomToolsActive"/> / <see cref="RandomSpellsActive"/>
/// so that nothing (counts, refills, forms) leaks onto other crests.</para>
/// </summary>
internal static class RandomToolService
{
    /// <summary>
    /// Internal names excluded from the pool because they do not work when thrown at random:
    /// Extractor (Needle Phial), Silk Snare (Snare Setter), Rosary Cannon (its usage differs and it
    /// misfires under rapid tool use) and Screw Attack (Delver's Drill / 掘洞钻, its downward dive
    /// does not work when thrown at random).
    /// </summary>
    private static readonly string[] DefaultExcluded = { "Extractor", "Silk Snare", "Rosary Cannon", "Screw Attack" };

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

    /// <summary>Shared capacity gained per Tool Pouch upgrade (vanilla's 25% pouch increase).</summary>
    private const float PouchCapacityIncrease = 0.25f;

    /// <summary>Free-throw chance granted by each obtained red tool.</summary>
    private const float FreeThrowChancePerTool = 0.02f;

    /// <summary>Extra free-throw chance once the Curve Claws have been upgraded to the Curvesickle.</summary>
    private const float FreeThrowCurveclawUpgradeBonus = 0.02f;

    /// <summary>Upper bound for the free-throw chance.</summary>
    private const float FreeThrowChanceCap = 0.4f;

    /// <summary>
    /// Quest tools that never contribute to the free-throw chance. Both are excluded from the random
    /// pool; here they are also the only collectables the chance ignores, so collecting them does not
    /// pay off (they are not thrown at random).
    /// </summary>
    private static readonly string[] FreeThrowExcludedTools = { "Extractor", "Silk Snare" };

    /// <summary>
    /// Quest / utility tools that keep their vanilla behaviour when equipped on the Chaos crest
    /// (Snare Setter / Extractor). They are not swapped out for a random tool, so their quests and
    /// special usage still work.
    /// </summary>
    private static readonly string[] VanillaEquipTools = { "Extractor", "Silk Snare" };

    /// <summary>Tool Pouch upgrade count of the current save (0 when unavailable).</summary>
    internal static int PouchLevel
    {
        get
        {
            try
            {
                return PlayerData.instance != null ? Mathf.Max(0, PlayerData.instance.ToolPouchUpgrades) : 0;
            }
            catch
            {
                return 0;
            }
        }
    }

    /// <summary>
    /// Shared capacity at a bench: the configured base plus 25% per Tool Pouch upgrade, rounded to
    /// the nearest whole use. This keeps the Tool Pouch upgrade meaningful on the Chaos crest
    /// instead of a flat value.
    /// </summary>
    internal static int UsesPerBench
    {
        get
        {
            var baseUses = Mathf.Max(1, RandomCrestModPlugin.ToolUsesPerBench.Value);
            var scaled = baseUses * (1f + (PouchCapacityIncrease * PouchLevel));

            // Round half up (Mathf.RoundToInt uses banker's rounding).
            return Mathf.Max(1, Mathf.FloorToInt(scaled + 0.5f));
        }
    }

    /// <summary>
    /// Called every frame. Refills the shared counter the first time the mod crest becomes active
    /// (the bench refill covers later rests).
    /// </summary>
    internal static void Tick()
    {
        if (!RandomToolsActive)
        {
            _initialized = false;
            EndChain(HeroController.instance);
            RestoreToggleState();
            return;
        }

        // Abort a running barrage if the hero can no longer continue (death / scene change /
        // budget spent), so a stale queued auto-throw can never fire later on its own.
        if (_chainArmed)
        {
            var hero = HeroController.instance;
            var cs = hero != null ? hero.cState : null;
            if (hero == null || cs == null || cs.dead || cs.hazardRespawning || cs.transitioning || _usesLeft <= 0)
            {
                EndChain(hero);
            }
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
        return PickRedForUse(requireThrowPrefab: false);
    }

    /// <summary>
    /// Picks a random Red tool. When <paramref name="requireThrowPrefab"/> is set only tools the
    /// throw path can actually spawn are considered; used for chained barrage throws so a
    /// non-projectile tool can never break the chain.
    /// </summary>
    internal static ToolItem? PickRedForUse(bool requireThrowPrefab)
    {
        EnsurePools();

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

        SetAmount(pick, _usesLeft);

        if (IsToggleTool(pick))
        {
            RollToggleState();
        }

        return pick;
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

    // ------------------------------------------------------------------ multi-throw barrage

    private static AccessTools.FieldRef<HeroController, ToolItem>? _willThrowRef;
    private static AccessTools.FieldRef<HeroController, bool>? _queuedAutoThrowRef;

    private static int _chainRemaining;
    private static AttackToolBinding _chainBinding;
    private static bool _chainArmed;
    private static ToolItem? _candidate;
    private static bool _throwConsumed;
    private static readonly List<ToolItem> ThrowablePool = new();

    internal static bool ChainActive => _chainArmed;

    internal static ToolItem? SpoofTool => _spoofTool;

    internal static int UsesLeft => _usesLeft;

    /// <summary>
    /// Number of extra throws a single press should produce. Tool Pouch upgrades each grant one
    /// extra throw; Quick Sling contributes its own extra throw as well (absorbed here rather than
    /// left to the game's own queue, so both work together).
    /// </summary>
    internal static int ExtraThrowsPerPress
    {
        get
        {
            var extra = PouchLevel;
            if (QuickSlingEquipped)
            {
                extra++;
            }

            return Mathf.Max(0, extra);
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

    /// <summary>
    /// Called from the <c>ThrowTool</c> prefix, before the game resolves/nulls its private
    /// <c>willThrowTool</c>. Captures the tool about to be thrown and resets the chain on a fresh
    /// manual press.
    /// </summary>
    internal static void BeforeThrow(HeroController hero, bool isAutoThrow)
    {
        _throwConsumed = false;
        _candidate = GetWillThrow(hero);
    }

    /// <summary>Called from the <c>DidUseAttackTool</c> postfix: a tool was actually thrown.</summary>
    internal static void NotifyToolConsumed()
    {
        _throwConsumed = true;
    }

    /// <summary>
    /// Called from the <c>ThrowTool</c> postfix. Drives the barrage by re-rolling the next tool and
    /// re-arming the game's own <c>queuedAutoThrowTool</c> chain (which already gates on the throw
    /// animation), so no custom timing loop is needed.
    /// </summary>
    internal static void AfterThrow(HeroController hero, bool isAutoThrow)
    {
        try
        {
            // Not our crest: never touch the game's throw state (that would cancel e.g. vanilla
            // Quick Sling double throws on every other crest). A pending barrage is torn down by
            // Tick when the gate closes.
            if (!RandomToolsActive)
            {
                return;
            }

            // Only a real spawned throw (the ThrowPrefab path) sets isToolThrowing; FSM-event tools
            // return before that and must never arm a chain.
            if (!_throwConsumed || hero == null || !hero.cState.isToolThrowing)
            {
                if (isAutoThrow)
                {
                    EndChain(hero);
                }

                return;
            }

            if (_candidate == null || _candidate.Type != ToolItemType.Red)
            {
                EndChain(hero);
                return;
            }

            if (!isAutoThrow)
            {
                _chainRemaining = ExtraThrowsPerPress;
                _chainBinding = _spoofBinding;
                _chainArmed = _chainRemaining > 0;
                RandomCrestModPlugin.Log(
                    $"[RandomTool] barrage armed: +{_chainRemaining} extra (pouch={PouchLevel}, quickSling={QuickSlingEquipped}).");
            }
            else
            {
                _chainRemaining--;
            }

            if (_chainRemaining > 0 && _usesLeft > 0)
            {
                var next = PickRedForUse(requireThrowPrefab: true);
                if (next != null)
                {
                    SetWillThrow(hero, next);
                    SetSpoof(next, _chainBinding);
                    SetQueuedAutoThrow(hero, true);
                    RandomCrestModPlugin.Log(
                        $"[RandomTool] barrage -> tool='{next.name}' ({_chainRemaining} left).");
                    return;
                }
            }

            EndChain(hero);
        }
        catch (Exception e)
        {
            EndChain(hero);
            RandomCrestModPlugin.LogError("[RandomTool] barrage failed: " + e.Message);
        }
    }

    private static void EndChain(HeroController? hero)
    {
        // Only clear the game's fields when we actually own a live chain. Otherwise a frame with no
        // barrage (including every frame on any other crest) must leave vanilla throw state alone.
        var hadChain = _chainArmed || _chainRemaining > 0;
        _chainArmed = false;
        _chainRemaining = 0;

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

        // Tool Pouch free throw: the game already decremented the thrown tool, so instead of
        // spending a use we mirror the untouched budget back onto every tool.
        if (RollFreeThrow())
        {
            ApplyUsesToAllRedTools();
            ToolItemManager.ReportAllBoundAttackToolsUpdated();
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
    /// Free-throw chance now scales with the number of <b>collected</b> red tools (2% each) instead
    /// of the Tool Pouch level, so every tool pickup matters. The Curvesickle upgrade adds another
    /// 2%. Still capped so an abnormal save cannot reach a guaranteed free throw.
    /// </summary>
    private static bool RollFreeThrow()
    {
        var toolCount = ObtainedRedToolCount();
        var upgraded = CurveclawUpgraded;
        var chance = toolCount * FreeThrowChancePerTool;
        if (upgraded)
        {
            chance += FreeThrowCurveclawUpgradeBonus;
        }

        chance = Mathf.Min(FreeThrowChanceCap, chance);
        RandomCrestModPlugin.Log(
            $"[RandomTool] free-throw chance={chance:P0} (tools={toolCount}, curvesickle={upgraded}).");
        return chance > 0f && UnityEngine.Random.value < chance;
    }

    /// <summary>
    /// Number of red tools the save has obtained. A tool still counts after an upgrade has replaced
    /// / hidden it (e.g. the base Curve Claws once the Curvesickle is owned), matching the rule that
    /// every collection permanently raises the free-throw chance. The two quest tools are ignored.
    /// </summary>
    private static int ObtainedRedToolCount()
    {
        // Group by CountKey so an upgrade line (Curve Claws -> Curvesickle) counts once, exactly
        // like the game's own tool achievement counting. The base tool still counts after an upgrade
        // has hidden/replaced it, so every collection permanently raises the chance.
        var groups = new HashSet<SavedItem>();
        try
        {
            foreach (var tool in ToolItemManager.GetAllTools())
            {
                if (tool == null || tool.Type != ToolItemType.Red || IsFreeThrowExcluded(tool))
                {
                    continue;
                }

                try
                {
                    if (tool.SavedData.IsUnlocked)
                    {
                        groups.Add(tool.CountKey);
                    }
                }
                catch
                {
                    // PlayerData is momentarily unavailable; ignore this tool for now.
                }
            }
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[RandomTool] obtained tool count failed: " + e.Message);
        }

        return groups.Count;
    }

    private static bool IsFreeThrowExcluded(ToolItem tool)
    {
        foreach (var name in FreeThrowExcludedTools)
        {
            if (string.Equals(tool.name, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True for the quest tools that keep their vanilla behaviour when equipped.</summary>
    internal static bool IsVanillaEquipTool(ToolItem? tool)
    {
        if (tool == null)
        {
            return false;
        }

        foreach (var name in VanillaEquipTools)
        {
            if (string.Equals(tool.name, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True once the Curvesickle upgrade (Curve Claws Upgraded) has been obtained.</summary>
    private static bool CurveclawUpgraded
    {
        get
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
        EndChain(null);
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
