namespace RandomCrestMod;

/// <summary>
/// The one place that answers "should the random feature run right now?". Every feature asks the
/// gate instead of combining its own switches with the crest check.
/// </summary>
internal static class Gate
{
    /// <summary>True while the mod's own crest is the equipped one.</summary>
    internal static bool Crest => CrestService.IsRandomCrestEquipped();

    internal static bool Attacks => RandomCrestModPlugin.EnableRandomAttacks && Crest;

    internal static bool Binds => RandomCrestModPlugin.EnableRandomBind && Crest;

    internal static bool Tools => RandomCrestModPlugin.EnableRandomTools && Crest;

    internal static bool Spells => RandomCrestModPlugin.EnableRandomSpells && Crest;

    internal static bool Taunts => RandomCrestModPlugin.EnableRandomTaunt && Crest;
}
