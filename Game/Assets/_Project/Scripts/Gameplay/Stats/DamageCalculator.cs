using System;
using Game.Core;
using Game.Data;

namespace Game.Gameplay.Stats
{
    public readonly struct DamageResult
    {
        public readonly float Amount;
        public readonly bool Crit;
        public DamageResult(float amount, bool crit) { Amount = amount; Crit = crit; }
    }

    /// <summary>Флаги читов боя (выставляются из дев-консоли).</summary>
    public static class CombatCheats
    {
        public static bool God;
        public static bool OneShot;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            God = false;
            OneShot = false;
        }
    }

    /// <summary>Единственное место, где считается урон. Чистая функция, удобно крутить формулы.</summary>
    public static class DamageCalculator
    {
        public static DamageResult Compute(StatSheet attacker, StatSheet defender, float multiplier, Random rng)
        {
            float dmg = attacker.Get(StatId.PhysDamage) * multiplier;
            dmg *= rng.Range(0.9f, 1.1f);

            bool crit = rng.NextDouble() * 100.0 < attacker.Get(StatId.CritChance);
            if (crit)
            {
                float critMulti = attacker.Get(StatId.CritMulti);
                dmg *= (critMulti > 0 ? critMulti : 150f) / 100f;
            }

            float armor = Math.Max(0f, defender.Get(StatId.Armor));
            dmg *= 100f / (100f + armor);

            return new DamageResult(Math.Max(1f, (float)Math.Round(dmg)), crit);
        }
    }
}
