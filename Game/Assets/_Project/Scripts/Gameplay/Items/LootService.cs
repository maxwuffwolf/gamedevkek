using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Data;

namespace Game.Gameplay.Items
{
    public sealed class LootResult
    {
        public readonly List<ItemInstance> Items = new();
        public int Gold;
        public bool IsEmpty => Items.Count == 0 && Gold <= 0;
    }

    /// <summary>Броски по лут-таблицам из JSON (вложенные таблицы, предметы по тегам, золото).</summary>
    public sealed class LootService
    {
        private readonly ContentDatabase _db;
        private readonly ItemGenerator _generator;
        private readonly RngService _rng;

        public LootService(ContentDatabase db, ItemGenerator generator, RngService rng)
        {
            _db = db;
            _generator = generator;
            _rng = rng;
        }

        public LootResult Roll(string tableId, int itemLevel)
        {
            var result = new LootResult();
            RollInto(tableId, itemLevel, result, 0, null);
            return result;
        }

        private void RollInto(string tableId, int ilvl, LootResult result, int depth, IReadOnlyDictionary<string, int> inheritedRarity)
        {
            var table = _db.LootTable(tableId);
            if (table == null || depth > 8) return;
            var rng = _rng.Stream("loot");
            var rarityWeights = table.RarityWeights.Count > 0 ? table.RarityWeights : inheritedRarity;

            int rolls = table.Rolls.Roll(rng);
            int total = table.NoDropWeight + table.Entries.Sum(e => Math.Max(0, e.Weight));
            for (int r = 0; r < rolls; r++)
            {
                int roll = rng.Next(Math.Max(1, total));
                if (roll < table.NoDropWeight) continue;
                roll -= table.NoDropWeight;
                foreach (var e in table.Entries)
                {
                    roll -= Math.Max(0, e.Weight);
                    if (roll >= 0) continue;
                    ApplyEntry(e, ilvl, result, depth, rarityWeights, rng);
                    break;
                }
            }
        }

        private void ApplyEntry(LootEntryDef e, int ilvl, LootResult result, int depth, IReadOnlyDictionary<string, int> rarityWeights, Random rng)
        {
            switch (e.Type)
            {
                case LootEntryType.Currency:
                    result.Gold += e.Amount.Roll(rng);
                    break;
                case LootEntryType.Table:
                    RollInto(e.Id, ilvl, result, depth + 1, rarityWeights);
                    break;
                case LootEntryType.Item:
                    AddItem(e.Id, e, ilvl, result, rarityWeights, rng);
                    break;
                case LootEntryType.ItemByTag:
                    var pool = _db.Items.Values.Where(i => !i.Deprecated && i.DropLevel <= ilvl && e.Tags.All(i.Tags.Contains)).ToList();
                    if (pool.Count > 0) AddItem(pool[rng.Next(pool.Count)].Id, e, ilvl, result, rarityWeights, rng);
                    break;
            }
        }

        private void AddItem(string baseId, LootEntryDef e, int ilvl, LootResult result, IReadOnlyDictionary<string, int> rarityWeights, Random rng)
        {
            string rarity = !string.IsNullOrEmpty(e.Rarity) ? e.Rarity : _generator.RollRarity(rarityWeights, rng);
            int count = Math.Max(1, e.Amount.Roll(rng));
            for (int i = 0; i < count; i++)
            {
                var item = _generator.Create(baseId, rarity, ilvl, rng);
                if (item != null) result.Items.Add(item);
            }
        }
    }
}
