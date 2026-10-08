using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// Refills shell shards whenever the player upgrades their Tool Pouch (工具袋) <b>while the Chaos
/// crest is equipped</b>. The Tool Pouch raises the tool capacity on the Chaos crest, so each
/// on-crest upgrade also tops the shard pouch back up.
///
/// <para>To keep the implementation simple the amount is a fixed <c>+800 shards per upgrade</c>
/// rather than "fill to the dynamic cap". The game's own <c>CurrencyManager.AddShards</c> still
/// clamps to the current shard cap (which itself grows by 25% per Tool Pouch upgrade), so this
/// reads as "top the shards up" and never overflows the HUD.</para>
///
/// <para>Upgrades are detected by polling <see cref="PlayerData.ToolPouchUpgrades"/>, which the
/// game's own "Tool Pouch Pickup" increments. The baseline is advanced even while another crest is
/// equipped, so an upgrade made off the Chaos crest is skipped rather than paid out later. The
/// baseline is (re)set on save load via <see cref="Reset"/> so loading a save never pays out for
/// upgrades it already had.</para>
/// </summary>
internal static class ToolPouchShardBonus
{
    /// <summary>Shell shards granted per Tool Pouch upgrade.</summary>
    private const int ShardsPerUpgrade = 800;

    /// <summary>Last seen Tool Pouch level; -1 means "not baselined yet".</summary>
    private static int _lastUpgrades = -1;

    /// <summary>Rebaselines on save load / new game so existing upgrades do not pay out.</summary>
    internal static void Reset()
    {
        _lastUpgrades = -1;
    }

    /// <summary>Per-frame poll for a Tool Pouch upgrade.</summary>
    internal static void Tick()
    {
        var pd = PlayerData.instance;
        if (pd == null || HeroController.instance == null)
        {
            _lastUpgrades = -1;
            return;
        }

        var upgrades = pd.ToolPouchUpgrades;
        if (_lastUpgrades < 0 || upgrades < _lastUpgrades)
        {
            // First frame after load, or the save was swapped out from under us.
            _lastUpgrades = upgrades;
            return;
        }

        if (upgrades == _lastUpgrades)
        {
            return;
        }

        var gained = upgrades - _lastUpgrades;
        _lastUpgrades = upgrades;

        // Chaos only: an upgrade made while another crest is equipped is skipped (the baseline was
        // already advanced above, so it is never paid out retroactively).
        if (!CrestService.IsRandomCrestEquipped())
        {
            return;
        }

        try
        {
            var shards = gained * ShardsPerUpgrade;
            CurrencyManager.AddShards(shards);
            RandomCrestModPlugin.LogInfo(
                $"[ToolPouch] +{gained} pouch upgrade(s) -> +{shards} shards (capped by the game).");
        }
        catch (System.Exception e)
        {
            RandomCrestModPlugin.LogError("[ToolPouch] shard refill failed: " + e.Message);
        }
    }
}
