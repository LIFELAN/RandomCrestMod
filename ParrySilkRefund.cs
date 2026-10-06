using HarmonyLib;

namespace RandomCrestMod;

/// <summary>
/// Rewards a successful Cross Stitch (十字绣 / Parry) block by refunding the silk the cast spent.
///
/// <para>Only the <b>real</b> parry path (the stance was struck, FSM event <c>PARRIED</c>) pays out;
/// the mod's auto counter (the stance simply expired) keeps its cost, so the two flavours stay
/// meaningfully different. The amount is captured from the FSM's own <c>TakeSilk</c> call at
/// <c>Parry Start</c>, so it always matches what was actually spent - 3 normally, or 2 with the Flea
/// Charm at full health once the mod's silk discount is applied.</para>
///
/// <para>Active only while the mod's random spells are live (i.e. the Chaos crest is equipped), the
/// same window that applies the silk discount.</para>
/// </summary>
internal static class ParrySilkRefund
{
    private const string FsmName = "Silk Specials";
    private const string CastState = "Parry Start";

    /// <summary>Silk the running Cross Stitch cast spent, or 0 when nothing is pending.</summary>
    private static int _pending;

    /// <summary>
    /// Captures the silk a Cross Stitch cast just took. Called from the FSM's <c>TakeSilk</c> action
    /// while <c>Parry Start</c> is active.
    /// </summary>
    internal static void Capture(int amount)
    {
        if (!RandomCrestModPlugin.EnableParrySilkRefund || !RandomToolService.RandomSpellsActive)
        {
            return;
        }

        _pending = amount;
    }

    /// <summary>
    /// Refunds the captured silk after a real parry. Safe to call repeatedly: the first call consumes
    /// the pending amount, later ones do nothing.
    /// </summary>
    internal static void OnParried()
    {
        if (_pending <= 0)
        {
            return;
        }

        var amount = _pending;
        _pending = 0;

        // Never leak onto another crest's Cross Stitch if a stale amount survived an auto counter.
        if (!RandomCrestModPlugin.EnableParrySilkRefund || !RandomToolService.RandomSpellsActive)
        {
            return;
        }

        // heroEffect false: vanilla only flashes Hornet for heals/pickups, and normal silk gain
        // (HeroController.SilkGain) also passes false. The spool refill animation is the readable
        // feedback, since Hornet is mid-Parry Clash when this runs.
        HeroController.instance?.AddSilk(amount, heroEffect: false);
    }
}

/// <summary>
/// Flags a Cross Stitch cast and hands the spent silk to <see cref="ParrySilkRefund"/>.
///
/// <para>The FSM's <c>TakeSilk</c> action calls the <see cref="HeroController.TakeSilk(int)"/>
/// overload; we only record while the active state is <c>Parry Start</c>, so no other skill's silk
/// use is mistaken for a Cross Stitch. A postfix means the amount is only remembered once the game
/// actually processed the spend.</para>
/// </summary>
[HarmonyPatch]
internal static class ParrySilkRefundPatch
{
    [HarmonyPatch(typeof(HeroController), nameof(HeroController.TakeSilk), new[] { typeof(int) })]
    [HarmonyPostfix]
    private static void TakeSilk_Postfix(HeroController __instance, int amount)
    {
        try
        {
            var fsm = __instance.silkSpecialFSM;
            if (fsm != null
                && fsm.Fsm != null
                && fsm.Fsm.Name == "Silk Specials"
                && fsm.ActiveStateName == "Parry Start")
            {
                ParrySilkRefund.Capture(amount);
            }
        }
        catch (System.Exception e)
        {
            RandomCrestModPlugin.LogError("[ParrySilkRefund] capture failed: " + e.Message);
        }
    }
}
