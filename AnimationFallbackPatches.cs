using System;
using HarmonyLib;
using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// Persistent crest states (Beast rage, Reaper mode, Witch tentacles, ...) keep playing
/// crest-specific animations after a random bind has ended and the config has been restored to the
/// player's crest. Those clips only live in that crest's override library, so the lookup fails and
/// spams "Could not resolve animation clip" every frame, which freezes the game.
///
/// <para>The fallback is applied ONLY after the normal lookup chain (current config -> windy
/// library -> base animator) has already failed, so movement/idle clips keep using the correct
/// base library and Hornet's appearance is untouched.</para>
/// </summary>
[HarmonyPatch]
internal static class AnimationFallbackPatches
{
    [HarmonyPatch(typeof(HeroAnimationController), nameof(HeroAnimationController.GetClip))]
    [HarmonyPostfix]
    private static void GetClip_Postfix(string clipName, ref tk2dSpriteAnimationClip __result)
    {
        if (__result != null)
        {
            return;
        }

        var fallback = RandomCrestAnimationLibrary.FindClip(clipName);
        if (fallback != null)
        {
            __result = fallback;
        }
    }

    /// <summary>
    /// The base lookup already logs an error before we can fill the result, so suppress that
    /// specific message to avoid the per-frame log flood (the clip is served by the fallback).
    /// </summary>
    [HarmonyPatch(typeof(Debug), nameof(Debug.LogError), new[] { typeof(object), typeof(UnityEngine.Object) })]
    [HarmonyPrefix]
    private static bool Debug_LogError_Prefix(object message)
    {
        return !(message is string text
            && text.StartsWith("Could not resolve animation clip", StringComparison.Ordinal));
    }
}
