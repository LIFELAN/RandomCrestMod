using GlobalSettings;
using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// Fixed "random" icons shown on the HUD in place of the actual equipped tool / spell. Tools use
/// the recoloured "Arsenal" achievement icon (with a purple variant while Poison Pouch poisons the
/// equipped tool) and spells use the "all Silk Skills" achievement icon.
/// </summary>
internal static class RandomIconService
{
    /// <summary>Icon shown for the random tool (Red) binding.</summary>
    internal static Sprite? ToolIcon { get; set; }

    /// <summary>Icon shown for the random tool (Red) binding while Poison Pouch is poisoning it.</summary>
    internal static Sprite? PoisonToolIcon { get; set; }

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
            // Quest tools keep their real HUD icon (they are not randomised when equipped).
            if (RandomToolService.IsVanillaEquipTool(tool))
            {
                return null;
            }

            return IsPoisoned(tool) && PoisonToolIcon != null ? PoisonToolIcon : ToolIcon;
        }

        if (tool.Type == ToolItemType.Skill && RandomToolService.RandomSpellsActive)
        {
            return SpellIcon;
        }

        return null;
    }

    /// <summary>
    /// Mirrors <see cref="ToolHudIcon"/>'s own "is the equipped tool poisoned" test so the random
    /// icon turns purple exactly when the vanilla HUD would.
    /// </summary>
    private static bool IsPoisoned(ToolItem tool)
    {
        try
        {
            return tool.PoisonDamageTicks > 0
                && Gameplay.PoisonPouchTool != null
                && Gameplay.PoisonPouchTool.IsEquippedHud;
        }
        catch
        {
            return false;
        }
    }

    internal static bool IsRandomIcon(Sprite? sprite)
    {
        return sprite != null
            && (ReferenceEquals(sprite, ToolIcon)
                || ReferenceEquals(sprite, PoisonToolIcon)
                || ReferenceEquals(sprite, SpellIcon));
    }
}
