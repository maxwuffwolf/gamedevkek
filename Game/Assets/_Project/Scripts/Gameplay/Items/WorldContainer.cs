using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Data;
using Game.Gameplay.Actors;
using Game.Gameplay.Zones;
using UnityEngine;

namespace Game.Gameplay.Items
{
    /// <summary>
    /// Контейнер в мире: сундук, труп, меч в земле. Клик → подойти → окно с содержимым.
    /// Что внутри и как называется — в StreamingAssets/Data/containers/*.json (поле <see cref="ContainerId"/>).
    ///
    /// Как поставить руками: модель сундука → Add Component → WorldContainer → вписать ContainerId.
    /// Коллайдер-триггер для наведения мыши добавится сам. В <see cref="HideWhenEmpty"/> можно перетащить
    /// части модели, которые пропадут, когда всё заберут (меч, золото на крышке), а в <see cref="ShowWhenOpened"/> —
    /// например, открытую крышку.
    /// </summary>
    public sealed class WorldContainer : MonoBehaviour, IInteractable
    {
        public static readonly List<WorldContainer> All = new();

        [Tooltip("id из StreamingAssets/Data/containers/*.json")]
        public string ContainerId = "chest_small";

        [Tooltip("Уникальный ключ для сейва. Пусто = <сцена>:<ContainerId>. Заполни, если в сцене несколько одинаковых сундуков")]
        public string InstanceId = "";

        [Min(0.5f)] public float Range = 2.2f;

        [Tooltip("Точка, к которой подходит герой (пусто = сам объект)")]
        public Transform InteractPoint;

        [Tooltip("Спрятать, когда контейнер опустеет (меч, монеты на крышке, блик)")]
        public GameObject[] HideWhenEmpty = new GameObject[0];

        [Tooltip("Включить после первого открытия (открытая крышка)")]
        public GameObject[] ShowWhenOpened = new GameObject[0];

        [Tooltip("Спрятать после первого открытия (закрытая крышка)")]
        public GameObject[] HideWhenOpened = new GameObject[0];

        private ContentDatabase _db;
        private ContainerService _service;
        private LootBeam _beam;

        public string Key => string.IsNullOrEmpty(InstanceId) ? $"{gameObject.scene.name}:{ContainerId}" : InstanceId;
        public ContainerDef Def => _db != null ? _db.Container(ContainerId) : null;

        private void Reset()
        {
            if (GetComponent<Collider>() != null) return;
            var box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.6f, 0f);
            box.size = new Vector3(1.2f, 1.2f, 1.2f);
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => All.Clear();

        private IEnumerator Start()
        {
            if (GetComponent<Collider>() == null) Reset();
            while (!GameState.IsReady) yield return null;
            _db = Services.Get<ContentDatabase>();
            _service = Services.Get<ContainerService>();
            if (Def == null) Log.Warn("Containers", $"\"{name}\": нет контейнера \"{ContainerId}\" в containers/*.json");
            EventBus.Subscribe<ContainerChangedEvent>(OnChanged);
            RefreshVisual();
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<ContainerChangedEvent>(OnChanged);
            if (_service != null && _service.Current == this) _service.Close();
        }

        private void OnChanged(ContainerChangedEvent e)
        {
            if (e.Key == null || e.Key == Key) RefreshVisual();
        }

        private bool Opened => _service != null && _service.Find(Key) != null;
        private bool Empty => _service != null && _service.IsKnownEmpty(Key);

        private void RefreshVisual()
        {
            var def = Def;
            bool hide = Empty && def != null && def.WhenEmpty == ContainerEmptyMode.Hide;
            foreach (var go in HideWhenEmpty) if (go != null) go.SetActive(!Empty);
            foreach (var go in ShowWhenOpened) if (go != null) go.SetActive(Opened);
            foreach (var go in HideWhenOpened) if (go != null) go.SetActive(!Opened);
            // режим Hide: прячем все рендеры и свет на самом объекте (меч в земле исчезает целиком)
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = !hide;
            foreach (var l in GetComponentsInChildren<Light>(true)) l.enabled = !hide && !(Empty && def != null && def.WhenEmpty != ContainerEmptyMode.Keep);
            RefreshBeam(def);
        }

        /// <summary>Столб света как у лута: есть, пока в контейнере что-то лежит (или его ещё не открывали).</summary>
        private void RefreshBeam(ContainerDef def)
        {
            bool want = def != null && def.LootBeam && !Empty;
            if (!want)
            {
                if (_beam != null) _beam.gameObject.SetActive(false);
                return;
            }
            var color = BeamColor(def);
            if (_beam == null) _beam = LootBeam.Create(transform, color);
            else _beam.SetColor(color);
            _beam.gameObject.SetActive(true);
        }

        private Color BeamColor(ContainerDef def)
        {
            if (!string.IsNullOrEmpty(def.BeamColor)) return ColorUtil.Parse(def.BeamColor, Color.white);
            string best = null;
            void Consider(string rarity)
            {
                if (rarity != null && (best == null || _db.RarityOrder(rarity) > _db.RarityOrder(best))) best = rarity;
            }
            var state = _service?.Find(Key);
            if (state != null) foreach (var it in state.Slots) Consider(it?.Rarity);
            else foreach (var it in def.Items) Consider(it.Rarity);
            return best != null ? _db.RarityColor(best) : new Color(1f, 0.82f, 0.3f);
        }

        // ---------------------------------------------------------------- IInteractable

        public string HoverText
        {
            get
            {
                var def = Def;
                if (def == null) return $"<color=#FF7070>Нет контейнера \"{ContainerId}\"</color>";
                return Empty ? $"{def.Name}\n<color=#8A8478>пусто</color>" : def.Name;
            }
        }

        public string ActionText => Def?.Action ?? "Открыть";
        public Vector3 InteractPosition => InteractPoint != null ? InteractPoint.position : transform.position;
        public float InteractRange => Range;

        public bool CanInteract
        {
            get
            {
                if (this == null || !isActiveAndEnabled || _service == null || ZoneService.Busy) return false;
                var def = Def;
                if (def == null) return false;
                return !(Empty && def.WhenEmpty != ContainerEmptyMode.Keep);
            }
        }

        public void Interact(Actor player)
        {
            if (_service == null) return;
            int zoneLevel = Services.TryGet<ZoneService>(out var zones) ? zones.CurrentZone?.Level ?? 1 : 1;
            _service.Open(this, zoneLevel);
        }
    }
}
