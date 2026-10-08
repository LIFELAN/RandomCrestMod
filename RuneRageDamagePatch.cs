using System;
using HarmonyLib;
using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// Doubles the damage of Rune Rage (符文之怒 / Silk Bomb) while the Chaos crest is equipped, so its
/// random spell stays competitive with vanilla's Shaman-crest rune bonus.
///
/// <para>Every Rune Rage blast (the <c>Weaver Bomb Blast</c> and <c>Weaver Bomb Blast Zap</c>
/// prefabs) is a root object with a <see cref="HeroShamanRuneEffect"/> that assigns its child
/// <c>damager</c>'s <see cref="DamageEnemies.DamageMultiplier"/> on Awake / OnEnable. Re-applying
/// that multiplier right after the game has done so doubles every blast without touching the
/// projectile, the FSM or the shared tool data.</para>
///
/// <para>The <see cref="HeroShamanRuneEffect"/> component is also used by several non-rune objects
/// (Needle Throw, Silk Charge, Cross Slash, Parry, ...), so only the Rune Rage blast roots are
/// matched by name.</para>
///
/// <para>Gated by <see cref="RandomToolService.RandomSpellsActive"/>: other crests keep the vanilla
/// Rune Rage damage.</para>
/// </summary>
internal static class RuneRageDamageService
{
    /// <summary>Extra multiplier applied on top of the game's own Rune Rage damage multiplier.</summary>
    internal const float DamageMultiplierBonus = 2f;

    /// <summary>The two Rune Rage blast roots share this name prefix (<c>... Blast</c> / <c>... Blast Zap</c>).</summary>
    private const string BlastRootName = "Weaver Bomb Blast";

    private static readonly AccessTools.FieldRef<HeroShamanRuneEffect, DamageEnemies> DamagerRef =
        AccessTools.FieldRefAccess<HeroShamanRuneEffect, DamageEnemies>("damager");

    /// <summary>
    /// Called after <see cref="HeroShamanRuneEffect.Refresh"/> has recomputed the blast's damage
    /// multiplier. <c>Refresh</c> always assigns from the prefab's original value, so doubling here
    /// never compounds even if the pooled blast is re-enabled many times.
    /// </summary>
    internal static void ApplyAfterRefresh(HeroShamanRuneEffect effect)
    {
        if (!RandomToolService.RandomSpellsActive || effect == null)
        {
            return;
        }

        try
        {
            var go = effect.gameObject;
            if (go == null || !go.name.StartsWith(BlastRootName, StringComparison.Ordinal))
            {
                return;
            }

            var damager = DamagerRef(effect);
            if (damager == null)
            {
                return;
            }

            damager.DamageMultiplier *= DamageMultiplierBonus;
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[RuneRage] damage boost failed: " + e.Message);
        }
    }
}

/// <summary>Applies the Chaos crest's Rune Rage damage boost after the game refreshes the effect.</summary>
[HarmonyPatch(typeof(HeroShamanRuneEffect), nameof(HeroShamanRuneEffect.Refresh))]
internal static class RuneRageDamagePatch
{
    [HarmonyPostfix]
    private static void Postfix(HeroShamanRuneEffect __instance)
    {
        RuneRageDamageService.ApplyAfterRefresh(__instance);
    }
}
