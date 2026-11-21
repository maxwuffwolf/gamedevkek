using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Фабрика элементов uGUI из кода (без префабов, чтобы каркас открывался без ручной настройки).</summary>
    public static class UiKit
    {
        public static readonly Color PanelColor = new(0.08f, 0.07f, 0.06f, 0.92f);
        public static readonly Color FrameColor = new(0.55f, 0.45f, 0.3f, 1f);
        public static readonly Color SlotColor = new(0.14f, 0.13f, 0.12f, 0.95f);
        public static readonly Color TextColor = new(0.92f, 0.88f, 0.8f, 1f);
        public static readonly Color DimText = new(0.6f, 0.57f, 0.5f, 1f);
        public static readonly Color Gold = new(1f, 0.82f, 0.3f, 1f);

        private static Font _font;
        private static Sprite _white;

        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
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

        public static Text Text(Transform parent, string name, string text, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Text Shadowed(this Text t)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.85f);
            o.effectDistance = new Vector2(1.2f, -1.2f);
            return t;
        }

        public static Button Button(Transform parent, string name, string label, Action onClick, int fontSize = 18)
        {
            var img = Image(parent, name, new Color(0.22f, 0.18f, 0.13f, 1f), true);
            var btn = img.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.3f, 1.2f, 1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;
            btn.targetGraphic = img;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            var t = Text(img.transform, "Label", label, fontSize, TextColor);
            t.rectTransform.Stretch(8, 2, 8, 2);
            return btn;
        }

        /// <summary>Рамка: тонкая обводка вокруг прямоугольника.</summary>
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
    }
}
