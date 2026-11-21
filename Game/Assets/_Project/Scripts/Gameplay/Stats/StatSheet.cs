using System;
using System.Collections.Generic;
using Game.Data;

namespace Game.Gameplay.Stats
{
    public readonly struct StatModifier
    {
        public readonly StatId Stat;
        public readonly ModType Type;
        public readonly float Value;
        public readonly object Source;

        public StatModifier(StatId stat, ModType type, float value, object source)
        {
            Stat = stat;
            Type = type;
            Value = value;
            Source = source;
        }
    }

    /// <summary>
    /// Лист статов актора: итог = (база + Σflat) × (1 + Σincreased%) × Π(1 + more%).
    /// Источники модификаторов: предметы, баффы, уровень, читы. Пересчёт ленивый.
    /// </summary>
    public sealed class StatSheet
    {
        private static readonly StatId[] AllStats = (StatId[])Enum.GetValues(typeof(StatId));

        private readonly Dictionary<StatId, float> _base = new();
        private readonly Dictionary<StatId, float> _overrides = new();
        private readonly List<StatModifier> _mods = new();
        private readonly Dictionary<StatId, float> _cache = new();
        private bool _dirty = true;

        public event Action Changed;

        public IReadOnlyList<StatModifier> Modifiers => _mods;

        public void SetBase(StatId stat, float value)
        {
            _base[stat] = value;
            MarkDirty();
        }

        public float GetBase(StatId stat) => _base.TryGetValue(stat, out var v) ? v : 0f;

        public void ClearBase()
        {
            _base.Clear();
            MarkDirty();
        }

        /// <summary>Жёстко задать итоговое значение (дев-команда stat). NaN снимает override.</summary>
        public void SetOverride(StatId stat, float value)
        {
            if (float.IsNaN(value)) _overrides.Remove(stat);
            else _overrides[stat] = value;
            MarkDirty();
        }

        public void AddModifier(StatModifier mod)
        {
            _mods.Add(mod);
            MarkDirty();
        }

        public int RemoveBySource(object source)
        {
            int n = _mods.RemoveAll(m => ReferenceEquals(m.Source, source));
            if (n > 0) MarkDirty();
            return n;
        }

        public float Get(StatId stat)
        {
            if (_dirty) Recalculate();
            return _cache.TryGetValue(stat, out var v) ? v : 0f;
        }

        private void MarkDirty()
        {
            _dirty = true;
            Changed?.Invoke();
        }

        private void Recalculate()
        {
            _dirty = false;
            foreach (var stat in AllStats)
            {
                float flat = GetBase(stat), inc = 0f, more = 1f;
                foreach (var m in _mods)
                {
                    if (m.Stat != stat) continue;
                    switch (m.Type)
                    {
                        case ModType.Flat: flat += m.Value; break;
                        case ModType.Increased: inc += m.Value; break;
                        case ModType.More: more *= 1f + m.Value / 100f; break;
                    }
                }
                float value = flat * (1f + inc / 100f) * more;
                if (_overrides.TryGetValue(stat, out var ov)) value = ov;
                _cache[stat] = value;
            }
        }
    }
}
