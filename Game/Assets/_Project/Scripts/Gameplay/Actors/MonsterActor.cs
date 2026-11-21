using Game.Core;
using Game.Data;
using Game.Gameplay.Items;
using Game.Gameplay.Progression;
using UnityEngine;

namespace Game.Gameplay.Actors
{
    /// <summary>Связывает Actor с MonsterDef: статы с учётом уровня, опыт, лут, растворение трупа.</summary>
    [RequireComponent(typeof(Actor))]
    public sealed class MonsterActor : MonoBehaviour
    {
        public string MonsterId;
        public Transform Model;

        private Actor _actor;

        public Actor Actor => _actor;
        public MonsterDef Def => Services.Get<ContentDatabase>().Monster(MonsterId);

        public void Init(string monsterId, int level, Transform model)
        {
            MonsterId = monsterId;
            Model = model;
            _actor = GetComponent<Actor>();
            _actor.Level = Mathf.Max(1, level);
            ApplyDef();
            _actor.FillResources();
            _actor.Died += OnDied;
            EventBus.Subscribe<ContentReloadedEvent>(OnContentReloaded);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<ContentReloadedEvent>(OnContentReloaded);
            if (_actor != null) _actor.Died -= OnDied;
        }

        private void OnContentReloaded(ContentReloadedEvent e)
        {
            if (_actor == null || _actor.IsDead) return;
            float hp01 = _actor.Health01;
            ApplyDef();
            _actor.FillResources();
            _actor.SetHealthFraction(hp01);
        }

        /// <summary>Статы = база × (1 + scaling × (уровень − baseLevel)).</summary>
        public void ApplyDef()
        {
            var def = Def;
            if (def == null) return;
            _actor.DisplayName = def.Name;
            _actor.Stats.ClearBase();
            int levelDiff = _actor.Level - def.BaseLevel;
            foreach (var kv in def.Stats)
            {
                float scale = def.Scaling.TryGetValue(kv.Key, out var s) ? 1f + s * levelDiff : 1f;
                _actor.Stats.SetBase(kv.Key, kv.Value * Mathf.Max(0.1f, scale));
            }
        }

        private void OnDied(Actor actor)
        {
            var def = Def;
            Vector3 pos = transform.position;

            if (def != null)
            {
                if (actor.LastAttacker != null && actor.LastAttacker.Team == Team.Player)
                {
                    int xp = Mathf.RoundToInt(def.Xp * (1f + 0.1f * (_actor.Level - 1)));
                    Services.Get<PlayerProfile>().AddXp(xp);
                }

                if (!string.IsNullOrEmpty(def.LootTable))
                {
                    var loot = Services.Get<LootService>().Roll(def.LootTable, _actor.Level);
                    if (!loot.IsEmpty) LootDrop.Spawn(pos, loot.Items, loot.Gold);
                }

                EventBus.Publish(new MonsterKilledEvent(def.Id, def.Tags, _actor.Level, pos, actor.LastAttacker));
            }

            var mover = GetComponent<Mover>();
            if (mover != null) mover.SetCollisionEnabled(false);
            CorpseDissolver.Begin(gameObject, Model, def?.CorpseSeconds ?? 2.5f, 1.2f);
        }
    }
}
