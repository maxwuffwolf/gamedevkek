using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Data;
using Game.Gameplay.Actors;
using Game.Gameplay.AI;
using Game.Gameplay.Combat;
using Game.Gameplay.Items;
using Game.Gameplay.Progression;
using Game.Gameplay.Quests;
using Game.Gameplay.Stats;
using Game.Gameplay.World;
using Game.Meta;
using UnityEngine;

namespace Game.DevTools
{
    /// <summary>Базовый набор команд консоли. Свои команды добавляются так же — атрибутом в любом классе.</summary>
    public static class BuiltinCommands
    {
        private static ContentDatabase Db => Services.Get<ContentDatabase>();

        [DevCommand("help", "Список команд или справка: help [команда]")]
        private static string Help(string command = null) => DevCommands.Help(command);

        [DevCommand("clear", "Очистить консоль")]
        private static string Clear()
        {
            DevConsole.Instance?.Clear();
            return null;
        }

        [DevCommand("timescale", "Скорость времени: timescale 0.2")]
        private static string TimeScale(float scale)
        {
            Time.timeScale = Mathf.Clamp(scale, 0f, 10f);
            return $"timeScale = {Time.timeScale}";
        }

        [DevCommand("god", "Бессмертие: god [on|off]")]
        private static string God(bool? on = null) => Flag(ref CombatCheats.God, on, "god");

        [DevCommand("oneshot", "Убийство с одного удара: oneshot [on|off]")]
        private static string OneShot(bool? on = null) => Flag(ref CombatCheats.OneShot, on, "oneshot");

        private static string Flag(ref bool flag, bool? on, string name)
        {
            flag = on ?? !flag;
            return $"{name}: {(flag ? "ВКЛ" : "выкл")}";
        }

        [DevCommand("heal", "Полностью восстановить здоровье и ману")]
        private static string Heal()
        {
            var p = GameWorld.Player;
            if (p == null) return "Нет игрока";
            if (p.IsDead) p.Revive();
            p.Heal(float.MaxValue);
            p.RestoreMana(float.MaxValue);
            p.GetComponent<SkillCaster>()?.ResetCooldowns();
            return "Здоров как бык";
        }

        [DevCommand("give", "Выдать предмет: give <id> [rarity] [ilvl]  (rarity: common/uncommon/rare/epic/legendary)")]
        private static string Give(string id, string rarity = "rare", int ilvl = 0)
        {
            if (Db.Item(id) == null) return $"Нет предмета \"{id}\". Есть: {string.Join(", ", Db.Items.Keys)}";
            if (Db.Rarity(rarity) == null) return $"Нет редкости \"{rarity}\". Есть: {string.Join(", ", Db.Rarities.Keys)}";
            var item = Services.Get<ItemGenerator>().Create(id, rarity, ilvl > 0 ? ilvl : Services.Get<PlayerProfile>().Level);
            return Services.Get<Inventory>().TryAdd(item) ? $"Выдано: {Db.Item(id).Name} ({rarity})" : "Рюкзак полон (inv.unlock 10?)";
        }

        [DevCommand("give.random", "Пачка случайного лута на землю: give.random [count] [ilvl]")]
        private static string GiveRandom(int count = 8, int ilvl = 5)
        {
            var p = GameWorld.Player;
            if (p == null) return "Нет игрока";
            var gen = Services.Get<ItemGenerator>();
            var rng = Services.Get<RngService>().Stream("loot");
            var ids = Db.Items.Keys.ToList();
            var rarities = Db.RaritiesOrdered.Select(r => r.Id).ToList();
            for (int i = 0; i < count; i++)
            {
                var item = gen.Create(ids[rng.Next(ids.Count)], rarities[rng.Next(rarities.Count)], ilvl, rng);
                var offset = Quaternion.Euler(0f, 360f * i / count, 0f) * Vector3.forward * 2.5f;
                LootDrop.Spawn(GameWorld.SnapToGround(p.transform.position + offset), new[] { item }, 0);
            }
            return $"Разбросано {count} предметов вокруг героя";
        }

        [DevCommand("spawn", "Заспавнить мобов у курсора: spawn <monsterId> [count] [lvl]")]
        private static string Spawn(string id, int count = 1, int lvl = 1)
        {
            var def = Db.Monster(id);
            if (def == null) return $"Нет моба \"{id}\". Есть: {string.Join(", ", Db.Monsters.Keys)}";
            Vector3 at = GameWorld.PlayerController != null ? GameWorld.PlayerController.AimPoint : Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                var offset = count > 1 ? Quaternion.Euler(0f, 360f * i / count, 0f) * Vector3.forward * 1.5f : Vector3.zero;
                var actor = ActorFactory.SpawnMonster(def, lvl, GameWorld.SnapToGround(at + offset), Quaternion.identity);
                if (actor != null) actor.GetComponent<MonsterBrain>().Home = actor.transform.position;
            }
            return $"Заспавнено: {def.Name} ×{count}, ур. {lvl}";
        }

        [DevCommand("killall", "Убить всех мобов в радиусе: killall [radius]")]
        private static string KillAll(float radius = 50f)
        {
            var p = GameWorld.Player;
            int n = 0;
            foreach (var a in Actor.All.ToList())
            {
                if (a.Team != Team.Monster || a.IsDead) continue;
                if (p != null && Vector3.Distance(a.transform.position, p.transform.position) > radius) continue;
                a.ApplyDamage(p, a.Health + 1f, false);
                n++;
            }
            return $"Убито: {n}";
        }

        [DevCommand("level", "Установить уровень героя: level <n>")]
        private static string Level(int level)
        {
            Services.Get<PlayerProfile>().SetLevel(level);
            return $"Уровень {level}";
        }

        [DevCommand("xp", "Добавить опыт: xp <n>")]
        private static string Xp(long amount)
        {
            Services.Get<PlayerProfile>().AddXp(amount);
            return $"+{amount} опыта";
        }

        [DevCommand("gold", "Добавить золото: gold <n>")]
        private static string Gold(int amount)
        {
            Services.Get<PlayerProfile>().AddGold(amount);
            return $"+{amount} золота";
        }

        [DevCommand("stat", "Зафиксировать стат героя: stat <StatId> <value> | stat <StatId> reset")]
        private static string Stat(StatId stat, string value)
        {
            var p = GameWorld.Player;
            if (p == null) return "Нет игрока";
            if (value == "reset") p.Stats.SetOverride(stat, float.NaN);
            else if (float.TryParse(value.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)) p.Stats.SetOverride(stat, v);
            else return "Значение должно быть числом или reset";
            return $"{stat} = {p.Stats.Get(stat)}";
        }

        [DevCommand("stats", "Показать статы героя и их источники")]
        private static string Stats()
        {
            var p = GameWorld.Player;
            if (p == null) return "Нет игрока";
            var sb = new StringBuilder();
            foreach (StatId s in System.Enum.GetValues(typeof(StatId)))
            {
                sb.Append($"<color=#ffd34d>{s}</color> = {p.Stats.Get(s):0.##}  (база {p.Stats.GetBase(s):0.##}");
                foreach (var m in p.Stats.Modifiers.Where(m => m.Stat == s)) sb.Append($", {m.Type} {m.Value:+0.##;-0.##}");
                sb.AppendLine(")");
            }
            return sb.ToString().TrimEnd();
        }

        [DevCommand("data.reload", "Перечитать JSON (то же, что F5)")]
        private static string DataReload()
        {
            HotReloader.Reload("команда");
            return $"Версия данных {Db.Version}, ошибок {Db.Errors.Count}";
        }

        [DevCommand("data.validate", "Проверить ссылки в данных")]
        private static string DataValidate()
        {
            var sb = new StringBuilder();
            foreach (var e in Db.Errors) sb.AppendLine($"<color=#ff6666>ERR</color> {e}");
            foreach (var w in Db.Warnings) sb.AppendLine($"<color=#ffcc55>WARN</color> {w}");
            return sb.Length == 0 ? "Данные в порядке" : sb.ToString().TrimEnd();
        }

        [DevCommand("save", "Сохранить сейчас")]
        private static string Save()
        {
            var s = Services.Get<SaveService>();
            s.Save();
            return $"Сохранено: {s.FilePath}";
        }

        [DevCommand("save.wipe", "Удалить сейв и начать заново (стартовые предметы, квесты сброшены)")]
        private static string SaveWipe()
        {
            var s = Services.Get<SaveService>();
            s.Wipe();
            s.ApplyNewGame();
            Heal();
            return "Новая игра";
        }

        [DevCommand("save.slot", "Переключить слот сейва (для экспериментов): save.slot <имя>")]
        private static string SaveSlot(string name)
        {
            var s = Services.Get<SaveService>();
            s.Save();
            s.SetSlot(name);
            if (s.TryLoad(out var data)) s.Apply(data);
            else s.ApplyNewGame();
            return $"Слот: {s.Slot} ({s.FilePath})";
        }

        [DevCommand("quest.list", "Состояние всех квестов")]
        private static string QuestList()
        {
            var q = Services.Get<QuestService>();
            return string.Join("\n", Db.Quests.Values.Select(d => $"{d.Id}: {q.Status(d.Id)}"));
        }

        [DevCommand("quest.complete", "Выполнить цели квеста (останется сдать): quest.complete <id>")]
        private static string QuestComplete(string id)
        {
            Services.Get<QuestService>().ForceComplete(id);
            return $"{id}: {Services.Get<QuestService>().Status(id)}";
        }

        [DevCommand("quest.reset", "Сбросить квест (или все): quest.reset [id]")]
        private static string QuestReset(string id = null)
        {
            var q = Services.Get<QuestService>();
            if (string.IsNullOrEmpty(id)) q.ResetAll();
            else q.Reset(id);
            return "Сброшено";
        }

        [DevCommand("inv.unlock", "Открыть ячейки рюкзака: inv.unlock <n>")]
        private static string InvUnlock(int count)
        {
            var inv = Services.Get<Inventory>();
            inv.UnlockSlots(count);
            return $"Открыто {inv.Unlocked}/{inv.Capacity}";
        }

        [DevCommand("inv.clear", "Очистить рюкзак и снять всё")]
        private static string InvClear()
        {
            Services.Get<Inventory>().Clear();
            return "Пусто";
        }

        [DevCommand("tp", "Телепорт героя: tp <x> <z>")]
        private static string Teleport(float x, float z)
        {
            var p = GameWorld.Player;
            if (p == null) return "Нет игрока";
            p.GetComponent<Mover>().Teleport(GameWorld.SnapToGround(new Vector3(x, 0f, z)));
            return $"→ ({x}, {z})";
        }

        [DevCommand("ai.debug", "Подписи состояний ИИ над мобами: ai.debug [on|off]")]
        private static string AiDebug(bool? on = null) => Flag(ref MonsterBrain.DebugLabels, on, "ai.debug");

        [DevCommand("loot.sim", "Симуляция лут-таблицы: loot.sim <tableId> [N] [ilvl]")]
        private static string LootSim(string table, int n = 1000, int ilvl = 1)
        {
            if (Db.LootTable(table) == null) return $"Нет таблицы \"{table}\". Есть: {string.Join(", ", Db.LootTables.Keys)}";
            var loot = Services.Get<LootService>();
            var byRarity = new Dictionary<string, int>();
            var byItem = new Dictionary<string, int>();
            int empty = 0;
            long gold = 0;
            for (int i = 0; i < n; i++)
            {
                var r = loot.Roll(table, ilvl);
                if (r.IsEmpty) empty++;
                gold += r.Gold;
                foreach (var it in r.Items)
                {
                    byRarity[it.Rarity] = byRarity.TryGetValue(it.Rarity, out var c) ? c + 1 : 1;
                    byItem[it.BaseId] = byItem.TryGetValue(it.BaseId, out var c2) ? c2 + 1 : 1;
                }
            }
            var sb = new StringBuilder($"{table} × {n}: пустых {empty} ({100f * empty / n:0.#}%), золота в среднем {(float)gold / n:0.#}\n");
            foreach (var r in Db.RaritiesOrdered)
                if (byRarity.TryGetValue(r.Id, out var c)) sb.AppendLine($"  {ColorUtil.Colorize(r.Name, ColorUtil.Parse(r.Color))}: {c}");
            sb.Append("  Предметы: " + string.Join(", ", byItem.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}")));
            return sb.ToString();
        }

        [DevCommand("loot.roll", "Сгенерировать предметы в лог: loot.roll <baseId> [rarity] [ilvl] [count]")]
        private static string LootRoll(string baseId, string rarity = "rare", int ilvl = 10, int count = 5)
        {
            if (Db.Item(baseId) == null) return $"Нет предмета \"{baseId}\"";
            var gen = Services.Get<ItemGenerator>();
            var sb = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                var it = gen.Create(baseId, rarity, ilvl);
                sb.AppendLine($"#{i + 1} {Db.Item(baseId).Name} [{it.Rarity}, ilvl {it.ItemLevel}]: " +
                              string.Join("; ", it.AllMods().Select(m => StatNames.Format(m.Stat, m.Type, m.Value))));
            }
            return sb.ToString().TrimEnd();
        }
    }
}
