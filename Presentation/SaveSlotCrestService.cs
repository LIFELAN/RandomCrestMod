using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// Holds the mod crest's save-slot spool art. The title/load screen draws one spool sprite per
/// crest from a fixed per-crest table, so the mod needs to provide its own art for the slot.
/// </summary>
internal static class SaveSlotCrestService
{
    /// <summary>Spool art supplied by the plugin (loaded from an embedded PNG).</summary>
    internal static Sprite? Normal { get; set; }

    internal static Sprite? For(bool steelsoulMode)
    {
        // Steel Soul currently reuses the same art.
        _ = steelsoulMode;
        return Normal;
    }
}
