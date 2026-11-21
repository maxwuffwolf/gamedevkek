using Game.Core;
using Game.Data;
using Game.Gameplay.Items;
using Game.Gameplay.Progression;
using Game.Gameplay.World;
using UnityEngine;

namespace Game.Gameplay.Actors
{
    /// <summary>
    /// Статы игрока = база из characters.json + прирост за уровень + надетые предметы.
    /// Пересчитывается при смене экипировки, уровня и hot reload. Также отвечает за смерть и воскрешение.
    /// </summary>
    [RequireComponent(typeof(Actor))]
    public sealed class PlayerCharacter : MonoBehaviour
    {
        private Actor _actor;
        private ContentDatabase _db;
        private Inventory _inventory;
        private PlayerProfile _profile;
        private readonly object _levelSource = new();
        private float _respawnAt = -1f;

        public Vector3 RespawnPoint { get; set; }
        public float RespawnRemaining => _respawnAt > 0 ? Mathf.Max(0f, _respawnAt - Time.time) : 0f;

        public void Init()
        {
            _actor = GetComponent<Actor>();
            _db = Services.Get<ContentDatabase>();
            _inventory = Services.Get<Inventory>();
            _profile = Services.Get<PlayerProfile>();
            RespawnPoint = transform.position;

            _inventory.Changed += Recalculate;
            _profile.Changed += Recalculate;
            _actor.Died += OnDied;
            EventBus.Subscribe<ContentReloadedEvent>(OnContentReloaded);
            EventBus.Subscribe<LevelUpEvent>(OnLevelUp);

            Recalculate();
            _actor.FillResources();
        }

        private void OnDestroy()
        {
            if (_inventory != null) _inventory.Changed -= Recalculate;
            if (_profile != null) _profile.Changed -= Recalculate;
            EventBus.Unsubscribe<ContentReloadedEvent>(OnContentReloaded);
            EventBus.Unsubscribe<LevelUpEvent>(OnLevelUp);
        }

        private void OnContentReloaded(ContentReloadedEvent e) => Recalculate();

        private void OnLevelUp(LevelUpEvent e)
        {
            Recalculate();
            _actor.FillResources();
            EventBus.Publish(new UiMessageEvent($"Новый уровень: {e.Level}!", new Color(1f, 0.85f, 0.3f)));
        }

        public void Recalculate()
        {
            var def = _profile.Character;
            if (def == null) return;
            _actor.DisplayName = def.Name;
            _actor.Level = _profile.Level;

            _actor.Stats.ClearBase();
            foreach (var kv in def.Stats) _actor.Stats.SetBase(kv.Key, kv.Value);

            _actor.Stats.RemoveBySource(_levelSource);
            int levels = _profile.Level - 1;
            if (levels > 0)
                foreach (var kv in def.PerLevel)
                    _actor.Stats.AddModifier(new Stats.StatModifier(kv.Key, ModType.Flat, kv.Value * levels, _levelSource));

            _actor.Stats.RemoveBySource(_inventory);
            foreach (var kv in _inventory.Equipped)
            foreach (var m in kv.Value.AllMods())
                _actor.Stats.AddModifier(new Stats.StatModifier(m.Stat, m.Type, m.Value, _inventory));

            _actor.ClampResources();
        }

        private void OnDied(Actor a)
        {
            float delay = _profile.Character?.RespawnSeconds ?? 4f;
            _respawnAt = Time.time + delay;
            EventBus.Publish(new UiMessageEvent("Вы погибли", new Color(1f, 0.3f, 0.3f)));
        }

        private void Update()
        {
            if (_respawnAt > 0f && Time.time >= _respawnAt)
            {
                _respawnAt = -1f;
                GetComponent<Mover>().Teleport(RespawnPoint);
                _actor.Revive();
            }
        }
    }
}
