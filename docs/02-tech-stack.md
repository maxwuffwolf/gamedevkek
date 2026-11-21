# 02. Технологический стек

## Движок и рендер

| Что | Выбор | Почему |
|---|---|---|
| Движок | **Unity 6 LTS** (последний LTS-релиз ветки 6.x на момент установки, сейчас 6.3 LTS) | Стабильность, огромное количество туториалов и ассетов |
| Рендер-пайплайн | **URP** (Universal Render Pipeline) | Идеален для стилизованной графики, быстрый, toon-шейдеры через Shader Graph |
| Язык | C# (.NET Standard 2.1, C# 9) | — |
| IDE | **JetBrains Rider** (бесплатен для некоммерческого использования) или Visual Studio 2022 Community | Rider лучше всего понимает Unity |
| ОС | Windows 11 Pro | — |

> Обновляемся только внутри одной LTS-ветки (patch-версии). Переход на следующую мажорную ветку делаем отдельной веткой git и только если есть причина.

### Создание проекта

1. Unity Hub → Installs → Install Editor → последний **6.x LTS**. Модули: *Windows Build Support (IL2CPP)*, *Documentation*.
2. New project → шаблон **Universal 3D** → расположение: `<репо>/Game`.
3. Edit → Project Settings:
   - **Player → Other Settings → Active Input Handling:** `Input System Package (New)`.
   - **Player → Color Space:** `Linear`.
   - **Editor → Enter Play Mode Settings:** `Do not reload Domain or Scene` (вход в Play Mode почти мгновенный; статические поля сбрасываем вручную через `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`).
   - **Editor → Asset Serialization:** `Force Text`; **Version Control:** `Visible Meta Files` (в Unity 6 по умолчанию).
   - **Quality:** оставить один URP-ассет для ПК (High), остальные уровни удалить, чтобы не путаться.

## Пакеты Unity (Package Manager)

### Обязательные

| Пакет | ID | Зачем |
|---|---|---|
| Input System | `com.unity.inputsystem` | WASD + мышь, ребинд, геймпад на будущее |
| Cinemachine 3 | `com.unity.cinemachine` | Top-down камера, тряска (Impulse), смена целей |
| AI Navigation | `com.unity.ai.navigation` | NavMesh для мобов, рантайм-запекание для процедурных данжей |
| Newtonsoft Json | `com.unity.nuget.newtonsoft-json` | Нормальный JSON (словари, полиморфизм, комментарии) вместо JsonUtility |
| Shader Graph | `com.unity.shadergraph` | Toon/cel-шейдеры (идёт с URP) |
| TextMeshPro / UGUI | встроено в `com.unity.ugui` | Цифры урона, UI инвентаря |
| ProBuilder | `com.unity.probuilder` | Блок-аут уровней и комнат данжей прямо в Unity |
| Visual Effect Graph *или* Particle System | встроено | Эффекты ударов (Particle System проще для начала) |

### Рекомендуемые (бесплатные, сторонние)

| Пакет | Как ставить | Зачем |
|---|---|---|
| **UniTask** | git URL: `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask` | async/await без аллокаций, удобные таймеры и кулдауны |
| **DOTween** (Free) | Asset Store | Твины для UI, «сочности» (подпрыгивание лута, вспышки) |
| **NaughtyAttributes** или **Tri-Inspector** | git URL / OpenUPM | `[Button]`, `[ShowIf]` в инспекторе без написания редакторов |
| **Hot Reload for Unity** (опционально, платный с триалом) | Asset Store | Перезагрузка C# кода без выхода из Play Mode |

### Чего пока не берём

- **Addressables:** до появления большого количества контента хватит `Resources` + реестра. Переход потом не болезненный, если ссылаемся на контент по строковым id (см. архитектуру).
- **ECS/DOTS:** избыточно для хобби-ARPG, сотни мобов MonoBehaviour вытягивают.
- **Odin Inspector:** хорош, но платный; NaughtyAttributes хватит.

## Внешние инструменты

| Инструмент | Зачем |
|---|---|
| **Blender 4.x LTS** | Свои модели (см. [06](06-art-pipeline.md)) |
| **Git + Git LFS** | Версионирование (см. [07](07-git-workflow.md)) |
| **GitHub Desktop** или **Fork** | GUI для git (по желанию) |
| **Mixamo** (бесплатно) | Риг и анимации гуманоидов |
| **Krita / GIMP** | Текстуры, иконки предметов |
| **VS Code** + JSON Schema | Редактирование JSON-контента с автодополнением |

## Железо и производительность

Ryzen 9 + RTX 5070 Ti laptop + 64 ГБ RAM — с большим запасом для Unity и Blender.
Целевая производительность: 144 FPS в 1440p на этой машине, это задаёт рамки (лоуполи, без тяжёлого пост-процесса).
Ноутбук: в Windows → Параметры → Дисплей → Графика назначить Unity и Blender на «Высокая производительность» (дискретная GPU).
