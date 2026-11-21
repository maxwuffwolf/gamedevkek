using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Dialogue;
using Game.Gameplay.World;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Окно диалога: имя NPC, реплика, варианты ответа (кнопки). Закрывается, если отойти.</summary>
    public sealed class DialogueView : MonoBehaviour
    {
        private DialogueService _dialogue;
        private Text _speaker, _body;
        private RectTransform _options;
        private readonly List<Button> _buttons = new();
        private RectTransform _rect;

        public bool IsOpen => gameObject.activeSelf;

        public static DialogueView Create(RectTransform canvas)
        {
            var window = UiKit.Image(canvas, "Dialogue", UiKit.PanelColor, true).rectTransform;
            window.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(720f, 300f));
            UiKit.Frame(window, UiKit.FrameColor, 3f);
            var view = window.gameObject.AddComponent<DialogueView>();
            view._rect = window;
            view._dialogue = Services.Get<DialogueService>();

            view._speaker = UiKit.Text(window, "Speaker", "", 22, UiKit.Gold, TextAnchor.UpperLeft, FontStyle.Bold).Shadowed();
            view._speaker.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -12f), new Vector2(600f, 28f));
            view._body = UiKit.Text(window, "Body", "", 18, UiKit.TextColor, TextAnchor.UpperLeft);
            view._body.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -46f), new Vector2(680f, 120f));
            view._options = UiKit.Rect("Options", window);
            view._options.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 14f), new Vector2(680f, 120f));
            var close = UiKit.Button(window, "Close", "×", () => view._dialogue.Close(), 22);
            close.GetComponent<RectTransform>().Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(32f, 32f));

            EventBus.Subscribe<DialogueChangedEvent>(view.OnChanged);
            window.gameObject.SetActive(false);
            return view;
        }

        private void OnDestroy() => EventBus.Unsubscribe<DialogueChangedEvent>(OnChanged);

        private void OnChanged(DialogueChangedEvent e)
        {
            if (!e.IsOpen || _dialogue.Node == null)
            {
                gameObject.SetActive(false);
                return;
            }
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            var node = _dialogue.Node;
            _speaker.text = string.IsNullOrEmpty(node.Speaker) ? _dialogue.Npc?.Name ?? "" : node.Speaker;
            _body.text = _dialogue.FormatText(node.Text);

            foreach (var b in _buttons) Destroy(b.gameObject);
            _buttons.Clear();
            var opts = _dialogue.Options;
            float h = 34f, gap = 6f;
            for (int i = 0; i < opts.Count; i++)
            {
                int index = i;
                var btn = UiKit.Button(_options, $"Option{i}", $"{i + 1}. {_dialogue.FormatText(opts[i].Text)}", () => _dialogue.Choose(index), 17);
                var label = btn.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                btn.GetComponent<RectTransform>().Place(new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(0f, (opts.Count - 1 - i) * (h + gap)), new Vector2(680f, h));
                _buttons.Add(btn);
            }
            float needed = 60f + Mathf.Max(60f, _body.preferredHeight) + opts.Count * (h + gap) + 20f;
            _rect.sizeDelta = new Vector2(720f, Mathf.Max(220f, needed));
        }

        private void Update()
        {
            if (!_dialogue.IsOpen)
            {
                gameObject.SetActive(false);
                return;
            }
            // отошёл далеко или умер → закрыть
            var p = GameWorld.Player;
            if (p == null || p.IsDead || (_dialogue.NpcTransform != null &&
                Vector3.Distance(p.transform.position, _dialogue.NpcTransform.position) > 6f))
                _dialogue.Close();
        }
    }
}
