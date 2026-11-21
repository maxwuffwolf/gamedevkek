using System.Text;
using Game.Core;
using Game.Data;
using Game.Gameplay.Quests;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Список активных заданий справа сверху.</summary>
    public sealed class QuestTrackerView : MonoBehaviour
    {
        private Text _text;
        private QuestService _quests;
        private ContentDatabase _db;
        private bool _dirty = true;

        public static QuestTrackerView Create(RectTransform canvas)
        {
            var rt = UiKit.Rect("QuestTracker", canvas);
            rt.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -90f), new Vector2(340f, 300f));
            var view = rt.gameObject.AddComponent<QuestTrackerView>();
            view._text = UiKit.Text(rt, "Text", "", 17, UiKit.TextColor, TextAnchor.UpperRight).Shadowed();
            view._text.rectTransform.Stretch();
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
                sb.AppendLine($"<b><color=#ffd34d>{q.Name}</color></b>");
                if (p.Status == QuestStatus.ReadyToTurnIn)
                {
                    var npc = _db.Npc(q.TurnInNpc);
                    sb.AppendLine($"<color=#8fff8f>» Вернитесь к: {npc?.Name ?? q.TurnInNpc}</color>");
                }
                else
                {
                    for (int i = 0; i < q.Objectives.Count; i++) sb.AppendLine("• " + _quests.ObjectiveText(q, i));
                }
                sb.AppendLine();
            }
            _text.text = sb.ToString().TrimEnd();
        }
    }
}
