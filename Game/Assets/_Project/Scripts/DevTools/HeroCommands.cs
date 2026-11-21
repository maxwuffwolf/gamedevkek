using Game.Gameplay.Actors;
using Game.Gameplay.World;
using UnityEngine;

namespace Game.DevTools
{
    /// <summary>Команды для внешнего вида героя (настоящая модель Hero.prefab).</summary>
    public static class HeroCommands
    {
        private static HeroAppearance Hero => HeroAppearance.Current;
        private const string NoHero = "Нет модели героя: собери её (Game → Герой → Собрать героя)";

        [DevCommand("dirt", "Грязь на коже героя 0..1: dirt 0.5 (без числа — показать)")]
        private static string Dirt(float? amount = null)
        {
            if (Hero == null) return NoHero;
            if (amount.HasValue) Hero.Dirt = amount.Value;
            return $"грязь = {Hero.Dirt:0.00}";
        }

        [DevCommand("wash", "Умыть героя (грязь плавно смывается)")]
        private static string Wash()
        {
            if (Hero == null) return NoHero;
            Hero.Wash();
            return "умывается…";
        }

        [DevCommand("balance", "Кристалл наруча: 0 Тьма .. 0.5 поровну .. 1 Свет: balance 0.8")]
        private static string Balance(float? value = null)
        {
            if (Hero == null) return NoHero;
            if (value.HasValue) Hero.Balance = Mathf.Clamp01(value.Value);
            return $"баланс = {Hero.Balance:0.00} (0 Тьма, 1 Свет)";
        }

        [DevCommand("absorb", "Проверить поглощение энергии наручем: absorb [искр]")]
        private static string Absorb(int motes = 4)
        {
            if (Hero == null) return NoHero;
            var p = GameWorld.Player != null ? GameWorld.Player.transform : Hero.transform;
            Hero.Absorb(p.position + p.forward * 3f + Vector3.up * 0.5f, Mathf.Clamp(motes, 1, 20));
            return "энергия летит в наруч";
        }
    }
}
