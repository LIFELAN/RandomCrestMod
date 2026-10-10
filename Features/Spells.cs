using System;
using HarmonyLib;
using RandomCrestMod.Core;
using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// Each random silk skill (except the Cross Stitch, which has its own rule) can refund the silk it just
/// spent, with a chance that grows with the skills collected. Only the first spend of a cast rolls, so a
/// multi-hit skill cannot stack the odds.
/// </summary>
internal static class SpellSilkRefund
{
    private const string FsmName = "Silk Specials";
    private const string IdleState = "Idle";

    /// <summary>Silk-spending states of the Silk Specials FSM. Cross Stitch and Silk Taunt are absent on purpose.</summary>
    private static readonly string[] SkillCostStates =
    {
        "Start Throw",
        "A Sphere Start",
        "A Sphere Repeat",
        "Silk Bomb Start",
        "Silk Bomb Restart",
        "Silk Charge Begin",
        "BossNeedle Cast",
    };

    private static bool _rolledThisCast;

    internal static bool IsSkillCostState(string? state)
    {
        return !string.IsNullOrEmpty(state) && Array.IndexOf(SkillCostStates, state) >= 0;
    }

    /// <summary>Called right after the game spent silk for a skill cast.</summary>
    internal static void OnSilkSpent(int amount)
    {
        if (_rolledThisCast)
        {
            return;
        }

        _rolledThisCast = true;

        if (!RandomCrestModPlugin.EnableSpellSilkRefund || amount <= 0)
        {
            return;
        }

        var chance = Rules.SpellRefundChance(CollectedSkills());
        if (chance <= 0f || UnityEngine.Random.value >= chance)
        {
            return;
        }

        HeroController.instance?.AddSilk(amount, heroEffect: false);
    }

    /// <summary>Clears the once-per-cast guard when the Silk Specials FSM is back in Idle.</summary>
    internal static void Tick(HeroController? hero)
    {
        if (!Gate.Spells)
        {
            _rolledThisCast = false;
            return;
        }

        var fsm = hero != null ? hero.silkSpecialFSM : null;
        if (fsm == null || fsm.Fsm == null || fsm.Fsm.Name != FsmName || fsm.ActiveStateName == IdleState)
        {
            _rolledThisCast = false;
        }
    }

    private static int CollectedSkills()
    {
        try
        {
            return Mathf.Max(0, ToolItemManager.GetOwnedToolsCount(ToolItemManager.OwnToolsCheckFlags.Skill));
        }
        catch
        {
            return 0;
        }
    }
}

/// <summary>Captures the silk a skill cast spends, while the Silk Specials FSM is in a cost state.</summary>
[HarmonyPatch]
internal static class SpellSilkRefundPatch
{
    [HarmonyPatch(typeof(HeroController), nameof(HeroController.TakeSilk), new[] { typeof(int) })]
    [HarmonyPostfix]
    private static void TakeSilk_Postfix(HeroController __instance, int amount)
    {
        try
        {
            if (!Gate.Spells)
            {
                return;
            }

            var fsm = __instance.silkSpecialFSM;
            if (fsm == null || fsm.Fsm == null || fsm.Fsm.Name != "Silk Specials")
            {
                return;
            }

            if (SpellSilkRefund.IsSkillCostState(fsm.ActiveStateName))
            {
                SpellSilkRefund.OnSilkSpent(amount);
            }
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[SpellSilkRefund] capture failed: " + e.Message);
        }
    }
}
