using HarmonyLib;
using InControl;
using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// Randomises the bind (缚丝). While the Cast button is held we install a random crest so the bind
/// uses that crest's effect (the full config swap + IsEquipped spoof is handled by
/// <see cref="RandomAttackService"/>).
/// </summary>
internal static class RandomBindService
{
    private static bool _attemptActive;
    private static float _attemptStart;

    internal static void Tick()
    {
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
        _attemptActive = true;
        _attemptStart = Time.time;

        // A small chance this bind resolves to the Cursed crest's refused bind. The roll is answered
        // later, inside the Bind FSM's own "Do Bind" state (see CursedBindService / CursedBindPatches),
        // so the normal silk / CanBind / ground / sprint gating still applies.
        CursedBindService.Arm(CursedBindService.Roll());

        // Also swaps the config, so crest-specific bind clips / roots are available.
        RandomAttackService.ApplyForBind(hero);
    }

    private static void End()
    {
        _attemptActive = false;
        CursedBindService.Clear();

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
