using System;
using GlobalSettings;
using HutongGames.PlayMaker;
using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// Randomises Hornet's taunt (R3 / V) into its three vanilla flavours: the standard flourish, the
/// Beast (Warrior) crest battle-cry, and the Shakra Ring (Throwing Ring) ring toss.
///
/// <para>The taunt is driven entirely by the hero's <c>Silk Specials</c> PlayMaker FSM. That FSM
/// branches on <c>CheckIfCrestEquipped</c> (Warrior crest) in its "Voice Type" state and on
/// <c>CheckIfToolEquipped</c> / <c>IntTestToBool</c> (Shakra Ring) in its "Silk Check" state. The
/// visual/action is the <c>HeroController.CurrentConfigGroup.TauntSlash</c> object the "Taunt"
/// state activates, so the Beast flavour also installs the Warrior crest's config group for the
/// duration of the taunt (its unique TauntSlash lives under the Warrior crest root). Standard and
/// Rings keep the real crest. The voice checks are answered with a spoof.</para>
///
/// <para>Nothing is written to PlayerData; the held config is swapped back as soon as the taunt
/// FSM returns to Idle.</para>
/// </summary>
internal static class RandomTauntService
{
    internal enum TauntFlavour
    {
        None = 0,
        Standard = 1,
        Beast = 2,
        Rings = 3,
    }

    /// <summary>Name of the hero FSM that owns the whole taunt flow.</summary>
    internal const string FsmName = "Silk Specials";

    /// <summary>State whose <c>CheckIfCrestEquipped</c> picks the voice (Beast vs standard).</summary>
    internal const string VoiceState = "Voice Type";

    /// <summary>State whose tool checks pick the ring-toss flavour.</summary>
    internal const string SilkCheckState = "Silk Check";

    /// <summary>Internal ToolItem name of the Shakra Ring (投掷环 / Throwing Ring).</summary>
    private const string RingToolName = "Shakra Ring";

    private const string IdleState = "Idle";

    /// <summary>Safety net: release the held crest even if the FSM never returns to Idle.</summary>
    private const float StaleTimeout = 5f;

    private static TauntFlavour _flavour = TauntFlavour.None;
    private static float _rollTime;
    private static PlayMakerFSM? _silkspecialsFsm;

    internal static bool IsActive => _flavour != TauntFlavour.None;

    internal static bool IsBeast => _flavour == TauntFlavour.Beast;

    internal static bool IsRings => _flavour == TauntFlavour.Rings;

    /// <summary>The Beast crest (internal "Warrior"), read lazily so early loads do not throw.</summary>
    internal static ToolCrest? BeastCrest
    {
        get
        {
            try
            {
                return Gameplay.WarriorCrest;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>Rolls a fresh flavour. Called from the taunt listener before the FSM event fires.</summary>
    internal static void Roll()
    {
        try
        {
            if (!RandomCrestModPlugin.EnableRandomTaunt)
            {
                Clear();
                return;
            }

            if (RandomCrestModPlugin.OnlyOnRandomCrest && !CrestService.IsRandomCrestEquipped())
            {
                Clear();
                return;
            }

            // Three equally likely flavours: 1 = Standard, 2 = Beast, 3 = Rings.
            _flavour = (TauntFlavour)(1 + UnityEngine.Random.Range(0, 3));
            _rollTime = Time.time;

            // The Beast taunt's unique visual lives under the Warrior crest root, so install that
            // crest for the taunt's duration; Standard/Rings use the real crest.
            if (_flavour == TauntFlavour.Beast)
            {
                RandomAttackService.ApplyTauntHold(BeastCrest);
            }
            else
            {
                RandomAttackService.ReleaseTauntHold();
            }

            RandomCrestModPlugin.Log($"[RandomTaunt] rolled flavour '{_flavour}'.");
        }
        catch (Exception e)
        {
            Clear();
            RandomCrestModPlugin.LogError("[RandomTaunt] Roll failed: " + e.Message);
        }
    }

    internal static void Clear()
    {
        _flavour = TauntFlavour.None;
        RandomAttackService.ReleaseTauntHold();
    }

    /// <summary>Drops the roll and any held crest; used on scene load / save load.</summary>
    internal static void Reset()
    {
        Clear();
        _silkspecialsFsm = null;
    }

    /// <summary>
    /// True when the PlayMaker action belongs to the hero's Silk Specials FSM and that FSM is
    /// currently sitting in <paramref name="stateName"/>. This scopes every override to the exact
    /// state we care about, so the same actions used elsewhere are never affected.
    /// </summary>
    internal static bool IsInState(FsmStateAction? action, string stateName)
    {
        try
        {
            var fsm = action != null ? action.Fsm : null;
            return fsm != null
                && fsm.Name == FsmName
                && fsm.ActiveStateName == stateName;
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

    /// <summary>
    /// Called every frame. Releases the held Beast crest as soon as the taunt FSM is back in Idle,
    /// when the crest is unequipped, or when the roll goes stale (safety net).
    /// </summary>
    internal static void Tick()
    {
        if (!IsActive)
        {
            return;
        }

        if (RandomCrestModPlugin.OnlyOnRandomCrest && !CrestService.IsRandomCrestEquipped())
        {
            Clear();
            return;
        }

        // The taunt is over once the FSM is back in Idle. The small delay covers the frame the roll
        // happens on, before the FSM has left Idle.
        var fsm = GetFsm();
        if (fsm != null && fsm.ActiveStateName == IdleState && Time.time - _rollTime > 0.2f)
        {
            Clear();
            return;
        }

        if (Time.time - _rollTime > StaleTimeout)
        {
            Clear();
        }
    }

    private static PlayMakerFSM? GetFsm()
    {
        if (_silkspecialsFsm != null)
        {
            return _silkspecialsFsm;
        }

        var hero = HeroController.instance;
        if (hero == null)
        {
            return null;
        }

        try
        {
            _silkspecialsFsm = FSMUtility.LocateFSM(hero.gameObject, FsmName);
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[RandomTaunt] Could not locate Silk Specials FSM: " + e.Message);
        }

        return _silkspecialsFsm;
    }
}
