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
}
