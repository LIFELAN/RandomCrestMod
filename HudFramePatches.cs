using System.Reflection;
using GlobalSettings;
using HarmonyLib;

namespace RandomCrestMod;

/// <summary>
/// Hooks the game's HUD crest-frame transition so the Chaos overlay appears in step with it.
///
/// <para><c>BindOrbHudFrame.DoChangeFrame</c> plays the old crest's <c>Disappear</c> and only then
/// starts the new crest's <c>Appear</c>. Because our crest is not one of the game's known crests,
/// that new frame is always the default (Hunter) one, and our custom overlay has to take over at
/// exactly the moment the game starts that appear - otherwise we either cut off the previous
/// crest's disappear or let Hunter's frame flash through. <c>FrameAppear</c> is the signal we need;
/// <c>AlreadyAppeared</c> covers the instant path that bypasses it.</para>
/// </summary>
[HarmonyPatch]
internal static class HudFramePatches
{
    [HarmonyPatch(typeof(BindOrbHudFrame), "FrameAppear")]
    [HarmonyPostfix]
    private static void FrameAppear_Postfix()
    {
        HudFrameService.NotifyFrameAppear();
    }

    [HarmonyPatch(typeof(BindOrbHudFrame), "FrameDisappear")]
    [HarmonyPostfix]
    private static void FrameDisappear_Postfix()
    {
        HudFrameService.NotifyFrameDisappear();
    }

    [HarmonyPatch(typeof(BindOrbHudFrame), nameof(BindOrbHudFrame.AlreadyAppeared))]
    [HarmonyPostfix]
    private static void AlreadyAppeared_Postfix()
    {
        HudFrameService.NotifyInstantAppear();
    }

    private static FieldInfo? _currentFrameAnimsField;

    /// <summary>
    /// The base Hunter crest and the Chaos crest both resolve to <c>defaultFrameAnims</c>, so
    /// switching between them defeats <c>DoChangeFrame</c>'s "same Idle clip" early-out: the whole
    /// transition (old disappear, new appear and the change sound) is skipped and the HUD swaps
    /// with no effect. Clearing <c>currentFrameAnims</c> makes the next call's <c>basicFrameAnims</c>
    /// null, bypassing that early-out so the transition runs normally. Only done for those two
    /// crests; same-crest refreshes still return earlier on the crest identity check.
    /// </summary>
    [HarmonyPatch(typeof(BindOrbHudFrame), "DoChangeFrame")]
    [HarmonyPrefix]
    private static void DoChangeFrame_Prefix(BindOrbHudFrame __instance)
    {
        try
        {
            if (!CrestService.IsRandomCrestEquipped())
            {
                var hunter = Gameplay.HunterCrest;
                if (hunter == null || !hunter.IsEquipped)
                {
                    return;
                }
            }

            _currentFrameAnimsField ??= AccessTools.Field(typeof(BindOrbHudFrame), "currentFrameAnims");
            _currentFrameAnimsField?.SetValue(__instance, null);
        }
        catch
        {
            // HUD / gameplay settings not ready; let the game behave normally.
        }
    }
}
