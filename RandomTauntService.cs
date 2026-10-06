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

    /// <summary>State whose <c>Tk2dWatchAnimationEvents</c> lands when the taunt action fully ends.</summary>
    internal const string TauntEndState = "Taunt End Wait";

    /// <summary>Safety net: release the held crest even if the FSM never returns to Idle.</summary>
    private const float StaleTimeout = 5f;

    /// <summary>Shell Shards (碎片) spent by a successful taunt offering.</summary>
    private const int ShardCost = 80;

    /// <summary>Rosaries (念珠) granted by each flavour. Standard rolls 1..StandardRosaryMax.</summary>
    private const int StandardRosaryMax = 50;

    private const int BeastRosary = 60;

    private const int RingsRosary = 80;

    private static TauntFlavour _flavour = TauntFlavour.None;

    /// <summary>
    /// The flavour the weighted roll actually produced, kept separate from the flavour used for the
    /// visuals. The payout follows the rolled odds even when the action had to fall back (e.g. a
    /// Beast roll downgraded to Standard because the config was busy).
    /// </summary>
    private static TauntFlavour _payoutFlavour = TauntFlavour.None;

    /// <summary>
    /// True once the FSM has entered <see cref="TauntEndState"/>, i.e. the taunt played all the way
    /// to its end. An air taunt or a taunt cancelled mid-way never does, so only a full taunt pays.
    /// </summary>
    private static bool _tauntCompleted;

    /// <summary>Guards the payout so it can never run twice for the same roll.</summary>
    private static bool _paid;

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
            // A new roll can only start from the FSM's Idle state, so any previous taunt has
            // already finished. Settle it here in case the re-press landed on the very frame the
            // FSM returned to Idle, before Tick had a chance to see it.
            TryPayout();

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

            // Weighted flavours: Standard 80%, Beast 14%, Rings 6%.
            var roll = UnityEngine.Random.value;
            var flavour = roll < 0.80f
                ? TauntFlavour.Standard
                : roll < 0.94f
                    ? TauntFlavour.Beast
                    : TauntFlavour.Rings;
            _flavour = flavour;
            _payoutFlavour = flavour;
            _rollTime = Time.time;
            _tauntCompleted = false;
            _paid = false;

            // The Beast taunt's unique visual lives under the Warrior crest root, so install that
            // crest for the taunt's duration. If the config cannot be taken right now (e.g. a
            // sprint / dash still holds it) fall back to Standard so the voice and action always
            // match instead of leaving a Beast voice over a standard slash.
            if (_flavour == TauntFlavour.Beast)
            {
                if (!RandomAttackService.ApplyTauntHold(BeastCrest))
                {
                    _flavour = TauntFlavour.Standard;
                }
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
        _payoutFlavour = TauntFlavour.None;
        _tauntCompleted = false;
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

        var fsm = GetFsm();
        if (fsm != null
            && fsm.ActiveStateName == IdleState
            && Time.time - _rollTime > 0.2f)
        {
            // The FSM has fully unwound the taunt; NotifyTauntCompleted already recorded whether
            // the action reached its end. Drop the roll and pay out only if it did.
            CompleteTaunt();
            return;
        }

        if (Time.time - _rollTime > StaleTimeout)
        {
            CompleteTaunt();
        }
    }

    /// <summary>Pays out what the roll promised (if anything), then drops the roll.</summary>
    private static void CompleteTaunt()
    {
        TryPayout();
        Clear();
    }

    /// <summary>
    /// Converts shell shards into rosaries at the end of a real taunt. Runs at most once per roll
    /// and only when the player can afford it; otherwise the taunt stays purely cosmetic. Both
    /// changes go through <see cref="CurrencyManager"/> so the HUD counters animate and the roll
    /// sound plays as the reminder.
    /// </summary>
    private static void TryPayout()
    {
        if (_paid)
        {
            return;
        }

        _paid = true;

        if (!_tauntCompleted
            || !RandomCrestModPlugin.EnableTauntShardOffer
            || _payoutFlavour == TauntFlavour.None
            || !PlayerData.HasInstance)
        {
            return;
        }

        if (PlayerData.instance.ShellShards < ShardCost)
        {
            return;
        }

        var rosaries = _payoutFlavour switch
        {
            TauntFlavour.Beast => BeastRosary,
            TauntFlavour.Rings => RingsRosary,
            _ => UnityEngine.Random.Range(1, StandardRosaryMax + 1),
        };

        try
        {
            CurrencyManager.TakeShards(ShardCost);
            CurrencyManager.AddGeo(rosaries);
            RandomCrestModPlugin.Log(
                $"[RandomTaunt] {ShardCost} shards -> +{rosaries} rosaries ({_payoutFlavour}).");
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[RandomTaunt] shard/rosary payout failed: " + e.Message);
        }
    }

    /// <summary>
    /// Called by the <see cref="TauntEndState"/> hook once the taunt animation has run to its end.
    /// Only a real, uninterrupted ground taunt reaches that state, so this unlocks the payout.
    /// </summary>
    internal static void NotifyTauntCompleted()
    {
        if (IsActive)
        {
            _tauntCompleted = true;
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
