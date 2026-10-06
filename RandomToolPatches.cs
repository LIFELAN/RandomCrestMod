using System.Reflection;
using HarmonyLib;

// The GetBoundAttackTool overload we need has an `out` parameter, whose reflected type cannot be
// written as a constant attribute argument, so its target is resolved at runtime via TargetMethod().
// The analyzer cannot see through that (HARMONIZE004) but Harmony applies it correctly.
#pragma warning disable HARMONIZE004

namespace RandomCrestMod;

/// <summary>
/// Opens the substitution window so only the throw path re-rolls a tool. Reporting code also reads
/// the bound tool, but must keep seeing the real equipped one.
/// </summary>
[HarmonyPatch(typeof(HeroController), nameof(HeroController.GetWillThrowTool))]
internal static class GetWillThrowToolWindowPatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        RandomToolService.BeginPick();
    }

    [HarmonyPostfix]
    private static void Postfix()
    {
        RandomToolService.EndPick();
    }
}

/// <summary>
/// Redirects every tool throw / skill cast to a random pool member.
///
/// <para>The substitution happens inside <c>ToolItemManager.GetBoundAttackTool</c> so it lands
/// before <c>HeroController.CanThrowTool</c> evaluates the tool. <c>ThrowTool</c> then re-derives
/// the slot binding from the (unequipped) random tool, so we answer that lookup with the binding
/// the player actually pressed.</para>
/// </summary>
[HarmonyPatch]
internal static class GetBoundAttackToolPatch
{
    /// <summary>
    /// The patch target must be resolved at runtime: the overload we want has an <c>out</c>
    /// parameter, whose reflected type cannot be written as a constant attribute argument.
    /// </summary>
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(ToolItemManager),
            nameof(ToolItemManager.GetBoundAttackTool),
            new[] { typeof(AttackToolBinding), typeof(ToolEquippedReadSource), typeof(AttackToolBinding).MakeByRefType() })!;
    }

    /// <summary>
    /// Swap the equipped attack tool for a random one. Only the live <c>Active</c> read (used by the
    /// throw path) is touched; the HUD read keeps showing the real equipped tool.
    /// </summary>
    [HarmonyPostfix]
    private static void Postfix(
        AttackToolBinding binding,
        ToolEquippedReadSource readSource,
        ref ToolItem? __result)
    {
        if (readSource != ToolEquippedReadSource.Active || __result == null || !RandomToolService.IsPicking)
        {
            return;
        }

        ToolItem? pick = null;
        if (RandomToolService.RandomToolsActive && __result.Type == ToolItemType.Red)
        {
            pick = RandomToolService.PickRedForUse();
        }
        else if (RandomToolService.RandomSpellsActive && __result.Type == ToolItemType.Skill)
        {
            pick = RandomToolService.PickSkill();
        }

        if (pick == null)
        {
            return;
        }

        // Always track the pick (even when it matches the equipped tool) so the shared-use
        // consumption still runs after the throw.
        RandomToolService.SetSpoof(pick, binding);
        __result = pick;
    }
}

/// <summary>
/// The random tool is not equipped anywhere, so the vanilla lookup returns null and
/// <c>ThrowTool</c> would bail out. Give it the binding the player pressed instead.
/// </summary>
[HarmonyPatch(typeof(ToolItemManager), nameof(ToolItemManager.GetAttackToolBinding))]
internal static class GetAttackToolBindingPatch
{
    [HarmonyPostfix]
    private static void Postfix(ToolItem tool, ref AttackToolBinding? __result)
    {
        if (__result == null && RandomToolService.IsSpoofed(tool))
        {
            __result = RandomToolService.SpoofBinding;
        }
    }
}

/// <summary>Report the configured capacity (default 20) for every Red pool tool.</summary>
[HarmonyPatch(typeof(ToolItemManager), nameof(ToolItemManager.GetToolStorageAmount))]
internal static class GetToolStorageAmountPatch
{
    [HarmonyPostfix]
    private static void Postfix(ToolItem tool, ref int __result)
    {
        if (RandomToolService.RandomToolsActive && RandomToolService.IsSharedCounterTool(tool))
        {
            __result = RandomToolService.UsesPerBench;
        }
    }
}

/// <summary>
/// The shared budget is mirrored onto every Red tool's amount, but some tools are usable when empty,
/// so refuse the throw explicitly once the budget is spent.
/// </summary>
[HarmonyPatch(typeof(HeroController), "CanThrowTool", new[] { typeof(ToolItem), typeof(AttackToolBinding), typeof(bool) })]
internal static class CanThrowToolPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ToolItem tool, AttackToolBinding binding, bool reportFailure, ref bool __result)
    {
        if (!RandomToolService.IsOutOfUses(tool))
        {
            return true;
        }

        __result = false;
        if (reportFailure)
        {
            ToolItemManager.ReportBoundAttackToolFailed(binding);
        }

        return false;
    }
}

/// <summary>Spend one shared use after a Red tool was actually thrown.</summary>
[HarmonyPatch(typeof(HeroController), "DidUseAttackTool")]
internal static class DidUseAttackToolPatch
{
    private static readonly AccessTools.FieldRef<HeroController, ToolItem> WillThrow =
        AccessTools.FieldRefAccess<HeroController, ToolItem>("willThrowTool");

    [HarmonyPostfix]
    private static void Postfix(HeroController __instance)
    {
        RandomToolService.NotifyToolConsumed();

        var used = WillThrow(__instance);
        if (used != null && used.Type == ToolItemType.Red && RandomToolService.IsRedPoolTool(used))
        {
            RandomToolService.ConsumeUse(used);
        }
    }
}

/// <summary>
/// Drives the multi-throw barrage. The chain is built on the game's own
/// <c>queuedAutoThrowTool</c> loop (which already waits for the throw animation), but every
/// chained throw re-rolls a fresh random tool instead of reusing the first one. Quick Sling's
/// extra throw is absorbed into the count by <see cref="RandomToolService.ExtraThrowsPerPress"/>.
/// </summary>
[HarmonyPatch(typeof(HeroController), "ThrowTool")]
internal static class ThrowToolBarragePatch
{
    [HarmonyPrefix]
    private static void Prefix(HeroController __instance, bool isAutoThrow)
    {
        RandomToolService.BeforeThrow(__instance, isAutoThrow);
    }

    [HarmonyPostfix]
    private static void Postfix(HeroController __instance, bool isAutoThrow)
    {
        RandomToolService.AfterThrow(__instance, isAutoThrow);
    }
}

/// <summary>
/// Vanilla only replenishes the tools currently equipped to the crest, and normally charges shell
/// shards. While the mod crest is active the whole pool is topped up and the refill is free; the
/// original replenish resources are always restored so other crests still pay.
/// </summary>
[HarmonyPatch(typeof(ToolItemManager), nameof(ToolItemManager.TryReplenishTools))]
internal static class TryReplenishToolsPatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        RandomToolService.BeginFreeRefill();
    }

    [HarmonyPostfix]
    private static void Postfix(bool doReplenish)
    {
        RandomToolService.EndFreeRefill();

        if (doReplenish)
        {
            RandomToolService.ResetUses();
        }
    }
}

/// <summary>
/// While the mod crest's random spells are active every silk skill costs
/// <see cref="RandomCrestModPlugin.RandomSpellSilkDiscount"/> silk less than vanilla: 3 normally,
/// or 2 with the Flea Charm at full health (vanilla is 4 / 3). <c>PlayerData.SilkSkillCost</c> is
/// the single source of truth: the affordability check (<c>HeroController.CanThrowTool</c>), the
/// HUD icon (<c>ToolHudIcon</c>) and every skill FSM's <c>TakeSilk</c> (via
/// <c>GetPlayerDataVariable</c>) all read it, so they stay consistent automatically. Only silk
/// skills are affected; binds (<c>SilkSpool.BindCost</c>) and tools (<c>Usage.SilkRequired</c>)
/// are untouched.
/// </summary>
[HarmonyPatch(typeof(PlayerData), nameof(PlayerData.SilkSkillCost), MethodType.Getter)]
internal static class PlayerDataSilkSkillCostPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref int __result)
    {
        if (RandomToolService.RandomSpellsActive)
        {
            // Vanilla returns 4, or 3 with the Flea Charm at full health. Shave one more silk
            // off so the charm keeps mattering: 4 -> 3, 3 -> 2. Never below 1.
            __result = System.Math.Max(1, __result - RandomCrestModPlugin.RandomSpellSilkDiscount);
        }
    }
}

/// <summary>Fresh save load: rebuild the pools and refill the budget.</summary>
[HarmonyPatch(typeof(GameManager), "SetLoadedGameData", new[] { typeof(SaveGameData), typeof(int) })]
internal static class RandomToolSaveLoadedPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        RandomToolService.OnSaveLoaded();
    }
}
