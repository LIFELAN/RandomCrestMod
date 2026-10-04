using System;
using System.Collections.Generic;
using System.Reflection;
using GlobalEnums;
using HarmonyLib;
using TeamCherry.Localization;
using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// Creates the mod's own crest at runtime by cloning the base Hunter crest, registering it in the
/// live <see cref="ToolCrestList"/> and unlocking it for the current save. The bench crest list is
/// built from <see cref="ToolItemManager.GetAllCrests"/>, so this makes the crest appear as an
/// extra, selectable slot.
/// </summary>
internal static class CrestService
{
    /// <summary>Asset name / PlayerData id of the mod crest.</summary>
    internal const string CrestName = "RandomCrest";

    private const string Sheet = "Tools";
    private const string NameKey = "CREST_RANDOMCREST_NAME";
    private const string DescKey = "CREST_RANDOMCREST_DESC";

    // Slot layout: 1 Red (up) + 1 Skill + 2 Blue + 2 Yellow = 6 slots, all unlocked.
    //
    //   slot0: Skill (0.0, -0.9)
    //   slot1: Red   (0.0,  0.9)  AttackBinding.Up
    //   slot2: Blue  (2.5, -1.8)
    //   slot3: Blue  (-2.5, -1.8)
    //   slot4: Yellow(-1.0, -2.7)
    //   slot5: Yellow(1.0, -2.7)
    //
    // Nav indices walk the circle: red<->skill in the middle, blues on the sides, yellows below.
    private static readonly (ToolItemType Type, AttackToolBinding Binding, float X, float Y,
        int Up, int Down, int Left, int Right)[] Slots =
    {
        (ToolItemType.Skill, AttackToolBinding.Neutral, 0.0f, -0.9f, 1, 4, 3, 2),
        (ToolItemType.Red, AttackToolBinding.Up, 0.0f, 0.9f, -1, 0, 3, 2),
        (ToolItemType.Blue, AttackToolBinding.Neutral, 2.5f, -1.8f, 1, 5, 0, -1),
        (ToolItemType.Blue, AttackToolBinding.Neutral, -2.5f, -1.8f, 1, 4, -1, 0),
        (ToolItemType.Yellow, AttackToolBinding.Neutral, -1.0f, -2.7f, 3, -1, -1, 5),
        (ToolItemType.Yellow, AttackToolBinding.Neutral, 1.0f, -2.7f, 2, -1, 4, -1),
    };

    private static float _nextCreationAttempt;
    private static LanguageCode _injectedLanguage = LanguageCode.N;
    private static FieldInfo? _slotsField;
    private static FieldInfo? _displayNameField;
    private static FieldInfo? _descriptionField;
    private static FieldInfo? _spriteField;
    private static FieldInfo? _silhouetteField;
    private static FieldInfo? _glowField;
    private static FieldInfo? _isHiddenField;
    private static FieldInfo? _crestListField;

    /// <summary>Optional custom crest icon supplied by the plugin (loaded from an embedded PNG).</summary>
    internal static Sprite? CustomIcon { get; set; }

    /// <summary>Optional custom crest silhouette.</summary>
    internal static Sprite? CustomSilhouette { get; set; }

    /// <summary>Optional custom crest glow.</summary>
    internal static Sprite? CustomGlow { get; set; }

    /// <summary>True while the mod's own crest is the equipped crest.</summary>
    internal static bool IsRandomCrestEquipped()
    {
        return PlayerData.HasInstance && PlayerData.instance.CurrentCrestID == CrestName;
    }

    /// <summary>
    /// True while the game's crest counters include the mod crest. The mod crest is unlocked from
    /// the start, so without this the game would count it towards the completion percentage.
    /// </summary>
    internal static bool CountsTowardCompletion
    {
        get
        {
            var crest = GetCrest();
            return crest != null && !crest.IsHidden && crest.IsBaseVersion && crest.IsUnlocked;
        }
    }

    internal static ToolCrest? GetCrest()
    {
        return ToolItemManager.GetCrestByName(CrestName);
    }

    /// <summary>
    /// Creates/registers the crest whenever it is missing. The crest list is a mutable
    /// ScriptableObject that the game resets when switching saves/scenes, so this is deliberately
    /// not gated by a one-shot flag: it re-registers the crest for whichever list instance is live.
    /// </summary>
    internal static void EnsureCreated()
    {
        if (Time.unscaledTime < _nextCreationAttempt)
        {
            return;
        }

        try
        {
            var manager = ManagerSingleton<ToolItemManager>.UnsafeInstance;
            if (manager == null)
            {
                return;
            }

            _crestListField ??= AccessTools.Field(typeof(ToolItemManager), "crestList");
            if (_crestListField?.GetValue(manager) is not ToolCrestList crestList)
            {
                return;
            }

            if (crestList.GetByName(CrestName) != null)
            {
                // The sheets are reloaded when the language changes; re-inject then.
                EnsureLocalisationCurrent();
                return;
            }

            var baseCrest = ToolItemManager.GetCrestByName("Hunter");
            if (baseCrest == null)
            {
                // Crests not loaded yet; try again later.
                return;
            }

            var crest = UnityEngine.Object.Instantiate(baseCrest);
            crest.name = CrestName;

            ApplySlots(crest);
            ApplyLocalisation(crest);
            ApplyVisuals(crest);

            crestList.Add(crest);
            UnlockForCurrentSave(crest);

            RandomCrestModPlugin.Log($"[CrestService] Registered crest '{CrestName}' ({crestList.Count} crests total).");
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[CrestService] EnsureCreated failed: " + e);
            _nextCreationAttempt = Time.unscaledTime + 5f; // back off instead of spamming
        }
    }

    /// <summary>Makes sure the crest is unlocked for the currently loaded save.</summary>
    internal static void EnsureUnlocked()
    {
        var crest = GetCrest();
        if (crest == null || !PlayerData.HasInstance)
        {
            return;
        }

        var data = PlayerData.instance.ToolEquips.GetData(CrestName);
        var expectedSlots = crest.Slots?.Length ?? 0;
        if (data.IsUnlocked && data.Slots != null && data.Slots.Count == expectedSlots)
        {
            return;
        }

        UnlockForCurrentSave(crest);
    }

    private static void ApplySlots(ToolCrest crest)
    {
        _slotsField ??= AccessTools.Field(typeof(ToolCrest), "slots");
        if (_slotsField == null)
        {
            return;
        }

        var slots = new ToolCrest.SlotInfo[Slots.Length];
        for (var i = 0; i < Slots.Length; i++)
        {
            var (type, binding, x, y, up, down, left, right) = Slots[i];
            slots[i] = new ToolCrest.SlotInfo
            {
                Type = type,
                AttackBinding = binding,
                Position = new Vector2(x, y),
                NavUpIndex = up,
                NavDownIndex = down,
                NavLeftIndex = left,
                NavRightIndex = right,
                NavUpFallbackIndex = -1,
                NavDownFallbackIndex = -1,
                NavLeftFallbackIndex = -1,
                NavRightFallbackIndex = -1,
                IsLocked = false,
            };
        }

        _slotsField.SetValue(crest, slots);
    }

    private static void ApplyLocalisation(ToolCrest crest)
    {
        _injectedLanguage = LanguageCode.N;
        EnsureLocalisationCurrent();

        _displayNameField ??= AccessTools.Field(typeof(ToolCrest), "displayName");
        _descriptionField ??= AccessTools.Field(typeof(ToolCrest), "description");
        _displayNameField?.SetValue(crest, new LocalisedString(Sheet, NameKey));
        _descriptionField?.SetValue(crest, new LocalisedString(Sheet, DescKey));
    }

    private static void ApplyVisuals(ToolCrest crest)
    {
        _spriteField ??= AccessTools.Field(typeof(ToolCrest), "crestSprite");
        _silhouetteField ??= AccessTools.Field(typeof(ToolCrest), "crestSilhouette");
        _glowField ??= AccessTools.Field(typeof(ToolCrest), "crestGlow");
        _isHiddenField ??= AccessTools.Field(typeof(ToolCrest), "isHidden");

        if (CustomIcon != null)
        {
            _spriteField?.SetValue(crest, CustomIcon);
        }

        if (CustomSilhouette != null)
        {
            _silhouetteField?.SetValue(crest, CustomSilhouette);
        }

        if (CustomGlow != null)
        {
            _glowField?.SetValue(crest, CustomGlow);
        }

        _isHiddenField?.SetValue(crest, false);
    }

    private static void UnlockForCurrentSave(ToolCrest crest)
    {
        if (!PlayerData.HasInstance)
        {
            return;
        }

        try
        {
            var pd = PlayerData.instance;
            var slots = new List<ToolCrestsData.SlotData>();
            if (crest.Slots != null)
            {
                foreach (var slot in crest.Slots)
                {
                    slots.Add(new ToolCrestsData.SlotData
                    {
                        IsUnlocked = !slot.IsLocked,
                        EquippedTool = string.Empty,
                    });
                }
            }

            var data = pd.ToolEquips.GetData(CrestName);
            data.IsUnlocked = true;
            data.Slots = slots;
            pd.ToolEquips.SetData(CrestName, data);
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[CrestService] UnlockForCurrentSave failed: " + e);
        }
    }

    /// <summary>Injects the crest name / description for the currently active language.</summary>
    private static void EnsureLocalisationCurrent()
    {
        var code = Language.CurrentLanguage();
        if (code == _injectedLanguage)
        {
            return;
        }

        _injectedLanguage = code;
        var chinese = IsChinese(code);
        InjectLocalisation(NameKey, chinese ? "纷乱" : "Chaos");
        InjectLocalisation(
            DescKey,
            chinese ? "你永远不知道下一秒会发生什么。" : "You never know what will happen next.");
    }

    private static bool IsChinese(LanguageCode code)
    {
        return code is LanguageCode.ZH or LanguageCode.ZH_TW or LanguageCode.ZH_HK
            or LanguageCode.ZH_CN or LanguageCode.ZH_SG or LanguageCode.TW;
    }

    private static void InjectLocalisation(string key, string value)
    {
        try
        {
            var languageType = typeof(LocalisedString).Assembly.GetType("TeamCherry.Localization.Language");
            var sheetsField = languageType != null
                ? AccessTools.Field(languageType, "_currentEntrySheets")
                : null;
            if (sheetsField?.GetValue(null) is not Dictionary<string, Dictionary<string, string>> sheets)
            {
                return;
            }

            if (!sheets.TryGetValue(Sheet, out var entries) || entries == null)
            {
                entries = new Dictionary<string, string>();
                sheets[Sheet] = entries;
            }

            entries[key] = value;
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[CrestService] InjectLocalisation failed: " + e);
        }
    }
}
