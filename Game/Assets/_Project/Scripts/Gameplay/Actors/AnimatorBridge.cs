using System.Collections.Generic;
using Game.Data;
using Game.Gameplay.Combat;
using UnityEngine;

namespace Game.Gameplay.Actors
{
    /// <summary>
    /// Мост к Animator настоящей модели (если она есть): float "Speed", триггер по имени из skills.json → "animation",
    /// триггеры "Die" и "Revive". Параметры, которых нет в контроллере, просто пропускаются.
    /// Если в контроллере есть float "{триггер}ClipLength" (длина клипа удара), то перед ударом ставится
    /// "CastSpeed" = длина клипа / длительность умения — анимация укладывается ровно во время удара.
    /// </summary>
    public sealed class AnimatorBridge : MonoBehaviour
    {
        private Animator _animator;
        private Mover _mover;
        private SkillCaster _caster;
        private Actor _actor;
        private readonly HashSet<string> _params = new();
        private readonly Dictionary<string, float> _clipLengths = new();

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
            foreach (var p in _animator.parameters)
            {
                _params.Add(p.name);
                if (p.type == AnimatorControllerParameterType.Float && p.name.EndsWith("ClipLength"))
                    _clipLengths[p.name.Substring(0, p.name.Length - "ClipLength".Length)] = p.defaultFloat;
            }
            if (_caster != null) _caster.CastStarted += OnCast;
            if (_actor != null)
            {
                _actor.Died += OnDied;
                _actor.Revived += OnRevived;
            }
        }

        private void OnDestroy()
        {
            if (_caster != null) _caster.CastStarted -= OnCast;
            if (_actor != null)
            {
                _actor.Died -= OnDied;
                _actor.Revived -= OnRevived;
            }
        }

        private void OnCast(SkillDef def, float duration)
        {
            string trigger = string.IsNullOrEmpty(def.Animation) ? "Attack" : def.Animation;
            if (!_params.Contains(trigger)) return;
            if (_params.Contains("CastSpeed") && _clipLengths.TryGetValue(trigger, out float len) && duration > 0.01f)
                _animator.SetFloat("CastSpeed", Mathf.Clamp(len / duration, 0.3f, 4f));
            _animator.SetTrigger(trigger);
        }

        private void OnDied(Actor a)
        {
            if (_params.Contains("Die")) _animator.SetTrigger("Die");
        }

        private void OnRevived()
        {
            if (_params.Contains("Revive")) _animator.SetTrigger("Revive");
        }

        private void Update()
        {
            if (_mover != null && _params.Contains("Speed"))
                _animator.SetFloat("Speed", _mover.Velocity.magnitude);
        }
    }
}
