using System;
using System.Collections.Generic;

namespace Game.Gameplay.Progression
{
    /// <summary>12 слотов панели навыков (клавиши 1..0, -, =). Заполняется из characters.json → skillBar.</summary>
    public sealed class SkillBar
    {
        public const int SlotCount = 12;
        public static readonly string[] KeyLabels = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=" };

        private readonly string[] _slots = new string[SlotCount];

        public event Action Changed;

        public string Get(int index) => index >= 0 && index < SlotCount ? _slots[index] : null;

        public void Set(int index, string skillId)
        {
            if (index < 0 || index >= SlotCount) return;
            _slots[index] = string.IsNullOrEmpty(skillId) ? null : skillId;
            Changed?.Invoke();
        }

        public void Fill(IList<string> skills)
        {
            for (int i = 0; i < SlotCount; i++)
                _slots[i] = skills != null && i < skills.Count && !string.IsNullOrEmpty(skills[i]) ? skills[i] : null;
            Changed?.Invoke();
        }
    }
}
