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
            // подложка-градиент во всю ширину снизу, имя и реплика по центру, ответы «таблетками» справа (как в Genshin)
            var band = UiKit.Image(canvas, "Dialogue", new Color(0.03f, 0.04f, 0.07f, 0.88f), true);
            band.sprite = UiKit.FadeUp;
            var window = band.rectTransform;
            window.anchorMin = new Vector2(0f, 0f);
            window.anchorMax = new Vector2(1f, 0f);
            window.pivot = new Vector2(0.5f, 0f);
            window.anchoredPosition = Vector2.zero;
            window.sizeDelta = new Vector2(0f, 330f);
            var view = window.gameObject.AddComponent<DialogueView>();
            view._rect = window;
            view._dialogue = Services.Get<DialogueService>();

            view._speaker = UiKit.Text(window, "Speaker", "", 24, UiTheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            view._speaker.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 128f), new Vector2(700f, 32f));
            view._body = UiKit.Text(window, "Body", "", 20, UiTheme.Text, TextAnchor.UpperCenter).Shadowed();
            view._body.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 120f), new Vector2(1000f, 90f));
            view._options = UiKit.Rect("Options", window);
            view._options.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-120f, 200f), new Vector2(OptionWidth, 10f));
            var close = UiKit.CloseButton(window, () => view._dialogue.Close());
            close.GetComponent<RectTransform>().Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(34f, 34f));

            EventBus.Subscribe<DialogueChangedEvent>(view.OnChanged);
            window.gameObject.SetActive(false);
            return view;
        }

        private const float OptionWidth = 380f;

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
            const float h = 42f, gap = 8f;
            for (int i = 0; i < opts.Count; i++)
            {
                int index = i;
                var btn = UiKit.Button(_options, $"Option{i}", _dialogue.FormatText(opts[i].Text), () => _dialogue.Choose(index), 17);
                var img = btn.GetComponent<Image>();
                img.color = UiTheme.PanelSoft;
                UiKit.Outline(img.rectTransform, UiTheme.Line, UiTheme.RadiusPill);
                var label = btn.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.Stretch(48, 2, 16, 2);
                var key = UiKit.KeyChip(btn.transform, (i + 1).ToString(), 13);
                key.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(9f, 0f), new Vector2(26f, 26f));
                // ответы растут вверх от нижней точки, первый — сверху
                btn.GetComponent<RectTransform>().Place(new Vector2(1f, 0f), new Vector2(1f, 0f),
                    new Vector2(0f, (opts.Count - 1 - i) * (h + gap)), new Vector2(OptionWidth, h));
                _buttons.Add(btn);
            }
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
