# Падальщик (гуль) — первый враг

Оригинальное существо сумеречного леса: сгорбленный тощий гуль на пальцеходящих ногах, длинные чёрные когти,
пепельно-зелёная шкура, глаза-угольки, трутовики и светящиеся споровые грибы на спине,
трофеи мародёра (набедренник из красного табарда павшего солдата, ожерелье из зубов, тряпки на предплечье).

## Пайплайн

| Шаг | Что | Где |
|---|---|---|
| 1 | Генератор модели, рига и анимаций | `Tools/Blender/ghoul_build.py` |
| 2 | FBX (клипы Idle / Walk / Run / Attack / Hit / Death) | `Game/Assets/_Project/Art/Models/Characters/Monsters/Ghoul/Mon_Ghoul.fbx` |
| 3 | Исходник Blender | `Art/Blender/Characters/ghoul.blend` |
| 4 | Сборка в Unity | меню **Game → Монстры → Собрать падальщика (гуль)** (`Editor/GhoulBuilder.cs`), также при пересборке оврага |

```
python ghoul_build.py --out <папка> --preview ghoul_preview.png --poses ghoul_poses.png
```

Сборка делает материалы `M_Ghoul`, `M_Ghoul_2S` (ткань, уши), `M_Ghoul_Glow` (Character Toon, `_EmissionFromAlbedo`),
контроллер `AC_Ghoul` (Speed: Idle 0 → Walk 1.3 → Run 3.9; Attack растягивается под длительность умения;
Die; аддитивный слой Hit — вздрог поверх бега и удара), префаб `Prefabs/Characters/Mon_Ghoul` и
`Resources/Visuals/AV_Scavenger` (id `scavenger` = `prefab` в `monsters/ravine.json`).

Смерть: у моделей с триггером `Die` труп не опрокидывается и не перекрашивается — лежит после анимации и уходит в землю
(`CorpseDissolver`). Удар в анимации — на 55 %, как `hitFrame` умения `scavenger_slash`.

Бинарники (fbx/blend) идут через Git LFS с ПК Max — из облака LFS не пушится.
