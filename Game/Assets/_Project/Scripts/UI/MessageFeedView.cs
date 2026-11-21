using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Сообщения по центру сверху (квест принят, рюкзак полон, новый уровень). Гаснут сами.</summary>
    public sealed class MessageFeedView : MonoBehaviour
    {
        private sealed class Line
        {
            public Text Text;
            public float Born;
        }

        private const float Life = 3f;
        private const int MaxLines = 5;
        private readonly List<Line> _lines = new();
        private RectTransform _root;

        public static MessageFeedView Create(RectTransform canvas)
        {
            var rt = UiKit.Rect("Messages", canvas);
            rt.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(900f, 200f));
            var view = rt.gameObject.AddComponent<MessageFeedView>();
            view._root = rt;
            EventBus.Subscribe<UiMessageEvent>(view.OnMessage);
            return view;
        }

        private void OnDestroy() => EventBus.Unsubscribe<UiMessageEvent>(OnMessage);

        private void OnMessage(UiMessageEvent e)
        {
            if (_lines.Count >= MaxLines)
            {
                Destroy(_lines[0].Text.gameObject);
                _lines.RemoveAt(0);
            }
            var t = UiKit.Text(_root, "Msg", e.Text, 24, e.Color, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            t.rectTransform.sizeDelta = new Vector2(900f, 30f);
            _lines.Add(new Line { Text = t, Born = Time.unscaledTime });
        }

        private void Update()
        {
            for (int i = _lines.Count - 1; i >= 0; i--)
            {
                float age = Time.unscaledTime - _lines[i].Born;
                if (age > Life)
                {
                    Destroy(_lines[i].Text.gameObject);
                    _lines.RemoveAt(i);
                }
            }
            for (int i = 0; i < _lines.Count; i++)
            {
                var l = _lines[i];
                float age = Time.unscaledTime - l.Born;
                var c = l.Text.color;
                c.a = Mathf.Clamp01((Life - age) / 0.6f);
                l.Text.color = c;
                l.Text.rectTransform.anchoredPosition = new Vector2(0f, -(_lines.Count - 1 - i) * 32f);
                l.Text.rectTransform.anchorMin = l.Text.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                l.Text.rectTransform.pivot = new Vector2(0.5f, 1f);
            }
        }
    }
}
