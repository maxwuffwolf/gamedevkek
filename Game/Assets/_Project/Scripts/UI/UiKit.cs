using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Токены стиля «Тёплое стекло» (концепт A): тёмные полупрозрачные панели со скруглениями,
    /// тёплая золотая обводка, светлые «чипы» клавиш, мягкий зелёный HP и голубая мана.
    /// Все цвета и радиусы интерфейса берутся отсюда — меняешь здесь, меняется везде.
    /// </summary>
    public static class UiTheme
    {
        // поверхности
        public static readonly Color Panel = new(0.086f, 0.102f, 0.165f, 0.92f);      // основное окно
        public static readonly Color PanelSoft = new(0.086f, 0.102f, 0.165f, 0.72f);  // мелкие плашки HUD
        public static readonly Color Line = new(1f, 0.925f, 0.785f, 0.22f);           // тонкая обводка
        public static readonly Color LineStrong = new(1f, 0.925f, 0.785f, 0.45f);
        public static readonly Color Cell = new(0.15f, 0.17f, 0.24f, 0.6f);               // пустая ячейка
        public static readonly Color Inset = new(0.13f, 0.15f, 0.22f, 0.45f);             // вложенная плашка внутри окна
        public static readonly Color ButtonSoft = new(0.2f, 0.22f, 0.31f, 0.85f);         // вторичная кнопка
        public static readonly Color CellLocked = new(0f, 0f, 0f, 0.35f);
        public static readonly Color Track = new(0.02f, 0.025f, 0.05f, 0.72f);         // фон полос (тёмный, чтобы полосы не сливались со сценой)
        public static readonly Color Shade = new(0.024f, 0.031f, 0.055f, 0.55f);       // затемнение сцены под окном

        // текст
        public static readonly Color Text = new(0.965f, 0.945f, 0.906f, 1f);
        public static readonly Color Dim = new(0.965f, 0.945f, 0.906f, 0.6f);
        public static readonly Color Ink = new(0.114f, 0.129f, 0.188f, 1f);            // тёмный текст на светлом
        public static readonly Color Paper = new(0.98f, 0.965f, 0.933f, 0.97f);        // карточка подсказки
        public static readonly Color InkDim = new(0.15f, 0.165f, 0.22f, 0.65f);

        // акценты
        public static readonly Color Gold = new(0.945f, 0.835f, 0.608f, 1f);            // Свет
        public static readonly Color Violet = new(0.725f, 0.643f, 1f, 1f);             // Тьма
        public static readonly Color Hp = new(0.545f, 0.863f, 0.565f, 1f);
        public static readonly Color HpLow = new(0.95f, 0.42f, 0.38f, 1f);
        public static readonly Color Mp = new(0.51f, 0.788f, 1f, 1f);
        public static readonly Color Xp = new(0.69f, 0.56f, 1f, 1f);                     // опыт — фиолетовый
        public static readonly Color Good = new(0.56f, 0.89f, 0.6f, 1f);
        public static readonly Color Bad = new(1f, 0.47f, 0.42f, 1f);
        public static readonly Color Enemy = new(0.88f, 0.35f, 0.3f, 1f);

        // ВАЖНО: проект в Linear-цвете, и полупрозрачный белый в UI смешивается «ярче», чем выглядит на макете
        // (белый с альфой 0.03 становится серым ~12%). Поэтому мелкие плашки — не белый с альфой, а светлый тёмно-синий.

        // формы (пиксели при 1920×1080)
        public const int RadiusWindow = 22;
        public const int RadiusPanel = 16;
        public const int RadiusCell = 12;
        public const int RadiusPill = 64; // «таблетка» / круг — Unity сам ужмёт до половины высоты
    }

    /// <summary>Фабрика элементов uGUI из кода (без префабов, чтобы каркас открывался без ручной настройки).</summary>
    public static class UiKit
    {
        // старые имена оставлены, чтобы не ломать остальной код
        public static Color PanelColor => UiTheme.Panel;
        public static Color FrameColor => UiTheme.Line;
        public static Color SlotColor => UiTheme.Cell;
        public static Color TextColor => UiTheme.Text;
        public static Color DimText => UiTheme.Dim;
        public static Color Gold => UiTheme.Gold;

        private static Font _font, _fontBold, _fontLegacy;
        private static Sprite _white;
        private static readonly Dictionary<int, Sprite> RoundCache = new();
        private static readonly Dictionary<(int, int), Sprite> RingCache = new();

        private static Font Legacy => _fontLegacy != null ? _fontLegacy : (_fontLegacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        /// <summary>Основной шрифт: Resources/Fonts/Nunito-SemiBold (если файла нет — встроенный).</summary>
        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.Load<Font>("Fonts/Nunito-SemiBold");
                return _font != null ? _font : Legacy;
            }
        }

        /// <summary>Жирное начертание: Nunito-ExtraBold (настоящий файл, не синтетический bold).</summary>
        public static Font FontBold
        {
            get
            {
                if (_fontBold == null) _fontBold = Resources.Load<Font>("Fonts/Nunito-ExtraBold");
                return _fontBold;
            }
        }

        /// <summary>Белый спрайт (нужен для Image.type = Filled и т.п.).</summary>
        public static Sprite White
        {
            get
            {
                if (_white == null)
                {
                    var tex = Texture2D.whiteTexture;
                    _white = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
                return _white;
            }
        }

        // ------------------------------------------------------------ скругления (генерируются в коде, 9-slice)

        /// <summary>Залитый прямоугольник со скруглёнными углами радиуса radius (px). Круг = большой радиус на квадрате.</summary>
        public static Sprite Rounded(int radius)
        {
            radius = Mathf.Clamp(radius, 1, 128);
            if (RoundCache.TryGetValue(radius, out var s) && s != null) return s;
            s = MakeRounded(radius, 0);
            RoundCache[radius] = s;
            return s;
        }

        /// <summary>Только обводка (кольцо) со скруглёнными углами.</summary>
        public static Sprite RoundedRing(int radius, int thickness)
        {
            radius = Mathf.Clamp(radius, 1, 128);
            thickness = Mathf.Clamp(thickness, 1, radius);
            var key = (radius, thickness);
            if (RingCache.TryGetValue(key, out var s) && s != null) return s;
            s = MakeRounded(radius, thickness);
            RingCache[key] = s;
            return s;
        }

        private static Sprite MakeRounded(int r, int ring)
        {
            int size = r * 2 + 2;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
                name = ring > 0 ? $"UiRing_{r}_{ring}" : $"UiRound_{r}",
            };
            var px = new Color32[size * size];
            float c = size * 0.5f;
            float inner = c - 1f - r; // полуразмер «прямой» части (здесь 0 — центр текстуры целиком скругление)
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // signed distance до скруглённого прямоугольника с полуразмером c-1
                float qx = Mathf.Abs(x + 0.5f - c) - inner;
                float qy = Mathf.Abs(y + 0.5f - c) - inner;
                float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
                float a = Mathf.Clamp01(0.5f - outside);
                if (ring > 0) a *= Mathf.Clamp01(0.5f + outside + ring);
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            int b = r + 1;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        private static Sprite _fadeUp;

        /// <summary>Вертикальный градиент: снизу непрозрачно, сверху прозрачно (подложка диалога).</summary>
        public static Sprite FadeUp
        {
            get
            {
                if (_fadeUp != null) return _fadeUp;
                const int h = 64;
                var tex = new Texture2D(1, h, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave, name = "UiFadeUp",
                };
                for (int y = 0; y < h; y++)
                {
                    float t = 1f - y / (h - 1f);
                    tex.SetPixel(0, y, new Color(1f, 1f, 1f, t * t * (3f - 2f * t)));
                }
                tex.Apply(false, true);
                _fadeUp = Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f));
                return _fadeUp;
            }
        }

        /// <summary>Сделать Image скруглённым (9-slice).</summary>
        public static Image Round(this Image img, int radius)
        {
            img.sprite = Rounded(radius);
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            if (radius >= UiTheme.RadiusPill)
            {
                var pill = img.GetComponent<PillShape>();
                if (pill == null) pill = img.gameObject.AddComponent<PillShape>();
                pill.RingThickness = 0;
            }
            return img;
        }

        // ------------------------------------------------------------ базовые элементы

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image Image(Transform parent, string name, Color color, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = White;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>Скруглённая плашка, по желанию с тонкой обводкой.</summary>
        public static Image Panel(Transform parent, string name, Color color, int radius, Color? outline = null, bool raycast = true)
        {
            var img = Image(parent, name, color, raycast).Round(radius);
            if (outline.HasValue) Outline(img.rectTransform, outline.Value, radius);
            return img;
        }

        /// <summary>Обводка 1–2 px по форме скруглённой плашки (дочерний Image поверх фона).</summary>
        public static Image Outline(RectTransform target, Color color, int radius, int thickness = 1)
        {
            var o = Image(target, "Outline", color);
            o.sprite = RoundedRing(radius, thickness);
            o.type = UnityEngine.UI.Image.Type.Sliced;
            o.rectTransform.Stretch();
            if (radius >= UiTheme.RadiusPill) o.gameObject.AddComponent<PillShape>().RingThickness = thickness;
            return o;
        }

        public static Text Text(Transform parent, string name, string text, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            // Bold → отдельный файл ExtraBold, без синтетического утолщения
            bool bold = style == FontStyle.Bold || style == FontStyle.BoldAndItalic;
            if (bold && FontBold != null)
            {
                t.font = FontBold;
                t.fontStyle = style == FontStyle.BoldAndItalic ? FontStyle.Italic : FontStyle.Normal;
            }
            else
            {
                t.font = Font;
                t.fontStyle = style;
            }
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>Мягкая тень под текстом (для надписей прямо поверх сцены).</summary>
        public static Text Shadowed(this Text t)
        {
            var o = t.gameObject.AddComponent<Shadow>();
            o.effectColor = new Color(0f, 0f, 0f, 0.7f);
            o.effectDistance = new Vector2(0f, -1.5f);
            return t;
        }

        /// <summary>Кнопка-«таблетка». primary = светлая (главное действие), иначе полупрозрачная.</summary>
        public static Button Button(Transform parent, string name, string label, Action onClick, int fontSize = 18, bool primary = false)
        {
            var img = Image(parent, name, primary ? UiTheme.Paper : UiTheme.ButtonSoft, true).Round(UiTheme.RadiusPill);
            var btn = img.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = primary ? new Color(1f, 0.93f, 0.8f, 1f) : new Color(1.35f, 1.35f, 1.35f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            colors.colorMultiplier = 1f;
            btn.colors = colors;
            btn.targetGraphic = img;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            var t = Text(img.transform, "Label", label, fontSize, primary ? UiTheme.Ink : UiTheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Stretch(12, 2, 12, 2);
            return btn;
        }

        /// <summary>Круглая кнопка закрытия «×».</summary>
        public static Button CloseButton(Transform parent, Action onClick)
        {
            var b = Button(parent, "Close", "×", onClick, 22);
            b.GetComponent<RectTransform>().Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -14f), new Vector2(34f, 34f));
            b.GetComponentInChildren<Text>().rectTransform.Stretch(0, 0, 0, 3);
            return b;
        }

        /// <summary>Светлый кружок/таблетка с подписью клавиши (Q, E, ЛКМ...).</summary>
        public static Image KeyChip(Transform parent, string label, int fontSize = 12)
        {
            var chip = Image(parent, "Key", UiTheme.Paper).Round(UiTheme.RadiusPill);
            var t = Text(chip.transform, "Label", label, fontSize, UiTheme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Stretch(4, 0, 4, 1);
            float w = Mathf.Max(fontSize + 8f, t.preferredWidth + 12f);
            chip.rectTransform.sizeDelta = new Vector2(w, fontSize + 8f);
            return chip;
        }

        /// <summary>Полоса: трек + заливка (скругления), заливка растёт через anchorMax.x.</summary>
        public static RectTransform Bar(Transform parent, string name, Color fill, out RectTransform fillRect)
        {
            // тёмный трек (кант 2 px вокруг заливки) + тонкая светлая обводка + блик: полоса читается на любом фоне
            var track = Image(parent, name, UiTheme.Track).Round(UiTheme.RadiusPill).rectTransform;
            var f = Image(track, "Fill", fill).Round(UiTheme.RadiusPill).rectTransform;
            f.anchorMin = Vector2.zero;
            f.anchorMax = Vector2.one;
            f.offsetMin = new Vector2(2f, 2f);   // заливка внутри тёмного канта трека
            f.offsetMax = new Vector2(-2f, -2f);
            var gloss = Image(f, "Gloss", new Color(1f, 1f, 1f, 0.22f)).Round(UiTheme.RadiusPill).rectTransform; // блик сверху
            gloss.anchorMin = new Vector2(0f, 0.55f);
            gloss.anchorMax = Vector2.one;
            gloss.offsetMin = new Vector2(3f, 0f);
            gloss.offsetMax = new Vector2(-3f, -1f);
            Outline(track, new Color(1f, 0.925f, 0.785f, 0.3f), UiTheme.RadiusPill, 1);
            fillRect = f;
            return track;
        }

        /// <summary>Старая прямоугольная рамка (оставлена для совместимости; новый UI использует Outline).</summary>
        public static void Frame(RectTransform target, Color color, float thickness = 2f)
        {
            void Edge(string n, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
            {
                var e = Image(target, n, color).rectTransform;
                e.anchorMin = aMin;
                e.anchorMax = aMax;
                e.offsetMin = offMin;
                e.offsetMax = offMax;
            }
            Edge("FrameT", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -thickness), Vector2.zero);
            Edge("FrameB", new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, thickness));
            Edge("FrameL", new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(thickness, 0));
            Edge("FrameR", new Vector2(1, 0), new Vector2(1, 1), new Vector2(-thickness, 0), Vector2.zero);
        }

        /// <summary>Якорь в точке anchor (0..1), pivot, позиция и размер в пикселях референсного разрешения.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Растянуть на родителя с отступами.</summary>
        public static RectTransform Stretch(this RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            RoundCache.Clear();
            RingCache.Clear();
        }
    }
}
