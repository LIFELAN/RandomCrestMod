using System;
using System.Reflection;
using HarmonyLib;
using RandomCrestMod.Core;

// The GetBoundAttackTool overload has an out parameter; its target is resolved at runtime. The analyzer
// cannot see through that (HARMONIZE004) but Harmony applies it correctly.
#pragma warning disable HARMONIZE004

namespace RandomCrestMod;

/// <summary>Opens the substitution window so only the throw path re-rolls a tool.</summary>
[HarmonyPatch(typeof(HeroController), nameof(HeroController.GetWillThrowTool))]
internal static class GetWillThrowToolWindowPatch
{
    [HarmonyPrefix]
    private static void Prefix() => ToolPool.BeginPick();

    [HarmonyPostfix]
    private static void Postfix() => ToolPool.EndPick();
}

/// <summary>Swaps the equipped attack tool for a random pool member during the throw / cast path only.</summary>
[HarmonyPatch]
internal static class GetBoundAttackToolPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(ToolItemManager),
            nameof(ToolItemManager.GetBoundAttackTool),
            new[] { typeof(AttackToolBinding), typeof(ToolEquippedReadSource), typeof(AttackToolBinding).MakeByRefType() })!;
    }

    [HarmonyPostfix]
    private static void Postfix(AttackToolBinding binding, ToolEquippedReadSource readSource, ref ToolItem? __result)
    {
        if (readSource != ToolEquippedReadSource.Active || __result == null || !ToolPool.IsPicking)
        {
            return;
        }

        // The quest tool keeps its vanilla behaviour so its quest can still be finished.
        if (ToolPool.IsVanillaEquip(__result))
        {
            return;
        }

        ToolItem? pick = null;
        if (Gate.Tools && __result.Type == ToolItemType.Red)
        {
            pick = ToolPool.PickRed(requireThrowPrefab: false);
        }
        else if (Gate.Spells && __result.Type == ToolItemType.Skill)
        {
            pick = ToolPool.PickSkill();
        }

        if (pick == null)
        {
            return;
        }

        // Tracked even when the pick matches the equipped tool, so the throw's bookkeeping still runs.
        ToolPool.SetSpoof(pick, binding);
        __result = pick;
    }
}

/// <summary>A random tool is never equipped, so the vanilla lookup returns null. Give back the pressed binding.</summary>
[HarmonyPatch(typeof(ToolItemManager), nameof(ToolItemManager.GetAttackToolBinding))]
internal static class GetAttackToolBindingPatch
{
    [HarmonyPostfix]
    private static void Postfix(ToolItem tool, ref AttackToolBinding? __result)
    {
        if (__result == null && ToolPool.IsSpoofed(tool))
        {
            __result = ToolPool.SpoofBinding;
        }
    }
}

/// <summary>Reports the shared capacity for every red tool while the feature is live.</summary>
[HarmonyPatch(typeof(ToolItemManager), nameof(ToolItemManager.GetToolStorageAmount))]
internal static class GetToolStorageAmountPatch
{
    [HarmonyPostfix]
    private static void Postfix(ToolItem tool, ref int __result)
    {
        if (Gate.Tools && ToolPool.IsRed(tool))
        {
            __result = ToolBudget.Capacity;
        }
    }
}

/// <summary>
/// Custom-usage loops (Rosary Cannon's Shoot Loop, Snare Setter's charge) ask whether their tool is
/// equipped. The random tool is never equipped, so report the spoofed tool as equipped for the Active read.
/// </summary>
[HarmonyPatch(typeof(ToolItemManager), nameof(ToolItemManager.IsToolEquipped), new[] { typeof(ToolItem), typeof(ToolEquippedReadSource) })]
internal static class IsToolEquippedPatch
{
    [HarmonyPostfix]
    private static void Postfix(ToolItem tool, ToolEquippedReadSource readSource, ref bool __result)
    {
        if (!__result && readSource == ToolEquippedReadSource.Active && Gate.Tools && ToolPool.IsSpoofed(tool))
        {
            __result = true;
        }
    }
}

/// <summary>Refuses a red throw once the shared budget is spent, since some tools are usable when empty.</summary>
[HarmonyPatch(typeof(HeroController), "CanThrowTool", new[] { typeof(ToolItem), typeof(AttackToolBinding), typeof(bool) })]
internal static class CanThrowToolPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ToolItem tool, AttackToolBinding binding, bool reportFailure, ref bool __result)
    {
        if (!ToolBudget.IsOutOfUses(tool))
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

/// <summary>After a tool really left the hero, reconcile the shared budget.</summary>
[HarmonyPatch(typeof(HeroController), "DidUseAttackTool")]
internal static class DidUseAttackToolPatch
{
    private static readonly AccessTools.FieldRef<HeroController, ToolItem> WillThrow =
        AccessTools.FieldRefAccess<HeroController, ToolItem>("willThrowTool");

    [HarmonyPostfix]
    private static void Postfix(HeroController __instance)
    {
        ThrowBarrage.NotifyConsumed();

        var used = WillThrow(__instance);
        if (used != null && used.Type == ToolItemType.Red && ToolPool.IsRed(used))
        {
            ToolBudget.Consume(used);
        }
    }
}

/// <summary>Drives the multi-throw barrage from the throw path.</summary>
[HarmonyPatch(typeof(HeroController), "ThrowTool")]
internal static class ThrowToolBarragePatch
{
    [HarmonyPrefix]
    private static void Prefix(HeroController __instance)
    {
        ThrowBarrage.BeforeThrow(__instance);
    }

    [HarmonyPostfix]
    private static void Postfix(HeroController __instance, bool isAutoThrow)
    {
        ThrowBarrage.AfterThrow(__instance, isAutoThrow);
    }
}

/// <summary>
/// Free bench refill for the whole red pool while the feature is live. The replenish cost is restored by a
/// finalizer, so it also comes back when the original method throws.
/// </summary>
[HarmonyPatch(typeof(ToolItemManager), nameof(ToolItemManager.TryReplenishTools))]
internal static class TryReplenishToolsPatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        ToolBudget.BeginFreeRefill();
    }

    [HarmonyPostfix]
    private static void Postfix(bool doReplenish)
    {
        if (doReplenish && Gate.Tools)
        {
            ToolBudget.Reset();
        }
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(Exception? __exception)
    {
        ToolBudget.EndFreeRefill();
        return __exception;
    }
}

/// <summary>Silk skills cost one less while the feature is live (4 -> 3, or 3 -> 2 with the Flea Charm).</summary>
[HarmonyPatch(typeof(PlayerData), nameof(PlayerData.SilkSkillCost), MethodType.Getter)]
internal static class PlayerDataSilkSkillCostPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref int __result)
    {
        if (Gate.Spells)
        {
            __result = Rules.SilkSkillCost(__result, RandomCrestModPlugin.RandomSpellSilkDiscount);
        }
    }
}

/// <summary>Save load: rebuild the pools and forget per-save state.</summary>
[HarmonyPatch(typeof(GameManager), "SetLoadedGameData", new[] { typeof(SaveGameData), typeof(int) })]
internal static class RandomToolSaveLoadedPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        Tools.OnSaveLoaded();
        RewardLedger.OnSaveLoaded();
    }
}

#pragma warning restore HARMONIZE004
