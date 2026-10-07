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
        _showing = false;
        _tinted = false;
        _gameFrameRevealed = false;
        _weHidGameFrame = false;
    }

    private static bool Enabled =>
        RandomCrestModPlugin.EnableCustomHudFrame && _sprite != null && _texture != null;

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

        if (!Enabled || !equipped)
        {
            Restore();
            return;
        }

        if (_hud == null && !Acquire())
        {
            return;
        }

        Apply();
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

        var scale = RandomCrestModPlugin.HudFrameScale.Value;
        var ox = RandomCrestModPlugin.HudFrameOffsetX.Value;
        var oy = RandomCrestModPlugin.HudFrameOffsetY.Value;

        if (_quad == null || scale != _cachedScale || ox != _cachedOffsetX || oy != _cachedOffsetY)
        {
            _cachedScale = scale;
            _cachedOffsetX = ox;
            _cachedOffsetY = oy;
            _quad = BuildQuad(_orbLocal.x + ox, _orbLocal.y + oy, scale);
            _overlayFilter.sharedMesh = _quad;
            _tinted = false; // fresh mesh is white; re-apply the lifeblood tint below if needed
        }

        _overlayRenderer.sharedMaterial = _quadMaterial;
        if (_texture != null)
        {
            _propertyBlock ??= new MaterialPropertyBlock();
            _propertyBlock.SetTexture("_MainTex", _texture);
            _overlayRenderer.SetPropertyBlock(_propertyBlock);
        }

        RefreshLifebloodTint();

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

        if (tinted == _tinted)
        {
            return;
        }

        _tinted = tinted;
        if (tinted)
        {
            _quadMaterial.EnableKeyword("RECOLOUR");
            SetQuadColors(_lifebloodTint);
        }
        else
        {
            _quadMaterial.DisableKeyword("RECOLOUR");
            SetQuadColors(Color.white);
        }
    }

    private static void SetQuadColors(Color color)
    {
        if (_quad == null)
        {
            return;
        }

        // The generated frame quad always has exactly four vertices.
        _quad.colors = new[] { color, color, color, color };
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

        Vector3 ToLocal(float px, float py) => new(
            spoolX + (px - originX) / ppu * scale,
            spoolY - (py - originY) / ppu * scale,
            0f);

        var mesh = new Mesh { name = "RandomCrestHudQuad" };
        mesh.vertices = new[]
        {
            ToLocal(0f, texH),      // bottom-left
            ToLocal(texW, texH),    // bottom-right
            ToLocal(texW, 0f),      // top-right
            ToLocal(0f, 0f),        // top-left
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
        };
        mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        // Both windings so the quad shows regardless of the shader's cull mode.
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateBounds();
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
