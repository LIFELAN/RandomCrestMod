using GlobalSettings;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// The crest spoof while the config lease is held. Kept as two narrow hooks: the global
/// <c>ToolCrest.IsEquipped</c> getter (for code that reads it directly) and the PlayMaker
/// <c>CheckIfCrestEquipped</c> action (whose tiny getter Mono may inline past the first hook).
/// </summary>
[HarmonyPatch]
internal static class QuietSwapPatches
{
    /// <summary>
    /// Swallows "HC CONFIG UPDATED" only during a quiet swap. That event globally cancels the Sprint FSM,
    /// so letting it through mid-sprint strands the hero, which then runs out of the scene.
    /// </summary>
    [HarmonyPatch(typeof(FSMUtility), nameof(FSMUtility.SendEventToGameObject), new[] { typeof(GameObject), typeof(string), typeof(bool) })]
    [HarmonyPrefix]
    private static bool SendEventToGameObject_Prefix(string eventName)
    {
        if (eventName != "HC CONFIG UPDATED" || !ConfigLease.Quiet)
        {
            return true;
        }

        var hero = HeroController.instance;
        var cs = hero != null ? hero.cState : null;
        RandomCrestModPlugin.LogDiag(
            "[QuietSwap] swallowed HC CONFIG UPDATED "
            + $"(nailCharging={(cs != null && cs.nailCharging)} "
            + $"transitioning={(cs != null && cs.transitioning)}).");
        return false;
    }
}

[HarmonyPatch]
internal static class SpoofPatches
{
    [HarmonyPatch(typeof(ToolCrest), nameof(ToolCrest.IsEquipped), MethodType.Getter)]
    [HarmonyPostfix]
    private static void ToolCrest_IsEquipped_Postfix(ToolCrest __instance, ref bool __result)
    {
        if (ShamanBridge.ForceSpellCrestForWater)
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
                // Gameplay settings not ready: fall through to the normal spoof.
            }
        }

        if (ConfigLease.IsHeld)
        {
            __result = ReferenceEquals(__instance, ConfigLease.SpoofCrest);
        }
    }

    [HarmonyPatch(typeof(CheckIfCrestEquipped), nameof(CheckIfCrestEquipped.IsTrue), MethodType.Getter)]
    [HarmonyPrefix]
    private static bool CheckIfCrestEquipped_IsTrue_Prefix(CheckIfCrestEquipped __instance, ref bool __result)
    {
        if (!ConfigLease.IsHeld)
        {
            return true;
        }

        var crest = __instance.Crest != null ? __instance.Crest.Value as ToolCrest : null;
        __result = ReferenceEquals(crest, ConfigLease.SpoofCrest);
        return false;
    }
}
