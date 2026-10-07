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

    // ---- Configurable ----

    /// <summary>Base number of uses the shared tools are refilled to at a bench. Each Tool Pouch
    /// upgrade increases it by 25% (rounded). Refills are free.</summary>
    internal static ConfigEntry<int> ToolUsesPerBench = null!;

    /// <summary>Horizontal offset (world units) of the custom HUD frame's spool centre. Internal
    /// tuning only - nudge in 0.01 steps. Larger values move the frame right.</summary>
    internal static ConfigEntry<float> HudFrameOffsetX = null!;

    /// <summary>Vertical offset (world units) of the custom HUD frame's spool centre. Internal
    /// tuning only - nudge in 0.01 steps. Larger values move the frame up.</summary>
    internal static ConfigEntry<float> HudFrameOffsetY = null!;

    // ---- Fixed values (previously configurable, now baked in) ----

    internal static readonly bool EnableRandomAttacks = true;

    internal static readonly bool EnableRandomBind = true;

    internal static readonly bool EnableRandomTools = true;

    internal static readonly bool EnableRandomSpells = true;

    internal static readonly bool EnableCustomHudFrame = true;

    internal static readonly bool EnableCustomSaveSpool = true;

    internal static readonly bool EnableRandomIcons = true;

    /// <summary>Randomises Hornet's taunt (R3 / V) into its standard / Beast / ring-toss flavours.</summary>
    internal static readonly bool EnableRandomTaunt = true;

    /// <summary>When true, a completed taunt on the Chaos crest spends 80 shell shards and grants
    /// rosaries based on the rolled flavour (standard 0-50, Beast 60, rings 80). Ignored when the
    /// player cannot afford the shards.</summary>
    internal static readonly bool EnableTauntShardOffer = true;

    /// <summary>Lets the Cross Stitch (十字绣 / Parry) skill counter even when the hero is not hit
    /// during the stance. Only active while the Chaos crest is equipped.</summary>
    internal static readonly bool EnableParryAutoCounter = true;

    /// <summary>When false, suppresses the "Parry Clash Effect" hit spark that pops on Hornet's
    /// needle when the stance is struck. Only active while the Chaos crest is equipped; the clash
    /// animation / audio are unaffected.</summary>
    internal static readonly bool EnableParryClashEffect = false;

    /// <summary>When true, a real Cross Stitch block refunds the silk that cast spent (3 normally,
    /// or 2 with the Flea Charm at full health). Only active while the Chaos crest is equipped; the
    /// auto counter (stance expired without a hit) still costs its silk.</summary>
    internal static readonly bool EnableParrySilkRefund = true;

    /// <summary>When true, every random silk skill except the Cross Stitch has a chance (3% per
    /// collected silk skill) to refund the silk it just spent, paid immediately like the Cross
    /// Stitch refund. Only the first silk spend of a cast rolls.</summary>
    internal static readonly bool EnableSpellSilkRefund = true;

    /// <summary>Highlight strength applied to Hornet during the Cross Stitch retreat step, only
    /// while the Chaos crest is equipped (0 = off).</summary>
    internal static readonly float HeroHighlightAmount = 1f;

    /// <summary>Highlight colour as hex RGB (pinkish white).</summary>
    internal static readonly string HeroHighlightColor = "FFE0F0";

    internal static readonly bool OnlyOnRandomCrest = true;

    /// <summary>Silk shaved off every silk skill while the mod crest's random spells are active
    /// (vanilla 4 -> 3, or 3 -> 2 with the Flea Charm at full health).</summary>
    internal static readonly int RandomSpellSilkDiscount = 1;

    internal static readonly bool DebugLogging = false;

    internal static readonly float HudFrameScale = 0.9125f;

    private Harmony _harmony = null!;

    private void Awake()
    {
        Instance = this;

        ToolUsesPerBench = Config.Bind(
            "Tools",
            "ToolUsesPerBench",
            16,
            "Base tool capacity shared by every tool when resting at a bench. Each Tool Pouch upgrade "
            + "increases it by 25% (rounded to the nearest use). Refills are free.");

        HudFrameOffsetX = Config.Bind(
            "Hud",
            "FrameOffsetX",
            -0.84f,
            "Horizontal offset of the Chaos HUD frame, in world units. Internal tuning only; change "
            + "in 0.01 steps. Larger values move the frame right.");

        HudFrameOffsetY = Config.Bind(
            "Hud",
            "FrameOffsetY",
            0.16f,
            "Vertical offset of the Chaos HUD frame, in world units. Internal tuning only; change "
            + "in 0.01 steps. Larger values move the frame up.");

        _harmony = new Harmony(Info.Metadata.GUID);
        _harmony.PatchAll(typeof(RandomAttackPatches));
        _harmony.PatchAll(typeof(RandomTauntPatches));
        _harmony.PatchAll(typeof(ParryAutoCounterPatch));
        _harmony.PatchAll(typeof(ParryTriggerPatches));
        _harmony.PatchAll(typeof(ParryClashEffectPatch));
        _harmony.PatchAll(typeof(ParrySilkRefundPatch));
        _harmony.PatchAll(typeof(SpellSilkRefundPatch));
        _harmony.PatchAll(typeof(LifebloodSyringePatch));
        _harmony.PatchAll(typeof(HeroHighlightPatch));
        _harmony.PatchAll(typeof(ReaperPayoutPatch));
        _harmony.PatchAll(typeof(AnimationFallbackPatches));
        _harmony.PatchAll(typeof(CrestPatches));
        _harmony.PatchAll(typeof(CrestUpgraderPatches));
        _harmony.PatchAll(typeof(SaveProfileHealthBarPatch));
        _harmony.PatchAll(typeof(GetWillThrowToolWindowPatch));
        _harmony.PatchAll(typeof(GetBoundAttackToolPatch));
        _harmony.PatchAll(typeof(GetAttackToolBindingPatch));
        _harmony.PatchAll(typeof(GetToolStorageAmountPatch));
        _harmony.PatchAll(typeof(IsToolEquippedPatch));
        _harmony.PatchAll(typeof(CanThrowToolPatch));
        _harmony.PatchAll(typeof(DidUseAttackToolPatch));
        _harmony.PatchAll(typeof(ThrowToolBarragePatch));
        _harmony.PatchAll(typeof(TryReplenishToolsPatch));
        _harmony.PatchAll(typeof(RandomToolSaveLoadedPatch));
        _harmony.PatchAll(typeof(PlayerDataSilkSkillCostPatch));
        _harmony.PatchAll(typeof(ToolHudIconSpritePatch));
        _harmony.PatchAll(typeof(ToolHudIconColourPatch));
        _harmony.PatchAll(typeof(SkillGetMsgCrestSilhouettePatch));

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
        // The tool glyphs are finer/denser than the spell ones, so load them at a lower ppu to make
        // the tool icon occupy a similar share of the HUD slot.
        RandomIconService.ToolIcon = LoadEmbeddedSprite("crest_random_tool.png", 390f);
        RandomIconService.PoisonToolIcon = LoadEmbeddedSprite("crest_random_tool_poison.png", 390f);
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

            // FullRect keeps soft alpha edges (the crest glow's halo) instead of letting a tight
            // mesh clip them; alignment is identical because the rect/pivot are unchanged.
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
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
        RandomTauntService.Tick();
        SpellSilkRefund.Tick();
        LifebloodSyringeService.Tick();
        ParryAutoCounterService.Tick();
        CrestService.EnsureCreated();
        CrestService.EnsureUnlocked();
    }

    private void LateUpdate()
    {
        // Runs after the game's tk2d animator so the custom HUD overlay / base-frame toggle stick.
        HudFrameService.Tick();
    }
}
