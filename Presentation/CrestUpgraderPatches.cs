using HarmonyLib;
using HutongGames.PlayMaker.Actions;

namespace RandomCrestMod;

/// <summary>
/// The crest upgrader (伊娃 / Eva) measures the player's progress with the PlayMaker action
/// <see cref="CountCrestUnlockPoints"/>, which sums the slots of every non-hidden, base-version
/// crest. The mod crest is always unlocked with all slots open, so it would hand the player free
/// "crest points" and trigger every upgrader stage early.
///
/// This postfix removes exactly the mod crest's own contribution - recomputed with the same rules
/// the vanilla action uses - and leaves everything else untouched.
/// </summary>
[HarmonyPatch(typeof(CountCrestUnlockPoints), nameof(CountCrestUnlockPoints.OnEnter))]
internal static class CrestUpgraderPatches
{
    [HarmonyPostfix]
    private static void CountCrestUnlockPoints_Postfix(CountCrestUnlockPoints __instance)
    {
        // Only touch the list the action actually used, so we never subtract something it did not add.
        if (__instance.CrestList?.Value is not ToolCrestList list)
        {
            return;
        }

        var crest = list.GetByName(CrestService.CrestName);
        if (crest == null)
        {
            return;
        }

        // Mirror the exact filters of CountCrestUnlockPoints.OnEnter.
        if (crest.IsHidden || !crest.IsBaseVersion || crest.IsUpgradedVersionUnlocked)
        {
            return;
        }

        var slots = crest.Slots;
        if (slots == null)
        {
            return;
        }

        if (__instance.StoreMaxPoints != null)
        {
            __instance.StoreMaxPoints.Value -= slots.Length;
        }

        if (!crest.IsUnlocked || __instance.StoreCurrentPoints == null)
        {
            return;
        }

        var data = PlayerData.HasInstance
            ? PlayerData.instance.ToolEquips.GetData(CrestService.CrestName)
            : default;

        var unlocked = 0;
        for (var i = 0; i < slots.Length; i++)
        {
            if (!slots[i].IsLocked ||
                (data.Slots != null && i < data.Slots.Count && data.Slots[i].IsUnlocked))
            {
                unlocked++;
            }
        }

        __instance.StoreCurrentPoints.Value -= unlocked;
    }
}
