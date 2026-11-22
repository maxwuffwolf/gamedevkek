using Game.Gameplay.Actors;
using Game.Gameplay.World;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Подпись над NPC и лутом под курсором: что это и что будет по клику. Только текст с тенью, без рамки.</summary>
    public sealed class HoverInfoView : MonoBehaviour
    {
        private RectTransform _rect;
        private Text _text;
        private RectTransform _canvas;

        public static HoverInfoView Create(RectTransform canvas)
        {
            var rt = UiKit.Rect("HoverInfo", canvas);
            var view = rt.gameObject.AddComponent<HoverInfoView>();
            view._rect = rt;
            view._canvas = canvas;
            view._rect.anchorMin = view._rect.anchorMax = new Vector2(0.5f, 0.5f);
            view._rect.pivot = new Vector2(0.5f, 0f);
            view._text = UiKit.Text(rt, "Text", "", 17, UiTheme.Text, TextAnchor.LowerCenter, FontStyle.Bold).Shadowed();
            view._text.rectTransform.Stretch();
            // вторая тень с другой стороны — чтобы текст читался на ярком фоне без подложки
            var o = view._text.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.55f);
            o.effectDistance = new Vector2(1f, -1f);
            return view;
        }

        private void LateUpdate()
        {
            var pc = GameWorld.PlayerController;
            var target = pc != null ? pc.Hovered : null;
            var cam = Camera.main;
            if (!target.IsAlive() || cam == null)
            {
                _text.enabled = false;
                return;
            }
            _text.enabled = true;
            _text.text = $"{target.HoverText}\n<size=14><color=#a8e8b0>ЛКМ — {target.ActionText}</color></size>";
            var settings = _text.GetGenerationSettings(new Vector2(400f, 0f));
            float h = _text.cachedTextGeneratorForLayout.GetPreferredHeight(_text.text, settings) / _text.pixelsPerUnit;
            float w = Mathf.Min(400f, _text.cachedTextGeneratorForLayout.GetPreferredWidth(_text.text, settings) / _text.pixelsPerUnit);
            _rect.sizeDelta = new Vector2(w + 8f, h + 4f);

            Vector3 screen = cam.WorldToScreenPoint(target.InteractPosition + Vector3.up * 2.6f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, screen, null, out var local);
            _rect.anchoredPosition = local;
        }
    }
}
