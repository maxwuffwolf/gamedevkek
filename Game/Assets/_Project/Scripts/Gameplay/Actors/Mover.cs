using UnityEngine;

namespace Game.Gameplay.Actors
{
    /// <summary>Движение через CharacterController + гравитация + отбрасывание. Вызывать Move каждый кадр.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class Mover : MonoBehaviour
    {
        private CharacterController _cc;
        private Actor _actor;
        private float _verticalSpeed;

        public Vector3 Velocity { get; private set; }
        public float NormalizedSpeed { get; private set; }

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _actor = GetComponent<Actor>();
        }

        /// <summary>direction в плоскости XZ (длина ≤ 1), speed в м/с.</summary>
        public void Move(Vector3 direction, float speed)
        {
            if (!_cc.enabled) return;
            float dt = Time.deltaTime;
            direction.y = 0f;
            Vector3 planar = direction * speed;
            if (_actor != null) planar += _actor.ExternalVelocity;

            _verticalSpeed = _cc.isGrounded ? -2f : _verticalSpeed + Physics.gravity.y * dt;
            _cc.Move((planar + Vector3.up * _verticalSpeed) * dt);

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
