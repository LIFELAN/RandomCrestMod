using System.Text;
using HarmonyLib;
using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// Gives the mod crest its own bind-orb HUD frame.
///
/// <para>Earlier versions overwrote the game's <c>Bind Orb</c> tk2d mesh/material. That corrupts
/// the sprite's cached mesh/material state, so once the mod crest had been equipped the frame
/// stayed blank for every other crest. This version never touches the game mesh/material: it adds
/// a small overlay renderer under the frame and toggles visibility only.</para>
///
/// <para>While the mod crest is equipped the overlay shows and the game frame renderer is hidden;
/// otherwise the overlay is hidden and the game's own frame (whatever crest it belongs to) renders
/// untouched. The game's own FSM drives <c>MeshRenderer.enabled</c> during the HUD intro, so we only
/// re-enable the frame when we were the one who hid it (never restoring a cached value), and we wait
/// for the game to reveal the frame once before taking over so the custom frame loads in step with
/// the rest of the HUD.</para>
/// </summary>
internal static class HudFrameService
{
    private static Sprite? _sprite;
    private static Texture2D? _texture;

    private static BindOrbHudFrame? _hud;
    private static MeshRenderer? _gameRenderer;
    private static Vector3 _orbLocal = Vector3.zero;

    private static GameObject? _overlayGo;
    private static MeshFilter? _overlayFilter;
    private static MeshRenderer? _overlayRenderer;
    private static Mesh? _quad;
    private static Material? _quadMaterial;
    private static MaterialPropertyBlock? _propertyBlock;

    private static bool _showing;

    // Blue health (lifeblood) tint. The game tints its own frame sprite by setting the sprite's
    // vertex colour and enabling the shader's RECOLOUR keyword. Our overlay is a separate mesh, so
    // it has to mirror that or the custom frame stays silver while every vanilla frame turns blue.
    private static AccessTools.FieldRef<BindOrbHudFrame, Color>? _lifebloodTintRef;
    private static Color _lifebloodTint = new(0.5568628f, 0.8901961f, 1f, 1f);
    private static bool _tinted;

    // The game's own "Bind Orb" FSM toggles the frame's MeshRenderer during the HUD intro
    // (`Init` hides it, then `Appear` shows it once the health HUD has appeared). We therefore
    // must never cache-and-restore the renderer's `enabled` value: at acquire time it is usually
    // `false`, and restoring that value would leave every other crest's frame permanently hidden.
    // Instead we only remember whether *we* were the one who turned it off.
    private static bool _gameFrameRevealed;
    private static bool _weHidGameFrame;
    private static float _cachedScale = float.NaN;
    private static float _cachedOffsetX = float.NaN;
    private static float _cachedOffsetY = float.NaN;

    // HUD appear. The game grows the frame out of the spool (a 6-frame, 15fps clip). The overlay
    // is a single static texture, so we reproduce the motion by scaling the whole art out from the
    // spool centre: the frame has three protruding directions (top spike, bottom spike, needle),
    // so a radial grow reads better than a left-to-right reveal. `_reveal` is the scale (0..1).
    private const float AppearDuration = 0.5f; // slow then fast (ease-in)
    private const float DisappearDuration = 0.15f;

    // Radial, per-direction reveal: the centre disk stays full size and each protrusion grows out of
    // it. Each vertex's distance is normalised by the farthest opaque pixel in that same direction,
    // so the long right needle and the short top/bottom spikes finish together instead of the needle
    // lagging behind. `_reveal` is the progress (0 = disk only, 1 = full frame).
    private const float AppearStartReveal = 0f;
    private const float MaxRevealFrac = 1f;
    private const float DiskRevealFrac = 0.1464f; // disk radius / texture height (640 px diameter)
    private const float MaskSoftness = 0.01f;     // fade band, in normalised units
    private const int MaskGrid = 160;
    private const int MaskAngleBins = 720;

    private enum AnimMode { None, Appear, Disappear }

    private static AnimMode _animMode;
    private static float _animTime;
    private static float _animDuration;
    private static float _reveal = MaxRevealFrac;
    private static float _cachedReveal = float.NaN;
    private static bool _shown;

    private static float[]? _quadNorm;
    private static Color[]? _maskColors;
    private static Color _tintColor = Color.white;
    private static bool _tintDirty = true;

    // Set by <see cref="HudFramePatches"/> from the game's HUD frame transition. The game calls
    // FrameAppear only after the previous crest's disappear has finished, so this is the moment to
    // swap our overlay in; AlreadyAppeared covers the instant path that skips FrameAppear.
    private static bool _frameAppearPending;
    private static bool _instantAppearPending;
    private static float _waitingSince;

    // After our disappear finishes we keep the game frame hidden until the next crest's own appear
    // starts, so the default (Hunter) idle frame cannot flash through in between.
    private static bool _holdingForNextAppear;
    private static float _holdSince;
    private static float _disappearWaitSince;

    // Set by Reset() (scene / save load). On a fresh scene there is no previous crest disappear to
    // wait for, so we may take over as soon as the game frame is on screen even if the FrameAppear
    // hook fired before Reset() wiped it.
    private static bool _sceneReset;
    private static float _sceneResetSince;

    // After our scene-reset appear starts, ignore the game's catch-up FrameDisappear (it briefly
    // used the default frame before it recognised our crest) so the appear is not interrupted.
    private static float _suppressDisappearUntil;

    private static readonly bool Debug = false;
    private static float _nextDebug;

    internal static void Initialize(Sprite? sprite)
    {
        _sprite = sprite;
        _texture = sprite != null ? sprite.texture : null;
    }

    /// <summary>
    /// Drops the cached HUD / overlay references. The <c>Bind Orb</c> object is rebuilt when a save
    /// is loaded (or a scene changes), so the cached references go stale and the overlay has to be
    /// re-acquired for the new instance - otherwise the frame only appears after a full restart.
    /// </summary>
    internal static void Reset()
    {
        // Put the game's frame back before dropping the references, otherwise a persistent HUD
        // (one that survives the reset) keeps its renderer disabled forever.
        Restore();

        if (_overlayGo != null)
        {
            UnityEngine.Object.Destroy(_overlayGo);
        }

        if (_quad != null)
        {
            UnityEngine.Object.Destroy(_quad);
        }

        _hud = null;
        _gameRenderer = null;
        _overlayGo = null;
        _overlayFilter = null;
        _overlayRenderer = null;
        _quad = null;
        _cachedScale = float.NaN;
        _cachedOffsetX = float.NaN;
        _cachedOffsetY = float.NaN;
        _cachedReveal = float.NaN;
        _animMode = AnimMode.None;
        _animTime = 0f;
        _reveal = MaxRevealFrac;
        _shown = false;
        _quadNorm = null;
        _maskColors = null;
        _tintColor = Color.white;
        _tintDirty = true;
        // Keep _frameAppearPending / _instantAppearPending: the new scene's HUD may have fired its
        // appear hook just before SceneInit ran, and losing it is what made Hunter show again.
        _waitingSince = 0f;
        _holdingForNextAppear = false;
        _holdSince = 0f;
        _disappearWaitSince = 0f;
        _sceneReset = true;
        _sceneResetSince = 0f;
        _suppressDisappearUntil = 0f;
        _showing = false;
        _tinted = false;
        _gameFrameRevealed = false;
        _weHidGameFrame = false;
    }

    private static bool Enabled =>
        RandomCrestModPlugin.EnableCustomHudFrame && _sprite != null && _texture != null;

    /// <summary>
    /// The same "Is Visible" flag the game's own frame appear uses. Vanilla crests only play the
    /// appear once the crest menu is closed and the HUD is back on screen; without this gate our
    /// animation ran invisibly behind the menu and was already finished on exit.
    /// </summary>
    private static bool HudVisible()
    {
        try
        {
            var cameras = GameCameras.instance;
            return cameras == null || cameras.IsHudVisible;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>Driven from <see cref="RandomCrestRunner.LateUpdate"/>.</summary>
    internal static void Tick()
    {
        var equipped = CrestService.IsRandomCrestEquipped();

        if (Debug && Time.unscaledTime >= _nextDebug)
        {
            _nextDebug = Time.unscaledTime + 3f;
            RandomCrestModPlugin.Log(
                $"[HudFrame] dbg equipped={equipped} enabled={Enabled} hud={(_hud != null)} " +
                $"showing={_showing} gameEnabled={(_gameRenderer != null && _gameRenderer.enabled)} " +
                $"worldPos={(_hud != null ? _hud.transform.position.ToString() : "<none>")}");
        }

        if (!Enabled)
        {
            _animMode = AnimMode.None;
            _shown = false;
            _reveal = 1f;
            Restore();
            return;
        }

        if (!equipped)
        {
            // The scene/save-load "show instantly" path is only valid while the scene's HUD is first
            // appearing. If a different crest is equipped, let it expire so a later switch back to
            // ours still plays the reveal (the crest can change in the menu before the game fires
            // its HUD transition, so FrameDisappear alone is not a reliable signal).
            if (_sceneReset)
            {
                // Only HudVisible() here: while another crest is equipped we never Acquire(), so
                // _gameRenderer can be null and a renderer-based check would never expire it.
                if (HudVisible())
                {
                    if (_sceneResetSince <= 0f)
                    {
                        _sceneResetSince = Time.unscaledTime;
                    }
                    else if (Time.unscaledTime - _sceneResetSince > 1f)
                    {
                        _sceneReset = false;
                        _sceneResetSince = 0f;
                    }
                }
                else
                {
                    _sceneResetSince = 0f;
                }
            }

            // Our crest just left: shrink the overlay away, then hold the game frame hidden until
            // the next crest's appear starts (see NotifyFrameAppear / NotifyInstantAppear).
            if (_animMode == AnimMode.Disappear)
            {
                StepAnim(Time.unscaledDeltaTime);
                if (_animMode == AnimMode.None)
                {
                    _shown = false;
                    _holdingForNextAppear = true;
                    _holdSince = 0f;
                }

                Apply();
                return;
            }

            if (_holdingForNextAppear)
            {
                if (_holdSince <= 0f)
                {
                    _holdSince = Time.unscaledTime;
                }
                else if (Time.unscaledTime - _holdSince > 1.5f)
                {
                    ReleaseNow();
                    return;
                }

                _reveal = AppearStartReveal;
                Apply();
                return;
            }

            // The crest changed while our overlay was still up. Keep it up and wait for the game's
            // FrameDisappear (it fires on the crest-menu close); only then do we shrink. The
            // timeout only counts while the HUD is on screen, so idling in the menu keeps it.
            if (_showing || _shown)
            {
                Apply();

                if (!HudVisible())
                {
                    _disappearWaitSince = 0f;
                }
                else if (_disappearWaitSince <= 0f)
                {
                    _disappearWaitSince = Time.unscaledTime;
                }
                else if (Time.unscaledTime - _disappearWaitSince > 0.75f)
                {
                    ReleaseNow();
                }

                return;
            }

            // Never our frame: hand it straight back.
            _animMode = AnimMode.None;
            _shown = false;
            _reveal = MaxRevealFrac;
            _frameAppearPending = false;
            _instantAppearPending = false;
            _waitingSince = 0f;
            _disappearWaitSince = 0f;
            Restore();
            return;
        }

        if (_hud == null && !Acquire())
        {
            return;
        }

        // The game just started our frame's appear (the previous crest's disappear is done).
        if (_frameAppearPending)
        {
            _frameAppearPending = false;
            _instantAppearPending = false;
            _sceneReset = false;
            _sceneResetSince = 0f;
            _waitingSince = 0f;
            _disappearWaitSince = 0f;

            // A redundant FrameAppear (e.g. a second refresh on scene load) must not replay the
            // whole animation or restart it half-way through.
            if (_shown || _animMode == AnimMode.Appear)
            {
                Apply();
                return;
            }

            _animMode = AnimMode.Appear;
            _animTime = 0f;
            _animDuration = AppearDuration;
            _reveal = AppearStartReveal;
        }
        else if (_instantAppearPending)
        {
            _instantAppearPending = false;
            _sceneReset = false;
            _disappearWaitSince = 0f;
            _animMode = AnimMode.None;
            _reveal = MaxRevealFrac;
            _shown = true;
            _waitingSince = 0f;
        }

        if (_animMode == AnimMode.Appear)
        {
            StepAnim(Time.unscaledDeltaTime);
            Apply();
            return;
        }

        if (_shown)
        {
            Apply();
            return;
        }

        // Fresh scene / save load: no previous crest disappear to wait for, and the game's own HUD
        // intro already animates, so show the frame instantly instead of replaying our grow (which
        // read as an overall enlarge). Only crest switches play the reveal.
        if (_sceneReset && _gameRenderer != null && _gameRenderer.enabled && HudVisible())
        {
            _sceneReset = false;
            _sceneResetSince = 0f;
            _suppressDisappearUntil = Time.unscaledTime + 3f;
            _animMode = AnimMode.None;
            _reveal = MaxRevealFrac;
            _shown = true;
            Apply();
            return;
        }

        // Still waiting for the game's appear hook. Leave the game frame alone (the previous
        // crest is still disappearing). Safety net for the few instant paths that skip the hook.
        if (!HudVisible())
        {
            _waitingSince = 0f;
            return;
        }

        if (_waitingSince <= 0f)
        {
            _waitingSince = Time.unscaledTime;
        }
        else if (Time.unscaledTime - _waitingSince > 1.5f)
        {
            _animMode = AnimMode.Appear;
            _animTime = 0f;
            _animDuration = AppearDuration;
            _reveal = AppearStartReveal;
        }
    }

    /// <summary>Called from <see cref="HudFramePatches"/> when the game starts a new frame appear.</summary>
    internal static void NotifyFrameAppear()
    {
        if (!Enabled)
        {
            return;
        }

        if (CrestService.IsRandomCrestEquipped())
        {
            _frameAppearPending = true;
            return;
        }

        // The new crest is not ours: release the HUD so its own appear shows. But never yank our
        // overlay away while it is still animating in (e.g. the game reports its default frame
        // before it has recognised our crest on scene load).
        if ((_showing || _holdingForNextAppear) && _animMode != AnimMode.Appear && !_sceneReset)
        {
            ReleaseNow();
        }
    }

    /// <summary>Called from <see cref="HudFramePatches"/> for the instant appear path.</summary>
    internal static void NotifyInstantAppear()
    {
        if (!Enabled)
        {
            return;
        }

        if (CrestService.IsRandomCrestEquipped())
        {
            _instantAppearPending = true;
            return;
        }

        if ((_showing || _holdingForNextAppear) && _animMode != AnimMode.Appear && !_sceneReset)
        {
            ReleaseNow();
        }
    }

    /// <summary>Called from <see cref="HudFramePatches"/> when the previous frame's disappear starts.</summary>
    internal static void NotifyFrameDisappear()
    {
        // The scene-load catch-up transition (the game briefly used the default frame before it
        // recognised our crest) must not interrupt the appear we already started.
        if (Time.unscaledTime < _suppressDisappearUntil)
        {
            return;
        }

        // Any other disappear is a real crest switch: drop the scene-reset "show instantly" path, so
        // switching back to our crest later still plays the reveal instead of being taken over by it.
        _sceneReset = false;

        if (!Enabled || (!_showing && !_shown))
        {
            return;
        }

        _frameAppearPending = false;
        _disappearWaitSince = 0f;
        _instantAppearPending = false;
        _holdingForNextAppear = false;
        _holdSince = 0f;
        _animMode = AnimMode.Disappear;
        _animTime = 0f;
        _animDuration = DisappearDuration;
    }

    /// <summary>Hides the overlay and gives the game frame back immediately.</summary>
    private static void ReleaseNow()
    {
        _animMode = AnimMode.None;
        _shown = false;
        _reveal = MaxRevealFrac;
        _frameAppearPending = false;
        _instantAppearPending = false;
        _holdingForNextAppear = false;
        _holdSince = 0f;
        _disappearWaitSince = 0f;
        _waitingSince = 0f;
        _sceneReset = false;
        _suppressDisappearUntil = 0f;
        Restore();
    }

    private static void StepAnim(float delta)
    {
        _animTime += delta;
        var progress = _animDuration > 0f ? Mathf.Clamp01(_animTime / _animDuration) : 1f;

        if (_animMode == AnimMode.Appear)
        {
            var eased = progress * progress; // ease-in: slow at first, then fast
            _reveal = Mathf.Lerp(AppearStartReveal, MaxRevealFrac, eased);
        }
        else
        {
            var eased = 1f - (1f - progress) * (1f - progress); // ease-out: fast, then slow
            // Retract the protrusions back to the disk instead of vanishing completely.
            _reveal = Mathf.Lerp(MaxRevealFrac, AppearStartReveal, eased);
        }

        if (progress < 1f)
        {
            return;
        }

        var wasAppear = _animMode == AnimMode.Appear;
        _reveal = wasAppear ? MaxRevealFrac : AppearStartReveal;
        _shown = wasAppear;
        _animMode = AnimMode.None;
    }

    private static bool Acquire()
    {
        _hud = null;
        _gameRenderer = null;

        BindOrbHudFrame? best = null;
        foreach (var hud in Resources.FindObjectsOfTypeAll<BindOrbHudFrame>())
        {
            if (hud == null)
            {
                continue;
            }

            var go = hud.gameObject;
            var isScene = go.scene.IsValid() && go.scene.isLoaded;
            if (Debug)
            {
                RandomCrestModPlugin.Log(
                    $"[HudFrame] candidate '{PathOf(hud.transform)}' scene={isScene} " +
                    $"activeInHierarchy={go.activeInHierarchy} layer={go.layer}");
            }

            if (!isScene)
            {
                continue; // prefab asset - editing it does nothing on screen
            }

            if (best == null)
            {
                best = hud;
            }

            if (go.activeInHierarchy)
            {
                best = hud;
                break; // prefer the live, on-screen instance
            }
        }

        _hud = best;
        if (_hud == null)
        {
            return false;
        }

        _gameRenderer = _hud.GetComponent<MeshRenderer>();
        if (_gameRenderer == null)
        {
            RandomCrestModPlugin.LogError("[HudFrame] Bind Orb has no MeshRenderer; cannot add the frame.");
            return false;
        }

        _gameFrameRevealed = _gameRenderer.enabled;
        _weHidGameFrame = false;

        // Read the game's own lifeblood tint from the live frame so a game update cannot desync us.
        try
        {
            _lifebloodTintRef ??= AccessTools.FieldRefAccess<BindOrbHudFrame, Color>("lifebloodTint");
            _lifebloodTint = _lifebloodTintRef(_hud);
        }
        catch
        {
            // Keep the known default tint.
        }

        // The game's own silk Orb child sits at the spool centre; anchor our art there so the
        // custom spool lines up with the real one regardless of the HUD layout.
        var orb = _hud.transform.Find("Orb");
        _orbLocal = orb != null ? orb.localPosition : Vector3.zero;

        if (_overlayGo == null)
        {
            _overlayGo = new GameObject("RandomCrest HUD Frame")
            {
                // Must share the game frame's layer or the HUD camera's culling mask skips it.
                layer = _hud.gameObject.layer,
            };
            _overlayGo.transform.SetParent(_hud.transform, false);
            _overlayGo.transform.localPosition = Vector3.zero;
            _overlayGo.transform.localRotation = Quaternion.identity;
            _overlayGo.transform.localScale = Vector3.one;

            _overlayFilter = _overlayGo.AddComponent<MeshFilter>();
            _overlayRenderer = _overlayGo.AddComponent<MeshRenderer>();
            _overlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _overlayRenderer.receiveShadows = false;
            _overlayRenderer.sortingLayerID = _gameRenderer.sortingLayerID;
            _overlayRenderer.sortingOrder = _gameRenderer.sortingOrder;
            _overlayGo.SetActive(false);
        }

        // Copy the game material so the shader / texture setup matches, then swap in our art.
        var baseMat = _gameRenderer.sharedMaterial;
        _quadMaterial = baseMat != null
            ? new Material(baseMat) { mainTexture = _texture }
            : new Material(Shader.Find("Sprites/Default")) { mainTexture = _texture };

        // The game creates an instanced copy of the shared material when it enables RECOLOUR, so if
        // we acquire while the frame is already tinted our clone would inherit the keyword. Start
        // from a clean, white material; RefreshLifebloodTint re-applies the tint as needed.
        try
        {
            _quadMaterial.DisableKeyword("RECOLOUR");
            _quadMaterial.color = Color.white;
        }
        catch
        {
        }

        RandomCrestModPlugin.Log($"[HudFrame] using '{PathOf(_hud.transform)}' frame art.");
        return true;
    }

    private static void Apply()
    {
        if (_overlayFilter == null || _overlayRenderer == null || _overlayGo == null || _sprite == null)
        {
            return;
        }

        // Sync with the game's HUD intro: the frame renderer is hidden by the FSM until `Appear`
        // fires ("SHOW HP"). Wait for it to be shown once, then take over, so the custom frame
        // loads together with the rest of the HUD instead of popping in the instant the save opens.
        if (!_gameFrameRevealed)
        {
            if (_gameRenderer != null && _gameRenderer.enabled)
            {
                _gameFrameRevealed = true;
            }
            else
            {
                if (_overlayGo.activeSelf)
                {
                    _overlayGo.SetActive(false);
                }

                return;
            }
        }

        var scale = RandomCrestModPlugin.HudFrameScale;
        var ox = RandomCrestModPlugin.HudFrameOffsetX;
        var oy = RandomCrestModPlugin.HudFrameOffsetY;

        if (_quad == null || scale != _cachedScale || ox != _cachedOffsetX || oy != _cachedOffsetY)
        {
            _cachedScale = scale;
            _cachedOffsetX = ox;
            _cachedOffsetY = oy;
            _cachedReveal = float.NaN;
            _quad = BuildQuad(_orbLocal.x + ox, _orbLocal.y + oy, scale);
            _overlayFilter.sharedMesh = _quad;
            _tintDirty = true; // fresh mesh needs the tint + reveal mask re-applied
        }

        _overlayRenderer.sharedMaterial = _quadMaterial;
        if (_texture != null)
        {
            _propertyBlock ??= new MaterialPropertyBlock();
            _propertyBlock.SetTexture("_MainTex", _texture);
            _overlayRenderer.SetPropertyBlock(_propertyBlock);
        }

        RefreshLifebloodTint();

        if (_reveal != _cachedReveal)
        {
            _cachedReveal = _reveal;
            ApplyMask(_reveal);
        }

        _overlayRenderer.enabled = true;
        _overlayGo.SetActive(true);

        if (_gameRenderer != null)
        {
            // Only claim the renderer if it is currently on; if the game has it off we leave it
            // alone so its own FSM stays in charge of when the frame should be visible.
            if (_gameRenderer.enabled)
            {
                _weHidGameFrame = true;
            }

            _gameRenderer.enabled = false;
        }

        _showing = true;
    }

    private static void Restore()
    {
        if (_overlayGo != null && _overlayGo.activeSelf)
        {
            _overlayGo.SetActive(false);
        }

        if (_weHidGameFrame && _gameRenderer != null)
        {
            _gameRenderer.enabled = true;
        }

        _weHidGameFrame = false;
        _showing = false;
    }

    /// <summary>
    /// Mirrors <see cref="BindOrbHudFrame.RefreshLifebloodTint"/> on our overlay: when the hero is
    /// in the blue-health (lifeblood) state, tint the quad's vertex colours and enable the shader's
    /// RECOLOUR keyword; otherwise go back to white and disable it. Only reacts to state changes.
    /// </summary>
    private static void RefreshLifebloodTint()
    {
        if (_quad == null || _quadMaterial == null)
        {
            return;
        }

        var tinted = false;
        try
        {
            var hero = HeroController.instance;
            tinted = hero != null && hero.IsInLifebloodState;
        }
        catch
        {
            tinted = false;
        }

        if (tinted == _tinted && !_tintDirty)
        {
            return;
        }

        _tinted = tinted;
        _tintDirty = false;
        _tintColor = tinted ? _lifebloodTint : Color.white;
        _cachedReveal = float.NaN; // force the reveal mask colours to re-apply with the new tint

        if (tinted)
        {
            _quadMaterial.EnableKeyword("RECOLOUR");
        }
        else
        {
            _quadMaterial.DisableKeyword("RECOLOUR");
        }
    }

    /// <summary>
    /// Applies the reveal at <paramref name="progress"/> (0 = disk only, 1 = full frame). Each
    /// vertex's normalised distance is compared against the progress, so the disk stays its full
    /// size and every protrusion reaches its own end together.
    /// </summary>
    private static void ApplyMask(float progress)
    {
        if (_quad == null || _quadNorm == null)
        {
            return;
        }

        var count = _quadNorm.Length;
        var colors = _maskColors;
        if (colors == null || colors.Length != count)
        {
            colors = _maskColors = new Color[count];
        }

        var c = _tintColor;
        var from = progress;
        var to = Mathf.Min(progress + MaskSoftness, 1f);
        if (to <= from)
        {
            to = from + 0.0001f;
        }

        for (var i = 0; i < count; i++)
        {
            // InverseLerp gives the 0..1 factor; SmoothStep's third arg must be that factor.
            var t = Mathf.InverseLerp(from, to, _quadNorm[i]);
            var alpha = 1f - Mathf.SmoothStep(0f, 1f, t);
            colors[i] = new Color(c.r, c.g, c.b, alpha);
        }

        _quad.colors = colors;
    }

    // Spool (disk) centre inside the source art, as a fraction of the image (x from left, y from top).
    // Current crest_hud_frame.png is the high-res 2374x2186 art; its disk centre is at pixel
    // (350.5,1153.0). The overlay is scaled so that 640 px disk = the vanilla 89 px disk
    // (HudFrameScale 0.9125). Keep those two in sync when the art changes.
    private const float SpoolFracX = 0.14764f;
    private const float SpoolFracY = 0.52745f;

    private static Mesh BuildQuad(float spoolX, float spoolY, float scale)
    {
        var texW = (float)_texture!.width;
        var texH = (float)_texture.height;
        var ppu = _sprite!.pixelsPerUnit;
        var originX = SpoolFracX * texW;
        var originY = SpoolFracY * texH;

        // A grid (rather than a single quad) so the reveal mask has enough vertices to fade
        // smoothly. Vertex positions are fixed; only the colours change per frame.
        var n = MaskGrid;
        var count = (n + 1) * (n + 1);
        var verts = new Vector3[count];
        var uvs = new Vector2[count];
        var distances = new float[count];
        var bins = new int[count];

        for (var y = 0; y <= n; y++)
        {
            for (var x = 0; x <= n; x++)
            {
                var px = (float)x / n * texW;
                var py = (float)y / n * texH;
                var i = y * (n + 1) + x;
                verts[i] = new Vector3(
                    spoolX + (px - originX) / ppu * scale,
                    spoolY - (py - originY) / ppu * scale,
                    0f);
                uvs[i] = new Vector2((float)x / n, 1f - (float)y / n);

                var dx = px - originX;
                var dy = py - originY;
                distances[i] = Mathf.Sqrt(dx * dx + dy * dy) / texH;

                var ang = Mathf.Atan2(dy, dx);
                bins[i] = Mathf.Clamp(
                    (int)((ang + Mathf.PI) / (2f * Mathf.PI) * MaskAngleBins), 0, MaskAngleBins - 1);
            }
        }

        // Farthest opaque sample in each direction, so every protrusion is normalised by its own
        // length (the long needle and the short spikes then finish together).
        var maxByBin = new float[MaskAngleBins];
        var globalMax = 0f;
        for (var i = 0; i < count; i++)
        {
            if (_texture.GetPixelBilinear(uvs[i].x, uvs[i].y).a <= 0.06f)
            {
                continue;
            }

            var d = distances[i];
            if (d > maxByBin[bins[i]])
            {
                maxByBin[bins[i]] = d;
            }

            if (d > globalMax)
            {
                globalMax = d;
            }
        }

        for (var b = 0; b < MaskAngleBins; b++)
        {
            if (maxByBin[b] <= 0f)
            {
                maxByBin[b] = globalMax;
            }
        }

        var norms = new float[count];
        for (var i = 0; i < count; i++)
        {
            var d = distances[i];
            if (d <= DiskRevealFrac)
            {
                norms[i] = 0f;
                continue;
            }

            var max = maxByBin[bins[i]];
            norms[i] = max > DiskRevealFrac
                ? Mathf.Clamp01((d - DiskRevealFrac) / (max - DiskRevealFrac))
                : 1f;
        }

        var tris = new int[n * n * 12];
        var t = 0;
        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                var i0 = y * (n + 1) + x;
                var i1 = i0 + 1;
                var i2 = i0 + (n + 1);
                var i3 = i2 + 1;
                // Both windings so the grid shows regardless of the shader's cull mode.
                tris[t++] = i0; tris[t++] = i1; tris[t++] = i3;
                tris[t++] = i0; tris[t++] = i3; tris[t++] = i2;
                tris[t++] = i0; tris[t++] = i3; tris[t++] = i1;
                tris[t++] = i0; tris[t++] = i2; tris[t++] = i3;
            }
        }

        var mesh = new Mesh { name = "RandomCrestHudQuad" };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.colors = new Color[count]; // filled by ApplyMask on the same frame
        mesh.triangles = tris;
        mesh.RecalculateBounds();

        _quadNorm = norms;
        _maskColors = null;
        return mesh;
    }

    private static string PathOf(Transform t)
    {
        var sb = new StringBuilder(t.name);
        for (var p = t.parent; p != null; p = p.parent)
        {
            sb.Insert(0, p.name + "/");
        }

        return sb.ToString();
    }
}
