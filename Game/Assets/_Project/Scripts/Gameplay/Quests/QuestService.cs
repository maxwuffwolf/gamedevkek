using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Data;
using Game.Gameplay.Items;
using Game.Gameplay.Progression;
using Game.Gameplay.World;
using UnityEngine;

namespace Game.Gameplay.Quests
{
    public enum QuestStatus
    {
        NotStarted,
        Active,
        ReadyToTurnIn,
        Completed,
    }

    /// <summary>Значок над NPC.</summary>
    public enum NpcMarker
    {
        None,
        Available,
        InProgress,
        Ready,
    }

    [Serializable]
    public sealed class QuestProgress
    {
        public string QuestId;
        public QuestStatus Status;
        public int[] Counts = Array.Empty<int>();
    }

    public readonly struct QuestChangedEvent
    {
        public readonly string QuestId;
        public readonly QuestStatus Status;
        public QuestChangedEvent(string questId, QuestStatus status) { QuestId = questId; Status = status; }
    }

    /// <summary>Состояние квестов игрока: взять → прогресс по событиям → сдать → награда.</summary>
    public sealed class QuestService
    {
        private readonly ContentDatabase _db;
        private readonly Dictionary<string, QuestProgress> _progress = new();

        public event Action Changed;

        public QuestService(ContentDatabase db)
        {
            _db = db;
            EventBus.Subscribe<MonsterKilledEvent>(OnMonsterKilled);
        }

        public QuestProgress Get(string questId) => _progress.TryGetValue(questId, out var p) ? p : null;
        public QuestStatus Status(string questId) => Get(questId)?.Status ?? QuestStatus.NotStarted;
        public IEnumerable<QuestProgress> All => _progress.Values;
        public IEnumerable<QuestProgress> Tracked => _progress.Values.Where(p => p.Status == QuestStatus.Active || p.Status == QuestStatus.ReadyToTurnIn);

        public bool CanAccept(string questId)
        {
            var q = _db.Quest(questId);
            if (q == null || Status(questId) != QuestStatus.NotStarted) return false;
            return q.Prerequisites.All(p => Status(p) == QuestStatus.Completed);
        }

        public bool Accept(string questId)
        {
            if (!CanAccept(questId)) return false;
            var q = _db.Quest(questId);
            _progress[questId] = new QuestProgress
            {
                QuestId = questId,
                Status = QuestStatus.Active,
                Counts = new int[q.Objectives.Count],
            };
            EventBus.Publish(new UiMessageEvent($"Задание принято: {q.Name}", new Color(1f, 0.85f, 0.3f)));
            Notify(questId);
            return true;
        }

        public bool TurnIn(string questId)
        {
            var q = _db.Quest(questId);
            var p = Get(questId);
            if (q == null || p == null || p.Status != QuestStatus.ReadyToTurnIn) return false;
            p.Status = QuestStatus.Completed;
            GiveRewards(q);
            EventBus.Publish(new UiMessageEvent($"Задание выполнено: {q.Name}", new Color(0.5f, 1f, 0.5f)));
            Notify(questId);
            return true;
        }

        /// <summary>Дев: сразу выполнить все цели.</summary>
        public void ForceComplete(string questId)
        {
            var q = _db.Quest(questId);
            if (q == null) return;
            if (Status(questId) == QuestStatus.NotStarted)
                _progress[questId] = new QuestProgress { QuestId = questId, Counts = new int[q.Objectives.Count] };
            var p = Get(questId);
            for (int i = 0; i < q.Objectives.Count && i < p.Counts.Length; i++) p.Counts[i] = q.Objectives[i].Count;
            p.Status = QuestStatus.ReadyToTurnIn;
            Notify(questId);
        }

        public void Reset(string questId)
        {
            if (_progress.Remove(questId)) Notify(questId);
        }

        public void ResetAll()
        {
            _progress.Clear();
            Notify(null);
        }

        public void SetState(IEnumerable<QuestProgress> list)
        {
            _progress.Clear();
            if (list != null)
                foreach (var p in list)
                    if (p != null && _db.Quest(p.QuestId) != null) _progress[p.QuestId] = p;
            Notify(null);
        }

        public NpcMarker MarkerFor(string npcId)
        {
            bool inProgress = false, available = false;
            foreach (var q in _db.Quests.Values)
            {
                var status = Status(q.Id);
                if (status == QuestStatus.ReadyToTurnIn && q.TurnInNpc == npcId) return NpcMarker.Ready;
                if (status == QuestStatus.Active && q.TurnInNpc == npcId) inProgress = true;
                if (q.Giver == npcId && CanAccept(q.Id)) available = true;
            }
            if (available) return NpcMarker.Available;
            return inProgress ? NpcMarker.InProgress : NpcMarker.None;
        }

        /// <summary>"Убить волков: 3/5"</summary>
        public string ObjectiveText(QuestDef q, int index)
        {
            var o = q.Objectives[index];
            var p = Get(q.Id);
            int count = p != null && index < p.Counts.Length ? p.Counts[index] : 0;
            string name = !string.IsNullOrEmpty(o.Text) ? o.Text
                : o.Monster != null ? $"Убить: {_db.Monster(o.Monster)?.Name ?? o.Monster}"
                : $"Убить ({o.Tag})";
            return $"{name}: {Math.Min(count, o.Count)}/{o.Count}";
        }

        private void OnMonsterKilled(MonsterKilledEvent e)
        {
            if (e.Killer == null || e.Killer != GameWorld.Player) return;
            foreach (var p in _progress.Values)
            {
                if (p.Status != QuestStatus.Active) continue;
                var q = _db.Quest(p.QuestId);
                if (q == null) continue;
                if (p.Counts.Length != q.Objectives.Count) Array.Resize(ref p.Counts, q.Objectives.Count);

                bool changed = false;
                for (int i = 0; i < q.Objectives.Count; i++)
                {
                    var o = q.Objectives[i];
                    if (o.Type != ObjectiveType.Kill || p.Counts[i] >= o.Count) continue;
                    bool match = (!string.IsNullOrEmpty(o.Monster) && o.Monster == e.MonsterId)
                                 || (!string.IsNullOrEmpty(o.Tag) && e.Tags != null && e.Tags.Contains(o.Tag));
                    if (!match) continue;
                    p.Counts[i]++;
                    changed = true;
                    EventBus.Publish(new UiMessageEvent(ObjectiveText(q, i), new Color(1f, 0.9f, 0.6f)));
                }
                if (!changed) continue;

                bool done = true;
                for (int i = 0; i < q.Objectives.Count; i++)
                    if (p.Counts[i] < q.Objectives[i].Count) done = false;
                if (done)
                {
                    p.Status = QuestStatus.ReadyToTurnIn;
                    var npc = _db.Npc(q.TurnInNpc);
                    EventBus.Publish(new UiMessageEvent(
                        string.IsNullOrEmpty(q.ReadyText) ? $"Вернитесь к: {npc?.Name ?? q.TurnInNpc}" : q.ReadyText,
                        new Color(1f, 0.85f, 0.3f)));
                }
                Notify(q.Id);
            }
        }

        private void GiveRewards(QuestDef q)
        {
            var r = q.Rewards;
            if (r == null) return;
            var profile = Services.Get<PlayerProfile>();
            var inv = Services.Get<Inventory>();
            var gen = Services.Get<ItemGenerator>();

            if (r.BackpackSlots > 0)
            {
                inv.UnlockSlots(r.BackpackSlots);
                EventBus.Publish(new UiMessageEvent($"Открыто ячеек рюкзака: +{r.BackpackSlots}", new Color(0.6f, 0.85f, 1f)));
            }
            if (r.Gold > 0) profile.AddGold(r.Gold);

            var overflow = new List<ItemInstance>();
            foreach (var it in r.Items)
            {
                var item = gen.Create(it.Id, it.Rarity, it.Ilvl > 0 ? it.Ilvl : profile.Level);
                if (item != null && !inv.TryAdd(item)) overflow.Add(item);
            }
            if (overflow.Count > 0 && GameWorld.Player != null)
                LootDrop.Spawn(GameWorld.Player.transform.position + GameWorld.Player.transform.forward, overflow, 0);

            if (r.Xp > 0) profile.AddXp(r.Xp);
        }

        private void Notify(string questId)
        {
            Changed?.Invoke();
            EventBus.Publish(new QuestChangedEvent(questId, questId != null ? Status(questId) : QuestStatus.NotStarted));
        }
    }
}
