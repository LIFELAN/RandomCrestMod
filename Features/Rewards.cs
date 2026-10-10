using System;
using HarmonyLib;
using HutongGames.PlayMaker;
using RandomCrestMod.Core;

namespace RandomCrestMod;

/// <summary>
/// Every currency or collectable the mod hands out goes through here, so caps and logging live in one
/// place. Sources: taunt offering, refused bind, Tool Pouch upgrades, Needle Phial hits.
/// </summary>
internal static class RewardLedger
{
    private const string StatueItemName = "Fixer Idol";

    /// <summary>
    /// When true a Needle Phial hit does not add a statue beyond the game's own consumable cap
    /// (<c>Gameplay.ConsumableItemCap</c>, read through <c>CollectableItem.IsAtMax()</c>). Set to false
    /// to restore the old unlimited behaviour.
    /// </summary>
    internal const bool StatueRespectsCap = true;

    private static int _lastPouchLevel = -1;

    internal static void PayTaunt(int shardCost, int rosaries, TauntFlavour flavour)
    {
        try
        {
            CurrencyManager.TakeShards(shardCost);
            CurrencyManager.AddGeo(rosaries);
            RandomCrestModPlugin.Log($"[RandomTaunt] {shardCost} shards -> +{rosaries} rosaries ({flavour}).");
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[RandomTaunt] payout failed: " + e.Message);
        }
    }

    internal static void PayCursedSilk(int silkTaken)
    {
        var reward = Rules.CursedRosaries(silkTaken);
        if (reward <= 0)
        {
            return;
        }

        CurrencyManager.AddGeo(reward);
        RandomCrestModPlugin.LogInfo($"[CursedBind] refused bind took {silkTaken} silk -> +{reward} rosaries.");
    }

    /// <summary>Grants one Hornet Statuette. Returns false when the cap is reached.</summary>
    internal static bool TryGrantStatue()
    {
        var item = CollectableItemManager.GetItemByName(StatueItemName);
        if (item == null)
        {
            return false;
        }

        if (StatueRespectsCap && item.IsAtMax())
        {
            return false;
        }

        CollectableItemManager.AddItem(item, 1);
        CollectableUIMsg.Spawn(item);
        return true;
    }

    /// <summary>Polls Tool Pouch upgrades and tops shards up for each one bought on the Chaos crest.</summary>
    internal static void Tick()
    {
        var pd = PlayerData.instance;
        if (pd == null || HeroController.instance == null)
        {
            _lastPouchLevel = -1;
            return;
        }

        var level = pd.ToolPouchUpgrades;
        if (_lastPouchLevel < 0 || level < _lastPouchLevel)
        {
            _lastPouchLevel = level;
            return;
        }

        if (level == _lastPouchLevel)
        {
            return;
        }

        var gained = level - _lastPouchLevel;
        _lastPouchLevel = level;

        // The baseline advances even off-crest, so an upgrade bought elsewhere is never paid out later.
        if (!Gate.Crest)
        {
            return;
        }

        var shards = Rules.ShardsForPouchUpgrades(gained);
        CurrencyManager.AddShards(shards);
        RandomCrestModPlugin.LogInfo($"[ToolPouch] +{gained} upgrade(s) -> +{shards} shards (capped by the game).");
    }

    /// <summary>Save load: rebaseline so existing upgrades are not paid out.</summary>
    internal static void OnSaveLoaded()
    {
        _lastPouchLevel = -1;
    }
}

/// <summary>
/// Needle Phial (Extractor) hits give one statue per use while the random tools are live. A use is one
/// <c>EXTRACTOR</c> event; a multi-hit stab pays once.
/// </summary>
[HarmonyPatch]
internal static class ExtractorRewardPatch
{
    private const string ExtractorToolName = "Extractor";

    private static bool _awardedThisUse;

    [HarmonyPatch(typeof(PlayMakerFSM), nameof(PlayMakerFSM.SendEvent), new[] { typeof(string) })]
    [HarmonyPrefix]
    private static void SendEvent_Prefix(string eventName)
    {
        if (eventName == "EXTRACTOR")
        {
            _awardedThisUse = false;
        }
    }

#pragma warning disable Harmony003
    [HarmonyPatch(typeof(HealthManager), "TakeDamage", new[] { typeof(HitInstance) })]
    [HarmonyPostfix]
    private static void TakeDamage_Postfix(HitInstance hitInstance)
    {
        try
        {
            if (_awardedThisUse || hitInstance.AttackType != AttackTypes.ExtractMoss || !Gate.Tools)
            {
                return;
            }

            // A deliberately equipped Needle Phial is vanilla and must not pay out.
            if (ToolItemManager.IsToolEquipped(ExtractorToolName))
            {
                return;
            }

            _awardedThisUse = RewardLedger.TryGrantStatue();
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[ExtractorReward] failed: " + e.Message);
        }
    }
#pragma warning restore Harmony003
}
