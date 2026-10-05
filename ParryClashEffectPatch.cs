using HarmonyLib;
using HutongGames.PlayMaker.Actions;

namespace RandomCrestMod;

/// <summary>
/// Suppresses the <c>Parry Clash Effect</c> hit spark on Hornet's needle, active only while the
/// Chaos crest is equipped.
///
/// <para>The <c>Parry Clash</c> state of the hero's <c>Silk Specials</c> FSM toggles a child object
/// called <c>Parry Clash Effect</c>. We intercept that activation so the spark never appears; the
/// clash animation, audio, camera shake and every other part of the Cross Stitch (十字绣) remain
/// untouched.</para>
///
/// <para>Intercepting the activation itself is robust against PlayMaker rebuilding its actions
/// (which would drop an <c>Enabled = false</c> edit), and only <i>activations</i> are skipped so the
/// deactivation in the <c>Cancel All</c> state still runs. Other crests keep the vanilla spark.</para>
/// </summary>
[HarmonyPatch]
internal static class ParryClashEffectPatch
{
    private const string EffectObject = "Parry Clash Effect";

    [HarmonyPatch(typeof(ActivateGameObject), nameof(ActivateGameObject.OnEnter))]
    [HarmonyPrefix]
    private static bool ActivateGameObject_OnEnter_Prefix(ActivateGameObject __instance)
    {
        if (RandomCrestModPlugin.EnableParryClashEffect || !CrestService.IsRandomCrestEquipped())
        {
            return true;
        }

        // Keep deactivations intact so the object is still cleaned up by the Cancel All state.
        if (__instance.activate == null || !__instance.activate.Value)
        {
            return true;
        }

        var ownerDefault = __instance.gameObject;
        var target = ownerDefault != null ? ownerDefault.GameObject : null;
        if (target == null || target.Name != EffectObject)
        {
            return true;
        }

        // Skip the spark but still finish the action, so the state can advance normally.
        __instance.Finish();
        return false;
    }
}
