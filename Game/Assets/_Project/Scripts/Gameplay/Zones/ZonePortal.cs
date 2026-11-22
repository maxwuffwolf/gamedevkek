using System.Collections;
using Game.Core;
using Game.Data;
using Game.Gameplay.Actors;
using UnityEngine;

namespace Game.Gameplay.Zones
{
    /// <summary>
    /// Объект-переход (дверь, арка, проход в пещеру): клик → экран загрузки → другая зона.
    /// Как поставить руками: пустой объект (или модель арки) → Add Component → ZonePortal,
    /// вписать <see cref="TargetZone"/> (id из zones/*.json) и <see cref="TargetEntrance"/> (Id ZoneEntrance в той сцене).
    /// Коллайдер-триггер для наведения мыши добавится сам.
    /// </summary>
    public sealed class ZonePortal : MonoBehaviour, IInteractable
    {
        [Tooltip("id зоны из StreamingAssets/Data/zones/*.json")]
        public string TargetZone = "forest";

        [Tooltip("Id ZoneEntrance в целевой сцене. Пусто = PlayerSpawnPoint той сцены")]
        public string TargetEntrance = "";

        [Tooltip("Подсказка действия при наведении")]
        public string ActionLabel = "Перейти";

        [Min(0.5f)] public float Range = 3f;

        [Tooltip("Подпись с названием зоны над порталом")]
        public bool ShowLabel = true;
        public float LabelHeight = 4.4f;

        private WorldLabel _label;

        private ZoneDef Target => Services.TryGet<ContentDatabase>(out var db) ? db.Zone(TargetZone) : null;

        private void Reset()
        {
            if (GetComponent<Collider>() != null) return;
            var box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 1.75f, 0f);
            box.size = new Vector3(3.5f, 3.5f, 1.5f);
        }

        private IEnumerator Start()
        {
            while (!GameState.IsReady) yield return null;
            if (Target == null) Log.Warn("Zones", $"Портал \"{name}\": нет зоны \"{TargetZone}\" в zones/*.json");
            if (ShowLabel)
                _label = WorldLabel.Create(transform, new Vector3(0f, LabelHeight, 0f), LabelText, new Color(0.75f, 0.85f, 1f), 0.4f);
            EventBus.Subscribe<ContentReloadedEvent>(OnContentReloaded);
        }

        private void OnDestroy() => EventBus.Unsubscribe<ContentReloadedEvent>(OnContentReloaded);

        private void OnContentReloaded(ContentReloadedEvent e)
        {
            if (_label != null) _label.Text.text = LabelText;
        }

        private string LabelText => Target?.Name ?? TargetZone;

        public string HoverText
        {
            get
            {
                var z = Target;
                if (z == null) return $"<color=#FF7070>Нет зоны \"{TargetZone}\"</color>";
                return z.Level > 1 ? $"{z.Name}\n<color=#A8A8B8>Уровень зоны: {z.Level}</color>" : z.Name;
            }
        }

        public string ActionText => ActionLabel;
        public Vector3 InteractPosition => transform.position;
        public float InteractRange => Range;
        public bool CanInteract => this != null && isActiveAndEnabled && !ZoneService.Busy;

        public void Interact(Actor player)
        {
            if (Services.TryGet<ZoneService>(out var zones)) zones.Travel(TargetZone, TargetEntrance);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.7f, 0.4f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.5f, Range);
        }
    }
}
