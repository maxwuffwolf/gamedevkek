using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data;
using Game.Gameplay.Stats;
using UnityEngine;

namespace Game.Gameplay.Actors
{
    public enum Team
    {
        Player,
        Monster,
        Neutral,
    }

    /// <summary>
    /// Базовый компонент любого живого существа: статы, здоровье, мана, смерть.
    /// Поведение (игрок/ИИ) живёт в отдельных компонентах рядом.
    /// </summary>
    public sealed class Actor : MonoBehaviour
    {
        public static readonly List<Actor> All = new();

        public Team Team = Team.Neutral;
        public string DisplayName = "???";
        public int Level = 1;
        [Tooltip("Радиус тела, учитывается в дальности ударов")]
        public float Radius = 0.45f;
        [Tooltip("Высота над землёй для полоски HP и цифр урона")]
        public float HeadHeight = 2f;

        public StatSheet Stats { get; } = new();
        public float Health { get; private set; }
        public float Mana { get; private set; }
        public bool IsDead { get; private set; }
        public Actor LastAttacker { get; private set; }
        public float LastDamagedTime { get; private set; } = -999f;

        /// <summary>Внешний толчок (отбрасывание), гасится со временем. Читает Mover.</summary>
        public Vector3 ExternalVelocity;

        public float MaxHealth => Mathf.Max(1f, Stats.Get(StatId.MaxHealth));
        public float MaxMana => Mathf.Max(0f, Stats.Get(StatId.MaxMana));
        public float Health01 => Health / MaxHealth;
        public float Mana01 => MaxMana > 0 ? Mana / MaxMana : 0f;
        public Vector3 HeadPosition => transform.position + Vector3.up * HeadHeight;

        public event Action<Actor> Died;
        public event Action<float, Actor> Damaged;
        public event Action Revived;

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        /// <summary>Заполнить HP и ману до максимума (после установки статов).</summary>
        public void FillResources()
        {
            Health = MaxHealth;
            Mana = MaxMana;
        }

        private void Update()
        {
            if (IsDead) return;
            float dt = Time.deltaTime;
            Health = Mathf.Min(MaxHealth, Health + Stats.Get(StatId.HealthRegen) * dt);
            Mana = Mathf.Min(MaxMana, Mana + Stats.Get(StatId.ManaRegen) * dt);
            if (ExternalVelocity.sqrMagnitude > 0.0001f)
                ExternalVelocity = Vector3.MoveTowards(ExternalVelocity, Vector3.zero, 25f * dt);
        }

        public void ApplyDamage(Actor source, float amount, bool crit, float screenShake = 0f)
        {
            if (IsDead) return;
            if (Team == Team.Player && CombatCheats.God) amount = 0f;
            if (source != null && source.Team == Team.Player && CombatCheats.OneShot) amount = Mathf.Max(amount, Health + 1f);

            Health -= amount;
            LastAttacker = source;
            LastDamagedTime = Time.time;
            EventBus.Publish(new DamageDealtEvent(source, this, amount, crit, HeadPosition, screenShake));
            Damaged?.Invoke(amount, source);
            if (Health <= 0f) Die(source);
        }

        public void Die(Actor killer)
        {
            if (IsDead) return;
            IsDead = true;
            Health = 0f;
            Died?.Invoke(this);
            EventBus.Publish(new ActorDiedEvent(this, killer));
        }

        public void Revive()
        {
            IsDead = false;
            FillResources();
            Revived?.Invoke();
        }

        /// <summary>Выставить здоровье долей от максимума (без события урона).</summary>
        public void SetHealthFraction(float fraction) => Health = Mathf.Clamp01(fraction) * MaxHealth;

        public void Heal(float amount) { if (!IsDead) Health = Mathf.Min(MaxHealth, Health + amount); }
        public void RestoreMana(float amount) => Mana = Mathf.Min(MaxMana, Mana + amount);

        public bool TrySpendMana(float amount)
        {
            if (amount <= 0f) return true;
            if (Mana < amount) return false;
            Mana -= amount;
            return true;
        }

        /// <summary>Подрезать ресурсы, если максимум уменьшился (сняли предмет).</summary>
        public void ClampResources()
        {
            Health = Mathf.Min(Health, MaxHealth);
            Mana = Mathf.Min(Mana, MaxMana);
        }

        public bool IsHostileTo(Actor other) =>
            other != null && other != this && Team != Team.Neutral && other.Team != Team.Neutral && other.Team != Team;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => All.Clear();
    }
}
