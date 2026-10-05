using HarmonyLib;
using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// Attaches the hero highlight component on each scene init.
/// </summary>
[HarmonyPatch]
internal static class HeroHighlightPatch
{
    [HarmonyPatch(typeof(HeroController), "SceneInit")]
    [HarmonyPostfix]
    private static void HeroController_SceneInit_Postfix(HeroController __instance)
    {
        HeroHighlight.Ensure(__instance.gameObject);
    }
}

/// <summary>
/// Adds a highlight to Hornet's own sprite during the Cross Stitch (十字绣) "body recoil" step
/// (<c>Parry Clash</c>) so the transition into the counter's glow is less abrupt. Active only while
/// the Chaos crest is equipped.
///
/// <para>The hero's sprite material exposes the same <c>_FlashAmount</c> / <c>_FlashColor</c>
/// shader properties the game's <see cref="SpriteFlash"/> uses. We set a base amount every
/// <c>LateUpdate</c> (after the game's flash update) while the FSM is in that one step, and clear
/// our contribution again when the step ends. The game only refreshes those properties when it has
/// its own flash, so without the explicit clear our value would otherwise stick.</para>
///
/// <para>We only raise the amount and only clear it when it is no higher than our own value, so the
/// game's stronger hit / invulnerability flashes are never touched.</para>
/// </summary>
internal sealed class HeroHighlight : MonoBehaviour
{
    private const string FsmName = "Silk Specials";
    private const string RetreatState = "Parry Clash";

    private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
    private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");

    private Renderer? _renderer;
    private MaterialPropertyBlock? _block;
    private bool _applied;
    private string? _colorHex;
    private Color _color = Color.white;

    /// <summary>Attaches (once) the highlight component to the hero.</summary>
    internal static void Ensure(GameObject? hero)
    {
        if (hero == null || hero.GetComponent<HeroHighlight>() != null)
        {
            return;
        }

        hero.AddComponent<HeroHighlight>();
    }

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _block = new MaterialPropertyBlock();
    }

    private void LateUpdate()
    {
        var amount = RandomCrestModPlugin.HeroHighlightAmount;
        if (_renderer == null || _block == null)
        {
            return;
        }

        if (amount > 0f && CrestService.IsRandomCrestEquipped() && IsInRetreatStep())
        {
            _renderer.GetPropertyBlock(_block);
            if (_block.GetFloat(FlashAmountId) < amount)
            {
                _block.SetFloat(FlashAmountId, amount);
                _block.SetColor(FlashColorId, GetColour());
                _renderer.SetPropertyBlock(_block);
            }

            _applied = true;
            return;
        }

        if (!_applied)
        {
            return;
        }

        _applied = false;
        _renderer.GetPropertyBlock(_block);
        if (_block.GetFloat(FlashAmountId) <= amount + 0.001f)
        {
            // Nothing stronger is flashing; remove only our contribution.
            _block.SetFloat(FlashAmountId, 0f);
            _renderer.SetPropertyBlock(_block);
        }
    }

    private Color GetColour()
    {
        var hex = RandomCrestModPlugin.HeroHighlightColor;
        if (hex == _colorHex)
        {
            return _color;
        }

        _colorHex = hex;
        var text = string.IsNullOrEmpty(hex) ? "#FFE0F0" : (hex[0] == '#' ? hex : "#" + hex);
        _color = ColorUtility.TryParseHtmlString(text, out var parsed) ? parsed : new Color(1f, 0.88f, 0.94f, 1f);
        return _color;
    }

    private static bool IsInRetreatStep()
    {
        var hero = HeroController.instance;
        var fsm = hero != null ? hero.silkSpecialFSM : null;
        return fsm != null && fsm.Fsm != null && fsm.Fsm.Name == FsmName
            && fsm.ActiveStateName == RetreatState;
    }
}
