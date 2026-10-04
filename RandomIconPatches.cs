using HarmonyLib;
using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// Replaces the HUD tool / spell sprite with the mod's fixed "random" icon. The vanilla code sets
/// the sprite inside <c>RadialHudIcon.UpdateDisplay</c> via <c>ToolHudIcon.TryGetHudSprite</c>.
/// </summary>
[HarmonyPatch(typeof(ToolHudIcon), "TryGetHudSprite")]
internal static class ToolHudIconSpritePatch
{
    [HarmonyPostfix]
    private static void Postfix(ToolHudIcon __instance, ref Sprite sprite, ref bool __result)
    {
        var custom = RandomIconService.For(__instance.CurrentTool);
        if (custom == null)
        {
            return;
        }

        sprite = custom;
        // true => the vanilla display keeps the larger 1.4x icon scale.
        __result = true;
    }
}

/// <summary>
/// The vanilla HUD tints the icon with the per-binding active/inactive colour. The random icons are
/// already coloured, so keep them untinted.
/// </summary>
[HarmonyPatch(typeof(RadialHudIcon), "SetIconColour")]
internal static class RadialHudIconColourPatch
{
    [HarmonyPostfix]
    private static void Postfix(SpriteRenderer spriteRenderer)
    {
        if (RandomIconService.IsRandomIcon(spriteRenderer.sprite))
        {
            spriteRenderer.color = Color.white;
        }
    }
}
