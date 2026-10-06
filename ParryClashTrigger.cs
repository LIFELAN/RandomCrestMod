using GlobalEnums;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// Remembers how the current <c>Parry Clash</c> was entered so the two Cross Stitch (十字绣)
/// flavours can keep their own visuals:
///
/// <list type="bullet">
/// <item>A real parry (the stance was struck, FSM event <c>PARRIED</c>) keeps the vanilla
/// <c>Parry Clash Effect</c> hit spark.</item>
/// <item>The auto counter (the stance simply expired, FSM event <c>FINISHED</c>) suppresses the
/// spark and uses the mod's <see cref="HeroHighlight"/> instead.</item>
/// </list>
///
/// <para>The flag is set by <see cref="ParryTriggerPatches"/> the moment the hero FSM is about to
/// receive <c>PARRIED</c>, and reset when a new stance begins / a clash ends, so it always
/// describes the <c>Parry Clash</c> that is running.</para>
/// </summary>
internal static class ParryClashTrigger
{
    /// <summary>True when the current parry was triggered by an actual hit (the <c>PARRIED</c> path).</summary>
    internal static bool Attacked { get; private set; }

    internal static void NotifyAttacked()
    {
        Attacked = true;
    }

    internal static void Reset()
    {
        Attacked = false;
    }
}

/// <summary>
/// Flags a real parry. The hero's <c>Silk Specials</c> FSM only receives the <c>PARRIED</c> event
/// when an actual hit is parried, so we set the flag on the way <i>into</i>
/// <c>PlayMakerFSM.SendEvent</c> - before the FSM handles it.
///
/// <para>The ordering matters: the <c>Parry Clash</c> state (and its <c>Parry Clash Effect</c>
/// activation) is entered from inside that event handling, so a postfix on the game method would
/// already be too late for the effect to see the flag.</para>
/// </summary>
[HarmonyPatch]
internal static class ParryTriggerPatches
{
    [HarmonyPatch(typeof(PlayMakerFSM), nameof(PlayMakerFSM.SendEvent), new[] { typeof(string) })]
    [HarmonyPrefix]
    private static void PlayMakerFSM_SendEvent_Prefix(PlayMakerFSM __instance, string eventName)
    {
        if (eventName != "PARRIED")
        {
            return;
        }

        var hero = HeroController.instance;
        if (hero != null && ReferenceEquals(__instance, hero.silkSpecialFSM))
        {
            ParryClashTrigger.NotifyAttacked();
        }
    }

    // Fallback for the unlikely case the event is delivered on a later FSM tick instead of
    // synchronously (the flag is still set before Parry Clash's effect would be activated then).
    // Both spots that parry a hit clear cState.parrying right after sending PARRIED, and nothing
    // else clears it while it was set, so this is an unambiguous signal too.
    [HarmonyPatch(typeof(HeroController), nameof(HeroController.CheckParry))]
    [HarmonyPrefix]
    private static void CheckParry_Prefix(HeroController __instance, out bool __state)
    {
        __state = __instance.cState.parrying;
    }

    [HarmonyPatch(typeof(HeroController), nameof(HeroController.CheckParry))]
    [HarmonyPostfix]
    private static void CheckParry_Postfix(HeroController __instance, bool __state)
    {
        if (__state && !__instance.cState.parrying)
        {
            ParryClashTrigger.NotifyAttacked();
        }
    }

    [HarmonyPatch(
        typeof(HeroController),
        nameof(HeroController.TakeDamage),
        new[] { typeof(GameObject), typeof(CollisionSide), typeof(int), typeof(HazardType), typeof(DamagePropertyFlags) })]
    [HarmonyPrefix]
    private static void TakeDamage_Prefix(HeroController __instance, out bool __state)
    {
        __state = __instance.cState.parrying;
    }

    [HarmonyPatch(
        typeof(HeroController),
        nameof(HeroController.TakeDamage),
        new[] { typeof(GameObject), typeof(CollisionSide), typeof(int), typeof(HazardType), typeof(DamagePropertyFlags) })]
    [HarmonyPostfix]
    private static void TakeDamage_Postfix(HeroController __instance, bool __state)
    {
        if (__state && !__instance.cState.parrying)
        {
            ParryClashTrigger.NotifyAttacked();
        }
    }
}
