using System;
using RandomCrestMod.Core;

var failures = 0;
var checks = 0;

void Check(string name, bool ok)
{
    checks++;
    if (!ok)
    {
        failures++;
        Console.WriteLine("FAIL: " + name);
    }
}

void CheckEq<T>(string name, T expected, T actual)
{
    Check($"{name} (expected {expected}, got {actual})", Equals(expected, actual));
}

void CheckNear(string name, float expected, float actual)
{
    Check($"{name} (expected {expected}, got {actual})", Math.Abs(expected - actual) < 1e-5f);
}

// Tool budget: base 16, +25% per pouch level, rounded half up.
CheckEq("uses base", 16, Rules.UsesPerBench(16, 0));
CheckEq("uses pouch 1", 20, Rules.UsesPerBench(16, 1));
CheckEq("uses pouch 2", 24, Rules.UsesPerBench(16, 2));
CheckEq("uses pouch 3", 28, Rules.UsesPerBench(16, 3));
CheckEq("uses rounds half up", 3, Rules.UsesPerBench(2, 1));
CheckEq("uses never below 1", 1, Rules.UsesPerBench(0, 0));
CheckEq("uses negative pouch ignored", 16, Rules.UsesPerBench(16, -2));

// Extra throws need the statue; pouch level plus Quick Sling.
CheckEq("no statue no extra", 0, Rules.ExtraThrows(false, 3, true));
CheckEq("statue pouch 0", 0, Rules.ExtraThrows(true, 0, false));
CheckEq("statue pouch 2", 2, Rules.ExtraThrows(true, 2, false));
CheckEq("statue pouch 2 + quick sling", 3, Rules.ExtraThrows(true, 2, true));

// Free throw: 2% per collected red tool, +2% curvesickle, capped at 40%.
CheckNear("free throw none", 0f, Rules.FreeThrowChance(0, false));
CheckNear("free throw 5 tools", 0.10f, Rules.FreeThrowChance(5, false));
CheckNear("free throw curvesickle", 0.12f, Rules.FreeThrowChance(5, true));
CheckNear("free throw capped", 0.40f, Rules.FreeThrowChance(50, true));

// Spell refund: 3% per collected skill, max six skills.
CheckNear("spell refund 0", 0f, Rules.SpellRefundChance(0));
CheckNear("spell refund 6", 0.18f, Rules.SpellRefundChance(6));

// Silk discount keeps at least one silk.
CheckEq("silk 4 -> 3", 3, Rules.SilkSkillCost(4, 1));
CheckEq("silk 3 -> 2", 2, Rules.SilkSkillCost(3, 1));
CheckEq("silk floor 1", 1, Rules.SilkSkillCost(1, 5));

// Pouch shards and cursed rosaries.
CheckEq("pouch shards 2 levels", 1600, Rules.ShardsForPouchUpgrades(2));
CheckEq("pouch shards negative", 0, Rules.ShardsForPouchUpgrades(-1));
CheckEq("cursed 9 silk", 90, Rules.CursedRosaries(9));
CheckEq("cursed 0 silk", 0, Rules.CursedRosaries(0));

// Taunt weights: 80 / 14 / 6.
CheckEq("taunt 0.0 standard", TauntFlavour.Standard, Rules.RollTaunt(0.0f));
CheckEq("taunt 0.79 standard", TauntFlavour.Standard, Rules.RollTaunt(0.79f));
CheckEq("taunt 0.80 beast", TauntFlavour.Beast, Rules.RollTaunt(0.80f));
CheckEq("taunt 0.93 beast", TauntFlavour.Beast, Rules.RollTaunt(0.93f));
CheckEq("taunt 0.94 rings", TauntFlavour.Rings, Rules.RollTaunt(0.94f));
CheckEq("taunt 0.999 rings", TauntFlavour.Rings, Rules.RollTaunt(0.999f));

// Taunt payouts.
CheckEq("payout beast", 60, Rules.TauntRosaries(TauntFlavour.Beast, 0));
CheckEq("payout rings", 80, Rules.TauntRosaries(TauntFlavour.Rings, 0));
CheckEq("payout standard roll 30", 30, Rules.TauntRosaries(TauntFlavour.Standard, 30));
CheckEq("payout standard capped", 50, Rules.TauntRosaries(TauntFlavour.Standard, 99));
CheckEq("payout standard floor", 0, Rules.TauntRosaries(TauntFlavour.Standard, -5));

// Taunt offer needs completion and 80 shards.
Check("offer needs completion", !Rules.CanOfferTaunt(false, 500));
Check("offer needs shards", !Rules.CanOfferTaunt(true, 79));
Check("offer ok", Rules.CanOfferTaunt(true, 80));

Console.WriteLine($"{checks - failures}/{checks} checks passed.");
return failures == 0 ? 0 : 1;
