using System.Collections.Generic;
using Game.Data;
using Game.Gameplay.Combat;
using UnityEngine;

namespace Game.Gameplay.Actors
{
    /// <summary>
    /// Мост к Animator настоящей модели (если она есть): float "Speed", триггер по имени из skills.json → "animation",
    /// триггер "Die". Параметры, которых нет в контроллере, просто пропускаются.
    /// </summary>
    public sealed class AnimatorBridge : MonoBehaviour
    {
        private Animator _animator;
        private Mover _mover;
        private SkillCaster _caster;
        private Actor _actor;
        private readonly HashSet<string> _params = new();

        private void Start()
        {
            _animator = GetComponentInChildren<Animator>();
            _mover = GetComponent<Mover>();
            _caster = GetComponent<SkillCaster>();
            _actor = GetComponent<Actor>();
            if (_animator == null || _animator.runtimeAnimatorController == null)
            {
                enabled = false;
                return;
            }
            foreach (var p in _animator.parameters) _params.Add(p.name);
            if (_caster != null) _caster.CastStarted += OnCast;
            if (_actor != null) _actor.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (_caster != null) _caster.CastStarted -= OnCast;
            if (_actor != null) _actor.Died -= OnDied;
        }

        private void OnCast(SkillDef def, float duration)
        {
            string trigger = string.IsNullOrEmpty(def.Animation) ? "Attack" : def.Animation;
            if (_params.Contains(trigger)) _animator.SetTrigger(trigger);
        }

        private void OnDied(Actor a)
        {
            if (_params.Contains("Die")) _animator.SetTrigger("Die");
        }

        private void Update()
        {
            if (_mover != null && _params.Contains("Speed"))
                _animator.SetFloat("Speed", _mover.Velocity.magnitude);
        }
    }
}
