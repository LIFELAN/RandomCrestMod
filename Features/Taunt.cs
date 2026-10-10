using System;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using RandomCrestMod.Core;
using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// Random taunt (R3 / V): standard, Beast (Warrior crest battle-cry) or Shakra Ring toss.
///
/// <para>The taunt is driven by the hero's <c>Silk Specials</c> FSM. The FSM's voice and ring checks are
/// answered by the patches in <see cref="TauntPatches"/>, and the Beast action is the Warrior crest's
/// config held through <see cref="ConfigLease"/> for the taunt's duration. Nothing writes PlayerData.</para>
/// </summary>
internal static class Taunt
{
    internal const string FsmName = "Silk Specials";
    internal const string VoiceState = "Voice Type";
    internal const string SilkCheckState = "Silk Check";
    internal const string TauntEndState = "Taunt End Wait";

    private const string IdleState = "Idle";
    private const string RingToolName = "Shakra Ring";
    private const float SettleDelay = 0.2f;
    private const float StaleTimeout = 5f;

    /// <summary>What the FSM shows and voices. Can fall back to Standard when the Beast config is unavailable.</summary>
    private static TauntFlavour _flavour = TauntFlavour.None;

    /// <summary>What the weighted roll produced. Payout follows this even after a fallback.</summary>
    private static TauntFlavour _rolled = TauntFlavour.None;

    private static bool _completed;
    private static bool _paid;
    private static float _rollTime;
    private static PlayMakerFSM? _fsm;

    internal static bool IsActive => _flavour != TauntFlavour.None;

    internal static bool IsBeast => _flavour == TauntFlavour.Beast;

    internal static bool IsRings => _flavour == TauntFlavour.Rings;

    internal static ToolCrest? BeastCrest
    {
        get
        {
            try
            {
                return GlobalSettings.Gameplay.WarriorCrest;
            }
            catch
            {
                return null;
            }
        }
    }

    internal static bool IsInState(FsmStateAction? action, string stateName)
    {
        try
        {
            var fsm = action != null ? action.Fsm : null;
            return fsm != null && fsm.Name == FsmName && fsm.ActiveStateName == stateName;
        }
        catch
        {
            return false;
        }
    }

    internal static bool IsShakraRing(ToolItem? tool)
    {
        return tool != null && string.Equals(tool.name, RingToolName, StringComparison.Ordinal);
    }

    /// <summary>Called from the listener prefix when the taunt button is pressed.</summary>
    internal static void Roll(HeroController? hero)
    {
        Settle();

        if (!Gate.Taunts)
        {
            Clear();
            return;
        }

        try
        {
            _rolled = Rules.RollTaunt(UnityEngine.Random.value);
            _flavour = _rolled;
            _rollTime = Time.time;
            _completed = false;
            _paid = false;

            if (_flavour == TauntFlavour.Beast && !TakeBeastConfig(hero))
            {
                // The Beast config could not be taken: play Standard so voice and action still match.
                _flavour = TauntFlavour.Standard;
            }
            else if (_flavour != TauntFlavour.Beast && hero != null)
            {
                ConfigLease.Release(hero, ConfigOwner.Taunt);
            }

            RandomCrestModPlugin.Log($"[RandomTaunt] rolled '{_rolled}', playing '{_flavour}'.");
        }
        catch (Exception e)
        {
            Clear();
            RandomCrestModPlugin.LogError("[RandomTaunt] Roll failed: " + e.Message);
        }
    }

    /// <summary>Per-frame driver: settles the taunt once the FSM is back in Idle, or on timeout.</summary>
    internal static void Tick(HeroController? hero)
    {
        if (!IsActive)
        {
            return;
        }

        if (!Gate.Taunts)
        {
            Clear();
            return;
        }

        var fsm = GetFsm(hero);
        if (fsm != null && fsm.ActiveStateName == IdleState && Time.time - _rollTime > SettleDelay)
        {
            Settle();
            return;
        }

        if (Time.time - _rollTime > StaleTimeout)
        {
            Settle();
        }
    }

    /// <summary>Called when the taunt FSM reaches <see cref="TauntEndState"/>: the animation played to its end.</summary>
    internal static void NotifyCompleted()
    {
        if (IsActive)
        {
            _completed = true;
        }
    }

    internal static void Reset()
    {
        Clear();
        _fsm = null;
    }

    /// <summary>Pays the roll (once) and drops it.</summary>
    private static void Settle()
    {
        Pay();
        Clear();
    }

    private static void Pay()
    {
        if (_paid)
        {
            return;
        }

        _paid = true;

        if (_rolled == TauntFlavour.None || !RandomCrestModPlugin.EnableTauntShardOffer || !PlayerData.HasInstance)
        {
            return;
        }

        if (!Rules.CanOfferTaunt(_completed, PlayerData.instance.ShellShards))
        {
            return;
        }

        var standardRoll = UnityEngine.Random.Range(0, Rules.TauntStandardRosaryMax + 1);
        RewardLedger.PayTaunt(Rules.TauntShardCost, Rules.TauntRosaries(_rolled, standardRoll), _rolled);
    }

    private static void Clear()
    {
        _flavour = TauntFlavour.None;
        _rolled = TauntFlavour.None;
        _completed = false;

        var hero = HeroController.instance;
        if (hero != null)
        {
            ConfigLease.Release(hero, ConfigOwner.Taunt);
        }
    }

    private static bool TakeBeastConfig(HeroController? hero)
    {
        var crest = BeastCrest;
        if (hero == null || crest == null)
        {
            return false;
        }

        // Never preempt a live sprint / attack / bind / charge. A config swap mid-action leaves the
        // Sprint / Bind FSM's cached attack objects and crest branches disagreeing with the active
        // config, which strands the hero (the sprint shoots off the scene / slides). Fall back to a
        // Standard taunt instead, so the voice and action still match.
        if (ConfigLease.IsHeld)
        {
            // A dash that already ended can still hold the moveset during its grace period. Release
            // it only when the hero is grounded and no longer sprinting / skidding; otherwise refuse.
            if (ConfigLease.Owner == ConfigOwner.Dash && !ConfigPool.IsSprintOrSkid(hero))
            {
                ConfigLease.Release(hero, ConfigOwner.Dash);
            }
            else
            {
                return false;
            }
        }

        var group = ConfigPool.Find(hero, crest);
        return group != null && ConfigLease.Acquire(hero, ConfigOwner.Taunt, group, quiet: true);
    }

    private static PlayMakerFSM? GetFsm(HeroController? hero)
    {
        if (_fsm != null)
        {
            return _fsm;
        }

        if (hero != null)
        {
            _fsm = ConfigPool.LocateFsm(hero, FsmName);
        }

        return _fsm;
    }
}

/// <summary>Feeds the rolled taunt flavour into the <c>Silk Specials</c> FSM. Every override is state-scoped.</summary>
[HarmonyPatch]
internal static class TauntPatches
{
    private static AccessTools.FieldRef<ListenForTauntV2, InputHandler>? _inputHandlerRef;

    [HarmonyPatch(typeof(ListenForTauntV2), "OnUpdate")]
    [HarmonyPrefix]
    private static void ListenForTauntV2_OnUpdate_Prefix(ListenForTauntV2 __instance)
    {
        if (!RandomCrestModPlugin.EnableRandomTaunt)
        {
            return;
        }

        try
        {
            var fsm = __instance.Fsm;
            if (fsm == null || fsm.Name != Taunt.FsmName)
            {
                return;
            }

            _inputHandlerRef ??= AccessTools.FieldRefAccess<ListenForTauntV2, InputHandler>("inputHandler");
            var input = _inputHandlerRef(__instance);
            if (input != null && input.inputActions.Taunt.WasPressed)
            {
                Taunt.Roll(HeroController.instance);
            }
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[RandomTaunt] listener prefix failed: " + e.Message);
        }
    }

    /// <summary>Beast voice: report the Warrior crest as equipped in the "Voice Type" state.</summary>
    [HarmonyPatch(typeof(CheckIfCrestEquipped), nameof(CheckIfCrestEquipped.IsTrue), MethodType.Getter)]
    [HarmonyPostfix]
    private static void CheckIfCrestEquipped_IsTrue_Postfix(CheckIfCrestEquipped __instance, ref bool __result)
    {
        if (!Taunt.IsActive || !Taunt.IsInState(__instance, Taunt.VoiceState))
        {
            return;
        }

        var crest = __instance.Crest != null ? __instance.Crest.Value as ToolCrest : null;
        if (crest != null && ReferenceEquals(crest, Taunt.BeastCrest))
        {
            __result = Taunt.IsBeast;
        }
    }

    /// <summary>Ring toss: the Shakra Ring counts as equipped only for a Rings roll.</summary>
    [HarmonyPatch(typeof(CheckIfToolEquipped), nameof(CheckIfToolEquipped.IsTrue), MethodType.Getter)]
    [HarmonyPostfix]
    private static void CheckIfToolEquipped_IsTrue_Postfix(CheckIfToolEquipped __instance, ref bool __result)
    {
        if (!Taunt.IsActive || !Taunt.IsInState(__instance, Taunt.SilkCheckState))
        {
            return;
        }

        var tool = __instance.Tool != null ? __instance.Tool.Value as ToolItem : null;
        if (Taunt.IsShakraRing(tool))
        {
            __result = Taunt.IsRings;
        }
    }

    /// <summary>Ring toss: make the stored equip info read as a full set of rings for a Rings roll only.</summary>
    [HarmonyPatch(typeof(GetToolEquipInfo), "DoAction")]
    [HarmonyPostfix]
    private static void GetToolEquipInfo_DoAction_Postfix(GetToolEquipInfo __instance)
    {
        if (!Taunt.IsActive || !Taunt.IsInState(__instance, Taunt.SilkCheckState))
        {
            return;
        }

        var tool = __instance.Tool != null ? __instance.Tool.Value as ToolItem : null;
        if (!Taunt.IsShakraRing(tool))
        {
            return;
        }

        if (!Taunt.IsRings)
        {
            if (__instance.StoreIsEquipped != null)
            {
                __instance.StoreIsEquipped.Value = false;
            }

            if (__instance.StoreAmountLeft != null)
            {
                __instance.StoreAmountLeft.Value = 0;
            }

            return;
        }

        if (__instance.StoreIsEquipped != null)
        {
            __instance.StoreIsEquipped.Value = true;
        }

        if (__instance.StoreIsUnlocked != null)
        {
            __instance.StoreIsUnlocked.Value = true;
        }

        if (__instance.StoreAmountLeft != null)
        {
            __instance.StoreAmountLeft.Value = Mathf.Max(__instance.StoreAmountLeft.Value, 10);
        }

        if (__instance.StoreMaxAmount != null)
        {
            __instance.StoreMaxAmount.Value = Mathf.Max(__instance.StoreMaxAmount.Value, 10);
        }
    }

    /// <summary>Ring toss: every output of the two-rings compare follows the roll only.</summary>
    [HarmonyPatch(typeof(IntTestToBool), "DoCompare")]
    [HarmonyPostfix]
    private static void IntTestToBool_DoCompare_Postfix(IntTestToBool __instance)
    {
        if (!Taunt.IsActive || !Taunt.IsInState(__instance, Taunt.SilkCheckState))
        {
            return;
        }

        var value = Taunt.IsRings;
        if (__instance.equalBool != null)
        {
            __instance.equalBool.Value = value;
        }

        if (__instance.lessThanBool != null)
        {
            __instance.lessThanBool.Value = value;
        }

        if (__instance.greaterThanBool != null)
        {
            __instance.greaterThanBool.Value = value;
        }
    }

    /// <summary>Reaching "Taunt End Wait" means the taunt played to its end. Air or interrupted taunts never do.</summary>
    [HarmonyPatch(typeof(Tk2dWatchAnimationEvents), nameof(Tk2dWatchAnimationEvents.OnEnter))]
    [HarmonyPostfix]
    private static void Tk2dWatchAnimationEvents_OnEnter_Postfix(Tk2dWatchAnimationEvents __instance)
    {
        if (Taunt.IsInState(__instance, Taunt.TauntEndState))
        {
            Taunt.NotifyCompleted();
        }
    }
}
