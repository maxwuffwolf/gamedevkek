using System.Text;
using Game.Core;
using Game.Data;
using Game.Gameplay.Quests;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Список активных заданий слева сверху (под названием зоны) на стеклянной плашке.</summary>
    public sealed class QuestTrackerView : MonoBehaviour
    {
        private Text _text;
        private RectTransform _panel;
        private QuestService _quests;
        private ContentDatabase _db;
        private bool _dirty = true;

        public static QuestTrackerView Create(RectTransform canvas)
        {
            var rt = UiKit.Panel(canvas, "QuestTracker", UiTheme.PanelSoft, UiTheme.RadiusPanel, UiTheme.Line, false).rectTransform;
            rt.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(44f, -100f), new Vector2(340f, 60f));
            var view = rt.gameObject.AddComponent<QuestTrackerView>();
            view._panel = rt;
            view._text = UiKit.Text(rt, "Text", "", 16, UiTheme.Text, TextAnchor.UpperLeft);
            view._text.rectTransform.Stretch(16, 12, 16, 12);
            view._text.lineSpacing = 1.1f;
            view._quests = Services.Get<QuestService>();
            view._db = Services.Get<ContentDatabase>();
            view._quests.Changed += view.MarkDirty;
            EventBus.Subscribe<ContentReloadedEvent>(view.OnReload);
            return view;
        }

        private void OnDestroy()
        {
            if (_quests != null) _quests.Changed -= MarkDirty;
            EventBus.Unsubscribe<ContentReloadedEvent>(OnReload);
        }

        private void OnReload(ContentReloadedEvent e) => _dirty = true;
        private void MarkDirty() => _dirty = true;

        private void Update()
        {
            if (!_dirty) return;
            _dirty = false;
            var sb = new StringBuilder();
            foreach (var p in _quests.Tracked)
            {
                var q = _db.Quest(p.QuestId);
                if (q == null) continue;
                sb.AppendLine($"<b>{ColorUtil.Colorize(q.Name, UiTheme.Gold)}</b>");
                if (p.Status == QuestStatus.ReadyToTurnIn)
                {
                    var npc = _db.Npc(q.TurnInNpc);
                    sb.AppendLine(ColorUtil.Colorize($"» Вернитесь к: {npc?.Name ?? q.TurnInNpc}", UiTheme.Good));
                }
                else
                {
                    for (int i = 0; i < q.Objectives.Count; i++) sb.AppendLine("• " + _quests.ObjectiveText(q, i));
                }
                sb.AppendLine();
            }
            _text.text = sb.ToString().TrimEnd();
            bool any = _text.text.Length > 0;
            _panel.GetComponent<UnityEngine.UI.Image>().enabled = any;
            _panel.Find("Outline").gameObject.SetActive(any);
            var settings = _text.GetGenerationSettings(new Vector2(340f - 32f, 0f));
            float h = _text.cachedTextGeneratorForLayout.GetPreferredHeight(_text.text, settings) / _text.pixelsPerUnit;
            _panel.sizeDelta = new Vector2(340f, h + 24f);
        }
    }
}
