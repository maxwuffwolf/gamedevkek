using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Data;

namespace Game.Gameplay.Items
{
    /// <summary>Создаёт предметы: редкость → множитель имплицитов + N случайных аффиксов по тегам и ilvl.</summary>
    public sealed class ItemGenerator
    {
        private readonly ContentDatabase _db;
        private readonly RngService _rng;

        public ItemGenerator(ContentDatabase db, RngService rng)
        {
            _db = db;
            _rng = rng;
        }

        public ItemInstance Create(string baseId, string rarityId, int itemLevel, Random rng = null)
        {
            rng ??= _rng.Stream("loot");
            var baseDef = _db.Item(baseId);
            if (baseDef == null) return null;
            var rarity = _db.Rarity(rarityId) ?? _db.RaritiesOrdered.FirstOrDefault();

            var item = new ItemInstance
            {
                BaseId = baseId,
                Rarity = rarity?.Id ?? rarityId,
                ItemLevel = Math.Max(1, itemLevel),
            };

            float implicitMul = rarity?.ImplicitMultiplier ?? 1f;
            foreach (var m in baseDef.Implicit)
            {
                float v = m.Value.Roll(rng) * implicitMul;
                item.Implicits.Add(new RolledMod(m.Stat, m.Type, RoundStat(v)));
            }

            int affixCount = rarity != null ? rarity.Affixes.Roll(rng) : 0;
            var usedGroups = new HashSet<string>();
            for (int i = 0; i < affixCount; i++)
            {
                var affix = RollAffix(baseDef, item.ItemLevel, usedGroups, rng);
                if (affix == null) break;
                item.Affixes.Add(affix);
            }
            return item;
        }

        /// <summary>Случайная редкость по весам {"common": 700, "uncommon": 250, ...}.</summary>
        public string RollRarity(IReadOnlyDictionary<string, int> weights, Random rng = null)
        {
            rng ??= _rng.Stream("loot");
            if (weights == null || weights.Count == 0) return _db.RaritiesOrdered.FirstOrDefault()?.Id;
            int total = weights.Values.Sum();
            int roll = rng.Next(Math.Max(1, total));
            foreach (var kv in weights)
            {
                roll -= kv.Value;
                if (roll < 0) return kv.Key;
            }
            return weights.Keys.First();
        }

        private RolledAffix RollAffix(ItemBaseDef baseDef, int ilvl, HashSet<string> usedGroups, Random rng)
        {
            var candidates = new List<(AffixDef affix, AffixTierDef tier)>();
            foreach (var a in _db.Affixes.Values)
            {
                string group = string.IsNullOrEmpty(a.Group) ? a.Id : a.Group;
                if (usedGroups.Contains(group)) continue;
                if (a.AllowedTags.Count > 0 && !a.AllowedTags.Any(baseDef.Tags.Contains)) continue;
                foreach (var t in a.Tiers)
                    if (t.Ilvl <= ilvl) candidates.Add((a, t));
            }
            if (candidates.Count == 0) return null;

            int total = candidates.Sum(c => Math.Max(0, c.tier.Weight));
            int roll = rng.Next(Math.Max(1, total));
            var pick = candidates[0];
            foreach (var c in candidates)
            {
                roll -= Math.Max(0, c.tier.Weight);
                if (roll < 0) { pick = c; break; }
            }

            usedGroups.Add(string.IsNullOrEmpty(pick.affix.Group) ? pick.affix.Id : pick.affix.Group);
            var rolled = new RolledAffix { Id = pick.affix.Id, Tier = pick.tier.Tier, Name = pick.tier.Name };
            foreach (var m in pick.tier.Mods)
                rolled.Mods.Add(new RolledMod(m.Stat, m.Type, RoundStat(m.Value.Roll(rng))));
            return rolled;
        }

        private static float RoundStat(float v) => Math.Abs(v) >= 3f ? (float)Math.Round(v) : (float)Math.Round(v, 1);
    }
}
