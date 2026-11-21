using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Data;
using Game.Gameplay.Actors;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>Держит Count мобов вокруг себя, убитых возрождает через RespawnSeconds.</summary>
    public sealed class MonsterSpawner : MonoBehaviour
    {
        public string MonsterId = "wolf";
        [Min(0)] public int Count = 3;
        [Min(0.5f)] public float Radius = 5f;
        public Vector2Int LevelRange = new(1, 2);
        [Min(1f)] public float RespawnSeconds = 20f;

        private readonly List<Actor> _alive = new();
        private System.Random _rng;

        private IEnumerator Start()
        {
            while (!GameState.IsReady) yield return null;
            _rng = Services.Get<RngService>().Stream("spawn");
            for (int i = 0; i < Count; i++) SpawnOne();
        }

        public Actor SpawnOne()
        {
            var def = Services.Get<ContentDatabase>().Monster(MonsterId);
            if (def == null)
            {
                Log.Warn("World", $"MonsterSpawner: нет моба \"{MonsterId}\" в данных");
                return null;
            }
            var p = _rng.InsideUnitCircle() * Radius;
            var pos = GameWorld.SnapToGround(transform.position + new Vector3(p.x, 0f, p.y));
            var rot = Quaternion.Euler(0f, _rng.Range(0f, 360f), 0f);
            int level = _rng.RangeInclusive(LevelRange.x, Mathf.Max(LevelRange.x, LevelRange.y));
            var actor = ActorFactory.SpawnMonster(def, level, pos, rot);
            if (actor == null) return null;
            _alive.Add(actor);
            actor.Died += OnDied;
            return actor;
        }

        private void OnDied(Actor actor)
        {
            actor.Died -= OnDied;
            _alive.Remove(actor);
            StartCoroutine(Respawn());
        }

        private IEnumerator Respawn()
        {
            yield return new WaitForSeconds(RespawnSeconds);
            if (_alive.Count < Count) SpawnOne();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.35f, 0.25f, 0.8f);
            const int seg = 32;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                Gizmos.DrawLine(transform.position + new Vector3(Mathf.Cos(a0), 0.1f, Mathf.Sin(a0)) * Radius,
                    transform.position + new Vector3(Mathf.Cos(a1), 0.1f, Mathf.Sin(a1)) * Radius);
            }
        }
    }
}
