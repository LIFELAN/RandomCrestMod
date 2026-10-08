using System;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// Adds the Cursed crest's "refused bind" (诅咒缚丝) as a fixed-probability (5%) outcome of the
/// random bind, without the instability the removed implementation had.
///
/// <para>The old version fired the Bind FSM's global <c>FORCE CURSED BIND</c> transition, which
/// jumps straight to <c>Cursed Bind Start</c> and skips the <c>Can Bind?</c> gate entirely - that
/// is what allowed cursed binds during cutscenes, with too little silk, or mid-skid, and eventually
/// froze the hero. This version does the opposite: the normal bind flow runs untouched (silk check,
/// <c>HeroController.CanBind()</c>, ground / sprint / cutscene gating all apply), and we only answer
/// the <c>PlayerDataVariableTest("IsAnyCursed")</c> inside the <c>Do Bind</c> state as true. The FSM
/// then takes its own <c>CURSED</c> branch into <c>Cursed Bind Start</c>.</para>
///
/// <para>Nothing is written to PlayerData. In particular <c>PlayerData.IsAnyCursed</c> is NOT
/// spoofed globally, because that getter also caps the silk spool at 3
/// (<c>PlayerData.CurrentSilkMaxBasic</c>). Only the one FSM action in that one state is overridden,
/// via <see cref="RandomCrestMod.CursedBindPatches"/>.</para>
/// </summary>
internal static class CursedBindService
{
    /// <summary>Hero FSM that owns the whole bind flow.</summary>
    internal const string FsmName = "Bind";

    /// <summary>State whose <c>PlayerDataVariableTest</c> picks the cursed branch.</summary>
    internal const string DoBindState = "Do Bind";

    /// <summary>PlayerData bool the bind flow tests to decide cursed vs normal.</summary>
    internal const string CursedVariable = "IsAnyCursed";

    /// <summary>FSM event that leads to <c>Cursed Bind Start</c>.</summary>
    internal const string CursedEventName = "CURSED";

    /// <summary>Chance a random bind resolves to the cursed (refused) flavour.</summary>
    internal static float Chance => RandomCrestModPlugin.CursedBindChance;

    /// <summary>Safety net: drop the flag even if the bind FSM never returns to Idle.</summary>
    private const float StaleTimeout = 15f;

    private static bool _active;
    private static float _armedTime;

    /// <summary>True while the current bind should be diverted into the cursed branch.</summary>
    internal static bool Active => _active;

    /// <summary>Rolls whether this bind is cursed.</summary>
    internal static bool Roll()
    {
        return UnityEngine.Random.value < Chance;
    }

    /// <summary>Marks the current bind as cursed (or not). Called when a bind attempt starts.</summary>
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

    /// <summary>Drops the flag on save load / scene init.</summary>
    internal static void Reset()
    {
        Clear();
    }

    /// <summary>
    /// Picks the event that leads to <c>Cursed Bind Start</c>. The FSM names it <c>CURSED</c>; if
    /// for some game build it is wired to the other slot, that one is used instead.
    /// </summary>
    internal static FsmEvent? ResolveCursedEvent(PlayerDataVariableTest action)
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

    /// <summary>
    /// True when this specific PlayMaker action is the Bind FSM's <c>Do Bind</c> test on
    /// <c>IsAnyCursed</c> and the current bind rolled cursed. Scoped to that exact FSM + state so
    /// the same action used anywhere else in the game is never affected.
    /// </summary>
    internal static bool ShouldOverride(FsmStateAction? action, string? variableName)
    {
        if (!_active || !RandomCrestModPlugin.EnableRandomBind)
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

    /// <summary>
    /// Clears the flag once the bind is over. The normal clear path is
    /// <see cref="RandomBindService"/> ending the attempt; this is the safety net for death /
    /// respawn / scene transition / unequipping the mod crest / a stalled bind.
    /// </summary>
    internal static void Tick()
    {
        if (!_active)
        {
            return;
        }

        if (RandomCrestModPlugin.OnlyOnRandomCrest && !CrestService.IsRandomCrestEquipped())
        {
            Clear();
            return;
        }

        var hero = HeroController.instance;
        if (hero == null)
        {
            Clear();
            return;
        }

        var cs = hero.cState;
        if (cs.dead || cs.hazardDeath || cs.hazardRespawning || cs.transitioning)
        {
            Clear();
            return;
        }

        if (Time.time - _armedTime > StaleTimeout)
        {
            Clear();
        }
    }
}
