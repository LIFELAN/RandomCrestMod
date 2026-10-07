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
/// The vanilla HUD tints the icon with the per-binding active/inactive colour and, while the tool
/// is poisoned / zapped, recolours it through the "RECOLOUR" / "CAN_HUESHIFT" shader keywords. The
/// random icons (including the purple poison variant) are already coloured, so keep them untinted
/// and strip those keywords. This patches the <see cref="ToolHudIcon.SetIconColour"/> override
/// rather than the <see cref="RadialHudIcon"/> base so the postfix runs after the vanilla keyword
/// logic (which would otherwise recolour the purple icon back to grey).
/// </summary>
[HarmonyPatch(typeof(ToolHudIcon), "SetIconColour")]
internal static class ToolHudIconColourPatch
{
    [HarmonyPostfix]
    private static void Postfix(SpriteRenderer icon)
    {
        if (!RandomIconService.IsRandomIcon(icon.sprite))
        {
            return;
        }

        icon.color = Color.white;

        var material = icon.material;
        if (material != null)
        {
            material.DisableKeyword("RECOLOUR");
            material.DisableKeyword("CAN_HUESHIFT");
        }
    }
}
