using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Именованные потоки случайных чисел (loot, combat, ai, ...). В геймплее не используем UnityEngine.Random,
    /// чтобы результат можно было воспроизвести по seed.
    /// </summary>
    public sealed class RngService
    {
        private readonly Dictionary<string, Random> _streams = new();
        public int Seed { get; private set; }

        public RngService(int seed) => Reseed(seed);

        public void Reseed(int seed)
        {
            Seed = seed;
            _streams.Clear();
        }

        public Random Stream(string name)
        {
            if (!_streams.TryGetValue(name, out var r))
            {
                r = new Random(Seed ^ StableHash(name));
                _streams[name] = r;
            }
            return r;
        }

        public static int StableHash(string s)
        {
            unchecked
            {
                int h = (int)2166136261;
                foreach (char c in s) h = (h ^ c) * 16777619;
                return h;
            }
        }
    }

    public static class RandomExtensions
    {
        public static float Range(this Random r, float min, float max) => min + (float)r.NextDouble() * (max - min);

        /// <summary>Целое в диапазоне [min, max] включительно.</summary>
        public static int RangeInclusive(this Random r, int min, int max) => max <= min ? min : r.Next(min, max + 1);

        public static bool Chance(this Random r, float probability01) => r.NextDouble() < probability01;

        public static UnityEngine.Vector2 InsideUnitCircle(this Random r)
        {
            double a = r.NextDouble() * Math.PI * 2;
            double d = Math.Sqrt(r.NextDouble());
            return new UnityEngine.Vector2((float)(Math.Cos(a) * d), (float)(Math.Sin(a) * d));
        }
    }
}
