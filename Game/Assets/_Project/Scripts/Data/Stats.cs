using System.Collections.Generic;

namespace Game.Data
{
    /// <summary>
    /// Все статы игры. Новый стат = новое значение здесь + где он учитывается в формулах.
    /// Всё остальное (аффиксы, предметы, мобы) ссылается на стат строкой в JSON.
    /// </summary>
    public enum StatId
    {
        MaxHealth,
        MaxMana,
        HealthRegen,
        ManaRegen,
        Armor,
        MoveSpeed,
        AttackSpeed,
        PhysDamage,
        CritChance,
        CritMulti,
    }

    /// <summary>Flat: +N. Increased: +N% (складываются). More: ×(1+N%) (перемножаются).</summary>
    public enum ModType
    {
        Flat,
        Increased,
        More,
    }

    /// <summary>Модификатор в определении контента (значение диапазоном, роллится при генерации).</summary>
    public sealed class StatModDef
    {
        public StatId Stat;
        public ModType Type = ModType.Flat;
        public FloatRange Value;
    }

    public static class StatNames
    {
        private static readonly Dictionary<StatId, string> _names = new()
        {
            { StatId.MaxHealth, "к здоровью" },
            { StatId.MaxMana, "к мане" },
            { StatId.HealthRegen, "к регенерации здоровья" },
            { StatId.ManaRegen, "к регенерации маны" },
            { StatId.Armor, "к броне" },
            { StatId.MoveSpeed, "к скорости бега" },
            { StatId.AttackSpeed, "к скорости атаки" },
            { StatId.PhysDamage, "к физическому урону" },
            { StatId.CritChance, "к шансу крита" },
            { StatId.CritMulti, "к силе крита" },
        };

        private static readonly Dictionary<StatId, string> _short = new()
        {
            { StatId.MaxHealth, "Здоровье" },
            { StatId.MaxMana, "Мана" },
            { StatId.HealthRegen, "Реген. здоровья" },
            { StatId.ManaRegen, "Реген. маны" },
            { StatId.Armor, "Броня" },
            { StatId.MoveSpeed, "Скорость бега" },
            { StatId.AttackSpeed, "Скорость атаки" },
            { StatId.PhysDamage, "Физ. урон" },
            { StatId.CritChance, "Шанс крита, %" },
            { StatId.CritMulti, "Сила крита, %" },
        };

        public static string Suffix(StatId s) => _names.TryGetValue(s, out var n) ? n : s.ToString();
        public static string Short(StatId s) => _short.TryGetValue(s, out var n) ? n : s.ToString();

        /// <summary>"+5 к броне", "+10% к скорости атаки".</summary>
        public static string Format(StatId stat, ModType type, float value)
        {
            string sign = value >= 0 ? "+" : "−";
            float v = System.Math.Abs(value);
            string num = v >= 10 || System.Math.Abs(v - System.Math.Round(v)) < 0.05f ? v.ToString("0") : v.ToString("0.#");
            return type switch
            {
                ModType.Flat => $"{sign}{num} {Suffix(stat)}",
                ModType.Increased => $"{sign}{num}% {Suffix(stat)}",
                _ => $"{sign}{num}% {Suffix(stat)} (множитель)",
            };
        }
    }
}
