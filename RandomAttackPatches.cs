using System.Collections.Generic;
using System.Reflection.Emit;
using GlobalEnums;
using GlobalSettings;
using HarmonyLib;
using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// Redirects every attack to a random crest moveset.
///
/// <para><c>HeroController.Attack()</c> internally calls <c>UpdateConfig()</c> right before it
/// reads the active config, so a prefix on <c>Attack</c> alone would be overwritten. We mark the
/// request in the <c>Attack</c> prefix and actually substitute the config group in the
/// <c>UpdateConfig</c> postfix, which runs at exactly the right moment.</para>
/// </summary>
[HarmonyPatch]
internal static class RandomAttackPatches
{
    [HarmonyPatch(typeof(HeroController), "Attack", new[] { typeof(AttackDirection) })]
    [HarmonyPrefix]
    private static void Attack_Prefix(HeroController __instance, AttackDirection attackDir)
    {
        var cs = __instance.cState;
        RandomAttackService.RequestRandom(attackDir, cs.wallSliding || cs.wallScrambling);
    }

    [HarmonyPatch(typeof(HeroController), "UpdateConfig")]
    [HarmonyPostfix]
    private static void UpdateConfig_Postfix(HeroController __instance)
    {
        RandomAttackService.ApplyIfRequested(__instance);
    }

    /// <summary>
    /// While a random attack is in progress, make the crest checks agree with the random config
    /// group. PlayMaker's <c>CheckIfCrestEquipped</c> reads this property, as does the attack and
    /// bind code, so the branching lines up with the hitbox/animation we installed.
    /// </summary>
    [HarmonyPatch(typeof(ToolCrest), nameof(ToolCrest.IsEquipped), MethodType.Getter)]
    [HarmonyPostfix]
    private static void ToolCrest_IsEquipped_Postfix(ToolCrest __instance, ref bool __result)
    {
        // Surface-water safety net: while a random Shaman air bind is falling into water, pretend
        // the Spell crest is equipped for the duration of the water trigger so the water catches
        // the hero instead of rejecting them (which would let them tunnel out of the scene).
        if (RandomAttackService.ForceSpellCrestForWater)
        {
            try
            {
                if (ReferenceEquals(Gameplay.SpellCrest, __instance))
                {
                    __result = true;
                    return;
                }
            }
            catch
            {
                // Gameplay settings not ready; fall through to the normal spoof.
            }
        }

        if (RandomAttackService.IsSpoofing)
        {
            __result = ReferenceEquals(__instance, RandomAttackService.SpoofCrest);
        }
    }

    /// <summary>
    /// The game's <c>SurfaceWaterRegion</c> pushes a binding hero out of the water unless the Spell
    /// (Shaman) crest is equipped. A randomly rolled Shaman air bind is in the <c>Shaman Fall</c>
    /// state and keeps re-applying its own downward velocity, so the one-off push is not enough and
    /// the hero falls straight through. When the Bind FSM is in a Shaman air state, force the water
    /// to take its normal entry path for the duration of the trigger.
    /// </summary>
    [HarmonyPatch(typeof(SurfaceWaterRegion), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void SurfaceWaterRegion_OnTriggerEnter2D_Prefix(Collider2D collision)
    {
        try
        {
            if (collision == null || !RandomCrestModPlugin.EnableRandomBind)
            {
                RandomAttackService.ForceSpellCrestForWater = false;
                return;
            }

            var hero = collision.GetComponent<HeroController>();
            RandomAttackService.ForceSpellCrestForWater =
                hero != null && hero.cState.isBinding && RandomAttackService.IsShamanAirBind(hero);
        }
        catch
        {
            RandomAttackService.ForceSpellCrestForWater = false;
        }
    }

    [HarmonyPatch(typeof(SurfaceWaterRegion), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void SurfaceWaterRegion_OnTriggerEnter2D_Postfix()
    {
        RandomAttackService.ForceSpellCrestForWater = false;
    }

    /// <summary>
    /// <c>HeroController.IsShamanCrestEquipped</c> reads <c>PlayerData.CurrentCrestID</c> rather
    /// than <c>SpellCrest.IsEquipped</c>, so our config spoof does not reach it. <c>TransitionPoint</c>
    /// uses it to decide whether a binding hero may cross a scene gate; with the Chaos crest equipped
    /// that check always fails, so a randomly rolled Shaman bind is repeatedly pushed back out of the
    /// gate while its <c>Shaman Fall</c> state keeps re-applying downward velocity, leaving the hero
    /// stuck on the scene edge.
    ///
    /// <para>We redirect the call itself instead of postfixing <c>IsShamanCrestEquipped</c>, because a
    /// method this small is a prime inlining candidate for Mono's JIT, which would make a detour on
    /// it silently ineffective. The replacement re-implements the vanilla check and ORs our spoof, so
    /// it behaves exactly like vanilla for every other crest / when the mod is off.</para>
    /// </summary>
    [HarmonyPatch(typeof(TransitionPoint), "TryDoTransition")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> TransitionPoint_TryDoTransition_Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var original = AccessTools.Method(typeof(HeroController), nameof(HeroController.IsShamanCrestEquipped));
        var replacement = AccessTools.Method(typeof(RandomAttackService), nameof(RandomAttackService.IsShamanCrestEquippedForTransition));

        foreach (var instruction in instructions)
        {
            if (original != null && instruction.Calls(original))
            {
                yield return new CodeInstruction(OpCodes.Call, replacement);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    /// <summary>
    /// Random bind: install the random crest as soon as the player starts a bind so the Bind FSM
    /// picks up that crest's effect.
    /// </summary>
    [HarmonyPatch(typeof(HeroController), nameof(HeroController.CanBind))]
    [HarmonyPostfix]
    private static void HeroController_CanBind_Postfix()
    {
        RandomBindService.EnsureAttempt();
    }

    /// <summary>
    /// Dash/sprint randomization swaps the crest config without letting the Sprint FSM see
    /// "HC CONFIG UPDATED" (which would globally cancel and strand the sprint).
    /// </summary>
    [HarmonyPatch(typeof(FSMUtility), nameof(FSMUtility.SendEventToGameObject), new[] { typeof(GameObject), typeof(string), typeof(bool) })]
    [HarmonyPrefix]
    private static bool FSMUtility_SendEventToGameObject_Prefix(string eventName)
    {
        return !(RandomAttackService.SuppressConfigUpdated && eventName == "HC CONFIG UPDATED");
    }

    /// <summary>
    /// Random charge slash (蓄力斩): the moment the Nail Arts FSM confirms the art, install a
    /// random crest so its ChargeSlash object and crest branch are used.
    /// </summary>
    [HarmonyPatch(typeof(HeroController), nameof(HeroController.CanNailArt))]
    [HarmonyPostfix]
    private static void HeroController_CanNailArt_Postfix(HeroController __instance, ref bool __result)
    {
        if (__result)
        {
            RandomAttackService.RequestNailArt(__instance);
        }
    }

    /// <summary>
    /// The Sprint FSM calls this at the start of every dash attack. Re-roll the crest for each
    /// dash attack (not just once per sprint).
    /// </summary>
    [HarmonyPatch(typeof(HeroController), nameof(HeroController.IncrementAttackCounter))]
    [HarmonyPostfix]
    private static void HeroController_IncrementAttackCounter_Postfix()
    {
        RandomAttackService.OnAttackCounterForDash();
    }
}
