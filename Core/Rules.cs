using System;

namespace RandomCrestMod.Core;

/// <summary>Flavours of the random taunt (R3 / V).</summary>
public enum TauntFlavour
{
    None = 0,
    Standard = 1,
    Beast = 2,
    Rings = 3,
}

/// <summary>
/// Every number-crunching rule of the mod. Nothing here touches Unity or the game, so the rules can
/// be unit tested in isolation (see <c>tests/CoreTests</c>) and tuned in one place.
/// </summary>
public static class Rules
{
    public const int MinUsesPerBench = 1;
    public const float PouchCapacityStep = 0.25f;

    public const float FreeThrowPerTool = 0.02f;
    public const float FreeThrowCurveclawBonus = 0.02f;
    public const float FreeThrowCap = 0.4f;

    public const float SpellRefundPerSkill = 0.03f;

    public const int ShardsPerPouchUpgrade = 800;

    public const int CursedRosariesPerSilk = 10;

    public const int TauntShardCost = 80;
    public const int TauntStandardRosaryMax = 50;
    public const int TauntBeastRosaries = 60;
    public const int TauntRingsRosaries = 80;

    /// <summary>Shared tool budget at a bench: base scaled by 25% per Tool Pouch level, rounded.</summary>
    public static int UsesPerBench(int baseUses, int pouchLevel)
    {
        var baseValue = Math.Max(MinUsesPerBench, baseUses);
        var scaled = baseValue * (1f + PouchCapacityStep * Math.Max(0, pouchLevel));
        return Math.Max(MinUsesPerBench, (int)Math.Floor(scaled + 0.5f));
    }

    /// <summary>Extra throws per press. Requires the Hornet Statuette; Tool Pouch and Quick Sling add one each.</summary>
    public static int ExtraThrows(bool statueHeld, int pouchLevel, bool quickSling)
    {
        if (!statueHeld)
        {
            return 0;
        }

        return Math.Max(0, pouchLevel) + (quickSling ? 1 : 0);
    }

    /// <summary>Chance that a throw costs no shared use: 2% per collected red tool, +2% for the Curvesickle, capped.</summary>
    public static float FreeThrowChance(int collectedRedTools, bool curveclawUpgraded)
    {
        var chance = Math.Max(0, collectedRedTools) * FreeThrowPerTool;
        if (curveclawUpgraded)
        {
            chance += FreeThrowCurveclawBonus;
        }

        return Math.Min(FreeThrowCap, chance);
    }

    /// <summary>Chance that the first silk spend of a random skill cast is refunded: 3% per collected skill.</summary>
    public static float SpellRefundChance(int collectedSkills)
    {
        return Math.Max(0, collectedSkills) * SpellRefundPerSkill;
    }

    /// <summary>Silk cost of a skill after the mod's discount; never below 1.</summary>
    public static int SilkSkillCost(int vanillaCost, int discount)
    {
        return Math.Max(1, vanillaCost - discount);
    }

    public static int ShardsForPouchUpgrades(int gainedLevels)
    {
        return Math.Max(0, gainedLevels) * ShardsPerPouchUpgrade;
    }

    public static int CursedRosaries(int silkTaken)
    {
        return Math.Max(0, silkTaken) * CursedRosariesPerSilk;
    }

    /// <summary>Maps a uniform roll in [0, 1) to a taunt flavour: Standard 80%, Beast 14%, Rings 6%.</summary>
    public static TauntFlavour RollTaunt(float roll)
    {
        if (roll < 0.80f)
        {
            return TauntFlavour.Standard;
        }

        return roll < 0.94f ? TauntFlavour.Beast : TauntFlavour.Rings;
    }

    /// <summary>Rosaries a completed taunt pays out. <paramref name="standardRoll"/> is used for Standard only.</summary>
    public static int TauntRosaries(TauntFlavour flavour, int standardRoll)
    {
        switch (flavour)
        {
            case TauntFlavour.Beast:
                return TauntBeastRosaries;
            case TauntFlavour.Rings:
                return TauntRingsRosaries;
            default:
                return Math.Min(TauntStandardRosaryMax, Math.Max(0, standardRoll));
        }
    }

    /// <summary>A taunt only converts shards when it played to its end and the player can afford the cost.</summary>
    public static bool CanOfferTaunt(bool completed, int shardsHeld)
    {
        return completed && shardsHeld >= TauntShardCost;
    }
}
