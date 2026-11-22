using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Data
{
    /// <summary>Контент перезагружен (F5 / data.reload / изменение файла). Живые объекты переприменяют данные.</summary>
    public readonly struct ContentReloadedEvent
    {
        public readonly int Version;
        public readonly int ErrorCount;
        public ContentReloadedEvent(int version, int errorCount) { Version = version; ErrorCount = errorCount; }
    }

    /// <summary>
    /// Реестр всего контента из StreamingAssets/Data/**/*.json.
    /// Файл может содержать любые разделы: { "items": [...], "monsters": [...], ... } — папки только для порядка.
    /// </summary>
    public sealed class ContentDatabase
    {
        private sealed class ContentSet
        {
            public readonly Dictionary<string, RarityDef> Rarities = new();
            public readonly Dictionary<string, ItemBaseDef> Items = new();
            public readonly Dictionary<string, AffixDef> Affixes = new();
            public readonly Dictionary<string, LootTableDef> LootTables = new();
            public readonly Dictionary<string, SkillDef> Skills = new();
            public readonly Dictionary<string, MonsterDef> Monsters = new();
            public readonly Dictionary<string, CharacterDef> Characters = new();
            public readonly Dictionary<string, NpcDef> Npcs = new();
            public readonly Dictionary<string, QuestDef> Quests = new();
            public readonly Dictionary<string, DialogueDef> Dialogues = new();
            public readonly Dictionary<string, ZoneDef> Zones = new();
        }

        private ContentSet _set = new();
        private readonly JsonSerializer _serializer;

        public string RootPath { get; private set; }
        public int Version { get; private set; }
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();

        public IReadOnlyDictionary<string, RarityDef> Rarities => _set.Rarities;
        public IReadOnlyDictionary<string, ItemBaseDef> Items => _set.Items;
        public IReadOnlyDictionary<string, AffixDef> Affixes => _set.Affixes;
        public IReadOnlyDictionary<string, LootTableDef> LootTables => _set.LootTables;
        public IReadOnlyDictionary<string, SkillDef> Skills => _set.Skills;
        public IReadOnlyDictionary<string, MonsterDef> Monsters => _set.Monsters;
        public IReadOnlyDictionary<string, CharacterDef> Characters => _set.Characters;
        public IReadOnlyDictionary<string, NpcDef> Npcs => _set.Npcs;
        public IReadOnlyDictionary<string, QuestDef> Quests => _set.Quests;
        public IReadOnlyDictionary<string, DialogueDef> Dialogues => _set.Dialogues;
        public IReadOnlyDictionary<string, ZoneDef> Zones => _set.Zones;

        /// <summary>Редкости по возрастанию ценности.</summary>
        public IReadOnlyList<RarityDef> RaritiesOrdered { get; private set; } = Array.Empty<RarityDef>();

        public ContentDatabase()
        {
            _serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                ObjectCreationHandling = ObjectCreationHandling.Replace,
            });
        }

        public static string DefaultRoot => Path.Combine(UnityEngine.Application.streamingAssetsPath, "Data");

        public T Get<T>(IReadOnlyDictionary<string, T> dict, string id) where T : class =>
            id != null && dict.TryGetValue(id, out var v) ? v : null;

        public ItemBaseDef Item(string id) => Get(Items, id);
        public RarityDef Rarity(string id) => Get(Rarities, id);
        public SkillDef Skill(string id) => Get(Skills, id);
        public MonsterDef Monster(string id) => Get(Monsters, id);
        public CharacterDef Character(string id) => Get(Characters, id);
        public NpcDef Npc(string id) => Get(Npcs, id);
        public QuestDef Quest(string id) => Get(Quests, id);
        public DialogueDef Dialogue(string id) => Get(Dialogues, id);
        public LootTableDef LootTable(string id) => Get(LootTables, id);
        public ZoneDef Zone(string id) => Get(Zones, id);

        /// <summary>Зона, которой принадлежит сцена (по имени сцены), или null.</summary>
        public ZoneDef ZoneByScene(string sceneName) =>
            Zones.Values.FirstOrDefault(z => string.Equals(z.Scene, sceneName, StringComparison.OrdinalIgnoreCase));

        public UnityEngine.Color RarityColor(string rarityId) =>
            ColorUtil.Parse(Rarity(rarityId)?.Color, new UnityEngine.Color(0.8f, 0.8f, 0.8f));

        public int RarityOrder(string rarityId) => Rarity(rarityId)?.Order ?? 0;

        /// <summary>Все id (для автодополнения в консоли).</summary>
        public IEnumerable<string> AllIds() =>
            _set.Items.Keys.Concat(_set.Monsters.Keys).Concat(_set.Skills.Keys).Concat(_set.Quests.Keys)
                .Concat(_set.LootTables.Keys).Concat(_set.Rarities.Keys).Concat(_set.Npcs.Keys).Concat(_set.Zones.Keys);

        /// <summary>
        /// Загрузить всё. Если есть ошибки парсинга, а данные уже были загружены, старые данные остаются
        /// (игра не падает от опечатки в JSON).
        /// </summary>
        public bool LoadAll(string rootPath)
        {
            RootPath = rootPath;
            Errors.Clear();
            Warnings.Clear();
            var fresh = new ContentSet();
            int parseErrors = 0;

            if (!Directory.Exists(rootPath))
            {
                Errors.Add($"Папка с данными не найдена: {rootPath}");
                return false;
            }

            var files = Directory.GetFiles(rootPath, "*.json", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal);
            foreach (var file in files)
            {
                string rel = file.Substring(rootPath.Length).TrimStart('/', '\\');
                try
                {
                    var root = JObject.Parse(File.ReadAllText(file));
                    foreach (var prop in root.Properties())
                    {
                        switch (prop.Name)
                        {
                            case "$schema":
                            case "_comment":
                                break;
                            case "rarities": LoadList(prop.Value, fresh.Rarities, rel); break;
                            case "items": LoadList(prop.Value, fresh.Items, rel); break;
                            case "affixes": LoadList(prop.Value, fresh.Affixes, rel); break;
                            case "lootTables": LoadList(prop.Value, fresh.LootTables, rel); break;
                            case "skills": LoadList(prop.Value, fresh.Skills, rel); break;
                            case "monsters": LoadList(prop.Value, fresh.Monsters, rel); break;
                            case "characters": LoadList(prop.Value, fresh.Characters, rel); break;
                            case "npcs": LoadList(prop.Value, fresh.Npcs, rel); break;
                            case "quests": LoadList(prop.Value, fresh.Quests, rel); break;
                            case "dialogues": LoadList(prop.Value, fresh.Dialogues, rel); break;
                            case "zones": LoadList(prop.Value, fresh.Zones, rel); break;
                            default:
                                Warnings.Add($"{rel}: неизвестный раздел \"{prop.Name}\" (пропущен)");
                                break;
                        }
                    }
                }
                catch (JsonReaderException e)
                {
                    parseErrors++;
                    Errors.Add($"{rel}: ошибка JSON в строке {e.LineNumber}, позиция {e.LinePosition}: {e.Message}");
                }
                catch (Exception e)
                {
                    parseErrors++;
                    Errors.Add($"{rel}: {e.Message}");
                }
            }

            if (parseErrors > 0 && Version > 0)
            {
                Errors.Add("Из-за ошибок оставлены предыдущие данные. Исправь JSON и нажми F5.");
                return false;
            }

            _set = fresh;
            RaritiesOrdered = _set.Rarities.Values.OrderBy(r => r.Order).ToList();
            Version++;
            Validate();
            return Errors.Count == 0;
        }

        /// <summary>Hot reload: перечитать файлы и оповестить подписчиков.</summary>
        public void Reload()
        {
            LoadAll(RootPath ?? DefaultRoot);
            foreach (var e in Errors) Log.Error("Content", e);
            foreach (var w in Warnings) Log.Warn("Content", w);
            Log.Info("Content", $"Данные перезагружены (версия {Version}): предметов {Items.Count}, мобов {Monsters.Count}, квестов {Quests.Count}, ошибок {Errors.Count}");
            EventBus.Publish(new ContentReloadedEvent(Version, Errors.Count));
        }

        private void LoadList<T>(JToken token, Dictionary<string, T> target, string file) where T : class, IContentDef
        {
            if (token is not JArray arr)
            {
                Errors.Add($"{file}: раздел должен быть массивом");
                return;
            }
            foreach (var el in arr)
            {
                T def;
                try
                {
                    def = el.ToObject<T>(_serializer);
                }
                catch (Exception e)
                {
                    var line = (el as IJsonLineInfo)?.LineNumber ?? 0;
                    Errors.Add($"{file} (строка {line}): {e.Message}");
                    continue;
                }
                if (def == null || string.IsNullOrEmpty(def.Id))
                {
                    Errors.Add($"{file}: объект без id");
                    continue;
                }
                if (target.ContainsKey(def.Id))
                    Errors.Add($"{file}: дубликат id \"{def.Id}\" ({typeof(T).Name})");
                target[def.Id] = def;
            }
        }

        /// <summary>Проверка ссылок между контентом. Результат в Errors/Warnings.</summary>
        public IReadOnlyList<string> Validate()
        {
            void Need<T>(IReadOnlyDictionary<string, T> dict, string id, string where)
            {
                if (!string.IsNullOrEmpty(id) && !dict.ContainsKey(id))
                    Errors.Add($"{where}: ссылка на несуществующий id \"{id}\"");
            }

            if (Rarities.Count == 0) Errors.Add("Нет ни одной редкости (rarities)");

            foreach (var t in LootTables.Values)
            {
                if (t.Entries.Count == 0) Warnings.Add($"Лут-таблица {t.Id} пустая");
                foreach (var e in t.Entries)
                {
                    if (e.Type == LootEntryType.Item) Need(Items, e.Id, $"lootTable {t.Id}");
                    if (e.Type == LootEntryType.Table) Need(LootTables, e.Id, $"lootTable {t.Id}");
                    if (!string.IsNullOrEmpty(e.Rarity)) Need(Rarities, e.Rarity, $"lootTable {t.Id}");
                    if (e.Type == LootEntryType.ItemByTag && !Items.Values.Any(i => e.Tags.All(i.Tags.Contains)))
                        Warnings.Add($"lootTable {t.Id}: под теги [{string.Join(",", e.Tags)}] не подходит ни один предмет");
                }
                foreach (var r in t.RarityWeights.Keys) Need(Rarities, r, $"lootTable {t.Id}.rarityWeights");
            }

            foreach (var m in Monsters.Values)
            {
                Need(LootTables, m.LootTable, $"monster {m.Id}");
                foreach (var s in m.Skills) Need(Skills, s, $"monster {m.Id}");
                if (m.Skills.Count == 0) Warnings.Add($"monster {m.Id}: нет навыков, он не сможет атаковать");
            }

            foreach (var c in Characters.Values)
            {
                Need(Skills, c.BasicAttack, $"character {c.Id}");
                foreach (var s in c.SkillBar) Need(Skills, s, $"character {c.Id}.skillBar");
                foreach (var it in c.StartingItems)
                {
                    Need(Items, it.Id, $"character {c.Id}.startingItems");
                    Need(Rarities, it.Rarity, $"character {c.Id}.startingItems");
                }
            }

            foreach (var n in Npcs.Values) Need(Dialogues, n.Dialogue, $"npc {n.Id}");

            foreach (var q in Quests.Values)
            {
                Need(Npcs, q.Giver, $"quest {q.Id}.giver");
                Need(Npcs, q.TurnIn, $"quest {q.Id}.turnIn");
                foreach (var p in q.Prerequisites) Need(Quests, p, $"quest {q.Id}.prerequisites");
                foreach (var o in q.Objectives) Need(Monsters, o.Monster, $"quest {q.Id}.objectives");
                foreach (var it in q.Rewards.Items)
                {
                    Need(Items, it.Id, $"quest {q.Id}.rewards");
                    Need(Rarities, it.Rarity, $"quest {q.Id}.rewards");
                }
            }

            foreach (var d in Dialogues.Values)
            {
                foreach (var s in d.Start)
                    if (!d.Nodes.ContainsKey(s.Node ?? "")) Errors.Add($"dialogue {d.Id}: start → нет узла \"{s.Node}\"");
                foreach (var kv in d.Nodes)
                foreach (var o in kv.Value.Options)
                    if (!string.IsNullOrEmpty(o.Next) && !d.Nodes.ContainsKey(o.Next))
                        Errors.Add($"dialogue {d.Id}.{kv.Key}: вариант ведёт в несуществующий узел \"{o.Next}\"");
            }

            foreach (var a in Affixes.Values)
                if (a.Tiers.Count == 0) Errors.Add($"affix {a.Id}: нет тиров");

            foreach (var z in Zones.Values)
                if (string.IsNullOrWhiteSpace(z.Scene)) Errors.Add($"zone {z.Id}: не указана сцена (scene)");
            int starts = Zones.Values.Count(z => z.Start);
            if (Zones.Count > 0 && starts != 1) Warnings.Add($"Зон со \"start\": true — {starts}, должна быть ровно одна");

            return Errors;
        }
    }
}
