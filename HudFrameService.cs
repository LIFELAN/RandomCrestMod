using System.Text;
using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// Gives the mod crest its own bind-orb HUD frame by replacing the mesh/material of the live
/// scene <c>Bind Orb</c> renderer (the object that already draws the crest frame). Reusing that
/// renderer guarantees the HUD camera draws it; we point it at a quad textured with the mod's
/// composited art while the mod crest is equipped, then restore the originals.
/// </summary>
internal static class HudFrameService
{
    private static Sprite? _sprite;
    private static Texture2D? _texture;

    private static BindOrbHudFrame? _hud;
    private static MeshFilter? _baseFilter;
    private static MeshRenderer? _baseRenderer;
    private static Mesh? _originalMesh;
    private static Material? _originalMaterial;
    private static Vector3 _orbLocal = Vector3.zero;

    private static Mesh? _quad;
    private static Material? _quadMaterial;
    private static MaterialPropertyBlock? _propertyBlock;
    private static bool _replaced;
    private static float _cachedScale = float.NaN;
    private static float _cachedOffsetX = float.NaN;
    private static float _cachedOffsetY = float.NaN;

    private static readonly bool Debug = false;
    private static float _nextDebug;

    internal static void Initialize(Sprite? sprite)
    {
        _sprite = sprite;
        _texture = sprite != null ? sprite.texture : null;

        // Re-apply right before the camera renders: the game's tk2d update runs after our
        // LateUpdate and would otherwise restore its own sprite mesh over our quad.
        Application.onBeforeRender -= OnBeforeRender;
        Application.onBeforeRender += OnBeforeRender;
        Camera.onPreRender -= OnCameraPreRender;
        Camera.onPreRender += OnCameraPreRender;
    }

    private static void OnBeforeRender()
    {
        PushToRenderer();
    }

    private static void OnCameraPreRender(Camera cam)
    {
        PushToRenderer();
    }

    private static void PushToRenderer()
    {
        if (!_replaced || _baseFilter == null || _baseRenderer == null || _quad == null)
        {
            return;
        }

        _baseFilter.sharedMesh = _quad;
        _baseRenderer.sharedMaterial = _quadMaterial;
        _baseRenderer.enabled = true;

        if (_texture != null)
        {
            _propertyBlock ??= new MaterialPropertyBlock();
            _propertyBlock.SetTexture("_MainTex", _texture);
            _baseRenderer.SetPropertyBlock(_propertyBlock);
        }
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
                $"replaced={_replaced} baseEnabled={(_baseRenderer != null && _baseRenderer.enabled)} " +
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
        _baseFilter = null;
        _baseRenderer = null;
        _originalMesh = null;
        _originalMaterial = null;

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

        _baseFilter = _hud.GetComponent<MeshFilter>();
        _baseRenderer = _hud.GetComponent<MeshRenderer>();

        if (_baseRenderer == null)
        {
            RandomCrestModPlugin.LogError("[HudFrame] Bind Orb has no MeshRenderer; cannot replace frame.");
            return false;
        }

        _originalMesh = _baseFilter != null ? _baseFilter.sharedMesh : null;
        _originalMaterial = _baseRenderer.sharedMaterial;

        // The game's own silk Orb child sits at the spool centre; anchor our art there so the
        // custom spool lines up with the real one regardless of the HUD layout.
        var orb = _hud.transform.Find("Orb");
        _orbLocal = orb != null ? orb.localPosition : Vector3.zero;

        var baseMat = _originalMaterial;
        _quadMaterial = baseMat != null
            ? new Material(baseMat) { mainTexture = _texture }
            : new Material(Shader.Find("Sprites/Default")) { mainTexture = _texture };

        RandomCrestModPlugin.Log(
            $"[HudFrame] using '{PathOf(_hud.transform)}' frame art.");
        return true;
    }

    private static void Apply()
    {
        if (_baseFilter == null || _baseRenderer == null || _sprite == null)
        {
            return;
        }

        var scale = RandomCrestModPlugin.HudFrameScale;
        var ox = RandomCrestModPlugin.HudFrameOffsetX;
        var oy = RandomCrestModPlugin.HudFrameOffsetY;

        if (_quad == null || scale != _cachedScale || ox != _cachedOffsetX || oy != _cachedOffsetY)
        {
            _cachedScale = scale;
            _cachedOffsetX = ox;
            _cachedOffsetY = oy;
            _quad = BuildQuad(_orbLocal.x + ox, _orbLocal.y + oy, scale);
        }

        _replaced = true;
        PushToRenderer();
    }

    private static void Restore()
    {
        if (!_replaced || _baseFilter == null || _baseRenderer == null)
        {
            return;
        }

        _baseFilter.sharedMesh = _originalMesh;
        _baseRenderer.sharedMaterial = _originalMaterial;
        _baseRenderer.SetPropertyBlock(null);
        _baseRenderer.enabled = true;
        _replaced = false;
    }

    // Spool (disk) centre inside the source art, as a fraction of the image (x from left, y from top).
    private const float SpoolFracX = 0.15143f;
    private const float SpoolFracY = 0.53109f;

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
