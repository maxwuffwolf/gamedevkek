using Game.Core;
using Game.Data;
using Game.Gameplay.Actors;
using Game.Gameplay.Combat;
using Game.Gameplay.World;
using UnityEngine;

namespace Game.Gameplay.AI
{
    /// <summary>
    /// ИИ "melee_basic": бродит у дома → замечает игрока (aggroRange) или получает удар → преследует →
    /// атакует первым навыком из monsters.json → при отрыве дальше leashRange возвращается домой и лечится.
    /// </summary>
    [RequireComponent(typeof(Actor), typeof(Mover), typeof(SkillCaster))]
    public sealed class MonsterBrain : MonoBehaviour
    {
        public enum State
        {
            Idle,
            Wander,
            Chase,
            Attack,
            Return,
            Dead,
        }

        /// <summary>Подписи состояний над мобами (дев-команда ai.debug).</summary>
        public static bool DebugLabels;

        public State Current { get; private set; } = State.Idle;
        public Vector3 Home { get; set; }

        private Actor _actor;
        private Mover _mover;
        private SkillCaster _caster;
        private MonsterActor _monster;
        private float _stateUntil;
        private Vector3 _wanderTarget;
        private System.Random _rng;

        private void Awake()
        {
            _actor = GetComponent<Actor>();
            _mover = GetComponent<Mover>();
            _caster = GetComponent<SkillCaster>();
            _monster = GetComponent<MonsterActor>();
            _rng = Services.Get<RngService>().Stream("ai");
            Home = transform.position;
        }

        private void OnEnable() => GetComponent<Actor>().Damaged += OnDamaged;
        private void OnDisable() => GetComponent<Actor>().Damaged -= OnDamaged;

        private void OnDamaged(float amount, Actor source)
        {
            if (Current == State.Idle || Current == State.Wander) Current = State.Chase;
        }

        private MonsterDef Def => _monster != null ? _monster.Def : null;

        private float MoveSpeed => Mathf.Max(0.5f, _actor.Stats.Get(StatId.MoveSpeed));

        private void Update()
        {
            if (_actor.IsDead)
            {
                Current = State.Dead;
                return;
            }
            var def = Def;
            if (def == null)
            {
                _mover.Stop();
                return;
            }

            var target = GameWorld.PlayerAlive ? GameWorld.Player : null;
            float distToTarget = target != null ? FlatDistance(transform.position, target.transform.position) : float.MaxValue;
            float distFromHome = FlatDistance(transform.position, Home);

            switch (Current)
            {
                case State.Idle:
                    _mover.Stop();
                    if (CheckAggro(def, distToTarget)) break;
                    if (Time.time >= _stateUntil)
                    {
                        var p = _rng.InsideUnitCircle() * def.WanderRadius;
                        _wanderTarget = Home + new Vector3(p.x, 0f, p.y);
                        Current = State.Wander;
                        _stateUntil = Time.time + 6f;
                    }
                    break;

                case State.Wander:
                    if (CheckAggro(def, distToTarget)) break;
                    if (FlatDistance(transform.position, _wanderTarget) < 0.5f || Time.time >= _stateUntil)
                    {
                        Current = State.Idle;
                        _stateUntil = Time.time + _rng.Range(1.5f, 4f);
                        break;
                    }
                    MoveTo(_wanderTarget, MoveSpeed * 0.35f);
                    break;

                case State.Chase:
                    if (target == null || distFromHome > def.LeashRange)
                    {
                        Current = State.Return;
                        break;
                    }
                    if (distToTarget <= def.AttackRange + target.Radius)
                    {
                        _mover.FaceTowards(target.transform.position);
                        string skill = def.Skills.Count > 0 ? def.Skills[0] : null;
                        if (skill != null && _caster.TryCast(skill, target.transform.position) == CastResult.Ok)
                            Current = State.Attack;
                        else
                            _mover.Stop();
                        break;
                    }
                    MoveTo(target.transform.position, MoveSpeed);
                    break;

                case State.Attack:
                    _mover.Move(Vector3.zero, 0f);
                    if (!_caster.IsCasting)
                    {
                        Current = State.Chase;
                        _stateUntil = Time.time;
                    }
                    break;

                case State.Return:
                    if (distFromHome < 1f)
                    {
                        _actor.FillResources();
                        Current = State.Idle;
                        _stateUntil = Time.time + 1f;
                        break;
                    }
                    MoveTo(Home, MoveSpeed * 1.2f);
                    break;
            }
        }

        private bool CheckAggro(MonsterDef def, float distToTarget)
        {
            if (distToTarget > def.AggroRange) return false;
            Current = State.Chase;
            return true;
        }

        private void MoveTo(Vector3 point, float speed)
        {
            Vector3 dir = point - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
            {
                _mover.Stop();
                return;
            }
            dir.Normalize();
            _mover.FaceDirection(dir, 540f);
            _mover.Move(dir, speed * _caster.MoveMultiplier);
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void OnDrawGizmosSelected()
        {
            var def = Application.isPlaying ? Def : null;
            if (def == null) return;
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, def.AggroRange);
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.6f);
            Gizmos.DrawWireSphere(Home, def.LeashRange);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => DebugLabels = false;
    }
}
