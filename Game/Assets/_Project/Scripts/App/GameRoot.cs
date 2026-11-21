using Game.Core;
using Game.Data;
using Game.Gameplay.Actors;
using Game.Gameplay.Dialogue;
using Game.Gameplay.Items;
using Game.Gameplay.Progression;
using Game.Gameplay.Quests;
using Game.Gameplay.World;
using Game.Meta;
using Game.UI;
using UnityEngine;

namespace Game.App
{
    /// <summary>
    /// Авто-бутстрап: срабатывает при запуске ЛЮБОЙ сцены (Play в редакторе или билд).
    /// Загружает JSON-контент, поднимает сервисы, сейв, создаёт игрока на PlayerSpawnPoint, камеру и UI.
    /// Чтобы сцена запускалась без игры (меню и т.п.), положи на неё объект с компонентом NoAutoBoot.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        public static GameRoot Instance { get; private set; }

        private SaveService _save;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBoot()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<NoAutoBoot>() != null) return;
            var go = new GameObject("[Game]");
            DontDestroyOnLoad(go);
            go.AddComponent<GameRoot>().Boot();
        }

        private void Boot()
        {
            Instance = this;

            // --- данные
            var db = new ContentDatabase();
            db.LoadAll(ContentDatabase.DefaultRoot);
            foreach (var e in db.Errors) Log.Error("Content", e);
            foreach (var w in db.Warnings) Log.Warn("Content", w);
            Log.Info("Content", $"Загружено: предметов {db.Items.Count}, мобов {db.Monsters.Count}, навыков {db.Skills.Count}, квестов {db.Quests.Count}, ошибок {db.Errors.Count}");
            Services.Register(db);
            var rng = new RngService(System.Environment.TickCount);
            Services.Register(rng);
            Services.Register(new VisualRegistry());

            // --- геймплейные сервисы
            var profile = new PlayerProfile(db);
            Services.Register(profile);
            var charDef = profile.Character;
            if (charDef == null) Log.Error("Boot", "Нет персонажа \"hero\" в characters/*.json — игрок не будет создан");

            var inventory = new Inventory(db, charDef?.Backpack.Size ?? 40, charDef?.Backpack.Unlocked ?? 12);
            inventory.PlayerLevelProvider = () => profile.Level;
            Services.Register(inventory);
            var generator = new ItemGenerator(db, rng);
            Services.Register(generator);
            Services.Register(new LootService(db, generator, rng));
            var quests = new QuestService(db);
            Services.Register(quests);
            Services.Register(new DialogueService(db, quests));
            var skillBar = new SkillBar();
            skillBar.Fill(charDef?.SkillBar);
            Services.Register(skillBar);
            EventBus.Subscribe<ContentReloadedEvent>(_ => skillBar.Fill(profile.Character?.SkillBar));

            // --- сохранение
            _save = new SaveService(db);
            Services.Register(_save);
            if (_save.TryLoad(out var data)) _save.Apply(data);
            else _save.ApplyNewGame();

            // --- мир
            SpawnPlayer(charDef);

            // --- интерфейс
            UIRoot.Create();

            GameState.SetReady();
            Log.Info("Boot", $"Игра готова. Сейв: {_save.FilePath}");
        }

        private static void SpawnPlayer(CharacterDef charDef)
        {
            if (charDef == null) return;
            var spawn = PlayerSpawnPoint.Current;
            Vector3 pos = spawn != null ? spawn.transform.position : Vector3.zero;
            Quaternion rot = spawn != null ? spawn.transform.rotation : Quaternion.identity;

            // пустая сцена без земли → создаём временный пол, чтобы было на чём стоять
            if (!Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, 50f, ~0, QueryTriggerInteraction.Ignore))
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "FallbackGround";
                ground.transform.position = new Vector3(pos.x, 0f, pos.z);
                ground.transform.localScale = new Vector3(10f, 1f, 10f);
                ground.GetComponent<Renderer>().sharedMaterial = MaterialFactory.Lit(new Color(0.35f, 0.5f, 0.3f));
                pos.y = 0f;
            }

            var player = ActorFactory.SpawnPlayer(charDef, GameWorld.SnapToGround(pos), rot);
            var pc = player.GetComponent<PlayerCharacter>();
            pc.RespawnPoint = player.transform.position;
            CameraRig.Create(player.transform);
        }

        private void Update() => _save?.Tick();

        private void OnApplicationQuit() => _save?.Save();

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;
    }
}
