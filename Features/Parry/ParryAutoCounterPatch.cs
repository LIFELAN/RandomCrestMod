using HarmonyLib;

namespace RandomCrestMod;

/// <summary>
/// Installs <see cref="ParryAutoCounterService"/> on the hero after each scene init. The hero's
/// <c>Silk Specials</c> FSM template is shared and initialised before this runs, so the transition
/// edit only has to be (re)applied once per hero initialisation.
/// </summary>
[HarmonyPatch]
internal static class ParryAutoCounterPatch
{
    [HarmonyPatch(typeof(HeroController), "SceneInit")]
    [HarmonyPostfix]
    private static void HeroController_SceneInit_Postfix(HeroController __instance)
    {
        try
        {
            ParryAutoCounterService.Apply(__instance);
        }
        catch (System.Exception e)
        {
            RandomCrestModPlugin.LogError("[ParryAutoCounter] scene init failed: " + e.Message);
        }
    }
}
