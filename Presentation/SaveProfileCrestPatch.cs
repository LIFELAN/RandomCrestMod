using System.Reflection;
using HarmonyLib;
using UnityEngine.UI;

namespace RandomCrestMod;

/// <summary>
/// The save-slot crest spool is picked by <see cref="SaveProfileHealthBar.ShowHealth"/> from a
/// fixed per-crest sprite table keyed by the private <c>CrestTypes</c> enum. Our runtime crest id
/// ("RandomCrest") cannot be parsed into that enum, so the vanilla code logs
/// "Could not parse crest id RandomCrest" and leaves the slot showing a stale sprite. This patch
/// lets the original method build the health pips while substituting the mod's own spool art.
/// </summary>
[HarmonyPatch(typeof(SaveProfileHealthBar), nameof(SaveProfileHealthBar.ShowHealth))]
internal static class SaveProfileHealthBarPatch
{
    private static readonly FieldInfo? SpoolImageField =
        AccessTools.Field(typeof(SaveProfileHealthBar), "spoolImage");

    /// <summary>
    /// For the mod crest only: remember that this call is ours and swap the id for a valid one so
    /// the vanilla lookup succeeds instead of falling into its error branch. <paramref name="__state"/>
    /// carries that flag to the postfix.
    /// </summary>
    [HarmonyPrefix]
    private static void ShowHealth_Prefix(ref string crestId, out bool __state)
    {
        __state = crestId == CrestService.CrestName;
        if (__state)
        {
            crestId = "Hunter";
        }
    }

    /// <summary>Restores the mod's own spool sprite over the placeholder crest sprite.</summary>
    [HarmonyPostfix]
    private static void ShowHealth_Postfix(SaveProfileHealthBar __instance, bool steelsoulMode, bool __state)
    {
        if (!__state || !RandomCrestModPlugin.EnableCustomSaveSpool)
        {
            return;
        }

        var sprite = SaveSlotCrestService.For(steelsoulMode);
        if (sprite == null || SpoolImageField?.GetValue(__instance) is not Image image)
        {
            return;
        }

        image.sprite = sprite;
    }
}
