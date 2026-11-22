using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data;

namespace Game.Gameplay.Items
{
    /// <summary>Слоты экипировки героя (кольца занимают два слота).</summary>
    public enum EquipSlot
    {
        Head,
        Neck,
        Shoulders,
        Chest,
        Hands,
        Waist,
        Legs,
        Feet,
        MainHand,
        OffHand,
        Ring1,
        Ring2,
    }

    public readonly struct InventoryChangedEvent { }

    /// <summary>Рюкзак (часть ячеек закрыта и открывается наградами) + надетая экипировка.</summary>
    public sealed class Inventory
    {
        private readonly ContentDatabase _db;
        private ItemInstance[] _backpack;
        private readonly Dictionary<EquipSlot, ItemInstance> _equipped = new();

        public int Capacity => _backpack.Length;
        public int Unlocked { get; private set; }

        /// <summary>Мгновенное уведомление (статы героя, UI). Для слабосвязанных систем есть InventoryChangedEvent.</summary>
        public event Action Changed;

        /// <summary>Проверка уровня при надевании; задаёт бутстрап (знает уровень игрока).</summary>
        public Func<int> PlayerLevelProvider = () => 1;

        public Inventory(ContentDatabase db, int capacity, int unlocked)
        {
            _db = db;
            _backpack = new ItemInstance[Math.Max(1, capacity)];
            Unlocked = Math.Clamp(unlocked, 0, _backpack.Length);
        }

        public ItemInstance GetBackpack(int index) => index >= 0 && index < _backpack.Length ? _backpack[index] : null;
        public ItemInstance GetEquipped(EquipSlot slot) => _equipped.TryGetValue(slot, out var it) ? it : null;
        public IEnumerable<KeyValuePair<EquipSlot, ItemInstance>> Equipped => _equipped;
        public bool IsUnlocked(int index) => index >= 0 && index < Unlocked;

        public int FreeSlots
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Unlocked; i++) if (_backpack[i] == null) n++;
                return n;
            }
        }

        public bool TryAdd(ItemInstance item)
        {
            if (item == null) return false;
            for (int i = 0; i < Unlocked; i++)
            {
                if (_backpack[i] != null) continue;
                _backpack[i] = item;
                Notify();
                return true;
            }
            return false;
        }

        /// <summary>Положить в конкретную ячейку (должна быть открыта и пуста).</summary>
        public bool TryPlace(int index, ItemInstance item)
        {
            if (item == null || !IsUnlocked(index) || _backpack[index] != null) return false;
            _backpack[index] = item;
            Notify();
            return true;
        }

        /// <summary>Индекс первой свободной открытой ячейки или -1.</summary>
        public int FirstFree()
        {
            for (int i = 0; i < Unlocked; i++) if (_backpack[i] == null) return i;
            return -1;
        }

        public void UnlockSlots(int count)
        {
            Unlocked = Math.Clamp(Unlocked + count, 0, _backpack.Length);
            Notify();
        }

        /// <summary>Поменять местами две ячейки рюкзака.</summary>
        public bool Move(int from, int to)
        {
            if (!IsUnlocked(from) || !IsUnlocked(to) || from == to) return false;
            (_backpack[from], _backpack[to]) = (_backpack[to], _backpack[from]);
            Notify();
            return true;
        }

        public static bool Fits(ItemSlot itemSlot, EquipSlot slot) => itemSlot switch
        {
            ItemSlot.Ring => slot == EquipSlot.Ring1 || slot == EquipSlot.Ring2,
            _ => slot.ToString() == itemSlot.ToString(),
        };

        public EquipSlot DefaultSlotFor(ItemSlot itemSlot)
        {
            if (itemSlot == ItemSlot.Ring)
                return GetEquipped(EquipSlot.Ring1) == null || GetEquipped(EquipSlot.Ring2) != null ? EquipSlot.Ring1 : EquipSlot.Ring2;
            return Enum.TryParse(itemSlot.ToString(), out EquipSlot s) ? s : EquipSlot.MainHand;
        }

        /// <summary>Надеть предмет из рюкзака. Надетый ранее предмет уходит на его место.</summary>
        public bool Equip(int backpackIndex, out string error, EquipSlot? targetSlot = null)
        {
            error = null;
            var item = GetBackpack(backpackIndex);
            var def = item != null ? _db.Item(item.BaseId) : null;
            if (def == null)
            {
                error = "Неизвестный предмет";
                return false;
            }
            var slot = targetSlot ?? DefaultSlotFor(def.Slot);
            if (!Fits(def.Slot, slot))
            {
                error = "Не подходит в этот слот";
                return false;
            }
            if (def.LevelReq > PlayerLevelProvider())
            {
                error = $"Нужен уровень {def.LevelReq}";
                return false;
            }
            _backpack[backpackIndex] = GetEquipped(slot);
            _equipped[slot] = item;
            Notify();
            return true;
        }

        /// <summary>Снять в рюкзак (в указанную ячейку или первую свободную).</summary>
        public bool Unequip(EquipSlot slot, out string error, int toIndex = -1)
        {
            error = null;
            var item = GetEquipped(slot);
            if (item == null) return false;

            if (toIndex >= 0)
            {
                if (!IsUnlocked(toIndex)) { error = "Ячейка закрыта"; return false; }
                var other = _backpack[toIndex];
                if (other != null)
                {
                    // Ячейка занята: пробуем надеть то, что в ней, вместо снятого.
                    var otherDef = _db.Item(other.BaseId);
                    if (otherDef == null || !Fits(otherDef.Slot, slot)) { error = "Ячейка занята"; return false; }
                    return Equip(toIndex, out error, slot);
                }
                _backpack[toIndex] = item;
                _equipped.Remove(slot);
                Notify();
                return true;
            }

            for (int i = 0; i < Unlocked; i++)
            {
                if (_backpack[i] != null) continue;
                _backpack[i] = item;
                _equipped.Remove(slot);
                Notify();
                return true;
            }
            error = "Рюкзак полон";
            return false;
        }

        public ItemInstance RemoveAt(int index)
        {
            var it = GetBackpack(index);
            if (it == null) return null;
            _backpack[index] = null;
            Notify();
            return it;
        }

        /// <summary>Надеть предмет напрямую (стартовая экипировка, загрузка сейва).</summary>
        public void ForceEquip(EquipSlot slot, ItemInstance item)
        {
            if (item == null) _equipped.Remove(slot);
            else _equipped[slot] = item;
            Notify();
        }

        /// <summary>Полная замена состояния (загрузка сейва).</summary>
        public void SetState(int capacity, int unlocked, IList<ItemInstance> backpack, IDictionary<EquipSlot, ItemInstance> equipped)
        {
            _backpack = new ItemInstance[Math.Max(capacity, backpack?.Count ?? 0)];
            Unlocked = Math.Clamp(unlocked, 0, _backpack.Length);
            if (backpack != null)
                for (int i = 0; i < backpack.Count; i++)
                    _backpack[i] = backpack[i] != null && _db.Item(backpack[i].BaseId) != null ? backpack[i] : null;
            _equipped.Clear();
            if (equipped != null)
                foreach (var kv in equipped)
                    if (kv.Value != null && _db.Item(kv.Value.BaseId) != null) _equipped[kv.Key] = kv.Value;
            Notify();
        }

        public ItemInstance[] SnapshotBackpack() => (ItemInstance[])_backpack.Clone();

        public void Clear()
        {
            Array.Clear(_backpack, 0, _backpack.Length);
            _equipped.Clear();
            Notify();
        }

        private void Notify()
        {
            Changed?.Invoke();
            EventBus.Publish(new InventoryChangedEvent());
        }
    }
}
