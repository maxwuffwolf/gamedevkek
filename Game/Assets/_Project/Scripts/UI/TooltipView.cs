using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using Game.Core;
using Game.Data;
using Game.Gameplay.Items;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Одна всплывающая подсказка на весь UI (предметы, навыки, слоты). Следует за курсором.
    /// Стиль «Тёплое стекло»: светлая карточка, первая строка — заголовок на плашке цвета редкости (или золота).
    /// </summary>
    public sealed class TooltipView : MonoBehaviour
    {
        public static TooltipView Instance { get; private set; }

        // цвета текста на светлой карточке (для rich text)
        public const string InkDim = "#6b6f7c";
        public const string InkBlue = "#3566c8";
        public const string InkLore = "#8a6a2e";
        public const string InkGood = "#2f8f45";
        public const string InkBad = "#c0392b";

        private const float PadX = 16f, PadY = 12f, HeaderGap = 8f;

        private RectTransform _rect;
        private RectTransform _canvasRect;
        private Image _header, _headerFill;
        private Text _title, _text;
        private object _owner;

        public static TooltipView Create(RectTransform canvas)
        {
            var bg = UiKit.Image(canvas, "Tooltip", UiTheme.Paper).Round(UiTheme.RadiusPanel);
            var view = bg.gameObject.AddComponent<TooltipView>();
            view._rect = bg.rectTransform;
            view._canvasRect = canvas;
            view._rect.pivot = new Vector2(0f, 1f);
            view._rect.anchorMin = view._rect.anchorMax = Vector2.zero;
            var shadow = bg.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            shadow.effectDistance = new Vector2(0f, -6f);

            // плашка заголовка: скругление сверху, снизу прячем под телом (рисуется первой)
            view._header = UiKit.Image(bg.transform, "Header", UiTheme.Gold).Round(UiTheme.RadiusPanel);
            var h = view._header.rectTransform;
            h.anchorMin = new Vector2(0f, 1f);
            h.anchorMax = new Vector2(1f, 1f);
            h.pivot = new Vector2(0.5f, 1f);
            h.anchoredPosition = Vector2.zero;
            view._headerFill = UiKit.Image(h, "SquareBottom", UiTheme.Gold); // нижние углы плашки прямые
            var hf = view._headerFill.rectTransform;
            hf.anchorMin = Vector2.zero;
            hf.anchorMax = new Vector2(1f, 0.5f);
            hf.offsetMin = hf.offsetMax = Vector2.zero;
            view._title = UiKit.Text(h, "Title", "", 19, UiTheme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            view._title.rectTransform.Stretch(PadX, 6, PadX, 6);

            view._text = UiKit.Text(bg.transform, "Text", "", 16, UiTheme.Ink, TextAnchor.UpperLeft);
            view._text.lineSpacing = 1.05f;
            var t = view._text.rectTransform;
            t.anchorMin = new Vector2(0f, 1f);
            t.anchorMax = new Vector2(1f, 1f);
            t.pivot = new Vector2(0.5f, 1f);
            bg.gameObject.SetActive(false);
            Instance = view;
            return view;
        }

        public void Show(object owner, string richText, Color accent, float width = 320f)
        {
            _owner = owner;
            // первая строка — заголовок (без тегов размера/цвета), остальное — тело
            string head = richText ?? "", body = "";
            int nl = head.IndexOf('\n');
            if (nl >= 0)
            {
                body = head.Substring(nl + 1);
                head = head.Substring(0, nl);
            }
            head = Regex.Replace(head, "<(/?)(size|color|b|i)(=[^>]*)?>", "");
            _title.text = head;
            _text.text = body;
            // полупрозрачная обводка (не предмет) → золотая плашка
            _header.color = accent.a < 0.5f ? UiTheme.Gold : new Color(accent.r, accent.g, accent.b, 1f);
            _headerFill.color = _header.color;

            float inner = width - PadX * 2f;
            float titleH = Measure(_title, head, inner) + 14f;
            float bodyH = body.Length > 0 ? Measure(_text, body, inner) : 0f;
            _header.rectTransform.sizeDelta = new Vector2(0f, titleH);
            var t = _text.rectTransform;
            t.offsetMin = new Vector2(PadX, 0f);
            t.offsetMax = new Vector2(-PadX, -(titleH + HeaderGap));
            t.sizeDelta = new Vector2(t.sizeDelta.x, bodyH);
            _text.gameObject.SetActive(body.Length > 0);
            _rect.sizeDelta = new Vector2(width, titleH + (body.Length > 0 ? HeaderGap + bodyH + PadY : 0f));
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Follow();
        }

        private static float Measure(Text text, string s, float width)
        {
            var settings = text.GetGenerationSettings(new Vector2(width, 0f));
            return text.cachedTextGeneratorForLayout.GetPreferredHeight(s, settings) / text.pixelsPerUnit;
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
            var sb = new StringBuilder();
            sb.AppendLine(def?.Name ?? item.BaseId);
            sb.AppendLine($"<color={InkDim}>{rarity?.Name ?? item.Rarity} · {SlotName(def?.Slot)}</color>");
            if (def != null && def.LevelReq > 1)
            {
                int lvl = Core.Services.TryGet<Game.Gameplay.Progression.PlayerProfile>(out var p) ? p.Level : 1;
                sb.AppendLine($"<color={(lvl >= def.LevelReq ? InkDim : InkBad)}>Требуется уровень {def.LevelReq}</color>");
            }
            if (item.Implicits.Count > 0)
            {
                sb.AppendLine();
                foreach (var m in item.Implicits) sb.AppendLine($"<b>{StatNames.Format(m.Stat, m.Type, m.Value)}</b>");
            }
            if (item.Affixes.Count > 0)
            {
                sb.AppendLine();
                foreach (var a in item.Affixes)
                foreach (var m in a.Mods)
                    sb.AppendLine($"<color={InkBlue}>{StatNames.Format(m.Stat, m.Type, m.Value)}</color>");
            }
            if (!string.IsNullOrEmpty(def?.Description))
            {
                sb.AppendLine();
                sb.AppendLine($"<i><color={InkLore}>{def.Description}</color></i>");
            }
            sb.AppendLine($"<color={InkDim}>Уровень предмета: {item.ItemLevel}</color>");
            if (!string.IsNullOrEmpty(hint)) sb.Append($"<size=13><color={InkGood}>{hint}</color></size>");
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
            sb.AppendLine(s.Name);
            var parts = new System.Collections.Generic.List<string>();
            if (s.ManaCost > 0) parts.Add($"Мана: {s.ManaCost:0}");
            if (s.Cooldown > 0) parts.Add($"Перезарядка: {s.Cooldown:0.#} с");
            parts.Add($"Урон: {s.DamageMultiplier * 100f:0}%");
            sb.AppendLine($"<color={InkDim}>{string.Join(" · ", parts)}</color>");
            if (!string.IsNullOrEmpty(s.Description)) sb.Append(s.Description);
            return sb.ToString().TrimEnd();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;
    }
}
