# 09. Каркас игры: как он устроен и как расширять

Это то, что реально лежит в `Game/` после стартового шоукейса. Документы 03–05 описывают целевую картину,
здесь — текущее состояние и рецепты.

## Как стартует игра

`Game.App.GameRoot` срабатывает при нажатии Play в **любой** сцене (`RuntimeInitializeOnLoadMethod`):

1. `ContentDatabase` читает все `*.json` из `StreamingAssets/Data` (папки только для порядка: в одном файле можно держать любые разделы `items`, `monsters`, `quests`…).
2. Регистрируются сервисы: `PlayerProfile` (уровень/опыт/золото), `Inventory`, `ItemGenerator`, `LootService`, `QuestService`, `DialogueService`, `SkillBar`, `SaveService`.
3. Загружается сейв (`%USERPROFILE%/AppData/LocalLow/maxwuffwolf/Gamedevkek/saves/main.json`) или выдаётся стартовый набор.
4. Игрок появляется на `PlayerSpawnPoint`, создаются камера и UI.
5. `GameState.SetReady()` — после этого `MonsterSpawner` и `NpcSpawnPoint` на сцене создают мобов и NPC.

Сцена хранит только **геометрию и маркеры** (точка спавна, спавнеры, точки NPC). Всё живое создаётся из данных.
Сцену `Showcase` генерирует `ShowcaseSceneBuilder` (меню Game), дальше её можно править руками как обычно.

## Сборки (asmdef)

```
Game.Core      сервисы, EventBus, ввод (GameInput), RNG, материалы
Game.Data      определения контента + загрузка JSON + ScriptableObject-визуалы
Game.Gameplay  статы, бой, акторы, ИИ, предметы, квесты, диалоги, мир
Game.Meta      сохранения
Game.UI        HUD, инвентарь, диалоги, тултипы (uGUI из кода, без префабов)
Game.App       бутстрап
Game.DevTools  консоль, читы, hot reload (только редактор и Development Build)
Game.Editor    генерация сцены, меню Game
```

Gameplay не знает про UI: общение через `EventBus` (`DamageDealtEvent`, `MonsterKilledEvent`, `UiMessageEvent`, …).

## Рецепты

### Новый моб
1. `Data/monsters/*.json`: скопировать `wolf`, поменять `id`, статы, `lootTable`, `placeholder` (модель-заглушка: `wolf`, `humanoid`, `npc`, `blob`).
2. Поставить на сцену объект с `MonsterSpawner` и вписать `MonsterId` — или проверить сразу: `~` → `spawn <id> 3`.
3. Настоящая модель: Create → Game → **Actor Visual** в любой папке `Resources/Visuals`, `Id` = значение `prefab` из JSON, перетащить префаб.
   Если у модели есть Animator с параметрами `Speed` (float), `Attack` и `Die` (триггеры) — они подхватятся сами.

### Новый предмет
1. `Data/items/*.json`: `id`, `name`, `slot` (`Head, Neck, Shoulders, Chest, Hands, Waist, Legs, Feet, MainHand, OffHand, Ring`), `implicit`, `tags`, `icon`.
2. Он сразу начнёт падать из таблиц с `ItemByTag`, если теги совпадают. Проверка: `give <id> epic`.
3. Иконка: Create → Game → **Item Visual** в `Resources/Visuals`, `Id` = id предмета.

### Новый аффикс / редкость
`Data/affixes/*.json`, `Data/rarities/rarities.json`. Цвет луча лута = цвет самой ценной (`order`) редкости в куче.

### Лут-таблица
`Data/loot_tables/*.json`: `rolls`, `noDropWeight`, `entries` (`Item`, `ItemByTag`, `Table`, `Currency`), `rarityWeights`.
Баланс смотреть так: `loot.sim wolf 1000`.

### Новый навык
1. `Data/skills/*.json`: скопировать `heavy_strike`. Поведение `Melee` уже умеет дугу (`Arc`) и круг (`Circle`), отбрасывание, тряску.
2. Поставить на панель: `Data/characters/hero.json` → `skillBar` (12 слотов, клавиши 1–0, -, =).
3. Новый тип поведения (снаряд, рывок, аура) = новый класс `ISkillBehavior` с атрибутом `[SkillBehavior("Projectile")]` в `Gameplay/Combat`. Больше ничего регистрировать не нужно.

### Новый квест
1. `Data/quests/*.json`: `giver` (id NPC), `objectives` (`Kill` по `monster` или `tag`), `rewards` (`xp`, `gold`, `items`, `backpackSlots`), `prerequisites` для цепочек.
2. В диалоге NPC добавить ветки с условиями `quest.available:<id>` / `quest.active:<id>` / `quest.ready:<id>` и действиями `quest.accept:<id>` / `quest.turnin:<id>`.
   Значок над NPC («!» / «?») считается автоматически.

### Новый NPC
`Data/npcs/*.json` + диалог в `Data/dialogues/*.json`, на сцене — объект с `NpcSpawnPoint`.

### Новый стат
Единственный случай, когда нужен код: добавить значение в `StatId` (`Game.Data/Stats.cs`), подпись в `StatNames` и учесть его в формуле (`DamageCalculator`, `PlayerController` и т.п.).

## Отличия от плана в 02/03 (осознанные упрощения на старте)

- **Камера** — свой `CameraRig` (follow + зум + тряска). Cinemachine встроен в редактор 6.6, подключим, когда понадобятся несколько камер/катсцены.
- **NavMesh** не используется: открытая плоская локация, мобы идут напрямую. Понадобится для данжей (этап 5).
- **UI** собран из кода (uGUI + встроенный шрифт), чтобы проект открывался без ручной настройки. Красивые префабы/TextMeshPro — позже.
- **Хитбоксы** по таймеру `hitFrame` из JSON, а не Animation Event — у заглушек нет анимаций.
