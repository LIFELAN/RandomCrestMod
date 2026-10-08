using System;
using HarmonyLib;
using HutongGames.PlayMaker;

namespace RandomCrestMod;

/// <summary>
/// Rewards a real Needle Phial (储液针管 / "Extractor") hit with one Hornet Statuette while the
/// mod's random tools are live. This is the second of the mod's two extra statue routes.
///
/// <para>A single use awards at most one statue even when the stab lands several damage instances
/// (multi-hit / multiple targets). A new use is opened whenever the Extractor FSM sends its
/// <c>EXTRACTOR</c> event (tool selection and the release that starts the stab both send it), so two
/// separate stabs award two statues. There is no cap.</para>
///
/// <para>The Extractor's stab damage carries <see cref="AttackTypes.ExtractMoss"/>, which is the
/// cleanest signal that the Needle Phial actually dealt damage rather than merely being swung (the
/// <c>EXTRACTOR DMG</c> animation event also fires on a whiff, so it is not used here).</para>
///
/// <para>Only a <b>randomly rolled</b> Needle Phial pays out. If the player has actually equipped the
/// Needle Phial it keeps its vanilla behaviour (it is never substituted), so that deliberate use
/// must not award a statue: <see cref="ToolItemManager.IsToolEquipped(string)"/> reads the real
/// tool slots directly, bypassing the random spoof and custom-usage override.</para>
/// </summary>
[HarmonyPatch]
internal static class ExtractorRewardPatch
{
    private const string ItemName = "Fixer Idol";

    /// <summary>Tool name of the Needle Phial (储液针管).</summary>
    private const string ExtractorToolName = "Extractor";

    private static bool _awardedThisUse;

    /// <summary>Opens a fresh Extractor use, so the next real hit can award again.</summary>
    [HarmonyPatch(typeof(PlayMakerFSM), nameof(PlayMakerFSM.SendEvent), new[] { typeof(string) })]
    [HarmonyPrefix]
    private static void SendEvent_Prefix(string eventName)
    {
        if (eventName == "EXTRACTOR")
        {
            _awardedThisUse = false;
        }
    }

    // Harmony003 is a false positive here: the patch only reads the struct's AttackType field, it
    // does not assign to it.
#pragma warning disable Harmony003
    [HarmonyPatch(typeof(HealthManager), "TakeDamage", new[] { typeof(HitInstance) })]
    [HarmonyPostfix]
    private static void TakeDamage_Postfix(HitInstance hitInstance)
    {
        try
        {
            var attackType = hitInstance.AttackType;
            if (_awardedThisUse
                || attackType != AttackTypes.ExtractMoss
                || !RandomToolService.RandomToolsActive)
            {
                return;
            }

            // Deliberately equipping the Needle Phial must not pay out - only a random roll does.
            // IsToolEquipped(string) reads the real crest slots, so the random spoof / custom-use
            // override does not leak into it.
            if (ToolItemManager.IsToolEquipped(ExtractorToolName))
            {
                return;
            }

            var item = CollectableItemManager.GetItemByName(ItemName);
            if (item == null)
            {
                return;
            }

            _awardedThisUse = true;
            CollectableItemManager.AddItem(item, 1);
            CollectableUIMsg.Spawn(item);
            RandomCrestModPlugin.LogInfo("[ExtractorReward] Needle Phial hit -> +1 Hornet Statuette.");
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[ExtractorReward] failed: " + e.Message);
        }
    }
#pragma warning restore Harmony003
}
