using HarmonyLib;
using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// Compensates the mod's refused bind (诅咒缚丝). The refused bind still takes <b>all</b> of the
/// hero's silk (vanilla behaviour, left untouched); in return each silk chunk that was taken pays
/// out rosaries.
///
/// <para>The Bind FSM's <c>Cursed Damage</c> state captures the current silk and spends it through
/// <c>HeroController.TakeSilk(amount, SilkSpool.SilkTakeSource.Curse)</c>, so hooking that overload
/// with the <c>Curse</c> source sees the exact amount deducted. Only the mod's own cursed bind pays
/// out (<see cref="CursedBindService.Active"/>); the real Cursed crest's bind is unaffected.</para>
/// </summary>
[HarmonyPatch]
internal static class CursedBindRewardPatch
{
    /// <summary>Rosaries granted per silk chunk taken by the refused bind.</summary>
    private const int RosariesPerSilk = 10;

    [HarmonyPatch(
        typeof(HeroController),
        nameof(HeroController.TakeSilk),
        new[] { typeof(int), typeof(SilkSpool.SilkTakeSource) })]
    [HarmonyPostfix]
    private static void TakeSilk_Postfix(int amount, SilkSpool.SilkTakeSource source)
    {
        try
        {
            if (source != SilkSpool.SilkTakeSource.Curse
                || !RandomCrestModPlugin.EnableCursedBind
                || !CursedBindService.Active)
            {
                return;
            }

            var reward = Mathf.Max(0, amount) * RosariesPerSilk;
            if (reward <= 0)
            {
                return;
            }

            CurrencyManager.AddGeo(reward);
            RandomCrestModPlugin.LogInfo(
                $"[CursedBind] refused bind took {amount} silk -> +{reward} rosaries.");
        }
        catch (System.Exception e)
        {
            RandomCrestModPlugin.LogError("[CursedBind] reward failed: " + e.Message);
        }
    }
}
