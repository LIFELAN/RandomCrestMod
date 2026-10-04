using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// Fixed "random" icons shown on the HUD in place of the actual equipped tool / spell. Tools use
/// the recoloured "Arsenal" achievement icon and spells use the "all Silk Skills" achievement icon.
/// </summary>
internal static class RandomIconService
{
    /// <summary>Icon shown for the random tool (Red) binding.</summary>
    internal static Sprite? ToolIcon { get; set; }

    /// <summary>Icon shown for the random spell (Skill) binding.</summary>
    internal static Sprite? SpellIcon { get; set; }

    internal static bool Enabled => RandomCrestModPlugin.EnableRandomIcons;

    /// <summary>Returns the replacement icon for a HUD binding's tool, or null to keep the vanilla one.</summary>
    internal static Sprite? For(ToolItem? tool)
    {
        if (!Enabled || tool == null)
        {
            return null;
        }

        if (tool.Type == ToolItemType.Red && RandomToolService.RandomToolsActive)
        {
            return ToolIcon;
        }

        if (tool.Type == ToolItemType.Skill && RandomToolService.RandomSpellsActive)
        {
            return SpellIcon;
        }

        return null;
    }

    internal static bool IsRandomIcon(Sprite? sprite)
    {
        return sprite != null && (ReferenceEquals(sprite, ToolIcon) || ReferenceEquals(sprite, SpellIcon));
    }
}
