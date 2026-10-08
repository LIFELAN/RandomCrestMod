using System;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;

namespace RandomCrestMod;

/// <summary>
/// Feeds the cursed-bind roll into the hero's <c>Bind</c> PlayMaker FSM.
///
/// <para>The Bind FSM's <c>Do Bind</c> state runs a <c>PlayerDataVariableTest</c> on
/// <c>PlayerData.IsAnyCursed</c>: expected -> <c>CURSED</c> (into <c>Cursed Bind Start</c>),
/// otherwise <c>FINISHED</c> (into the normal bind). When the current bind rolled cursed we fire
/// the cursed event ourselves and skip the original test, so the game plays its own cursed bind
/// sequence while every earlier gate (silk check, <c>CanBind</c>, ground / sprint / cutscene) has
/// already run normally.</para>
///
/// <para>The override is scoped to that exact FSM + state + variable by
/// <see cref="CursedBindService.ShouldOverride"/>, so the same action used elsewhere is untouched.</para>
/// </summary>
[HarmonyPatch]
internal static class CursedBindPatches
{
    [HarmonyPatch(typeof(PlayerDataVariableTest), nameof(PlayerDataVariableTest.OnEnter))]
    [HarmonyPrefix]
    private static bool PlayerDataVariableTest_OnEnter_Prefix(PlayerDataVariableTest __instance)
    {
        if (!RandomCrestModPlugin.EnableCursedBind || __instance == null)
        {
            return true;
        }

        try
        {
            var variableName = __instance.VariableName != null ? __instance.VariableName.Value : null;
            if (!CursedBindService.ShouldOverride(__instance, variableName))
            {
                return true;
            }

            var target = CursedBindService.ResolveCursedEvent(__instance);
            if (target != null && __instance.Fsm != null)
            {
                __instance.Fsm.Event(target);
            }

            __instance.Finish();
            RandomCrestModPlugin.LogInfo("[CursedBind] Diverted the bind into the Cursed branch.");
            return false;
        }
        catch (Exception e)
        {
            // Never let our override break the bind flow: fall through to the vanilla test.
            RandomCrestModPlugin.LogError("[CursedBind] override failed: " + e.Message);
            return true;
        }
    }
}
