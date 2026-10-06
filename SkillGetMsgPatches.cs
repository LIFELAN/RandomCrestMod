using HarmonyLib;
using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// The skill-get prompt ("Silk_Skill_Get_Prompt") shows the currently equipped crest behind the
/// newly acquired skill. Its <c>SkillGetMsg.Setup</c> only copies the crest's <c>CrestSprite</c>
/// and <c>CrestGlow</c> onto the prompt, but the prefab also has a <c>Crest_Silhouette</c> child
/// whose sprite is baked to Hunter's silhouette and never updated. That leaves Hunter's silhouette
/// overlapping the mod crest's icon during the get animation.
///
/// Only the mod crest is touched: every other crest keeps the vanilla behaviour.
/// </summary>
[HarmonyPatch(typeof(SkillGetMsg), "Setup", new[] { typeof(ToolItemSkill) })]
internal static class SkillGetMsgCrestSilhouettePatch
{
    private const string SilhouetteName = "Crest_Silhouette";

    [HarmonyPostfix]
    private static void Postfix(SkillGetMsg __instance)
    {
        if (!CrestService.IsRandomCrestEquipped())
        {
            return;
        }

        var crest = CrestService.GetCrest();
        if (crest == null || crest.CrestSilhouette == null)
        {
            return;
        }

        var silhouette = FindChild(__instance.transform, SilhouetteName);
        if (silhouette != null && silhouette.TryGetComponent<SpriteRenderer>(out var renderer))
        {
            renderer.sprite = crest.CrestSilhouette;
        }

        static Transform? FindChild(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindChild(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
