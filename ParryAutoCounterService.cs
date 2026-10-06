namespace RandomCrestMod;

/// <summary>
/// Cross Stitch (十字绣 / Parry) auto counter, active only while the Chaos crest is equipped.
///
/// <para>In vanilla the hero's <c>Silk Specials</c> FSM holds a <c>Parry Stance</c> for 0.4s and
/// only advances to <c>Parry Clash</c> when the parry box is struck (<c>HeroController.CheckParry</c>
/// sends the <c>PARRIED</c> event). Without a hit it falls through <c>FINISHED</c> -&gt;
/// <c>Parry Recover</c> and the hero just stands there. We retarget that one <c>FINISHED</c>
/// transition to <c>Parry Clash</c> so the stance always flows into the counter.</para>
///
/// <para>Because the FSM instance is shared by every crest, we apply the edit only while
/// <see cref="CrestService.IsRandomCrestEquipped"/> is true and restore the vanilla
/// <c>FINISHED</c> -&gt; <c>Parry Recover</c> link otherwise, checked every frame from
/// <see cref="Tick"/>. Only the <c>Parry Stance</c> state of that single FSM is touched; the
/// <c>Cancel Parry</c> path is a separate global transition and is left intact.</para>
/// </summary>
internal static class ParryAutoCounterService
{
    private const string FsmName = "Silk Specials";
    private const string StanceStartState = "Parry Start";
    private const string StanceState = "Parry Stance";
    private const string ClashState = "Parry Clash";
    private const string RecoverState = "Parry Recover";
    private const string FinishedEvent = "FINISHED";

    private static string? _lastState;

    /// <summary>Per-frame check: retargets the transition while the Chaos crest is equipped and
    /// restores it otherwise.</summary>
    internal static void Tick()
    {
        Apply(HeroController.instance);
    }

    /// <summary>Applies or restores the stance transition for the current crest. Idempotent.</summary>
    internal static void Apply(HeroController? hero)
    {
        if (!RandomCrestModPlugin.EnableParryAutoCounter)
        {
            return;
        }

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

            _lastState = state;
        }

        SetRedirect(fsm, CrestService.IsRandomCrestEquipped());
    }

    private static void SetRedirect(HutongGames.PlayMaker.Fsm fsm, bool redirect)
    {
        var stance = fsm.GetState(StanceState);
        var clash = fsm.GetState(ClashState);
        var recover = fsm.GetState(RecoverState);
        if (stance == null || clash == null || recover == null)
        {
            return;
        }

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
    }
}
