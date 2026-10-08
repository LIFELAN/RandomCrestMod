using HarmonyLib;

namespace RandomCrestMod;

/// <summary>
/// Keeps the mod crest registered and unlocked across save loads. The crest lives in memory only,
/// so it has to be re-added and re-unlocked for whichever save is active.
/// </summary>
[HarmonyPatch]
internal static class CrestPatches
{
    [HarmonyPatch(typeof(GameManager), "SetLoadedGameData", new[] { typeof(SaveGameData), typeof(int) })]
    [HarmonyPostfix]
    private static void GameManager_SetLoadedGameData_Postfix()
    {
        CrestService.EnsureCreated();
        CrestService.EnsureUnlocked();

        // The HUD / Bind Orb is rebuilt for the new save, so re-acquire the frame overlay.
        HudFrameService.Reset();

        // Drop any taunt roll / Beast crest root we were holding.
        RandomTauntService.Reset();

        // Drop any pending cursed-bind roll.
        CursedBindService.Reset();
    }

    /// <summary>Scene load / respawn: the HUD and crest roots can be rebuilt here too.</summary>
    [HarmonyPatch(typeof(HeroController), "SceneInit")]
    [HarmonyPostfix]
    private static void HeroController_SceneInit_Postfix()
    {
        HudFrameService.Reset();
        RandomTauntService.Reset();
        CursedBindService.Reset();
    }

    /// <summary>
    /// The bench crest list is rebuilt from <c>ToolItemManager.GetAllCrests()</c> every time the
    /// pane opens. Make sure the mod crest is registered (the list asset can be reloaded between
    /// saves) and unlocked for the current save before that rebuild happens.
    /// </summary>
    [HarmonyPatch(typeof(InventoryToolCrestList), "Setup")]
    [HarmonyPrefix]
    private static void InventoryToolCrestList_Setup_Prefix()
    {
        CrestService.EnsureCreated();
        CrestService.EnsureUnlocked();
    }

    /// <summary>
    /// <c>PlayerData.CountGameCompletion</c> adds <c>GetUnlockedCrestsCount() - 1</c>, which
    /// includes the always-unlocked mod crest. Remove exactly that one point so the mod crest does
    /// not inflate the save completion percentage. (The ALL_CRESTS achievement is left alone -
    /// because the mod crest is always unlocked its numerator and denominator stay balanced.)
    /// </summary>
    [HarmonyPatch(typeof(PlayerData), nameof(PlayerData.CountGameCompletion))]
    [HarmonyPostfix]
    private static void PlayerData_CountGameCompletion_Postfix(PlayerData __instance)
    {
        if (CrestService.CountsTowardCompletion)
        {
            __instance.completionPercentage -= 1f;
        }
    }
}
