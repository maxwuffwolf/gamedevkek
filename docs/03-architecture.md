# 03. Архитектура кода

## Цели архитектуры

- Добавлять контент **без кода** (JSON + префабы).
- Каждую систему можно **запустить и проверить отдельно** (сцена-песочница).
- **Быстрая компиляция** за счёт assembly definitions.
- Простота важнее «правильности»: никаких DI-фреймворков, пока не станет больно. Сервис-локатор + события.

## Структура папок `Game/Assets/`

```
Assets/
├── _Project/                    всё наше; сторонние ассеты живут вне этой папки
│   ├── Scripts/
│   │   ├── Core/                Game.asmdef-независимое ядро: сервисы, события, утилиты, RNG
│   │   ├── Data/                модели данных (POCO), загрузка JSON, реестр контента
│   │   ├── Gameplay/
│   │   │   ├── Stats/           статы, модификаторы, формулы урона
│   │   │   ├── Combat/          хитбоксы, урон, скиллы, снаряды, статусы
│   │   │   ├── Actors/          игрок, мобы, компоненты здоровья/движения
│   │   │   ├── AI/              поведение мобов (стейт-машины)
│   │   │   ├── Items/           генерация предметов, аффиксы, лут-таблицы, инвентарь
│   │   │   └── World/           зоны, генерация данжей, спавнеры, порталы
│   │   ├── Meta/                сохранения, тайник, прогрессия между забегами
│   │   ├── UI/                  HUD, инвентарь, тултипы, цифры урона
│   │   └── DevTools/            консоль, чит-меню, оверлеи, hot reload (только Editor/Dev build)
│   ├── Content/                 ScriptableObjects: визуальные «скины» контента (префабы, иконки, VFX)
│   ├── Art/                     модели, материалы, текстуры, анимации (наши)
│   ├── Prefabs/                 префабы акторов, комнат, предметов на земле, VFX
│   ├── Scenes/
│   │   ├── Boot.unity           инициализация сервисов, загрузка данных
│   │   ├── Hub.unity
│   │   ├── Zones/               открытые зоны
│   │   ├── Dungeon.unity        пустая сцена-контейнер для генератора
│   │   └── Sandbox/             тестовые сцены (бой, лут, генерация)
│   └── Settings/                URP-ассеты, Input Actions, Volume профили
├── StreamingAssets/
│   └── Data/                    JSON-контент (читается в рантайме, hot reload)
│       ├── items/
│       ├── affixes/
│       ├── monsters/
│       ├── loot_tables/
│       ├── zones/
│       ├── dungeons/
│       └── skills/
└── ThirdParty/                  импортированные паки ассетов (как есть, не правим)
```

**Почему JSON в `StreamingAssets`, а не в `Resources`:** файлы остаются обычными файлами и в билде, их можно править и перезагружать на лету без пересборки. Это ключ к «свободному тестированию».

## Assembly Definitions

```
Game.Core          (ничего не зависит)
Game.Data          → Core
Game.Gameplay      → Core, Data
Game.Meta          → Core, Data, Gameplay
Game.UI            → Core, Data, Gameplay, Meta
Game.DevTools      → всё выше   [Define Constraint: UNITY_EDITOR || DEVELOPMENT_BUILD]
Game.Editor        → всё выше   [Editor only]
```

Правило: зависимости только «вниз». Если `Gameplay` нужно что-то сказать `UI`, то через событие, а не прямой вызов.

## Ключевые паттерны

### 1. Сервисы (простой локатор)

```csharp
public static class Services
{
    private static readonly Dictionary<Type, object> _map = new();
    public static void Register<T>(T service) where T : class => _map[typeof(T)] = service;
    public static T Get<T>() where T : class => (T)_map[typeof(T)];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => _map.Clear(); // нужно при отключённом Domain Reload
}
```

Сервисы: `ContentDatabase`, `SaveService`, `LootService`, `RngService`, `EventBus`, `DevConsole`.
Регистрируются в сцене `Boot`. Для запуска любой сцены напрямую из редактора используем
`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`-бутстрап, который поднимает сервисы, если их ещё нет.
Так можно жать Play в `Sandbox/Combat.unity` и всё работает.

### 2. Шина событий

```csharp
public readonly struct DamageDealtEvent { public readonly Actor Source, Target; public readonly DamageResult Result; }
EventBus.Publish(new DamageDealtEvent(...));
EventBus.Subscribe<DamageDealtEvent>(OnDamage);
```

Используется для: цифр урона, звуков, ачивок, статистики, логов отладки. Системы не знают друг о друге.

### 3. Данные: определения vs экземпляры

| Слой | Где | Пример |
|---|---|---|
| **Definition** (статичное, из JSON) | `Game.Data` | `ItemBaseDef { id: "sword_rusty", slot: "MainHand", damage: [3,6] }` |
| **Visual** (ScriptableObject / префаб) | `Content/` | `ItemVisual { id: "sword_rusty", icon, worldPrefab }` |
| **Instance** (рантайм, сохраняется) | `Game.Gameplay` / сейв | `ItemInstance { baseId, rarity, affixes[{id, tier, value}], ilvl, seed }` |

- JSON отвечает за **цифры и логику**, SO/префабы за **внешний вид**. Связь по строковому `id`.
- Если визуала для `id` нет, подставляется заглушка (серый куб / иконка «?»). Значит можно добавить предмет одним JSON и сразу его выбить.
- Сейвы хранят только `id` + сгенерированные значения, поэтому ребаланс базы не ломает сохранения.

### 4. ContentDatabase

```csharp
public sealed class ContentDatabase
{
    public IReadOnlyDictionary<string, ItemBaseDef> Items { get; }
    public IReadOnlyDictionary<string, AffixDef> Affixes { get; }
    public IReadOnlyDictionary<string, MonsterDef> Monsters { get; }
    public IReadOnlyDictionary<string, LootTableDef> LootTables { get; }
    // ...
    public void LoadAll(string rootPath);        // читает все *.json из StreamingAssets/Data
    public void Reload();                        // hot reload, кидает ContentReloadedEvent
    public IReadOnlyList<string> Validate();     // ссылки на несуществующие id, пустые таблицы и т.д.
}
```

Все JSON-файлы в папке типа сливаются: можно держать один `items/swords.json` или 50 файлов, без разницы.

### 5. Статы и модификаторы

Классическая схема ARPG:

```
итог = (база + Σ flat) × (1 + Σ increased%) × Π (1 + more%)
```

```csharp
public enum StatId { MaxHealth, Armor, MoveSpeed, AttackSpeed, PhysDamage, CritChance, CritMulti, ... }
public enum ModType { Flat, Increased, More }
public readonly struct StatModifier { StatId Stat; ModType Type; float Value; object Source; }
```

`StatSheet` на акторе пересчитывает значения лениво (dirty-флаг) при добавлении/снятии модификатора.
Предметы, баффы, пассивки, модификаторы забега — всё это просто источники `StatModifier`.
Аффиксы в JSON ссылаются на `StatId` строкой, т.е. новый аффикс на существующий стат = только JSON.

### 6. Бой

- **Скилл** = данные (`skills/*.json`: кулдаун, стоимость, множитель урона, тип доставки) + **поведение** (C#-класс по ключу `behavior`: `Melee`, `Projectile`, `AoE`, `Dash`).
- Урон считается в одном месте: `DamageCalculator.Compute(attackerStats, defenderStats, hit)` (чистая функция, легко дебажить через консоль).
- Хитбоксы через `Physics.OverlapSphere/Box` в нужный кадр анимации (Animation Event), не через постоянные триггер-коллайдеры.
- «Сочность» отдельным слоем: `HitFeedback` слушает `DamageDealtEvent` и делает hit-stop, тряску, вспышку, цифру.

### 7. ИИ мобов

Простая конечная машина состояний: `Idle → Chase → Attack → (Recover) → Chase`, плюс `Dead`.
Параметры (дистанция агро, атаки, скорость) берутся из `MonsterDef`. Поведение выбирается по ключу `ai: "melee_basic" | "ranged_kiter" | "charger"`.
Behaviour Tree подключаем только если FSM станет тесно (скорее всего для боссов).

### 8. Генерация данжей

Подход «комнаты-префабы + граф»:

1. Генерируется граф комнат (старт → N комнат → босс, с ответвлениями) по seed.
2. Для каждого узла выбирается префаб комнаты нужного типа (`combat`, `treasure`, `elite`, `boss`) и размера, со стыковкой по «дверям» (сокетам).
3. Коридоры между дверями (или комнаты стыкуются напрямую).
4. Рантайм-запекание NavMesh (`NavMeshSurface.BuildNavMesh()`).
5. Спавнеры в комнатах берут пачки мобов из `dungeons/*.json` с учётом тира и модификаторов.

Всё детерминировано от `seed` → баг в генерации воспроизводится командой `dungeon.generate <seed>`.

### 9. Случайность

Один `RngService` с именованными потоками (`loot`, `dungeon`, `combat`), каждый из которых `System.Random` (или xorshift) с сидом.
Никаких `UnityEngine.Random` в геймплее, иначе нельзя воспроизвести результат.

### 10. Сохранения

- Формат: JSON (Newtonsoft) в `Application.persistentDataPath/saves/`.
- Содержимое: персонаж (уровень, опыт, надетое), инвентарь, тайник, валюты, открытые тиры, настройки.
- Поле `version` + цепочка миграций `SaveMigrator` (v1 → v2 → …).
- Автосейв при переходе между сценами и при выходе; бэкап предыдущего файла (`.bak`).

## Конвенции кода

- Namespace = assembly: `Game.Gameplay.Combat`.
- Приватные поля `_camelCase`, сериализуемые `[SerializeField] private`.
- Без `FindObjectOfType` и `GameObject.Find` в рантайме (только в DevTools).
- Без логики в `Update` там, где хватит событий или таймеров.
- `Debug.Log` только через свою обёртку `Log.Info/Warn/Error(category, msg)`, чтобы фильтровать по категориям в консоли.
