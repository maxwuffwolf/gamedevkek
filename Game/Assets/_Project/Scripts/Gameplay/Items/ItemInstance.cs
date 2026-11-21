using System;
using System.Collections.Generic;
using System.Linq;
using Game.Data;

namespace Game.Gameplay.Items
{
    /// <summary>Выпавшее значение модификатора.</summary>
    [Serializable]
    public sealed class RolledMod
    {
        public StatId Stat;
        public ModType Type;
        public float Value;

        public RolledMod() { }
        public RolledMod(StatId stat, ModType type, float value) { Stat = stat; Type = type; Value = value; }
    }

    [Serializable]
    public sealed class RolledAffix
    {
        public string Id;
        public int Tier;
        public string Name;
        public List<RolledMod> Mods = new();
    }

    /// <summary>
    /// Конкретный предмет (то, что лежит в рюкзаке и сохраняется). Хранит id базы + выпавшие значения,
    /// поэтому ребаланс JSON не ломает сохранения.
    /// </summary>
    [Serializable]
    public sealed class ItemInstance
    {
        public string Uid = Guid.NewGuid().ToString("N");
        public string BaseId;
        public string Rarity;
        public int ItemLevel = 1;
        public List<RolledMod> Implicits = new();
        public List<RolledAffix> Affixes = new();

        public IEnumerable<RolledMod> AllMods() => Implicits.Concat(Affixes.SelectMany(a => a.Mods));
    }
}
