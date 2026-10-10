namespace RandomCrestMod;

/// <summary>
/// Cross Stitch (十字绣 / Parry) auto counter, active only while the Chaos crest is equipped.
///
/// <para>In vanilla the hero's <c>Silk Specials</c> FSM holds a <c>Parry Stance</c> for 0.4s and
/// only advances to <c>Parry Clash</c> when the parry box is struck (<c>HeroController.CheckParry</c>
/// sends the <c>PARRIED</c> event). Without a hit it falls through <c>FINISHED</c> -&gt;
/// <c>Parry Recover</c> and the hero just stands there. We retarget that one <c>FINISHED</c>
/// transition to <c>Parry Clash</c> so the stance always flows into the counter's retreat /
/// highlight.</para>
///
/// <para>Only the auto counter (stance expired without a hit) then rolls a fixed 50% chance to
/// actually land its cross-slash. A missed roll still plays the full <c>Parry Clash</c> retreat and
/// its highlight, but ends in <c>Parry Recover</c> instead of running the counter damage. A real
/// parry (<c>PARRIED</c>) never rolls and always counters.</para>
///
/// <para>Because the FSM instance is shared by every crest, we apply the edits only while
/// <see cref="CrestService.IsRandomCrestEquipped"/> is true and restore the vanilla links
/// otherwise, checked every frame from <see cref="Tick"/>. Only the <c>Parry Stance</c> and
/// <c>Parry Clash</c> states of that single FSM are touched; the <c>Cancel Parry</c> path is a
/// separate global transition and is left intact.</para>
/// </summary>
internal static class ParryAutoCounterService
{
    private const string FsmName = "Silk Specials";
    private const string StanceStartState = "Parry Start";
    private const string StanceState = "Parry Stance";
    private const string ClashState = "Parry Clash";

    /// <summary>Vanilla target after the clash: leads into the cross-slash counter that deals damage.</summary>
    private const string CounterState = "Change Facing?";

    /// <summary>Target used when the auto counter whiffs: ends the sequence without the counter.</summary>
    private const string FailState = "Parry Recover";

    private const string RecoverState = "Parry Recover";
    private const string FinishedEvent = "FINISHED";

    /// <summary>Chance an auto counter lands its cross-slash after the retreat highlight.</summary>
    private const float AutoCounterSuccessChance = 0.5f;

    private static string? _lastState;

    /// <summary>Whether the Parry Clash currently running should end in the counter (true) or whiff.</summary>
    private static bool _autoCounterHits = true;

    /// <summary>Per-frame check: retargets the transition while the Chaos crest is equipped and
    /// restores it otherwise.</summary>
    internal static void Tick()
    {
        Apply(HeroController.instance);
    }

    /// <summary>Applies or restores the stance transition for the current crest. Idempotent.</summary>
    internal static void Apply(HeroController? hero)
    {
        var playMaker = hero != null ? hero.silkSpecialFSM : null;
        var fsm = playMaker != null ? playMaker.Fsm : null;
        if (fsm == null || fsm.Name != FsmName)
        {
            return;
        }

        // Remember how the running Parry Clash was entered (real hit vs. our auto counter). A fresh
        // stance must not inherit the previous clash's flag.
        var state = playMaker!.ActiveStateName;
        if (state != _lastState)
        {
            // Clear the flag when a new stance begins and again when a clash ends, so it can never
            // leak from one Cross Stitch into the next regardless of which entry/exit path is used.
            if (state == StanceStartState || _lastState == ClashState)
            {
                ParryClashTrigger.Reset();
            }

            // Decide whether the auto counter lands, once per clash entry. A real parry always
            // lands; only the stance-expired auto counter rolls.
            if (state == ClashState && !ParryClashTrigger.Attacked)
            {
                _autoCounterHits = RandomCrestModPlugin.ParryAutoCounterAlwaysSucceeds.Value
                    || UnityEngine.Random.value < AutoCounterSuccessChance;
                RandomCrestModPlugin.LogInfo(
                    $"[ParryAutoCounter] auto counter roll -> {(_autoCounterHits ? "hit" : "whiff")}.");
            }
            else if (state != ClashState)
            {
                _autoCounterHits = true;
            }

            _lastState = state;
        }

        var redirect = RandomCrestModPlugin.EnableParryAutoCounter && CrestService.IsRandomCrestEquipped();
        SetRedirect(fsm, redirect, _autoCounterHits);
    }

    private static void SetRedirect(HutongGames.PlayMaker.Fsm fsm, bool redirect, bool autoCounterHits)
    {
        var stance = fsm.GetState(StanceState);
        var clash = fsm.GetState(ClashState);
        var recover = fsm.GetState(RecoverState);
        if (stance == null || clash == null || recover == null)
        {
            return;
        }

        // Parry Stance: FINISHED -> Parry Clash (auto counter) while the crest is active, otherwise
        // the vanilla FINISHED -> Parry Recover.
        foreach (var transition in stance.Transitions)
        {
            if (transition.EventName != FinishedEvent)
            {
                continue;
            }

            if (redirect)
            {
                if (transition.ToState == RecoverState)
                {
                    transition.ToState = ClashState;
                    // Fsm.DoTransition only reads ToFsmState, so keep the two in sync.
                    transition.ToFsmState = clash;
                }
            }
            else if (transition.ToState == ClashState)
            {
                transition.ToState = RecoverState;
                transition.ToFsmState = recover;
            }
        }

        // Parry Clash: a whiffed auto counter still plays the retreat / highlight but ends in
        // Parry Recover instead of running the cross-slash counter.
        var counter = fsm.GetState(CounterState);
        var fail = fsm.GetState(FailState);
        if (counter == null || fail == null)
        {
            return;
        }

        foreach (var transition in clash.Transitions)
        {
            if (transition.EventName != FinishedEvent)
            {
                continue;
            }

            if (redirect && !autoCounterHits)
            {
                if (transition.ToState == CounterState)
                {
                    transition.ToState = FailState;
                    transition.ToFsmState = fail;
                }
            }
            else if (transition.ToState == FailState)
            {
                transition.ToState = CounterState;
                transition.ToFsmState = counter;
            }
        }
    }
}
