using HarmonyLib;

namespace RandomCrestMod;

/// <summary>
/// The Reaper crest's bonus silk payout is gated on <c>PlayerData.CurrentCrestID == "Reaper"</c>
/// inside <see cref="HealthManager.TakeDamage(HitInstance)"/> (a plain field, so the
/// <see cref="ToolCrest.IsEquipped"/> spoof does not reach it). When a random bind put the hero
/// into Reaper mode without the Reaper crest actually equipped, temporarily report the Reaper
/// crest for the duration of the damage call so the bonus silk orbs spawn.
/// </summary>
[HarmonyPatch]
internal static class ReaperPayoutPatch
{
    private static string? _savedCrestId;

    [HarmonyPatch(typeof(HealthManager), "TakeDamage", new[] { typeof(HitInstance) })]
    [HarmonyPrefix]
    private static void TakeDamage_Prefix()
    {
        _savedCrestId = null;

        try
        {
            if (!RandomCrestModPlugin.EnableRandomBind)
            {
                return;
            }

            var hero = HeroController.instance;
            var pd = PlayerData.instance;
            if (hero == null || pd == null)
            {
                return;
            }

            if (!hero.ReaperState.IsInReaperMode || pd.CurrentCrestID == "Reaper")
            {
                return;
            }

            _savedCrestId = pd.CurrentCrestID;
            pd.CurrentCrestID = "Reaper";
        }
        catch
        {
            _savedCrestId = null;
        }
    }

    [HarmonyPatch(typeof(HealthManager), "TakeDamage", new[] { typeof(HitInstance) })]
    [HarmonyPostfix]
    private static void TakeDamage_Postfix()
    {
        if (_savedCrestId == null)
        {
            return;
        }

        try
        {
            PlayerData.instance.CurrentCrestID = _savedCrestId;
        }
        catch
        {
            // ignored
        }
        finally
        {
            _savedCrestId = null;
        }
    }
}
