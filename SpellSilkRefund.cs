using System;
using HarmonyLib;
using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// Gives every random silk skill - except the Cross Stitch, which keeps its own guaranteed
/// real-parry refund in <see cref="ParrySilkRefund"/> - a chance to refund the silk it just spent.
/// The chance grows with the number of silk skills the player has collected (3% each), so collecting
/// spells pays off.
///
/// <para>Only the <b>first</b> silk spend of a cast rolls, so a multi-hit skill (Thread Sphere /
/// Silk Bomb) cannot stack the odds. The refund is paid immediately on a successful roll, exactly
/// like the Cross Stitch refund, so the silk-spool refill animation reads the same. The silk must
/// still be spent up front, so a skill can never be cast without silk.</para>
///
/// <para>Active only while the mod's random spells are live (the Chaos crest is equipped), the same
/// window that applies the silk discount.</para>
/// </summary>
internal static class SpellSilkRefund
{
    /// <summary>Chance added per collected silk skill.</summary>
    private const float ChancePerSkill = 0.03f;

    /// <summary>
    /// States of the hero's <c>Silk Specials</c> FSM that spend silk for a skill cast. <c>Parry
    /// Start</c> (Cross Stitch) and <c>Silk Taunt</c> are deliberately absent.
    /// </summary>
    private static readonly string[] SkillCostStates =
    {
        "Start Throw",        // Silk Spear
        "A Sphere Start",     // Thread Sphere
        "A Sphere Repeat",    // Thread Sphere (extended)
        "Silk Bomb Start",    // Silk Bomb
        "Silk Bomb Restart",  // Silk Bomb (re-cast)
        "Silk Charge Begin",  // Silk Charge
        "BossNeedle Cast",    // Silk Boss Needle
    };

    /// <summary>Set once the current cast has rolled, so later spends in the same cast do not.</summary>
    private static bool _rolledThisCast;

    internal static bool IsSkillCostState(string? state)
    {
        if (string.IsNullOrEmpty(state))
        {
            return false;
        }

        foreach (var name in SkillCostStates)
        {
            if (state == name)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Called right after the game spent silk for a skill cast. The first spend of a cast rolls; on
    /// success the spent amount is returned immediately, mirroring the Cross Stitch refund.
    /// </summary>
    internal static void OnSkillSilkSpent(int amount)
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

        var chance = ObtainedSkillCount() * ChancePerSkill;
        if (chance <= 0f || UnityEngine.Random.value >= chance)
        {
            return;
        }

        // heroEffect false: the same feedback as the Cross Stitch refund (the spool refill animation
        // is the readable cue, Hornet is mid-cast when this runs).
        HeroController.instance?.AddSilk(amount, heroEffect: false);
    }

    /// <summary>
    /// Clears the once-per-cast guard once the <c>Silk Specials</c> FSM has returned to <c>Idle</c>.
    /// Staying set for the whole cast is what keeps Thread Sphere / Silk Bomb extensions from rolling
    /// a second time.
    /// </summary>
    internal static void Tick()
    {
        if (!RandomToolService.RandomSpellsActive)
        {
            _rolledThisCast = false;
            return;
        }

        var hero = HeroController.instance;
        var fsm = hero != null ? hero.silkSpecialFSM : null;
        if (fsm == null || fsm.Fsm == null || fsm.Fsm.Name != "Silk Specials")
        {
            _rolledThisCast = false;
            return;
        }

        if (fsm.ActiveStateName == "Idle")
        {
            _rolledThisCast = false;
        }
    }

    private static int ObtainedSkillCount()
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

/// <summary>
/// Captures the silk each skill cast spends. <c>Parry Start</c> (Cross Stitch) is left to
/// <see cref="ParrySilkRefund"/> so the two refund rules never both fire for one cast.
/// </summary>
[HarmonyPatch]
internal static class SpellSilkRefundPatch
{
    [HarmonyPatch(typeof(HeroController), nameof(HeroController.TakeSilk), new[] { typeof(int) })]
    [HarmonyPostfix]
    private static void TakeSilk_Postfix(HeroController __instance, int amount)
    {
        try
        {
            if (!RandomToolService.RandomSpellsActive)
            {
                return;
            }

            var fsm = __instance.silkSpecialFSM;
            if (fsm == null || fsm.Fsm == null || fsm.Fsm.Name != "Silk Specials")
            {
                return;
            }

            if (!SpellSilkRefund.IsSkillCostState(fsm.ActiveStateName))
            {
                return;
            }

            SpellSilkRefund.OnSkillSilkSpent(amount);
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[SpellSilkRefund] capture failed: " + e.Message);
        }
    }
}
