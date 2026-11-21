using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.Core;
using Game.Data;
using Game.Gameplay.Actors;
using Game.Gameplay.Items;
using Game.Gameplay.Progression;
using Game.Gameplay.Quests;
using Game.Gameplay.World;
using UnityEngine;

namespace Game.Gameplay.Dialogue
{
    public readonly struct DialogueChangedEvent
    {
        public readonly bool IsOpen;
        public DialogueChangedEvent(bool isOpen) { IsOpen = isOpen; }
    }

    /// <summary>
    /// Диалоги из dialogues/*.json. Условия и действия — строки "тип:аргумент":
    ///   if:      quest.available:q_id | quest.active:q_id | quest.ready:q_id | quest.completed:q_id | quest.notstarted:q_id (можно с "!")
    ///   actions: quest.accept:q_id | quest.turnin:q_id | heal | give:item_id:rarity | gold:N | close
    /// В тексте: {player}, {npc}, {progress:q_id}.
    /// </summary>
    public sealed class DialogueService
    {
        private readonly ContentDatabase _db;
        private readonly QuestService _quests;
        private readonly List<DialogueOptionDef> _visibleOptions = new();

        public bool IsOpen { get; private set; }
        public NpcDef Npc { get; private set; }
        public Transform NpcTransform { get; private set; }
        public DialogueDef Current { get; private set; }
        public DialogueNodeDef Node { get; private set; }
        public IReadOnlyList<DialogueOptionDef> Options => _visibleOptions;

        public DialogueService(ContentDatabase db, QuestService quests)
        {
            _db = db;
            _quests = quests;
        }

        public void Open(string npcId, Transform npcTransform)
        {
            Npc = _db.Npc(npcId);
            Current = _db.Dialogue(Npc?.Dialogue);
            NpcTransform = npcTransform;
            if (Current == null)
            {
                EventBus.Publish(new UiMessageEvent($"{Npc?.Name ?? npcId}: ...", Color.white));
                return;
            }

            string start = null;
            foreach (var s in Current.Start)
            {
                if (!Check(s.If)) continue;
                start = s.Node;
                break;
            }
            IsOpen = true;
            Show(start);
        }

        public void Choose(int index)
        {
            if (!IsOpen || index < 0 || index >= _visibleOptions.Count) return;
            var opt = _visibleOptions[index];
            bool close = string.IsNullOrEmpty(opt.Next);
            foreach (var a in opt.Actions)
                if (Run(a)) close = true;
            if (close || !IsOpen) Close();
            else Show(opt.Next);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Node = null;
            _visibleOptions.Clear();
            EventBus.Publish(new DialogueChangedEvent(false));
        }

        public string FormatText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("{player}", GameWorld.Player != null ? GameWorld.Player.DisplayName : "герой");
            text = text.Replace("{npc}", Npc?.Name ?? "");
            return Regex.Replace(text, @"\{progress:([\w\-]+)\}", m =>
            {
                var q = _db.Quest(m.Groups[1].Value);
                if (q == null || q.Objectives.Count == 0) return "?";
                var parts = new List<string>();
                for (int i = 0; i < q.Objectives.Count; i++) parts.Add(_quests.ObjectiveText(q, i));
                return string.Join(", ", parts);
            });
        }

        private void Show(string nodeId)
        {
            if (nodeId == null || !Current.Nodes.TryGetValue(nodeId, out var node))
            {
                Log.Warn("Dialogue", $"{Current.Id}: нет узла \"{nodeId}\"");
                Close();
                return;
            }
            Node = node;
            _visibleOptions.Clear();
            foreach (var o in node.Options)
                if (Check(o.If)) _visibleOptions.Add(o);
            if (_visibleOptions.Count == 0) _visibleOptions.Add(new DialogueOptionDef { Text = "До встречи." });
            EventBus.Publish(new DialogueChangedEvent(true));
        }

        public bool Check(string condition)
        {
            if (string.IsNullOrWhiteSpace(condition)) return true;
            bool negate = condition.StartsWith("!");
            if (negate) condition = condition.Substring(1);
            var parts = condition.Split(':');
            string arg = parts.Length > 1 ? parts[1] : null;
            bool result = parts[0] switch
            {
                "quest.available" => _quests.CanAccept(arg),
                "quest.notstarted" => _quests.Status(arg) == QuestStatus.NotStarted,
                "quest.active" => _quests.Status(arg) == QuestStatus.Active,
                "quest.ready" => _quests.Status(arg) == QuestStatus.ReadyToTurnIn,
                "quest.completed" => _quests.Status(arg) == QuestStatus.Completed,
                _ => Unknown(condition),
            };
            return negate ? !result : result;
        }

        private static bool Unknown(string what)
        {
            Log.Warn("Dialogue", $"Неизвестное условие/действие: {what}");
            return false;
        }

        /// <returns>true, если действие закрывает диалог.</returns>
        private bool Run(string action)
        {
            var parts = action.Split(':');
            string arg = parts.Length > 1 ? parts[1] : null;
            switch (parts[0])
            {
                case "quest.accept": _quests.Accept(arg); return false;
                case "quest.turnin": _quests.TurnIn(arg); return false;
                case "heal":
                    if (GameWorld.Player != null)
                    {
                        GameWorld.Player.Heal(float.MaxValue);
                        GameWorld.Player.RestoreMana(float.MaxValue);
                    }
                    return false;
                case "give":
                    var item = Services.Get<ItemGenerator>().Create(arg, parts.Length > 2 ? parts[2] : "common", Services.Get<PlayerProfile>().Level);
                    if (item != null && !Services.Get<Inventory>().TryAdd(item))
                        EventBus.Publish(new UiMessageEvent("Рюкзак полон", new Color(1f, 0.4f, 0.4f)));
                    return false;
                case "gold":
                    if (int.TryParse(arg, out int g)) Services.Get<PlayerProfile>().AddGold(g);
                    return false;
                case "close": return true;
                default: Unknown(action); return false;
            }
        }
    }
}
