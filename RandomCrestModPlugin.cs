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

    // ---- Configurable (ConfigurationManager) ----

    /// <summary>Base shared tool budget at a bench. Each Tool Pouch level adds 25% (rounded).</summary>
    internal static ConfigEntry<int> ToolUsesPerBench = null!;

    /// <summary>When true, a random bind can resolve to the Cursed crest's refused bind.</summary>
    internal static ConfigEntry<bool> CursedBindEnabled = null!;

    /// <summary>When true, the Cross Stitch auto counter always lands instead of the 50% roll.</summary>
    internal static ConfigEntry<bool> ParryAutoCounterAlwaysSucceeds = null!;

    // ---- Feature switches (fixed) ----

    internal static readonly bool EnableRandomAttacks = true;

    internal static readonly bool EnableRandomBind = true;

    internal static readonly bool EnableRandomTools = true;

    internal static readonly bool EnableRandomSpells = true;

    internal static readonly bool EnableRandomTaunt = true;

    internal static readonly bool EnableRandomIcons = true;

    internal static readonly bool EnableCustomHudFrame = true;

    internal static readonly bool EnableCustomSaveSpool = true;

    internal static readonly bool EnableTauntShardOffer = true;

    internal static readonly bool EnableParryAutoCounter = true;

    internal static readonly bool EnableParryClashEffect = false;

    internal static readonly bool EnableParrySilkRefund = true;

    internal static readonly bool EnableSpellSilkRefund = true;

    internal static readonly bool DebugLogging = false;

    /// <summary>Chance (0-1) that a random bind resolves to the refused (cursed) bind.</summary>
    internal const float CursedBindChance = 0.05f;

    /// <summary>Silk shaved off every random skill cast.</summary>
    internal const int RandomSpellSilkDiscount = 1;

    internal static bool EnableCursedBind => CursedBindEnabled.Value;

    internal const float HeroHighlightAmount = 1f;

    internal const string HeroHighlightColor = "FFE0F0";

    internal const float HudFrameOffsetX = -0.84f;

    internal const float HudFrameOffsetY = 0.16f;

    internal const float HudFrameScale = 0.9125f;

    private Harmony _harmony = null!;

    private void Awake()
    {
        Instance = this;

        // All three live in the same unnamed section: ConfigurationManager skips the header for an
        // empty section name, so the list is just the three settings (no group titles in between).
        const string section = "";

        ToolUsesPerBench = Config.Bind(
            section,
            "ToolUsesPerBench",
            16,
            new ConfigDescription(
                "Base shared tool budget when resting at a bench. Each Tool Pouch upgrade adds 25% (rounded). Refills are free.",
                new AcceptableValueRange<int>(1, 999)));

        CursedBindEnabled = Config.Bind(
            section,
            "CursedBind",
            true,
            "When true, a random bind has a 5% chance to resolve to the Cursed crest's refused bind.");

        ParryAutoCounterAlwaysSucceeds = Config.Bind(
            section,
            "ParryAlwaysSucceed",
            false,
            "Cross Stitch auto counter (stance expires without a hit): false = 50% to land, true = always lands.");

        _harmony = new Harmony(Info.Metadata.GUID);
        _harmony.PatchAll(typeof(RandomCrestModPlugin).Assembly);

        CrestService.CustomIcon = LoadEmbeddedSprite("crest_icon.png");
        CrestService.CustomSilhouette = LoadEmbeddedSprite("crest_silhouette.png");
        CrestService.CustomGlow = LoadEmbeddedSprite("crest_glow.png");

        HudFrameService.Initialize(LoadEmbeddedSprite("crest_hud_frame.png", 420f));
        SaveSlotCrestService.Normal = LoadEmbeddedSprite("crest_save_spool.png");

        RandomIconService.ToolIcon = LoadEmbeddedSprite("crest_random_tool.png", 390f);
        RandomIconService.PoisonToolIcon = LoadEmbeddedSprite("crest_random_tool_poison.png", 390f);
        RandomIconService.SpellIcon = LoadEmbeddedSprite("crest_random_spell.png", 420f);

        gameObject.AddComponent<RandomCrestRunner>();

        Logger.LogInfo($"{Info.Metadata.Name} {Info.Metadata.Version} loaded.");
    }

    /// <summary>Loads an embedded PNG as a sprite, or null when absent.</summary>
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
                pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            sprite.name = "RandomCrest " + fileName;
            sprite.hideFlags = HideFlags.HideAndDontSave;
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

    /// <summary>Always-on info log.</summary>
    internal static void LogInfo(string message)
    {
        Instance.Logger.LogInfo(message);
    }

    internal static void LogError(string message)
    {
        Instance.Logger.LogError(message);
    }
}

/// <summary>
/// Per-frame driver. Runs the feature ticks in one fixed order and drops every config lease when the
/// hero dies or changes scene, so no feature can leave the moveset installed.
/// </summary>
internal sealed class RandomCrestRunner : MonoBehaviour
{
    private void Update()
    {
        var hero = HeroController.instance;
        if (hero == null)
        {
            ConfigLease.Abort(null);
        }
        else
        {
            var cs = hero.cState;
            if (cs.dead || cs.hazardDeath || cs.hazardRespawning || cs.transitioning)
            {
                ConfigLease.Abort(hero);
            }
        }

        Attacks.Tick(hero);
        Bind.Tick(hero);
        CursedBind.Tick(hero);
        Taunt.Tick(hero);
        Tools.Tick(hero);
        SpellSilkRefund.Tick(hero);
        LifebloodSyringeService.Tick();
        ParryAutoCounterService.Tick();
        RewardLedger.Tick();
        StatuePickupService.Tick();
        CrestService.EnsureCreated();
        CrestService.EnsureUnlocked();
    }

    private void LateUpdate()
    {
        // After the game's animator, so the custom HUD overlay stays on top.
        HudFrameService.Tick();
    }
}
