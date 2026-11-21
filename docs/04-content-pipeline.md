# 04. Пайплайн добавления контента

Все примеры ниже лежат в `Game/Assets/StreamingAssets/Data/`. После правки JSON нажми **F5 в игре**
(или команда `data.reload`), и изменения применятся без перезапуска. Ошибки валидации видны в консоли (`~`).

Общие правила:

- `id` в формате `snake_case`, уникален в пределах типа. Никогда не переименовываем `id` у контента, который мог попасть в сейв (вместо этого добавляем новый и помечаем старый `"deprecated": true`).
- Диапазоны записываются массивом `[min, max]`.
- Ссылки на другой контент только по `id`. Валидатор проверяет, что они существуют.
- Для автодополнения в VS Code/Rider к каждому файлу подключается JSON Schema из `Tools/schemas/` (`"$schema": "../../../../Tools/schemas/item.schema.json"`).

---

## 1. Новый базовый предмет

**Шаг 1.** `items/weapons.json`:

```json
{
  "items": [
    {
      "id": "axe_bearded",
      "name": "Бородатый топор",
      "slot": "MainHand",
      "category": "Axe",
      "levelReq": 8,
      "dropLevel": 6,
      "implicit": [{ "stat": "PhysDamage", "type": "Flat", "value": [7, 14] }],
      "baseStats": { "AttackSpeed": 1.1 },
      "tags": ["weapon", "melee", "axe", "two_hand"]
    }
  ]
}
```

**Шаг 2 (опционально).** Визуал: `Content/Items/` → Create → *Game/Item Visual* → `id = axe_bearded`, иконка, префаб на земле, префаб в руке. Без него будет заглушка.

**Шаг 3.** Проверка: `~` → `give axe_bearded rare 10` (выдать редкий топор ilvl 10).

**Шаг 4.** Чтобы он падал: добавить `axe_bearded` в нужную лут-таблицу (см. п. 3) или в общий пул по тегу.

---

## 2. Новый аффикс

`affixes/weapon_prefixes.json`:

```json
{
  "affixes": [
    {
      "id": "pre_phys_flat",
      "kind": "Prefix",
      "group": "phys_flat",
      "allowedTags": ["weapon"],
      "tiers": [
        { "tier": 3, "ilvl": 1,  "weight": 1000, "name": "Острый",    "mods": [{ "stat": "PhysDamage", "type": "Flat", "value": [2, 4] }] },
        { "tier": 2, "ilvl": 12, "weight": 600,  "name": "Зазубренный", "mods": [{ "stat": "PhysDamage", "type": "Flat", "value": [5, 9] }] },
        { "tier": 1, "ilvl": 25, "weight": 200,  "name": "Жестокий",  "mods": [{ "stat": "PhysDamage", "type": "Flat", "value": [10, 16] }] }
      ]
    }
  ]
}
```

- `group`: на предмете не может быть двух аффиксов одной группы.
- `allowedTags`: на какие базы может выпасть (по тегам предмета).
- `weight`: относительный шанс выбора среди доступных.
- Новый **стат** (не существующий в `StatId`) — единственный случай, когда нужен код: добавить значение в enum и где он учитывается в формулах.

Проверка: `loot.roll axe_bearded rare 30 x20` — сгенерировать 20 предметов и вывести аффиксы в консоль. `loot.stats pre_phys_flat 10000` — гистограмма выпадения тиров.

---

## 3. Лут-таблица

`loot_tables/zone_forest.json`:

```json
{
  "lootTables": [
    {
      "id": "forest_common",
      "rolls": [1, 2],
      "noDropWeight": 600,
      "entries": [
        { "type": "Currency", "id": "gold", "amount": [5, 20], "weight": 500 },
        { "type": "ItemByTag", "tags": ["weapon"], "weight": 120 },
        { "type": "ItemByTag", "tags": ["armor"],  "weight": 150 },
        { "type": "Item", "id": "axe_bearded", "weight": 10 },
        { "type": "Table", "id": "global_uniques", "weight": 2 }
      ],
      "rarityWeights": { "Normal": 700, "Magic": 250, "Rare": 48, "Unique": 2 }
    }
  ]
}
```

Таблицы вкладываются (`"type": "Table"`). Модификаторы забега (`+30% количества лута`) умножают `rolls` и `rarityWeights` глобально.

Проверка: `loot.sim forest_common 1000` — симуляция 1000 дропов, сводка по редкостям и типам.

---

## 4. Новый моб

**Шаг 1.** `monsters/forest.json`:

```json
{
  "monsters": [
    {
      "id": "goblin_spear",
      "name": "Гоблин-копейщик",
      "prefab": "goblin_spear",
      "ai": "melee_basic",
      "baseLevel": 3,
      "stats": { "MaxHealth": 40, "Armor": 5, "MoveSpeed": 4.2, "PhysDamage": 6, "AttackSpeed": 0.9 },
      "scaling": { "MaxHealth": 0.12, "PhysDamage": 0.08 },
      "skills": ["mob_thrust"],
      "aggroRange": 10,
      "xp": 12,
      "lootTable": "forest_common",
      "tags": ["goblin", "melee"]
    }
  ]
}
```

`scaling` — прирост за уровень моба (12% HP за уровень). Уровень моба задаёт зона/данж.

**Шаг 2.** Префаб: `Prefabs/Monsters/goblin_spear.prefab` (модель + Animator + `NavMeshAgent` + компонент `MonsterActor`).
Префаб берётся по ключу `prefab` через `ContentDatabase` (реестр `MonsterVisual` SO). Удобно создавать из префаба-шаблона `MonsterTemplate` (Prefab Variant), меняя только модель и аниматор.

**Шаг 3.** Проверка: `spawn goblin_spear 5 lvl=10` — 5 гоблинов 10 уровня под курсором. `god` — бессмертие, чтобы смотреть на их поведение.

---

## 5. Новая открытая зона

**Шаг 1.** Сцена `Scenes/Zones/Forest.unity`: блок-аут через ProBuilder → позже заменить на ассеты. Запечь NavMesh (`NavMeshSurface`).

**Шаг 2.** Расставить `SpawnArea` (компонент с объёмом и `packId`) и `ZoneTransition` (порталы).

**Шаг 3.** `zones/forest.json`:

```json
{
  "zones": [
    {
      "id": "forest",
      "name": "Шепчущий лес",
      "scene": "Forest",
      "monsterLevel": [1, 8],
      "packs": {
        "goblins_small": { "monsters": [{ "id": "goblin_spear", "count": [3, 5] }], "eliteChance": 0.05 },
        "goblins_mixed": { "monsters": [{ "id": "goblin_spear", "count": [2, 3] }, { "id": "goblin_archer", "count": [1, 2] }], "eliteChance": 0.1 }
      },
      "respawnSeconds": 120,
      "lootTable": "forest_common",
      "dungeonEntrances": ["forest_cave"]
    }
  ]
}
```

Расстановка в сцене (геометрия) отдельно, состав мобов и баланс в JSON.

**Шаг 4.** Проверка: `zone.goto forest`.

---

## 6. Новый тип данжа

**Шаг 1.** Комнаты: сцена-мастерская `Sandbox/RoomWorkshop.unity`, где собираются префабы комнат. Каждая комната:
- корневой компонент `DungeonRoom` (тип: `start | combat | elite | treasure | boss`, размер),
- дочерние `RoomSocket` на месте дверей (направление + ширина),
- `SpawnArea` внутри.

Префабы складываются в `Prefabs/Dungeons/<тема>/`.

**Шаг 2.** `dungeons/forest_cave.json`:

```json
{
  "dungeons": [
    {
      "id": "forest_cave",
      "name": "Пещера гоблинов",
      "roomSet": "cave",
      "roomCount": [8, 12],
      "branchChance": 0.25,
      "rooms": { "combat": 70, "elite": 15, "treasure": 15 },
      "boss": "goblin_chief",
      "packs": ["goblins_small", "goblins_mixed"],
      "levelOffset": 2,
      "modifiersPool": ["mod_extra_health", "mod_extra_loot", "mod_fast_monsters"],
      "modifiersCount": [0, 2],
      "deathPenalty": "LoseRunLoot"
    }
  ]
}
```

**Шаг 3.** Проверка: `dungeon.generate forest_cave seed=12345` (одна и та же планировка), `dungeon.generate forest_cave` (случайная), `dungeon.debug on` (показать граф комнат и сокеты гизмосами).

---

## 7. Новый скилл

`skills/player.json`:

```json
{
  "skills": [
    {
      "id": "cleave",
      "name": "Рассечение",
      "behavior": "Melee",
      "cooldown": 0,
      "cost": { "Mana": 6 },
      "damageMultiplier": 1.3,
      "shape": { "type": "Arc", "radius": 2.5, "angle": 120 },
      "animation": "attack_cleave",
      "hitFrame": 0.35,
      "tags": ["attack", "melee", "aoe"]
    }
  ]
}
```

Новый `behavior` (например «цепная молния») = новый C#-класс `ISkillBehavior` с атрибутом `[SkillBehavior("ChainLightning")]`. Всё остальное данные.

---

## Валидация

- При загрузке: `ContentDatabase.Validate()` проверяет битые ссылки, дубли `id`, пустые таблицы, перекрытие тиров. Ошибки в консоль + красная плашка в углу экрана.
- Вне Unity: `Tools/validate_content.py` (позже) — тот же набор проверок, можно повесить на git pre-commit hook.
