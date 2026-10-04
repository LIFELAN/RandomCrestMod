using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace RandomCrestMod;

[BepInAutoPlugin(id: "io.github.lifelan.randomcrestmod")]
public partial class RandomCrestModPlugin : BaseUnityPlugin
{
    internal static RandomCrestModPlugin Instance { get; private set; } = null!;

    // ---- Configurable (the only entry left in the BepInEx config) ----

    /// <summary>Number of uses every tool is refilled to at a bench; refills are free.</summary>
    internal static ConfigEntry<int> ToolUsesPerBench = null!;

    // ---- Fixed values (previously configurable, now baked in) ----

    internal static readonly bool EnableRandomAttacks = true;

    internal static readonly bool EnableRandomBind = true;

    internal static readonly bool EnableRandomTools = true;

    internal static readonly bool EnableRandomSpells = true;

    internal static readonly bool EnableCustomHudFrame = true;

    internal static readonly bool EnableCustomSaveSpool = true;

    internal static readonly bool EnableRandomIcons = true;

    internal static readonly bool OnlyOnRandomCrest = true;

    internal static readonly bool DebugLogging = false;

    internal static readonly float HudFrameOffsetX = -0.84f;

    internal static readonly float HudFrameOffsetY = 0.16f;

    internal static readonly float HudFrameScale = 0.945f;

    private Harmony _harmony = null!;

    private void Awake()
    {
        Instance = this;

        ToolUsesPerBench = Config.Bind(
            "Tools",
            "ToolUsesPerBench",
            20,
            "Number of uses every tool is refilled to when resting at a bench (refills are free).");

        _harmony = new Harmony(Info.Metadata.GUID);
        _harmony.PatchAll(typeof(RandomAttackPatches));
        _harmony.PatchAll(typeof(ReaperPayoutPatch));
        _harmony.PatchAll(typeof(AnimationFallbackPatches));
        _harmony.PatchAll(typeof(CrestPatches));
        _harmony.PatchAll(typeof(SaveProfileHealthBarPatch));
        _harmony.PatchAll(typeof(GetWillThrowToolWindowPatch));
        _harmony.PatchAll(typeof(GetBoundAttackToolPatch));
        _harmony.PatchAll(typeof(GetAttackToolBindingPatch));
        _harmony.PatchAll(typeof(GetToolStorageAmountPatch));
        _harmony.PatchAll(typeof(CanThrowToolPatch));
        _harmony.PatchAll(typeof(DidUseAttackToolPatch));
        _harmony.PatchAll(typeof(TryReplenishToolsPatch));
        _harmony.PatchAll(typeof(RandomToolSaveLoadedPatch));
        _harmony.PatchAll(typeof(ToolHudIconSpritePatch));
        _harmony.PatchAll(typeof(RadialHudIconColourPatch));

        // Optional custom crest art (embedded PNGs). Drop crest_icon.png / crest_silhouette.png /
        // crest_glow.png into Assets/ to use them.
        CrestService.CustomIcon = LoadEmbeddedSprite("crest_icon.png");
        CrestService.CustomSilhouette = LoadEmbeddedSprite("crest_silhouette.png");
        CrestService.CustomGlow = LoadEmbeddedSprite("crest_glow.png");

        // Custom bind-orb HUD frame (user-composited art). 420 px/unit makes the ~607 px spool
        // disk match the game's evolved spool size at scale 1.
        HudFrameService.Initialize(LoadEmbeddedSprite("crest_hud_frame.png", 420f));

        // Save-slot spool art for the title/load screen (226x173, matching the game's sprites).
        SaveSlotCrestService.Normal = LoadEmbeddedSprite("crest_save_spool.png");

        // Fixed HUD icons for the random tool / random spell bindings, sized to match the game's
        // tiny HUD tool icons (~0.62 units at ppu 100).
        RandomIconService.ToolIcon = LoadEmbeddedSprite("crest_random_tool.png", 420f);
        RandomIconService.SpellIcon = LoadEmbeddedSprite("crest_random_spell.png", 420f);

        gameObject.AddComponent<RandomCrestRunner>();

        Logger.LogInfo($"{Info.Metadata.Name} {Info.Metadata.Version} loaded.");
    }

    /// <summary>Loads an embedded PNG (Resources/Assets) as a sprite, or null when absent.</summary>
    internal static Sprite? LoadEmbeddedSprite(string fileName, float pixelsPerUnit = 100f)
    {
        try
        {
            using var stream = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("RandomCrestMod." + fileName);
            if (stream == null)
            {
                return null;
            }

            var bytes = new byte[stream.Length];
            var read = 0;
            while (read < bytes.Length)
            {
                var count = stream.Read(bytes, read, bytes.Length - read);
                if (count <= 0)
                {
                    break;
                }

                read += count;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                return null;
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.hideFlags = HideFlags.HideAndDontSave;

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit);
            sprite.name = "RandomCrest " + fileName;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Instance.Logger.LogInfo("Loaded embedded crest sprite: " + fileName);
            return sprite;
        }
        catch (Exception e)
        {
            LogError("Failed to load embedded sprite " + fileName + ": " + e.Message);
            return null;
        }
    }

    internal static void Log(string message)
    {
        if (DebugLogging)
        {
            Instance.Logger.LogInfo(message);
        }
    }

    internal static void LogError(string message)
    {
        Instance.Logger.LogError(message);
    }

}

/// <summary>Small helper component that drives the per-frame restore of the random attack config.</summary>
internal sealed class RandomCrestRunner : MonoBehaviour
{
    private void Update()
    {
        RandomAttackService.Tick();
        RandomBindService.Tick();
        RandomToolService.Tick();
        CrestService.EnsureCreated();
        CrestService.EnsureUnlocked();
    }

    private void LateUpdate()
    {
        // Runs after the game's tk2d animator so the custom HUD overlay / base-frame toggle stick.
        HudFrameService.Tick();
    }
}
