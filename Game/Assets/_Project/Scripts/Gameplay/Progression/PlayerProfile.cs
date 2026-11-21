using System;
using Game.Core;
using Game.Data;
using UnityEngine;

namespace Game.Gameplay.Progression
{
    public readonly struct LevelUpEvent
    {
        public readonly int Level;
        public LevelUpEvent(int level) { Level = level; }
    }

    public readonly struct ProfileChangedEvent { }

    /// <summary>Прогресс героя между забегами: уровень, опыт, золото.</summary>
    public sealed class PlayerProfile
    {
        private readonly ContentDatabase _db;

        public string CharacterId = "hero";
        public int Level { get; private set; } = 1;
        public long Xp { get; private set; }
        public int Gold { get; private set; }

        public event Action Changed;

        public PlayerProfile(ContentDatabase db) => _db = db;

        public CharacterDef Character => _db.Character(CharacterId);

        public long XpToNext(int level)
        {
            var curve = Character?.XpCurve ?? new XpCurveDef();
            return (long)Mathf.Round(curve.Base * Mathf.Pow(curve.Growth, level - 1));
        }

        public float Xp01 => Mathf.Clamp01((float)Xp / Mathf.Max(1, XpToNext(Level)));

        public void AddXp(long amount)
        {
            if (amount <= 0) return;
            Xp += amount;
            while (Xp >= XpToNext(Level) && Level < 100)
            {
                Xp -= XpToNext(Level);
                Level++;
                EventBus.Publish(new LevelUpEvent(Level));
            }
            Notify();
        }

        public void SetLevel(int level)
        {
            int old = Level;
            Level = Mathf.Clamp(level, 1, 100);
            Xp = 0;
            if (Level > old) EventBus.Publish(new LevelUpEvent(Level));
            Notify();
        }

        public void AddGold(int amount)
        {
            Gold = Mathf.Max(0, Gold + amount);
            Notify();
        }

        public void SetState(int level, long xp, int gold)
        {
            Level = Mathf.Max(1, level);
            Xp = Math.Max(0, xp);
            Gold = Math.Max(0, gold);
            Notify();
        }

        private void Notify()
        {
            Changed?.Invoke();
            EventBus.Publish(new ProfileChangedEvent());
        }
    }
}
