using System;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// Upgrades the randomly thrown Plasmium Phial (生质液瓶 / Lifeblood Syringe) from its vanilla +1
/// blue health to +3 once the player has obtained the Plasmium Gland (生质液腺 / Plasmium Gland).
///
/// <para>The gland normally only removes the Phial's refill reserve; this makes the questline
/// collection payoff felt on the Chaos crest, consistent with the spell / tool collection
/// rewards. Only active while the mod's random tools are live (the Chaos crest is equipped).</para>
///
/// <para>Implementation: the Phial's <c>Heal</c> step sends <c>ADD BLUE HEALTH</c>, which the
/// vanilla <c>Blue Health Control</c> FSM turns into one mask (+1 <c>playerData.healthBlue</c>). We
/// queue two more <c>ADD BLUE HEALTH</c> sends and fire each once that FSM is back in <c>Idle</c>,
/// so all three masks and the count come from the vanilla pipeline.</para>
///
/// <para>Do <b>not</b> write <c>playerData.healthBlue</c> directly or send <c>UPDATE BLUE
/// HEALTH</c>: the latter is a "clear blue health" event that calls
/// <c>HeroController.UpdateBlueHealth()</c> and resets the counter to 0, wiping the boost.</para>
/// </summary>
internal static class LifebloodSyringeService
{
    private const string AddBlueHealthEvent = "ADD BLUE HEALTH";
    private const string ToolHealState = "Heal";
    private const string BlueHealthFsmName = "Blue Health Control";
    private const string BlueHealthIdleState = "Idle";

    /// <summary>Extra <c>ADD BLUE HEALTH</c> sends on top of the vanilla one (three masks total).</summary>
    private const int ExtraAdds = 2;

    /// <summary>Minimum gap between sends so the FSM is never spammed.</summary>
    private const float AddGap = 0.1f;

    /// <summary>Give up on the queue if the vanilla FSM cannot be found in this long.</summary>
    private const float QueueTimeout = 3f;

    private static int _extraAddsRemaining;
    private static float _nextAddTime;
    private static float _queueExpiry;
    private static PlayMakerFSM? _blueHealthFsm;

    /// <summary>
    /// Called after a <c>SendEventToRegister</c> runs. When it is the Phial's <c>Heal</c> step sending
    /// <c>ADD BLUE HEALTH</c> while the Chaos crest is equipped and the Plasmium Gland is owned, queue
    /// two extra sends.
    /// </summary>
    internal static void OnAddBlueHealth(SendEventToRegister action)
    {
        try
        {
            if (!RandomToolService.RandomToolsActive)
            {
                return;
            }

            var eventName = action.eventName != null ? action.eventName.Value : null;
            if (!string.Equals(eventName, AddBlueHealthEvent, StringComparison.Ordinal))
            {
                return;
            }

            var fsm = action.Fsm;
            if (fsm == null || fsm.ActiveStateName != ToolHealState)
            {
                return;
            }

            var playerData = PlayerData.instance;
            if (playerData == null || !playerData.HasLifebloodSyringeGland)
            {
                return;
            }

            _extraAddsRemaining = ExtraAdds;
            _nextAddTime = Time.time + AddGap;
            _queueExpiry = Time.time + QueueTimeout;
            RandomCrestModPlugin.Log(
                $"[LifebloodSyringe] Plasmium Gland owned; queued {ExtraAdds} extra blue masks.");
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[LifebloodSyringe] boost failed: " + e.Message);
        }
    }

    /// <summary>
    /// Fires the queued extra <c>ADD BLUE HEALTH</c> sends. Each one is only sent while the vanilla
    /// <c>Blue Health Control</c> is idle, so it runs the full vanilla add (+1 mask, +1 count).
    /// </summary>
    internal static void Tick()
    {
        if (_extraAddsRemaining <= 0)
        {
            return;
        }

        if (Time.time > _queueExpiry)
        {
            _extraAddsRemaining = 0;
            return;
        }

        if (Time.time < _nextAddTime)
        {
            return;
        }

        var fsm = FindBlueHealthFsm();
        if (fsm == null || fsm.Fsm == null || fsm.ActiveStateName != BlueHealthIdleState)
        {
            return;
        }

        try
        {
            EventRegister.SendEvent(AddBlueHealthEvent);
            _extraAddsRemaining--;
            _nextAddTime = Time.time + AddGap;
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[LifebloodSyringe] extra add failed: " + e.Message);
            _extraAddsRemaining = 0;
        }
    }

    private static PlayMakerFSM? FindBlueHealthFsm()
    {
        if (_blueHealthFsm != null && _blueHealthFsm.Fsm != null)
        {
            return _blueHealthFsm;
        }

        _blueHealthFsm = null;
        try
        {
            foreach (var fsm in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
            {
                if (fsm == null || fsm.Fsm == null || fsm.Fsm.Name != BlueHealthFsmName)
                {
                    continue;
                }

                var go = fsm.gameObject;
                if (!go.scene.IsValid() || !go.scene.isLoaded)
                {
                    continue; // prefab asset
                }

                _blueHealthFsm = fsm;
                break;
            }
        }
        catch
        {
            _blueHealthFsm = null;
        }

        return _blueHealthFsm;
    }
}

/// <summary>Hooks the Plasmium Phial's blue-health event to apply the collection bonus.</summary>
[HarmonyPatch]
internal static class LifebloodSyringePatch
{
    [HarmonyPatch(typeof(SendEventToRegister), nameof(SendEventToRegister.OnEnter))]
    [HarmonyPostfix]
    private static void SendEventToRegister_OnEnter_Postfix(SendEventToRegister __instance)
    {
        LifebloodSyringeService.OnAddBlueHealth(__instance);
    }
}
