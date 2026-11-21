using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data;
using Game.Gameplay.Actors;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>Контекст срабатывания навыка (момент удара).</summary>
    public readonly struct SkillContext
    {
        public readonly Actor Caster;
        public readonly SkillDef Def;
        public readonly Vector3 AimPoint;
        public readonly Vector3 Direction;

        public SkillContext(Actor caster, SkillDef def, Vector3 aimPoint, Vector3 direction)
        {
            Caster = caster;
            Def = def;
            AimPoint = aimPoint;
            Direction = direction;
        }
    }

    /// <summary>Поведение навыка. Данные (урон, радиус, кулдаун) берутся из SkillDef.</summary>
    public interface ISkillBehavior
    {
        void Execute(in SkillContext ctx);
    }

    /// <summary>Помечает класс поведения ключом из JSON: "behavior": "Melee".</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class SkillBehaviorAttribute : Attribute
    {
        public readonly string Key;
        public SkillBehaviorAttribute(string key) => Key = key;
    }

    public static class SkillBehaviors
    {
        private static Dictionary<string, ISkillBehavior> _map;

        public static ISkillBehavior Get(string key)
        {
            if (_map == null) Build();
            return key != null && _map.TryGetValue(key, out var b) ? b : null;
        }

        public static IEnumerable<string> Keys
        {
            get
            {
                if (_map == null) Build();
                return _map.Keys;
            }
        }

        private static void Build()
        {
            _map = new Dictionary<string, ISkillBehavior>(StringComparer.OrdinalIgnoreCase);
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!asm.GetName().Name.StartsWith("Game")) continue;
                Type[] types;
                try { types = asm.GetTypes(); }
                catch { continue; }
                foreach (var t in types)
                {
                    if (t.IsAbstract || !typeof(ISkillBehavior).IsAssignableFrom(t)) continue;
                    foreach (SkillBehaviorAttribute a in t.GetCustomAttributes(typeof(SkillBehaviorAttribute), false))
                        _map[a.Key] = (ISkillBehavior)Activator.CreateInstance(t);
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _map = null;
    }

    /// <summary>Удар в ближнем бою: дуга или круг вокруг кастера.</summary>
    [SkillBehavior("Melee")]
    public sealed class MeleeSkillBehavior : ISkillBehavior
    {
        private static readonly Collider[] Buffer = new Collider[64];
        private static readonly HashSet<Actor> Hit = new();

        public void Execute(in SkillContext ctx)
        {
            var caster = ctx.Caster;
            var def = ctx.Def;
            Vector3 origin = caster.transform.position;
            Vector3 fwd = ctx.Direction;
            float radius = def.Shape.Radius;
            bool isArc = def.Shape.Type == SkillShapeType.Arc && def.Shape.Angle < 359f;
            var rng = Services.Get<RngService>().Stream("combat");

            Hit.Clear();
            int n = Physics.OverlapSphereNonAlloc(origin, radius + 1f, Buffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var target = Buffer[i].GetComponentInParent<Actor>();
                if (target == null || target.IsDead || !caster.IsHostileTo(target) || !Hit.Add(target)) continue;

                Vector3 to = target.transform.position - origin;
                to.y = 0f;
                if (to.magnitude - target.Radius > radius) continue;
                if (isArc && to.sqrMagnitude > 0.04f && Vector3.Angle(fwd, to) > def.Shape.Angle * 0.5f) continue;

                var result = Stats.DamageCalculator.Compute(caster.Stats, target.Stats, def.DamageMultiplier, rng);
                if (def.Knockback > 0f && to.sqrMagnitude > 0.0001f)
                    target.ExternalVelocity += to.normalized * def.Knockback;
                target.ApplyDamage(caster, result.Amount, result.Crit, def.ScreenShake);
            }

            SkillVfx.Swing(origin + Vector3.up * 0.1f, fwd, radius, isArc ? def.Shape.Angle : 360f,
                ColorUtil.Parse(def.VfxColor, Color.white));
        }
    }
}
