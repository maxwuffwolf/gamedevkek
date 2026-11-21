using Game.Data;
using Game.Gameplay.Combat;
using UnityEngine;

namespace Game.Gameplay.Actors
{
    /// <summary>Процедурная «анимация» заглушек: шаги, покачивание, взмах оружием, выпад.</summary>
    public sealed class PlaceholderRig : MonoBehaviour
    {
        public Transform Body;
        public Transform Head;
        public Transform WeaponPivot;
        public Quaternion WeaponRest = Quaternion.identity;
        public Transform[] Legs = new Transform[0];

        private Mover _mover;
        private SkillCaster _caster;
        private float _walkPhase;
        private float _castStart = -10f;
        private float _castDuration = 0.5f;

        private void Start()
        {
            _mover = GetComponentInParent<Mover>();
            _caster = GetComponentInParent<SkillCaster>();
            if (_caster != null) _caster.CastStarted += OnCast;
        }

        private void OnDestroy()
        {
            if (_caster != null) _caster.CastStarted -= OnCast;
        }

        private void OnCast(SkillDef def, float duration)
        {
            _castStart = Time.time;
            _castDuration = duration;
        }

        private void Update()
        {
            float speed = _mover != null ? _mover.Velocity.magnitude : 0f;
            _walkPhase += speed * Time.deltaTime * 3.2f;
            float swing = Mathf.Sin(_walkPhase) * Mathf.Clamp01(speed / 2f) * 35f;

            for (int i = 0; i < Legs.Length; i++)
                if (Legs[i] != null)
                    Legs[i].localRotation = Quaternion.Euler(((i % 2 == 0) ^ (i >= 2) ? swing : -swing), 0f, 0f);

            if (Body != null)
                Body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(_walkPhase)) * 0.05f * Mathf.Clamp01(speed / 2f), 0f);

            float ct = (Time.time - _castStart) / Mathf.Max(0.05f, _castDuration);
            if (ct >= 0f && ct <= 1f)
            {
                // замах → удар → возврат
                float k = ct < 0.45f ? Mathf.SmoothStep(0f, -1f, ct / 0.45f)
                    : ct < 0.6f ? Mathf.Lerp(-1f, 1f, (ct - 0.45f) / 0.15f)
                    : Mathf.Lerp(1f, 0f, (ct - 0.6f) / 0.4f);
                if (WeaponPivot != null)
                    WeaponPivot.localRotation = WeaponRest * Quaternion.Euler(-70f * k + 40f, 50f * k, 0f);
                if (Body != null)
                    Body.localPosition += new Vector3(0f, 0f, Mathf.Max(0f, k) * 0.25f);
            }
            else if (WeaponPivot != null)
            {
                WeaponPivot.localRotation = Quaternion.Slerp(WeaponPivot.localRotation, WeaponRest, 10f * Time.deltaTime);
            }
        }
    }
}
