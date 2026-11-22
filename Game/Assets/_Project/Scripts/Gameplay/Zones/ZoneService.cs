using System.Collections;
using Game.Core;
using Game.Data;
using Game.Gameplay.Actors;
using Game.Gameplay.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Gameplay.Zones
{
    /// <summary>
    /// Переходы между зонами в стиле Path of Exile:
    /// затемнение → асинхронная загрузка сцены под экраном загрузки → игрок ставится на ZoneEntrance → проявление.
    ///
    /// Почему ничего не теряется: сервисы (инвентарь, квесты, профиль) — обычные C#-объекты, сцены они не видят;
    /// объекты [Game], [UI] и сам игрок помечены DontDestroyOnLoad и переживают смену сцены.
    /// Из старой сцены уничтожается всё остальное: мобы, лут на земле, камера, свет.
    /// Экран загрузки (UI/LoadingScreenView) только рисует состояние отсюда: <see cref="ScreenAlpha"/>, <see cref="Progress"/>.
    /// </summary>
    public sealed class ZoneService : MonoBehaviour
    {
        /// <summary>Сколько длится затемнение/проявление.</summary>
        public float FadeSeconds = 0.35f;
        /// <summary>Минимум времени на экране загрузки (иначе маленькая сцена мелькнёт за кадр).</summary>
        public float MinLoadingSeconds = 0.9f;

        public static bool Busy { get; private set; }

        public bool IsLoading => Busy;
        /// <summary>Непрозрачность экрана загрузки 0..1.</summary>
        public float ScreenAlpha { get; private set; }
        /// <summary>Прогресс загрузки 0..1.</summary>
        public float Progress { get; private set; }
        /// <summary>Куда грузимся (для экрана загрузки).</summary>
        public ZoneDef Destination { get; private set; }
        public string Tip { get; private set; }

        public string CurrentZoneId { get; private set; }
        public ZoneDef CurrentZone => Db.Zone(CurrentZoneId);

        private static ContentDatabase Db => Services.Get<ContentDatabase>();

        /// <summary>Вызывается при старте игры: определяем, в какой зоне запустились (по имени открытой сцены).</summary>
        public void Init()
        {
            var scene = SceneManager.GetActiveScene().name;
            CurrentZoneId = Db.ZoneByScene(scene)?.Id;
            if (CurrentZoneId == null)
                Log.Info("Zones", $"Сцена \"{scene}\" не описана в zones/*.json — переходы работают, но текущая зона неизвестна");
        }

        /// <summary>Перейти в зону. entranceId — Id ZoneEntrance в целевой сцене (пусто = PlayerSpawnPoint).</summary>
        public bool Travel(string zoneId, string entranceId = null)
        {
            if (Busy) return false;
            var def = Db.Zone(zoneId);
            if (def == null)
            {
                Fail($"Нет зоны \"{zoneId}\" в zones/*.json");
                return false;
            }
            if (!Application.CanStreamedLevelBeLoaded(def.Scene))
            {
                Fail($"Сцена \"{def.Scene}\" не добавлена в Build Settings. В редакторе: Game → Зоны → Обновить список сцен");
                return false;
            }
            StartCoroutine(TravelRoutine(def, entranceId));
            return true;
        }

        private static void Fail(string message)
        {
            Log.Error("Zones", message);
            EventBus.Publish(new UiMessageEvent(message, new Color(1f, 0.45f, 0.4f)));
        }

        private IEnumerator TravelRoutine(ZoneDef def, string entranceId)
        {
            Busy = true;
            GameInput.LoadingActive = true;
            Destination = def;
            Progress = 0f;
            Tip = def.Tips != null && def.Tips.Count > 0 ? def.Tips[Random.Range(0, def.Tips.Count)] : null;
            string from = CurrentZoneId;
            EventBus.Publish(new ZoneLeavingEvent(from, def.Id));
            Log.Info("Zones", $"Переход: {from ?? "?"} → {def.Id} (сцена {def.Scene}, вход {(string.IsNullOrEmpty(entranceId) ? "PlayerSpawnPoint" : entranceId)})");

            yield return Fade(1f);

            // 1) грузим сцену в фоне, но не активируем, пока не пройдёт минимальное время
            float started = Time.unscaledTime;
            var op = SceneManager.LoadSceneAsync(def.Scene, LoadSceneMode.Single);
            op.allowSceneActivation = false;
            while (op.progress < 0.9f || Time.unscaledTime - started < MinLoadingSeconds)
            {
                // Unity останавливает progress на 0.9, пока allowSceneActivation = false
                float loadPart = Mathf.Clamp01(op.progress / 0.9f);
                float timePart = Mathf.Clamp01((Time.unscaledTime - started) / MinLoadingSeconds);
                Progress = Mathf.Min(loadPart, timePart) * 0.95f;
                yield return null;
            }

            // 2) активируем: старая сцена выгружается, у новой срабатывают Awake/OnEnable
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;
            yield return null; // кадр на Start() объектов новой сцены
            Progress = 1f;

            CurrentZoneId = def.Id;
            PlacePlayer(entranceId);
            EventBus.Publish(new ZoneEnteredEvent(def.Id, from));

            yield return null;
            yield return Fade(0f);

            Destination = null;
            GameInput.LoadingActive = false;
            Busy = false;
            EventBus.Publish(new UiMessageEvent(def.Name, new Color(0.8f, 0.88f, 1f)));
        }

        private IEnumerator Fade(float target)
        {
            float start = ScreenAlpha;
            float t = 0f;
            while (t < FadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                ScreenAlpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / FadeSeconds));
                yield return null;
            }
            ScreenAlpha = target;
        }

        /// <summary>Ставит игрока на вход (или PlayerSpawnPoint) и привязывает к нему камеру новой сцены.</summary>
        public static void PlacePlayer(string entranceId)
        {
            var player = GameWorld.Player;
            if (player == null) return;

            Transform point = null;
            var entrance = ZoneEntrance.Find(entranceId);
            if (entrance != null) point = entrance.transform;
            else
            {
                if (!string.IsNullOrEmpty(entranceId))
                    Log.Warn("Zones", $"В сцене нет ZoneEntrance с Id \"{entranceId}\" — ставлю на PlayerSpawnPoint");
                if (PlayerSpawnPoint.Current != null) point = PlayerSpawnPoint.Current.transform;
            }

            Vector3 pos = GameWorld.SnapToGround(point != null ? point.position : Vector3.zero);
            Quaternion rot = point != null ? Quaternion.Euler(0f, point.eulerAngles.y, 0f) : Quaternion.identity;

            var mover = player.GetComponent<Mover>();
            if (mover != null) mover.Teleport(pos);
            else player.transform.position = pos;
            player.transform.rotation = rot;

            var character = player.GetComponent<PlayerCharacter>();
            if (character != null) character.RespawnPoint = pos;

            // камера принадлежит сцене (у каждой сцены свой фон/пост-эффекты), поэтому цепляем риг заново
            CameraRig.Create(player.transform);
        }

        private void OnDestroy()
        {
            Busy = false;
            GameInput.LoadingActive = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Busy = false;
            GameInput.LoadingActive = false;
        }
    }
}
