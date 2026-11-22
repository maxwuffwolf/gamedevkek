using System.Collections.Generic;

// Определения контента. Каждый класс 1:1 соответствует объекту в JSON из StreamingAssets/Data.
// Имена полей в JSON в camelCase (id, levelReq, ...), Newtonsoft сопоставляет их без учёта регистра.

namespace Game.Data
{
    public interface IContentDef
    {
        string Id { get; }
    }

    /// <summary>Внешний вид-заглушка, пока нет настоящей модели/иконки.</summary>
    public sealed class PlaceholderDef
    {
        /// <summary>humanoid | wolf | npc | blob</summary>
        public string Model = "blob";
        public string Color = "#888888";
        public string Accent = "#444444";
        public float Scale = 1f;
    }

    /// <summary>Иконка-заглушка: цветной квадрат с короткой подписью.</summary>
    public sealed class IconDef
    {
        public string Color = "#666666";
        public string Text = "?";
    }

    // ---------------------------------------------------------------- Редкость

    public sealed class RarityDef : IContentDef
    {
        public string Id;
        public string Name;
        public string Color = "#C8C8C8";
        /// <summary>Порядок: чем выше, тем ценнее. Луч лута берёт цвет самой высокой редкости.</summary>
        public int Order;
        public IntRange Affixes;
        /// <summary>Множитель к имплицитным статам базы.</summary>
        public float ImplicitMultiplier = 1f;
        string IContentDef.Id => Id;
    }

    // ---------------------------------------------------------------- Предметы

    public enum ItemSlot
    {
        Head,
        Neck,
        Shoulders,
        Chest,
        Hands,
        Waist,
        Legs,
        Feet,
        MainHand,
        OffHand,
        Ring,
    }

    public sealed class ItemBaseDef : IContentDef
    {
        public string Id;
        public string Name;
        public ItemSlot Slot;
        public string Category;
        public int LevelReq = 1;
        public int DropLevel = 1;
        public List<StatModDef> Implicit = new();
        public List<string> Tags = new();
        public IconDef Icon = new();
        public string Description;
        public bool Deprecated;
        string IContentDef.Id => Id;
    }

    public enum AffixKind
    {
        Prefix,
        Suffix,
    }

    public sealed class AffixTierDef
    {
        public int Tier = 1;
        public int Ilvl = 1;
        public int Weight = 100;
        public string Name;
        public List<StatModDef> Mods = new();
    }

    public sealed class AffixDef : IContentDef
    {
        public string Id;
        public AffixKind Kind;
        /// <summary>На предмете не бывает двух аффиксов одной группы.</summary>
        public string Group;
        public List<string> AllowedTags = new();
        public List<AffixTierDef> Tiers = new();
        string IContentDef.Id => Id;
    }

    // ---------------------------------------------------------------- Лут

    public enum LootEntryType
    {
        Item,
        ItemByTag,
        Table,
        Currency,
    }

    public sealed class LootEntryDef
    {
        public LootEntryType Type;
        public string Id;
        public List<string> Tags = new();
        public IntRange Amount = new(1);
        public int Weight = 100;
        /// <summary>Если задано, предмет всегда этой редкости.</summary>
        public string Rarity;
    }

    public sealed class LootTableDef : IContentDef
    {
        public string Id;
        public IntRange Rolls = new(1);
        public int NoDropWeight;
        public List<LootEntryDef> Entries = new();
        public Dictionary<string, int> RarityWeights = new();
        string IContentDef.Id => Id;
    }

    // ---------------------------------------------------------------- Навыки

    public enum SkillShapeType
    {
        Arc,
        Circle,
    }

    public sealed class SkillShapeDef
    {
        public SkillShapeType Type = SkillShapeType.Arc;
        public float Radius = 2f;
        public float Angle = 120f;
    }

    public sealed class SkillDef : IContentDef
    {
        public string Id;
        public string Name;
        public string Description;
        /// <summary>Ключ C#-поведения: Melee, ... (новое поведение = класс с [SkillBehavior("Key")]).</summary>
        public string Behavior = "Melee";
        public float Cooldown;
        public Dictionary<string, float> Cost = new();
        public float DamageMultiplier = 1f;
        public SkillShapeDef Shape = new();
        /// <summary>Длительность каста в секундах (при скорости атаки 1.0).</summary>
        public float Duration = 0.5f;
        /// <summary>Момент удара, доля от длительности (0..1).</summary>
        public float HitFrame = 0.5f;
        public bool UsesAttackSpeed = true;
        /// <summary>Множитель скорости движения во время каста.</summary>
        public float MoveWhileCasting = 0.4f;
        public float Knockback;
        public float ScreenShake;
        public string VfxColor = "#FFFFFF";
        /// <summary>Имя триггера в Animator (если у модели есть аниматор).</summary>
        public string Animation = "Attack";
        public IconDef Icon = new();
        public List<string> Tags = new();
        string IContentDef.Id => Id;

        public float ManaCost => Cost != null && Cost.TryGetValue("Mana", out var m) ? m : 0f;
    }

    // ---------------------------------------------------------------- Мобы

    public sealed class MonsterDef : IContentDef
    {
        public string Id;
        public string Name;
        /// <summary>Ключ визуала (ActorVisual). Если не найден, строится заглушка из Placeholder.</summary>
        public string Prefab;
        public string Ai = "melee_basic";
        public int BaseLevel = 1;
        public Dictionary<StatId, float> Stats = new();
        /// <summary>Прирост стата за уровень выше BaseLevel (0.12 = +12%).</summary>
        public Dictionary<StatId, float> Scaling = new();
        public List<string> Skills = new();
        public float AggroRange = 8f;
        public float LeashRange = 18f;
        public float AttackRange = 1.6f;
        public float WanderRadius = 4f;
        public int Xp = 10;
        public string LootTable;
        public List<string> Tags = new();
        public PlaceholderDef Placeholder = new();
        public float CorpseSeconds = 2.5f;
        string IContentDef.Id => Id;
    }

    // ---------------------------------------------------------------- Персонаж

    public sealed class XpCurveDef
    {
        public int Base = 100;
        public float Growth = 1.5f;
    }

    public sealed class BackpackDef
    {
        public int Size = 40;
        public int Unlocked = 12;
    }

    public sealed class StartingItemDef
    {
        public string Id;
        public string Rarity = "common";
        public bool Equip;
    }

    public sealed class CharacterDef : IContentDef
    {
        public string Id;
        public string Name;
        public string Prefab;
        public Dictionary<StatId, float> Stats = new();
        public Dictionary<StatId, float> PerLevel = new();
        public string BasicAttack;
        public List<string> SkillBar = new();
        public BackpackDef Backpack = new();
        public List<StartingItemDef> StartingItems = new();
        public XpCurveDef XpCurve = new();
        public PlaceholderDef Placeholder = new();
        public float RespawnSeconds = 4f;
        /// <summary>Внешний вид настоящей модели (цвета, грязь, кристалл наруча, оружие в руке). Hot reload (F5).</summary>
        public AppearanceDef Appearance = new();
        string IContentDef.Id => Id;
    }

    public sealed class AppearanceDef
    {
        public string HairColor = "#FFD26A";
        public string BrowColor = "#C08A3A";
        public string SkinTint = "#FFFFFF";
        /// <summary>Правый глаз (Свет) и левый (Тьма), сила лёгкого свечения радужки.</summary>
        public string EyeRight = "#FFB52E";
        public string EyeLeft = "#6A2FC0";
        public float EyeGlow = 0.6f;

        /// <summary>Грязь/пепел/царапины на коже в начале (0..1) и сколько секунд длится умывание.</summary>
        public float DirtStart = 1f;
        public float WashSeconds = 3f;
        public string DirtColor = "#5C5752";

        /// <summary>Кристалл наруча: 0 — вся сила у Тьмы, 1 — у Света.</summary>
        public float Balance = 0.5f;
        public float CrystalGlow = 1.6f;
        public string CrystalLight = "#FFB838";
        public string CrystalDark = "#8C3CFF";
        /// <summary>С какого расстояния наруч тянет энергию из убитых врагов (0 — выключено).</summary>
        public float AbsorbRadius = 14f;

        /// <summary>id предмета → имя модели оружия внутри Hero.fbx. Оружие без модели показывает DefaultWeapon.</summary>
        public Dictionary<string, string> Weapons = new();
        public string DefaultWeapon;
    }

    // ---------------------------------------------------------------- NPC, квесты, диалоги

    public sealed class NpcDef : IContentDef
    {
        public string Id;
        public string Name;
        public string Title;
        public string Prefab;
        public string Dialogue;
        public PlaceholderDef Placeholder = new();
        string IContentDef.Id => Id;
    }

    public enum ObjectiveType
    {
        Kill,
    }

    public sealed class QuestObjectiveDef
    {
        public ObjectiveType Type = ObjectiveType.Kill;
        /// <summary>Конкретный моб по id...</summary>
        public string Monster;
        /// <summary>...или любой моб с тегом.</summary>
        public string Tag;
        public int Count = 1;
        public string Text;
    }

    public sealed class QuestRewardItemDef
    {
        public string Id;
        public string Rarity = "uncommon";
        public int Ilvl;
    }

    public sealed class QuestRewardDef
    {
        public int Xp;
        public int Gold;
        public List<QuestRewardItemDef> Items = new();
        /// <summary>Сколько ячеек рюкзака открыть.</summary>
        public int BackpackSlots;
    }

    public sealed class QuestDef : IContentDef
    {
        public string Id;
        public string Name;
        public string Description;
        public string Giver;
        /// <summary>Кому сдавать. Пусто = тому же, кто выдал.</summary>
        public string TurnIn;
        public List<string> Prerequisites = new();
        public List<QuestObjectiveDef> Objectives = new();
        public QuestRewardDef Rewards = new();
        public string ReadyText;
        string IContentDef.Id => Id;

        public string TurnInNpc => string.IsNullOrEmpty(TurnIn) ? Giver : TurnIn;
    }

    public sealed class DialogueStartDef
    {
        /// <summary>Условие (см. DialogueConditions). Пусто = всегда.</summary>
        public string If;
        public string Node;
    }

    public sealed class DialogueOptionDef
    {
        public string Text;
        public string Next;
        public string If;
        public List<string> Actions = new();
    }

    public sealed class DialogueNodeDef
    {
        public string Speaker;
        public string Text;
        public List<DialogueOptionDef> Options = new();
    }

    public sealed class DialogueDef : IContentDef
    {
        public string Id;
        public List<DialogueStartDef> Start = new();
        public Dictionary<string, DialogueNodeDef> Nodes = new();
        string IContentDef.Id => Id;
    }

    /// <summary>
    /// Зона (локация) = одна сцена Unity. Переходы между зонами — ZonePortal на сцене, точки прибытия — ZoneEntrance.
    /// </summary>
    public sealed class ZoneDef : IContentDef
    {
        public string Id;
        public string Name;
        /// <summary>Строка под названием на экране загрузки.</summary>
        public string Subtitle;
        /// <summary>Имя файла сцены без .unity (сцена должна быть в Build Settings: Game → Зоны → Обновить список сцен).</summary>
        public string Scene;
        /// <summary>Рекомендуемый уровень (пока только для экрана загрузки).</summary>
        public int Level = 1;
        /// <summary>С этой зоны начинается игра: в билде её сцена идёт первой.</summary>
        public bool Start;
        /// <summary>Цвет фона экрана загрузки.</summary>
        public string LoadingColor = "#0B0D16";
        /// <summary>Подсказки на экране загрузки (показывается случайная).</summary>
        public List<string> Tips = new();
        string IContentDef.Id => Id;
    }

    // ------------------------------------------------------------------ контейнеры (сундуки, трупы, тайники)

    /// <summary>Что делать с объектом в мире, когда контейнер опустел.</summary>
    public enum ContainerEmptyMode
    {
        /// <summary>Остаётся, его можно открыть пустым и положить что-то внутрь (сундук).</summary>
        Keep,
        /// <summary>Перестаёт подсвечиваться и кликаться, модель остаётся (обысканный труп).</summary>
        Disable,
        /// <summary>Модель прячется (меч забрали — его больше нет в земле).</summary>
        Hide,
    }

    /// <summary>Гарантированный предмет в контейнере.</summary>
    public sealed class ContainerItemDef
    {
        public string Id;
        public string Rarity = "common";
        public IntRange Amount = new(1);
        /// <summary>Шанс 0..1, что предмет вообще будет.</summary>
        public float Chance = 1f;
    }

    /// <summary>
    /// Шаблон контейнера: в сцене ставится WorldContainer с этим id, содержимое генерируется
    /// при первом открытии (items + бросок lootTable + золото) и дальше хранится в сейве.
    /// </summary>
    public sealed class ContainerDef : IContentDef
    {
        public string Id;
        /// <summary>Имя при наведении («Ржавый меч», «Старый сундук»).</summary>
        public string Name;
        /// <summary>Заголовок окна (по умолчанию = Name).</summary>
        public string Title;
        /// <summary>Подсказка действия: «Осмотреть», «Открыть».</summary>
        public string Action = "Открыть";
        /// <summary>Строка-описание в окне (лор, подсказка).</summary>
        public string Description;
        /// <summary>Сколько ячеек в окне.</summary>
        public int Slots = 8;
        public List<ContainerItemDef> Items = new();
        /// <summary>Лут-таблица (необязательно) — бросается вдобавок к items.</summary>
        public string LootTable;
        /// <summary>Уровень предметов для лут-таблицы. 0 = уровень зоны.</summary>
        public int ItemLevel;
        public IntRange Gold = new(0);
        public ContainerEmptyMode WhenEmpty = ContainerEmptyMode.Keep;
        /// <summary>false = содержимое не сохраняется и генерируется заново при каждом заходе в зону.</summary>
        public bool Persistent = true;
        /// <summary>Можно ли класть свои вещи (сундук-хранилище). Для трупов обычно false.</summary>
        public bool AllowPut;

        string IContentDef.Id => Id;
    }
}
