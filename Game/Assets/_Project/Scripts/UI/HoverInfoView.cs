using Game.Gameplay.Actors;
using Game.Gameplay.World;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Подпись под курсором для NPC и лута: что это и что будет по клику.</summary>
    public sealed class HoverInfoView : MonoBehaviour
    {
        private RectTransform _rect;
        private Text _text;
        private Image _bg;
        private RectTransform _canvas;

        public static HoverInfoView Create(RectTransform canvas)
        {
            var bg = UiKit.Panel(canvas, "HoverInfo", UiTheme.PanelSoft, UiTheme.RadiusPill, UiTheme.Line, false);
            var view = bg.gameObject.AddComponent<HoverInfoView>();
            view._rect = bg.rectTransform;
            view._bg = bg;
            view._canvas = canvas;
            view._rect.anchorMin = view._rect.anchorMax = new Vector2(0.5f, 0.5f);
            view._rect.pivot = new Vector2(0.5f, 0f);
            view._text = UiKit.Text(bg.transform, "Text", "", 16, UiTheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            view._text.rectTransform.Stretch(14, 4, 14, 4);
            return view;
        }

        private void LateUpdate()
        {
            var pc = GameWorld.PlayerController;
            var target = pc != null ? pc.Hovered : null;
            var cam = Camera.main;
            if (!target.IsAlive() || cam == null)
            {
                _bg.enabled = false;
                _text.enabled = false;
                return;
            }
            _bg.enabled = true;
            _text.enabled = true;
            _text.text = $"{target.HoverText}\n<size=13><color=#a0e0a0>ЛКМ — {target.ActionText}</color></size>";
            var settings = _text.GetGenerationSettings(new Vector2(400f, 0f));
            float h = _text.cachedTextGeneratorForLayout.GetPreferredHeight(_text.text, settings) / _text.pixelsPerUnit;
            float w = Mathf.Min(400f, _text.cachedTextGeneratorForLayout.GetPreferredWidth(_text.text, settings) / _text.pixelsPerUnit);
            _rect.sizeDelta = new Vector2(w + 32f, h + 14f);

            Vector3 screen = cam.WorldToScreenPoint(target.InteractPosition + Vector3.up * 2.6f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, screen, null, out var local);
            _rect.anchoredPosition = local;
        }
    }
}
