using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Actors
{
    /// <summary>
    /// Движение через CharacterController + гравитация + отбрасывание. Вызывать Move каждый кадр.
    /// Мобы не сталкиваются капсулами друг с другом (иначе CharacterController «заезжает» на соседа и встаёт ему
    /// на голову) — вместо этого мягко расталкиваются в стороны по земле и стоят плотной кучкой рядом.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class Mover : MonoBehaviour
    {
        private CharacterController _cc;
        private Actor _actor;
        private float _verticalSpeed;

        private static readonly List<Mover> Monsters = new();
        /// <summary>Зазор между капсулами мобов (м) и сила расталкивания (м/с на метр перекрытия).</summary>
        private const float SeparationGap = 0.12f;
        private const float SeparationStrength = 7f;
        private const float SeparationMaxSpeed = 3f;
        private bool _isMonster;

        public Vector3 Velocity { get; private set; }
        public float NormalizedSpeed { get; private set; }

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _actor = GetComponent<Actor>();
        }

        private void Start()
        {
            _isMonster = _actor != null && _actor.Team == Team.Monster;
            if (!_isMonster) return;
            // не стоим на чужих головах: никаких ступенек
            _cc.stepOffset = Mathf.Min(_cc.stepOffset, 0.12f);
            foreach (var other in Monsters)
                if (other != null) Physics.IgnoreCollision(_cc, other._cc, true);
            Monsters.Add(this);
        }

        private void OnDestroy()
        {
            if (_isMonster) Monsters.Remove(this);
        }

        /// <summary>Горизонтальная скорость, разводящая перекрывшихся мобов (живых и с включённой капсулой).</summary>
        private Vector3 Separation()
        {
            Vector3 push = Vector3.zero;
            Vector3 p = transform.position;
            float r = _cc.radius * transform.lossyScale.x;
            foreach (var other in Monsters)
            {
                if (other == this || other == null || !other._cc.enabled) continue;
                Vector3 d = p - other.transform.position;
                d.y = 0f;
                float min = r + other._cc.radius * other.transform.lossyScale.x + SeparationGap;
                float dist = d.magnitude;
                if (dist >= min) continue;
                // ровно в одной точке — разводим в случайную, но стабильную сторону
                Vector3 dir = dist > 0.001f ? d / dist
                    : Quaternion.Euler(0f, (GetInstanceID() * 47) % 360, 0f) * Vector3.forward;
                push += dir * ((min - dist) * SeparationStrength);
            }
            return Vector3.ClampMagnitude(push, SeparationMaxSpeed);
        }

        /// <summary>direction в плоскости XZ (длина ≤ 1), speed в м/с.</summary>
        public void Move(Vector3 direction, float speed)
        {
            if (!_cc.enabled) return;
            float dt = Time.deltaTime;
            direction.y = 0f;
            Vector3 planar = direction * speed;
            if (_actor != null) planar += _actor.ExternalVelocity;
            Vector3 separation = _isMonster ? Separation() : Vector3.zero;

            _verticalSpeed = _cc.isGrounded ? -2f : _verticalSpeed + Physics.gravity.y * dt;
            _cc.Move((planar + separation + Vector3.up * _verticalSpeed) * dt);

            Velocity = planar;
            NormalizedSpeed = speed > 0.01f ? Mathf.Clamp01(direction.magnitude) : 0f;
        }

        public void Stop() => Move(Vector3.zero, 0f);

        public void FaceTowards(Vector3 worldPoint, float degreesPerSecond = 900f)
        {
            Vector3 dir = worldPoint - transform.position;
            dir.y = 0f;
            FaceDirection(dir, degreesPerSecond);
        }

        public void FaceDirection(Vector3 dir, float degreesPerSecond = 900f)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            var target = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, degreesPerSecond * Time.deltaTime);
        }

        public void Teleport(Vector3 position)
        {
            _cc.enabled = false;
            transform.position = position;
            _cc.enabled = true;
            _verticalSpeed = 0f;
        }

        public void SetCollisionEnabled(bool enabled) => _cc.enabled = enabled;
    }
}
