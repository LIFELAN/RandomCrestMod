using HarmonyLib;
using HutongGames.PlayMaker;
using InControl;
using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// Randomises the bind (缚丝). While the Cast button is held we install a random crest (full
/// config swap + IsEquipped spoof, handled by <see cref="RandomAttackService"/>), and with a
/// configurable chance we forbid the bind entirely (like the Cursed crest).
/// </summary>
internal static class RandomBindService
{
    private static bool _attemptActive;
    private static bool _forbidBind;
    private static float _attemptStart;
    private static PlayMakerFSM? _bindFsm;
    private static bool _cursedBindActive;
    private static float _cursedBindStart;

    internal static bool AttemptActive => _attemptActive;

    internal static bool ForbidBind => _forbidBind;

    /// <summary>
    /// True while our cursed bind animation is playing. While set,
    /// <see cref="HeroController.TakeSilk(int)"/> is skipped so the refused bind does not drain
    /// the player's silk.
    /// </summary>
    internal static bool ShouldSuppressSilk => _cursedBindActive;

    internal static void Tick()
    {
        TickCursedBind();

        if (!RandomCrestModPlugin.EnableRandomBind)
        {
            if (_attemptActive)
            {
                End();
            }

            return;
        }

        var hero = HeroController.instance;
        if (hero == null)
        {
            if (_attemptActive)
            {
                End();
            }

            return;
        }

        var pressed = IsCastHeld();

        if (pressed && !_attemptActive && !GateBlocked())
        {
            BeginAttempt(hero);
        }

        if (!_attemptActive)
        {
            return;
        }

        // End the attempt when the button is released and the hero is no longer binding.
        if ((!pressed && !hero.cState.isBinding) || Time.time - _attemptStart > 10f)
        {
            End();
        }
    }

    /// <summary>Called from the <c>HeroController.CanBind</c> postfix.</summary>
    internal static void EnsureAttempt()
    {
        if (!RandomCrestModPlugin.EnableRandomBind || _attemptActive || GateBlocked())
        {
            return;
        }

        var hero = HeroController.instance;
        if (hero != null && IsCastHeld())
        {
            BeginAttempt(hero);
        }
    }

    private static void BeginAttempt(HeroController hero)
    {
        // A normal bind needs a full spool (SilkSpool.BindCost). The Cursed crest can be bound with
        // less silk, and FORCE CURSED BIND is a global transition that skips the FSM's own silk
        // check, so an attempt started without enough silk runs that sequence with no silk to spend
        // and eventually freezes. Never start an attempt unless a normal bind would be possible.
        if (!HasEnoughSilk())
        {
            return;
        }

        _attemptActive = true;
        _attemptStart = Time.time;

        // The chance is fixed at 15%; only the on/off toggle is configurable.
        _forbidBind = RandomCrestModPlugin.EnableCursedBind.Value
            && UnityEngine.Random.value < RandomCrestModPlugin.CursedBindChance;

        if (_forbidBind)
        {
            RandomCrestModPlugin.Log("Random bind -> cursed bind (refused).");

            // Play the game's own cursed bind sequence (curl into a cocoon, refuse, fall).
            TriggerCursedBind(hero);
            return;
        }

        // Also swaps the config, so crest-specific bind clips / roots are available.
        RandomAttackService.ApplyForBind(hero);
    }

    private static bool HasEnoughSilk()
    {
        return PlayerData.HasInstance && (float)PlayerData.instance.silk >= SilkSpool.BindCost;
    }

    private static void TriggerCursedBind(HeroController hero)
    {
        try
        {
            if (_bindFsm == null)
            {
                _bindFsm = FSMUtility.LocateFSM(hero.gameObject, "Bind");
            }

            _bindFsm?.SendEvent("FORCE CURSED BIND");
            if (_bindFsm != null)
            {
                _cursedBindActive = true;
                _cursedBindStart = Time.time;
            }
        }
        catch (System.Exception e)
        {
            RandomCrestModPlugin.LogError("TriggerCursedBind failed: " + e.Message);
        }
    }

    private static void TickCursedBind()
    {
        if (!_cursedBindActive)
        {
            return;
        }

        try
        {
            var state = _bindFsm != null ? _bindFsm.ActiveStateName : null;
            if (string.IsNullOrEmpty(state) || state == "Idle" || Time.time - _cursedBindStart > 8f)
            {
                _cursedBindActive = false;
            }
        }
        catch
        {
            _cursedBindActive = false;
        }
    }

    private static void End()
    {
        _attemptActive = false;
        _forbidBind = false;

        var hero = HeroController.instance;
        if (hero != null)
        {
            RandomAttackService.EndBind(hero);
        }
    }

    /// <summary>
    /// True when the random bind must be skipped because it is restricted to the mod's own crest
    /// and that crest is not currently equipped.
    /// </summary>
    private static bool GateBlocked()
    {
        return RandomCrestModPlugin.OnlyOnRandomCrest && !CrestService.IsRandomCrestEquipped();
    }

    internal static bool IsCastHeld()
    {
        var cast = GetCast();
        return cast != null && (cast.IsPressed || cast.WasPressed);
    }

    private static PlayerAction? GetCast()
    {
        try
        {
            var handler = InputHandler.UnsafeInstance;
            return handler != null ? handler.inputActions.Cast : null;
        }
        catch
        {
            return null;
        }
    }
}
