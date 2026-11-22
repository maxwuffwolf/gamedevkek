using Game.Core;
using Game.Data;
using Game.Gameplay.Zones;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Экран загрузки между зонами: название зоны, подзаголовок, совет, полоса прогресса и крутящийся кристалл.
    /// Сам ничего не грузит — только рисует состояние <see cref="ZoneService"/> (прозрачность, прогресс, куда идём).
    /// Позже сюда легко добавить арт-заставку: Image на весь экран под текстами.
    /// </summary>
    public sealed class LoadingScreenView : MonoBehaviour
    {
        private CanvasGroup _group;
        private Image _bg;
        private Text _title;
        private Text _subtitle;
        private Text _tip;
        private RectTransform _fill;
        private RectTransform _crystalGold;
        private RectTransform _crystalViolet;
        private Image _crystalGoldImg;
        private Image _crystalVioletImg;
        private ZoneDef _shown;

        private static readonly Color Violet = new(0.62f, 0.42f, 1f, 1f);

        public static LoadingScreenView Create(RectTransform canvas)
        {
            var root = UiKit.Rect("LoadingScreen", canvas).Stretch();
            var view = root.gameObject.AddComponent<LoadingScreenView>();
            view._group = root.gameObject.AddComponent<CanvasGroup>();
            view._group.alpha = 0f;
            view._group.blocksRaycasts = false;
            view._group.interactable = false;

            view._bg = UiKit.Image(root, "Background", new Color(0.04f, 0.05f, 0.09f, 1f), raycast: true);
            view._bg.rectTransform.Stretch();

            // мягкое затемнение к краям: четыре полупрозрачные полосы (дёшево и без текстур)
            for (int i = 0; i < 4; i++)
            {
                var edge = UiKit.Image(root, "Shade", new Color(0f, 0f, 0f, 0.35f)).rectTransform;
                bool horizontal = i < 2;
                edge.anchorMin = new Vector2(horizontal ? 0f : (i == 2 ? 0f : 0.85f), horizontal ? (i == 0 ? 0f : 0.8f) : 0f);
                edge.anchorMax = new Vector2(horizontal ? 1f : (i == 2 ? 0.15f : 1f), horizontal ? (i == 0 ? 0.2f : 1f) : 1f);
                edge.offsetMin = edge.offsetMax = Vector2.zero;
            }

            view._title = UiKit.Text(root, "Title", "", 56, UiKit.Gold, style: FontStyle.Bold).Shadowed();
            view._title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(1400f, 80f));

            var line = UiKit.Image(root, "Line", new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.6f)).rectTransform;
            line.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 22f), new Vector2(420f, 2f));

            view._subtitle = UiKit.Text(root, "Subtitle", "", 24, UiKit.TextColor, style: FontStyle.Italic).Shadowed();
            view._subtitle.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 6f), new Vector2(1200f, 70f));

            view._tip = UiKit.Text(root, "Tip", "", 19, UiKit.DimText).Shadowed();
            view._tip.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1100f, 60f));

            var bar = UiKit.Image(root, "ProgressBack", new Color(0f, 0f, 0f, 0.6f)).rectTransform;
            bar.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(720f, 8f));
            UiKit.Frame(bar, new Color(UiKit.FrameColor.r, UiKit.FrameColor.g, UiKit.FrameColor.b, 0.8f), 1f);
            view._fill = UiKit.Image(bar, "Fill", UiKit.Gold).rectTransform;
            view._fill.anchorMin = Vector2.zero;
            view._fill.anchorMax = new Vector2(0f, 1f);
            view._fill.offsetMin = view._fill.offsetMax = Vector2.zero;

            // кристалл наруча: золото (Свет) и фиолет (Тьма) по очереди разгораются
            view._crystalViolet = Crystal(root, Violet, new Vector2(-90f, 100f), out view._crystalVioletImg);
            view._crystalGold = Crystal(root, UiKit.Gold, new Vector2(-90f, 100f), out view._crystalGoldImg);
            return view;
        }

        private static RectTransform Crystal(RectTransform parent, Color color, Vector2 pos, out Image img)
        {
            img = UiKit.Image(parent, "Crystal", color);
            var rt = img.rectTransform;
            rt.Place(new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), pos, new Vector2(26f, 26f));
            return rt;
        }

        private void LateUpdate()
        {
            if (!Services.TryGet<ZoneService>(out var zones)) return;
            float a = zones.ScreenAlpha;
            _group.alpha = a;
            _group.blocksRaycasts = a > 0.01f;
            if (a <= 0.001f)
            {
                _shown = null;
                return;
            }

            var dest = zones.Destination;
            if (dest != null && dest != _shown)
            {
                _shown = dest;
                _bg.color = ColorUtil.Parse(dest.LoadingColor, new Color(0.04f, 0.05f, 0.09f));
                _title.text = dest.Name ?? dest.Id;
                string sub = dest.Subtitle ?? "";
                if (dest.Level > 1) sub += (sub.Length > 0 ? "\n" : "") + $"<color=#A8A8B8>Уровень зоны: {dest.Level}</color>";
                _subtitle.text = sub;
                _tip.text = string.IsNullOrEmpty(zones.Tip) ? "" : $"Совет: {zones.Tip}";
            }

            _fill.anchorMax = new Vector2(Mathf.Clamp01(zones.Progress), 1f);

            float t = Time.unscaledTime;
            _crystalGold.localRotation = _crystalViolet.localRotation = Quaternion.Euler(0f, 0f, 45f + t * 90f);
            float k = 0.5f + 0.5f * Mathf.Sin(t * 3f);
            _crystalGoldImg.color = new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, k);
            _crystalVioletImg.color = new Color(Violet.r, Violet.g, Violet.b, 1f - k * 0.7f);
            float s = 1f + 0.12f * Mathf.Sin(t * 6f);
            _crystalGold.localScale = Vector3.one * s;
        }
    }
}
