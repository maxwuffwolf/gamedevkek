using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data;
using Game.Gameplay.Actors;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    public enum CastResult
    {
        Ok,
        Busy,
        Cooldown,
        NoMana,
        Unknown,
        Dead,
    }

    /// <summary>Запускает навыки: проверка маны/кулдауна, каст, момент удара (HitFrame) → ISkillBehavior.</summary>
    [RequireComponent(typeof(Actor))]
    public sealed class SkillCaster : MonoBehaviour
    {
        private sealed class ActiveCast
        {
            public SkillDef Def;
            public Vector3 Aim;
            public Vector3 Direction;
            public float Start;
            public float Duration;
            public bool HitDone;
        }

        private Actor _actor;
        private ContentDatabase _db;
        private readonly Dictionary<string, float> _cooldownEnds = new();
        private readonly Dictionary<string, float> _cooldownTotal = new();
        private ActiveCast _current;

        public bool IsCasting => _current != null;
        public SkillDef CurrentSkill => _current?.Def;
        public float MoveMultiplier => _current != null ? _current.Def.MoveWhileCasting : 1f;
        public Vector3 CastDirection => _current?.Direction ?? transform.forward;

        /// <summary>(навык, длительность каста). Слушают аниматоры/плейсхолдеры.</summary>
        public event Action<SkillDef, float> CastStarted;

        private void Awake()
        {
            _actor = GetComponent<Actor>();
            _db = Services.Get<ContentDatabase>();
        }

        public float CooldownRemaining(string skillId) =>
            skillId != null && _cooldownEnds.TryGetValue(skillId, out var end) ? Mathf.Max(0f, end - Time.time) : 0f;

        /// <summary>0 = готов, 1 = только что использован.</summary>
        public float CooldownFraction(string skillId)
        {
            float rem = CooldownRemaining(skillId);
            if (rem <= 0f) return 0f;
            return _cooldownTotal.TryGetValue(skillId, out var total) && total > 0f ? rem / total : 0f;
        }

        public void ResetCooldowns() => _cooldownEnds.Clear();

        public CastResult TryCast(string skillId, Vector3 aimPoint)
        {
            if (_actor.IsDead) return CastResult.Dead;
            var def = _db.Skill(skillId);
            if (def == null) return CastResult.Unknown;
            if (_current != null) return CastResult.Busy;
            if (CooldownRemaining(skillId) > 0f) return CastResult.Cooldown;
            if (!_actor.TrySpendMana(def.ManaCost)) return CastResult.NoMana;

            if (def.Cooldown > 0f)
            {
                _cooldownEnds[skillId] = Time.time + def.Cooldown;
                _cooldownTotal[skillId] = def.Cooldown;
            }

            float speed = def.UsesAttackSpeed ? Mathf.Max(0.1f, _actor.Stats.Get(StatId.AttackSpeed)) : 1f;
            Vector3 dir = aimPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = transform.forward;

            _current = new ActiveCast
            {
                Def = def,
                Aim = aimPoint,
                Direction = dir.normalized,
                Start = Time.time,
                Duration = Mathf.Max(0.05f, def.Duration / speed),
            };
            CastStarted?.Invoke(def, _current.Duration);
            return CastResult.Ok;
        }

        public void Cancel() => _current = null;

        private void Update()
        {
            if (_current == null) return;
            if (_actor.IsDead)
            {
                _current = null;
                return;
            }

            float t = (Time.time - _current.Start) / _current.Duration;
            if (!_current.HitDone && t >= _current.Def.HitFrame)
            {
                _current.HitDone = true;
                var behavior = SkillBehaviors.Get(_current.Def.Behavior);
                if (behavior != null)
                    behavior.Execute(new SkillContext(_actor, _current.Def, _current.Aim, _current.Direction));
                else
                    Log.Warn("Combat", $"Нет поведения навыка \"{_current.Def.Behavior}\" (skill {_current.Def.Id})");
            }
            if (t >= 1f) _current = null;
        }
    }
}
