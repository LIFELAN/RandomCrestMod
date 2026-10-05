using System;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;

// FieldRefAccess is resolved at runtime; the analyzer cannot see through it (HARMONIZE004) but
// Harmony applies it correctly.
#pragma warning disable HARMONIZE004

namespace RandomCrestMod;

/// <summary>
/// Feeds the randomly rolled taunt flavour into the hero's <c>Silk Specials</c> FSM.
///
/// <para>Every override is scoped to a specific state of that one FSM, so the very same PlayMaker
/// actions used by the attack / bind / silk-special flows are untouched. Nothing here mutates
/// PlayerData or any game object.</para>
/// </summary>
[HarmonyPatch]
internal static class RandomTauntPatches
{
    private static AccessTools.FieldRef<ListenForTauntV2, InputHandler>? _inputHandlerRef;

    /// <summary>
    /// Roll the flavour as the taunt button is pressed, before the action sends its FSM event.
    /// Only the hero's Silk Specials listener is touched; any other ListenForTauntV2 user is
    /// ignored.
    /// </summary>
    [HarmonyPatch(typeof(ListenForTauntV2), "OnUpdate")]
    [HarmonyPrefix]
    private static void ListenForTauntV2_OnUpdate_Prefix(ListenForTauntV2 __instance)
    {
        if (!RandomCrestModPlugin.EnableRandomTaunt)
        {
            return;
        }

        try
        {
            var fsm = __instance.Fsm;
            if (fsm == null || fsm.Name != RandomTauntService.FsmName)
            {
                return;
            }

            var input = GetInputHandler(__instance);
            if (input != null && input.inputActions.Taunt.WasPressed)
            {
                RandomTauntService.Roll();
            }
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[RandomTaunt] listener prefix failed: " + e.Message);
        }
    }

    /// <summary>Beast voice: pretend the Beast (Warrior) crest is equipped in the "Voice Type" state.</summary>
    [HarmonyPatch(typeof(CheckIfCrestEquipped), nameof(CheckIfCrestEquipped.IsTrue), MethodType.Getter)]
    [HarmonyPostfix]
    private static void CheckIfCrestEquipped_IsTrue_Postfix(CheckIfCrestEquipped __instance, ref bool __result)
    {
        if (!RandomTauntService.IsActive || !RandomTauntService.IsInState(__instance, RandomTauntService.VoiceState))
        {
            return;
        }

        var crest = __instance.Crest != null ? __instance.Crest.Value as ToolCrest : null;
        if (crest != null && ReferenceEquals(crest, RandomTauntService.BeastCrest))
        {
            // Answer for both outcomes so a lingering attack/bind crest spoof cannot leak in.
            __result = RandomTauntService.IsBeast;
        }
    }

    /// <summary>Ring toss: pretend the Shakra Ring is equipped in the "Silk Check" state.</summary>
    [HarmonyPatch(typeof(CheckIfToolEquipped), nameof(CheckIfToolEquipped.IsTrue), MethodType.Getter)]
    [HarmonyPostfix]
    private static void CheckIfToolEquipped_IsTrue_Postfix(CheckIfToolEquipped __instance, ref bool __result)
    {
        if (!RandomTauntService.IsRings || !RandomTauntService.IsInState(__instance, RandomTauntService.SilkCheckState))
        {
            return;
        }

        var tool = __instance.Tool != null ? __instance.Tool.Value as ToolItem : null;
        if (RandomTauntService.IsShakraRing(tool))
        {
            __result = true;
        }
    }

    /// <summary>
    /// Ring toss: make the equip info the FSM stores (equipped / unlocked / remaining) look like a
    /// full set of rings for the single "Silk Check" pass. Only FSM variables are written - no
    /// PlayerData or tool data is touched.
    /// </summary>
    [HarmonyPatch(typeof(GetToolEquipInfo), "DoAction")]
    [HarmonyPostfix]
    private static void GetToolEquipInfo_DoAction_Postfix(GetToolEquipInfo __instance)
    {
        if (!RandomTauntService.IsRings || !RandomTauntService.IsInState(__instance, RandomTauntService.SilkCheckState))
        {
            return;
        }

        var tool = __instance.Tool != null ? __instance.Tool.Value as ToolItem : null;
        if (!RandomTauntService.IsShakraRing(tool))
        {
            return;
        }

        if (__instance.StoreIsEquipped != null)
        {
            __instance.StoreIsEquipped.Value = true;
        }

        if (__instance.StoreIsUnlocked != null)
        {
            __instance.StoreIsUnlocked.Value = true;
        }

        if (__instance.StoreAmountLeft != null)
        {
            __instance.StoreAmountLeft.Value = UnityEngine.Mathf.Max(__instance.StoreAmountLeft.Value, 10);
        }

        if (__instance.StoreMaxAmount != null)
        {
            __instance.StoreMaxAmount.Value = UnityEngine.Mathf.Max(__instance.StoreMaxAmount.Value, 10);
        }
    }

    /// <summary>
    /// Ring toss: "Has Two Rings" is written by this compare. Force every output true for the
    /// single "Silk Check" pass so whichever output the FSM feeds into BoolAllTrue is satisfied.
    /// </summary>
    [HarmonyPatch(typeof(IntTestToBool), "DoCompare")]
    [HarmonyPostfix]
    private static void IntTestToBool_DoCompare_Postfix(IntTestToBool __instance)
    {
        if (!RandomTauntService.IsRings || !RandomTauntService.IsInState(__instance, RandomTauntService.SilkCheckState))
        {
            return;
        }

        if (__instance.equalBool != null)
        {
            __instance.equalBool.Value = true;
        }

        if (__instance.lessThanBool != null)
        {
            __instance.lessThanBool.Value = true;
        }

        if (__instance.greaterThanBool != null)
        {
            __instance.greaterThanBool.Value = true;
        }
    }

    private static InputHandler? GetInputHandler(ListenForTauntV2 action)
    {
        _inputHandlerRef ??= AccessTools.FieldRefAccess<ListenForTauntV2, InputHandler>("inputHandler");
        return _inputHandlerRef(action);
    }
}
