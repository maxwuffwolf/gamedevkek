using System.Linq;
using System.Text;
using Game.Core;
using Game.Data;
using Game.Gameplay.Items;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Одна всплывающая подсказка на весь UI (предметы, навыки, слоты). Следует за курсором.</summary>
    public sealed class TooltipView : MonoBehaviour
    {
        public static TooltipView Instance { get; private set; }

        private RectTransform _rect;
        private RectTransform _canvasRect;
        private Text _text;
        private Image _border;
        private object _owner;

        public static TooltipView Create(RectTransform canvas)
        {
            var bg = UiKit.Image(canvas, "Tooltip", new Color(0.05f, 0.045f, 0.04f, 0.96f));
            var view = bg.gameObject.AddComponent<TooltipView>();
            view._rect = bg.rectTransform;
            view._canvasRect = canvas;
            view._rect.pivot = new Vector2(0f, 1f);
            view._rect.anchorMin = view._rect.anchorMax = Vector2.zero;
            view._border = UiKit.Image(bg.transform, "Border", UiKit.FrameColor);
            view._border.rectTransform.Stretch(-2, -2, -2, -2);
            view._border.transform.SetAsFirstSibling();
            var inner = UiKit.Image(bg.transform, "Inner", new Color(0.05f, 0.045f, 0.04f, 0.98f));
            inner.rectTransform.Stretch();
            view._text = UiKit.Text(bg.transform, "Text", "", 17, UiKit.TextColor, TextAnchor.UpperLeft);
            view._text.rectTransform.Stretch(12, 10, 12, 10);
            var fitter = bg.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            bg.gameObject.SetActive(false);
            Instance = view;
            return view;
        }

        public void Show(object owner, string richText, Color borderColor, float width = 320f)
        {
            _owner = owner;
            _text.text = richText;
            _border.color = borderColor;
            float height = _text.preferredHeight;
            // preferredHeight зависит от ширины: считаем через генератор с нужной шириной
            var settings = _text.GetGenerationSettings(new Vector2(width - 24f, 0f));
            height = _text.cachedTextGeneratorForLayout.GetPreferredHeight(richText, settings) / _text.pixelsPerUnit;
            _rect.sizeDelta = new Vector2(width, height + 22f);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Follow();
        }

        public void Hide(object owner)
        {
            if (owner != null && owner != _owner) return;
            _owner = null;
            gameObject.SetActive(false);
        }

        private void LateUpdate() => Follow();

        private void Follow()
        {
            if (_canvasRect == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, GameInput.MousePosition, null, out var local);
            var size = _canvasRect.rect.size;
            Vector2 pos = local + size * 0.5f + new Vector2(18f, -18f);
            // не выходить за экран
            if (pos.x + _rect.sizeDelta.x > size.x) pos.x = pos.x - _rect.sizeDelta.x - 36f;
            if (pos.y - _rect.sizeDelta.y < 0f) pos.y = _rect.sizeDelta.y + 4f;
            _rect.anchoredPosition = pos;
        }

        // ---------------------------------------------------------------- тексты

        public static string ItemText(ContentDatabase db, ItemInstance item, string hint = null)
        {
            var def = db.Item(item.BaseId);
            var rarity = db.Rarity(item.Rarity);
            var color = db.RarityColor(item.Rarity);
            var sb = new StringBuilder();
            sb.AppendLine($"<size=21><b>{ColorUtil.Colorize(def?.Name ?? item.BaseId, color)}</b></size>");
            sb.AppendLine(ColorUtil.Colorize($"{rarity?.Name ?? item.Rarity} · {SlotName(def?.Slot)}", UiKit.DimText));
            if (def != null && def.LevelReq > 1)
            {
                int lvl = Core.Services.TryGet<Game.Gameplay.Progression.PlayerProfile>(out var p) ? p.Level : 1;
                var c = lvl >= def.LevelReq ? UiKit.DimText : new Color(1f, 0.4f, 0.4f);
                sb.AppendLine(ColorUtil.Colorize($"Требуется уровень {def.LevelReq}", c));
            }
            if (item.Implicits.Count > 0)
            {
                sb.AppendLine();
                foreach (var m in item.Implicits) sb.AppendLine(StatNames.Format(m.Stat, m.Type, m.Value));
            }
            if (item.Affixes.Count > 0)
            {
                sb.AppendLine();
                foreach (var a in item.Affixes)
                foreach (var m in a.Mods)
                    sb.AppendLine(ColorUtil.Colorize(StatNames.Format(m.Stat, m.Type, m.Value), new Color(0.55f, 0.7f, 1f)));
            }
            if (!string.IsNullOrEmpty(def?.Description))
            {
                sb.AppendLine();
                sb.AppendLine($"<i>{ColorUtil.Colorize(def.Description, new Color(0.75f, 0.65f, 0.45f))}</i>");
            }
            sb.AppendLine(ColorUtil.Colorize($"Уровень предмета: {item.ItemLevel}", UiKit.DimText));
            if (!string.IsNullOrEmpty(hint)) sb.Append(ColorUtil.Colorize(hint, new Color(0.5f, 0.8f, 0.5f)));
            return sb.ToString().TrimEnd();
        }

        public static string SlotName(ItemSlot? slot) => slot switch
        {
            ItemSlot.Head => "Голова",
            ItemSlot.Neck => "Шея",
            ItemSlot.Shoulders => "Плечи",
            ItemSlot.Chest => "Торс",
            ItemSlot.Hands => "Руки",
            ItemSlot.Waist => "Пояс",
            ItemSlot.Legs => "Ноги",
            ItemSlot.Feet => "Ступни",
            ItemSlot.MainHand => "Оружие",
            ItemSlot.OffHand => "Левая рука",
            ItemSlot.Ring => "Кольцо",
            _ => "",
        };

        public static string SkillText(SkillDef s)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<size=21><b>{s.Name}</b></size>");
            var parts = new System.Collections.Generic.List<string>();
            if (s.ManaCost > 0) parts.Add($"Мана: {s.ManaCost:0}");
            if (s.Cooldown > 0) parts.Add($"Перезарядка: {s.Cooldown:0.#} с");
            parts.Add($"Урон: {s.DamageMultiplier * 100f:0}%");
            sb.AppendLine(ColorUtil.Colorize(string.Join(" · ", parts), UiKit.DimText));
            if (!string.IsNullOrEmpty(s.Description)) sb.Append(s.Description);
            return sb.ToString().TrimEnd();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;
    }
}
