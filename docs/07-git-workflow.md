# 07. Git-процесс

## Первоначальная настройка (один раз на машине)

```powershell
winget install Git.Git
git lfs install
git config --global user.name "Max"
git config --global core.autocrlf false     # переводы строк управляются .gitattributes
git config --global core.longpaths true     # длинные пути в Library/ассетах
```

### Unity Smart Merge (YAML merge)

Чтобы git умел мержить сцены и префабы, в `~/.gitconfig`:

```ini
[merge]
    tool = unityyamlmerge
[merge "unityyamlmerge"]
    name = Unity SmartMerge
    driver = 'C:/Program Files/Unity/Hub/Editor/<версия>/Editor/Data/Tools/UnityYAMLMerge.exe' merge -h -p --force %O %B %A %A
    recursive = binary
[mergetool "unityyamlmerge"]
    cmd = 'C:/Program Files/Unity/Hub/Editor/<версия>/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"
    trustExitCode = false
```

В соло-разработке конфликты редки, но настройка помогает, когда мержишь свою же экспериментальную ветку.

## Git LFS

Правила в [`.gitattributes`](../.gitattributes): модели, текстуры, звук, шрифты, `.blend` идут через LFS.

- Квота LFS на бесплатном GitHub ограничена (хранилище и трафик). Текущие лимиты смотреть в Settings → Billing. Для лоуполи-проекта обычно хватает надолго, но паки на гигабайты лучше не коммитить.
- Тяжёлые паки ассетов, которые можно скачать заново, разумно **не коммитить** (добавить путь в `.gitignore` и записать в `Art/README.md`, откуда пак брать).
- Проверка, что файл ушёл в LFS: `git lfs ls-files`.

## Ветки

Соло-проект, поэтому лёгкий процесс:

- `main` — всегда открывается и запускается.
- `feature/<что-то>` — для всего, что дольше одного вечера или может сломать проект (`feature/dungeon-gen`, `feature/toon-shader`).
- `experiment/<идея>` — «а что если…». Не получилось — просто удаляешь ветку.
- Мелочи (правка баланса в JSON, фикс) можно коммитить сразу в `main`.

Мерж в `main` через Pull Request на GitHub — так видно историю фич и удобно просить ревью у Claude.

## Коммиты

Формат: `<область>: <что сделано>` на русском или английском, как удобнее.

```
combat: добавлен hit-stop и тряска камеры
items: аффиксы на скорость атаки
data: ребаланс HP гоблинов
art: модель бочки (своя, blender)
docs: обновлён роадмап
```

Коммитить часто: как минимум в конце каждой сессии. Unity-проект без коммита = риск потерять вечер работы.

## Перед коммитом

1. **Сохранить проект** в Unity (File → Save Project), иначе `.asset`/`.meta` могут не записаться.
2. Проверить, что у каждого нового файла есть `.meta` (и наоборот: нет «осиротевших» `.meta`).
3. Не коммитить `Library/`, `Temp/`, `Logs/`, `UserSettings/` (уже в `.gitignore`).

## Теги и вехи

На каждом завершённом этапе роадмапа: `git tag m1-movement-combat` и т.п. Можно всегда откатиться и посмотреть, как выглядела игра.

## GitHub

- Репозиторий **приватный**.
- **Issues** как бэклог идей и багов (метки: `bug`, `idea`, `content`, `art`, `tech-debt`).
- **Projects** (доска) по желанию: колонки *Идеи → Следующее → В работе → Готово*.
- **Milestones** = этапы роадмапа.
