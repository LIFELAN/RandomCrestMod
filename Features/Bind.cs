using System;
using GlobalSettings;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// Random bind (缚丝). While the cast button is held a random moveset is held for the bind, and the
/// bind FSM decides when the attempt is over. A small share of attempts is answered with the cursed
/// (refused) bind; see <see cref="CursedBind"/>.
/// </summary>
internal static class Bind
{
    private const float AttemptTimeout = 10f;
    private const float MinHold = 0.15f;
    private const float MaxHold = 30f;
    private const string FsmName = "Bind";

    private static bool _attemptActive;
    private static int _rollFrame = -1;
    private static float _attemptStart;
    private static bool _wasBinding;
    private static bool _cancelSent;
    private static PlayMakerFSM? _bindFsm;

    /// <summary>Called from the <c>CanBind</c> postfix: the earliest point where a bind attempt can start.</summary>
    internal static void OnCanBind(HeroController hero)
    {
        // Every new press re-rolls, even while the previous attempt is still open.
        if (Gate.Binds && CastWasPressed())
        {
            BeginAttempt(hero);
        }
    }

    internal static void Tick(HeroController? hero)
    {
        if (!RandomCrestModPlugin.EnableRandomBind || hero == null)
        {
            if (_attemptActive)
            {
                EndAttempt(hero);
            }

            return;
        }

        var pressed = IsCastHeld();
        if (Gate.Binds && (CastWasPressed() || (pressed && !_attemptActive)))
        {
            BeginAttempt(hero);
        }

        if (_attemptActive && ((!pressed && !hero.cState.isBinding) || Time.time - _attemptStart > AttemptTimeout))
        {
            EndAttempt(hero);
        }

        TickOwner(hero);
    }

    /// <summary>Drops the attempt state on save load / scene init.</summary>
    internal static void Reset()
    {
        CursedBind.Clear();
    }

    internal static bool IsShamanAirBind(HeroController hero)
    {
        try
        {
            var state = BindState(hero);
            return state == "Shaman Air" || state == "Shaman Fall";
        }
        catch
        {
            return false;
        }
    }

    private static void BeginAttempt(HeroController hero)
    {
        // CanBind and the per-frame tick can both see the same press; roll only once per frame.
        if (_rollFrame == Time.frameCount)
        {
            return;
        }

        // A bind is already running: keep its crest instead of re-rolling mid-bind (that would swap the
        // moveset out from under the bind FSM and cancel the rage / Shaman effects).
        // Also refuse to start during a scene transition: the hero's FSMs are being reset there, so
        // installing a moveset is pointless and only risks the Bind FSM / crest roots disagreeing.
        if (hero.cState != null && (hero.cState.isBinding || hero.cState.transitioning))
        {
            return;
        }

        _rollFrame = Time.frameCount;
        _attemptActive = true;
        _attemptStart = Time.time;
        CursedBind.Arm(CursedBind.Roll());

        var group = ConfigPool.PickAny(hero);
        if (group == null)
        {
            return;
        }

        // Replaces a moveset still held by the previous bind. A swap while a sprint or a bind is still
        // running must be quiet, so the game's own "HC CONFIG UPDATED" cannot cancel it.
        var quiet = ConfigPool.IsSprintOrSkid(hero) || IsBindFsmBusy(hero);
        if (ConfigLease.Acquire(hero, ConfigOwner.Bind, group, quiet))
        {
            _wasBinding = false;
            _cancelSent = false;
            RandomCrestModPlugin.Log($"Random bind -> '{(group.Config != null ? group.Config.name : "?")}'.");
        }
    }

    private static void EndAttempt(HeroController? hero)
    {
        _attemptActive = false;
        CursedBind.Clear();

        if (hero == null || !ConfigLease.IsOwnedBy(ConfigOwner.Bind))
        {
            return;
        }

        // Restoring now would fire "HC CONFIG UPDATED" while the sprint or the bind FSM still runs.
        // TickOwner releases the moveset once both are idle.
        if (ConfigPool.IsSprintOrSkid(hero) || IsBindFsmBusy(hero))
        {
            return;
        }

        Release(hero);
    }

    private static void TickOwner(HeroController hero)
    {
        if (!ConfigLease.IsOwnedBy(ConfigOwner.Bind))
        {
            return;
        }

        var cs = hero.cState;
        if (cs.isBinding)
        {
            _wasBinding = true;
        }

        // The vanilla bind cancels on surface water; our swapped config misses that branch.
        if (cs.swimming && !_cancelSent)
        {
            _cancelSent = true;
            CancelBindFsm(hero);
        }

        var elapsed = ConfigLease.HeldFor;
        if (elapsed > MaxHold)
        {
            Release(hero);
            return;
        }

        var sprinting = ConfigPool.IsSprintOrSkid(hero);
        var fsmBusy = IsBindFsmBusy(hero);
        if (!sprinting && !fsmBusy && _wasBinding && !cs.isBinding && elapsed >= MinHold)
        {
            Release(hero);
        }
    }

    private static void Release(HeroController hero)
    {
        _wasBinding = false;
        _cancelSent = false;
        ConfigLease.Release(hero, ConfigOwner.Bind);
    }

    private static void CancelBindFsm(HeroController hero)
    {
        try
        {
            _bindFsm ??= ConfigPool.LocateFsm(hero, FsmName);
            if (_bindFsm != null && _bindFsm.ActiveStateName != "Idle")
            {
                _bindFsm.SendEvent("CANCEL");
            }
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[Bind] cancel failed: " + e.Message);
        }
    }

    private static bool IsBindFsmBusy(HeroController hero)
    {
        var state = BindState(hero);
        return !string.IsNullOrEmpty(state) && state != "Idle";
    }

    private static string? BindState(HeroController hero)
    {
        _bindFsm ??= ConfigPool.LocateFsm(hero, FsmName);
        return _bindFsm != null ? _bindFsm.ActiveStateName : null;
    }

    private static bool IsCastHeld()
    {
        try
        {
            var handler = InputHandler.UnsafeInstance;
            if (handler == null)
            {
                return false;
            }

            var cast = handler.inputActions.Cast;
            return cast.IsPressed || cast.WasPressed;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>True only on the frame the cast button went down, so holding it does not re-roll.</summary>
    private static bool CastWasPressed()
    {
        try
        {
            var handler = InputHandler.UnsafeInstance;
            return handler != null && handler.inputActions.Cast.WasPressed;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// The refused bind (诅咒缚丝). Rolled per random bind attempt; when it hits, the bind FSM's own
/// "Do Bind" test on <c>IsAnyCursed</c> is answered with the cursed branch. Every earlier gate (silk,
/// CanBind, ground, sprint, cutscene) has already run normally.
/// </summary>
internal static class CursedBind
{
    private const string FsmName = "Bind";
    private const string DoBindState = "Do Bind";
    private const string CursedVariable = "IsAnyCursed";
    private const string CursedEventName = "CURSED";
    private const float StaleTimeout = 15f;

    private static bool _active;
    private static float _armedTime;

    internal static bool Active => _active;

    internal static bool Roll()
    {
        return RandomCrestModPlugin.EnableCursedBind && UnityEngine.Random.value < RandomCrestModPlugin.CursedBindChance;
    }

    internal static void Arm(bool cursed)
    {
        _active = cursed;
        _armedTime = Time.time;
        if (cursed)
        {
            RandomCrestModPlugin.LogInfo("[CursedBind] cursed bind rolled.");
        }
    }

    internal static void Clear()
    {
        _active = false;
    }

    /// <summary>Safety net: drops the roll if the bind stalls, the hero dies, or the crest is unequipped.</summary>
    internal static void Tick(HeroController? hero)
    {
        if (!_active)
        {
            return;
        }

        if (!Gate.Crest || hero == null || hero.cState.dead || hero.cState.transitioning || Time.time - _armedTime > StaleTimeout)
        {
            Clear();
        }
    }

    /// <summary>True only for this exact action in the Bind FSM's Do Bind state, while a cursed roll is armed.</summary>
    internal static bool ShouldOverride(FsmStateAction? action, string? variableName)
    {
        if (!_active || !RandomCrestModPlugin.EnableCursedBind)
        {
            return false;
        }

        try
        {
            var fsm = action != null ? action.Fsm : null;
            return fsm != null
                && fsm.Name == FsmName
                && fsm.ActiveStateName == DoBindState
                && string.Equals(variableName, CursedVariable, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>The event that leads into <c>Cursed Bind Start</c>.</summary>
    internal static FsmEvent? ResolveEvent(PlayerDataVariableTest action)
    {
        var expected = action.IsExpectedEvent;
        var notExpected = action.IsNotExpectedEvent;

        if (expected != null && expected.Name == CursedEventName)
        {
            return expected;
        }

        if (notExpected != null && notExpected.Name == CursedEventName)
        {
            return notExpected;
        }

        return expected ?? notExpected;
    }
}

/// <summary>Diverts the bind FSM's <c>Do Bind</c> test into the cursed branch when the roll hit.</summary>
[HarmonyPatch]
internal static class CursedBindPatches
{
    [HarmonyPatch(typeof(PlayerDataVariableTest), nameof(PlayerDataVariableTest.OnEnter))]
    [HarmonyPrefix]
    private static bool PlayerDataVariableTest_OnEnter_Prefix(PlayerDataVariableTest __instance)
    {
        if (__instance == null)
        {
            return true;
        }

        try
        {
            var variableName = __instance.VariableName != null ? __instance.VariableName.Value : null;
            if (!CursedBind.ShouldOverride(__instance, variableName))
            {
                return true;
            }

            var target = CursedBind.ResolveEvent(__instance);
            if (target != null && __instance.Fsm != null)
            {
                __instance.Fsm.Event(target);
            }

            __instance.Finish();
            RandomCrestModPlugin.LogInfo("[CursedBind] Diverted the bind into the Cursed branch.");
            return false;
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[CursedBind] override failed: " + e.Message);
            return true;
        }
    }
}

/// <summary>Starts the bind attempt as early as the game checks whether a bind is possible.</summary>
[HarmonyPatch]
internal static class BindStartPatches
{
    [HarmonyPatch(typeof(HeroController), nameof(HeroController.CanBind))]
    [HarmonyPostfix]
    private static void CanBind_Postfix(HeroController __instance)
    {
        Bind.OnCanBind(__instance);
    }
}

/// <summary>Pays rosaries for the silk a refused bind takes (the Bind FSM spends it with the Curse source).</summary>
[HarmonyPatch]
internal static class CursedBindRewardPatch
{
    [HarmonyPatch(
        typeof(HeroController),
        nameof(HeroController.TakeSilk),
        new[] { typeof(int), typeof(SilkSpool.SilkTakeSource) })]
    [HarmonyPostfix]
    private static void TakeSilk_Postfix(int amount, SilkSpool.SilkTakeSource source)
    {
        if (source != SilkSpool.SilkTakeSource.Curse || !RandomCrestModPlugin.EnableCursedBind || !CursedBind.Active)
        {
            return;
        }

        RewardLedger.PayCursedSilk(amount);
    }
}

/// <summary>
/// Shaman (Spell) bridge. A random Shaman air bind looks, to the game, like a bind without the Spell
/// crest: surface water rejects it and the scene gate pushes it back. These hooks make the game accept
/// the spoofed Shaman crest only in those two places, so every other check stays vanilla.
/// </summary>
internal static class ShamanBridge
{
    /// <summary>While true, <c>SurfaceWaterRegion</c> takes its Shaman entry path for the trigger.</summary>
    internal static bool ForceSpellCrestForWater { get; set; }

    /// <summary>True while the spoof reports the Spell (Shaman) crest.</summary>
    internal static bool SpoofingShaman
    {
        get
        {
            if (!ConfigLease.IsHeld || ConfigLease.SpoofCrest == null)
            {
                return false;
            }

            try
            {
                return ReferenceEquals(ConfigLease.SpoofCrest, Gameplay.SpellCrest);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>Vanilla <c>IsShamanCrestEquipped</c> plus the spoof. Called from the <c>TryDoTransition</c> transpiler.</summary>
    internal static bool IsShamanCrestEquippedForTransition(HeroController hero)
    {
        if (hero == null)
        {
            return false;
        }

        if (hero.playerData != null && hero.playerData.CurrentCrestID == "Spell")
        {
            return true;
        }

        return SpoofingShaman;
    }
}

[HarmonyPatch]
internal static class ShamanWaterPatches
{
#pragma warning disable HARMONIZE004
    [HarmonyPatch(typeof(SurfaceWaterRegion), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void OnTriggerEnter2D_Prefix(Collider2D collision)
    {
        try
        {
            if (collision == null || !RandomCrestModPlugin.EnableRandomBind)
            {
                ShamanBridge.ForceSpellCrestForWater = false;
                return;
            }

            var hero = collision.GetComponent<HeroController>();
            ShamanBridge.ForceSpellCrestForWater =
                hero != null && hero.cState.isBinding && Bind.IsShamanAirBind(hero);
        }
        catch
        {
            ShamanBridge.ForceSpellCrestForWater = false;
        }
    }

    [HarmonyPatch(typeof(SurfaceWaterRegion), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void OnTriggerEnter2D_Postfix()
    {
        ShamanBridge.ForceSpellCrestForWater = false;
    }

    [HarmonyPatch(typeof(TransitionPoint), "TryDoTransition")]
    [HarmonyTranspiler]
    private static System.Collections.Generic.IEnumerable<CodeInstruction> TryDoTransition_Transpiler(
        System.Collections.Generic.IEnumerable<CodeInstruction> instructions)
    {
        var original = AccessTools.Method(typeof(HeroController), nameof(HeroController.IsShamanCrestEquipped));
        var replacement = AccessTools.Method(typeof(ShamanBridge), nameof(ShamanBridge.IsShamanCrestEquippedForTransition));

        foreach (var instruction in instructions)
        {
            yield return original != null && instruction.Calls(original)
                ? new CodeInstruction(System.Reflection.Emit.OpCodes.Call, replacement)
                : instruction;
        }
    }
#pragma warning restore HARMONIZE004
}
