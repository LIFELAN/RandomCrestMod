using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace RandomCrestMod;

/// <summary>
/// Persistent crest states (Beast rage, Reaper mode, Witch tentacles, ...) keep playing
/// crest-specific animations after the random bind has ended and the config has been restored to
/// the player's crest. Those clips only live in that crest's <c>heroAnimOverrideLib</c>, so the
/// lookup fails every frame and the "Could not resolve animation clip" spam freezes the game.
///
/// <para>This merges the clip libraries of every crest into one lookup that
/// <see cref="HeroControllerConfig.GetAnimationClip"/> falls back to.</para>
/// </summary>
internal static class RandomCrestAnimationLibrary
{
    private static readonly Dictionary<string, tk2dSpriteAnimationClip> Clips = new();
    private static bool _built;
    private static FieldInfo? _overrideLibField;

    internal static tk2dSpriteAnimationClip? FindClip(string clipName)
    {
        if (string.IsNullOrEmpty(clipName))
        {
            return null;
        }

        if (!_built)
        {
            Build();
        }

        return Clips.TryGetValue(clipName, out var clip) ? clip : null;
    }

    private static void Build()
    {
        try
        {
            var crests = ToolItemManager.GetAllCrests();
            if (crests == null || crests.Count == 0)
            {
                // Tool item manager not ready yet; try again on a later lookup.
                return;
            }

            _overrideLibField ??= AccessTools.Field(typeof(HeroControllerConfig), "heroAnimOverrideLib");

            foreach (var crest in crests)
            {
                if (crest == null)
                {
                    continue;
                }

                var config = crest.HeroConfig;
                if (config == null)
                {
                    continue;
                }

                if (_overrideLibField?.GetValue(config) is not tk2dSpriteAnimation lib || lib.clips == null)
                {
                    continue;
                }

                foreach (var clip in lib.clips)
                {
                    if (clip != null && !string.IsNullOrEmpty(clip.name) && !Clips.ContainsKey(clip.name))
                    {
                        Clips[clip.name] = clip;
                    }
                }
            }

            _built = Clips.Count > 0;
            if (_built)
            {
                RandomCrestModPlugin.Log($"Random crest animation library merged {Clips.Count} clips from all crests.");
            }
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("RandomCrestAnimationLibrary.Build failed: " + e);
        }
    }
}
