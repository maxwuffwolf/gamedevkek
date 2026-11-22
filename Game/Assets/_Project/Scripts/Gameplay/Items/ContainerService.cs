using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Data;
using Game.Gameplay.Progression;
using Game.Gameplay.Zones;
using UnityEngine;

namespace Game.Gameplay.Items
{
    /// <summary>Содержимое конкретного контейнера в мире (то, что лежит в сейве).</summary>
    [Serializable]
    public sealed class ContainerState
    {
        /// <summary>Уникальный ключ экземпляра: "Ravine:ravine_rusty_sword" (сцена:id) или InstanceId из инспектора.</summary>
        public string Key;
        public string DefId;
        public int Gold;
        /// <summary>Ячейки окна (null = пусто). Длина = slots из JSON (или больше, если содержимое не влезло).</summary>
        public List<ItemInstance> Slots = new();

        public bool IsEmpty => Gold <= 0 && Slots.All(s => s == null);
    }

    /// <summary>Открыт контейнер (окно должно показаться).</summary>
    public readonly struct ContainerOpenedEvent
    {
        public readonly WorldContainer Container;
        public ContainerOpenedEvent(WorldContainer container) { Container = container; }
    }

    public readonly struct ContainerClosedEvent { }

    /// <summary>Содержимое какого-то контейнера изменилось (для автосейва и обновления моделей в мире).</summary>
    public readonly struct ContainerChangedEvent
    {
        public readonly string Key;
        public ContainerChangedEvent(string key) { Key = key; }
    }

    /// <summary>
    /// Контейнеры: сундуки, трупы, тайники, «меч в земле». Шаблон — containers/*.json, экземпляр — WorldContainer в сцене.
    /// Содержимое генерируется при первом открытии и дальше хранится тут (persistent → в сейве),
    /// поэтому забранный меч не появится снова после перезахода.
    /// </summary>
    public sealed class ContainerService
    {
        private readonly ContentDatabase _db;
        private readonly ItemGenerator _generator;
        private readonly LootService _loot;
        private readonly RngService _rng;
        private readonly Dictionary<string, ContainerState> _persistent = new();
        private readonly Dictionary<string, ContainerState> _temporary = new();

        /// <summary>Открытый сейчас контейнер (окно на экране) или null.</summary>
        public WorldContainer Current { get; private set; }
        public ContainerState CurrentState => Current != null ? Find(Current.Key) : null;
        public ContainerDef CurrentDef => Current != null ? _db.Container(Current.ContainerId) : null;
        public bool IsOpen => Current != null;

        public ContainerService(ContentDatabase db, ItemGenerator generator, LootService loot, RngService rng)
        {
            _db = db;
            _generator = generator;
            _loot = loot;
            _rng = rng;
            EventBus.Subscribe<ZoneLeavingEvent>(_ =>
            {
                Close();
                ForgetTemporary();
            });
        }

        public IEnumerable<ContainerState> All => _persistent.Values.Concat(_temporary.Values);

        public ContainerState Find(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_persistent.TryGetValue(key, out var s)) return s;
            return _temporary.TryGetValue(key, out s) ? s : null;
        }

        /// <summary>Контейнер уже открывали, и в нём ничего не осталось.</summary>
        public bool IsKnownEmpty(string key) => Find(key)?.IsEmpty ?? false;

        /// <summary>Содержимое (сгенерирует при первом обращении).</summary>
        public ContainerState GetOrCreate(string key, ContainerDef def, int zoneLevel)
        {
            var state = Find(key);
            if (state != null) return state;
            state = Generate(key, def, zoneLevel);
            (def.Persistent ? _persistent : _temporary)[key] = state;
            Changed(key);
            return state;
        }

        private ContainerState Generate(string key, ContainerDef def, int zoneLevel)
        {
            var rng = _rng.Stream("loot");
            int ilvl = def.ItemLevel > 0 ? def.ItemLevel : Math.Max(1, zoneLevel);
            var items = new List<ItemInstance>();
            foreach (var it in def.Items)
            {
                if (it.Chance < 1f && rng.NextDouble() > it.Chance) continue;
                int n = Math.Max(1, it.Amount.Roll(rng));
                for (int i = 0; i < n; i++)
                {
                    var inst = _generator.Create(it.Id, it.Rarity, ilvl, rng);
                    if (inst != null) items.Add(inst);
                }
            }
            int gold = def.Gold.Roll(rng);
            if (!string.IsNullOrEmpty(def.LootTable))
            {
                var res = _loot.Roll(def.LootTable, ilvl);
                items.AddRange(res.Items);
                gold += res.Gold;
            }

            var state = new ContainerState { Key = key, DefId = def.Id, Gold = gold };
            int size = Math.Max(Math.Max(1, def.Slots), items.Count);
            for (int i = 0; i < size; i++) state.Slots.Add(i < items.Count ? items[i] : null);
            return state;
        }

        // ------------------------------------------------------------ окно

        public void Open(WorldContainer container, int zoneLevel)
        {
            var def = _db.Container(container.ContainerId);
            if (def == null)
            {
                Log.Error("Containers", $"{container.name}: нет контейнера \"{container.ContainerId}\" в containers/*.json");
                return;
            }
            if (Current != null && Current != container) Close();
            var state = GetOrCreate(container.Key, def, zoneLevel);
            Current = container;

            // золото забираем сразу, как в Diablo
            if (state.Gold > 0)
            {
                Services.Get<PlayerProfile>().AddGold(state.Gold);
                EventBus.Publish(new UiMessageEvent($"+{state.Gold} золота", new Color(1f, 0.82f, 0.3f)));
                state.Gold = 0;
                Changed(state.Key);
            }
            EventBus.Publish(new ContainerOpenedEvent(container));
        }

        public void Close()
        {
            if (Current == null) return;
            Current = null;
            EventBus.Publish(new ContainerClosedEvent());
        }

        // ------------------------------------------------------------ перекладывание

        /// <summary>Взять предмет из ячейки контейнера в рюкзак (в указанную ячейку или первую свободную).</summary>
        public bool Take(int slot, out string error, int backpackIndex = -1)
        {
            error = null;
            var state = CurrentState;
            var item = state != null && slot >= 0 && slot < state.Slots.Count ? state.Slots[slot] : null;
            if (item == null) return false;

            var inv = Services.Get<Inventory>();
            int target = backpackIndex >= 0 && inv.GetBackpack(backpackIndex) == null && inv.IsUnlocked(backpackIndex) ? backpackIndex : inv.FirstFree();
            if (target < 0)
            {
                error = "Рюкзак полон";
                return false;
            }
            inv.TryPlace(target, item);
            state.Slots[slot] = null;
            var def = _db.Item(item.BaseId);
            EventBus.Publish(new UiMessageEvent($"{def?.Name ?? item.BaseId} — в рюкзаке", _db.RarityColor(item.Rarity)));
            AfterChange(state);
            return true;
        }

        /// <summary>Взять и сразу надеть (перетащили из контейнера на слот экипировки).</summary>
        public bool TakeAndEquip(int slot, EquipSlot equipSlot, out string error)
        {
            var inv = Services.Get<Inventory>();
            int free = inv.FirstFree();
            if (!Take(slot, out error, free)) return false;
            return inv.Equip(free, out error, equipSlot);
        }

        public int TakeAll(out string error)
        {
            error = null;
            var state = CurrentState;
            if (state == null) return 0;
            int taken = 0;
            for (int i = 0; i < state.Slots.Count && CurrentState == state; i++)
            {
                if (state.Slots[i] == null) continue;
                if (!Take(i, out error)) break;
                taken++;
            }
            return taken;
        }

        /// <summary>Положить предмет из рюкзака в контейнер (только если allowPut).</summary>
        public bool Put(int backpackIndex, int slot, out string error)
        {
            error = null;
            var state = CurrentState;
            var def = CurrentDef;
            if (state == null || def == null) return false;
            if (!def.AllowPut)
            {
                error = "Сюда ничего не положить";
                return false;
            }
            if (slot < 0 || slot >= state.Slots.Count || state.Slots[slot] != null)
            {
                slot = state.Slots.IndexOf(null);
                if (slot < 0)
                {
                    error = "Здесь нет места";
                    return false;
                }
            }
            var item = Services.Get<Inventory>().RemoveAt(backpackIndex);
            if (item == null) return false;
            state.Slots[slot] = item;
            AfterChange(state);
            return true;
        }

        /// <summary>Переложить внутри контейнера.</summary>
        public void Move(int from, int to)
        {
            var state = CurrentState;
            if (state == null || from == to || from < 0 || to < 0 || from >= state.Slots.Count || to >= state.Slots.Count) return;
            (state.Slots[from], state.Slots[to]) = (state.Slots[to], state.Slots[from]);
            AfterChange(state);
        }

        private void AfterChange(ContainerState state)
        {
            Changed(state.Key);
            var def = _db.Container(state.DefId);
            // меч забрали / труп обыскан — окно закрывается само, объект в мире гаснет или пропадает
            if (state.IsEmpty && def != null && def.WhenEmpty != ContainerEmptyMode.Keep && Current != null && Current.Key == state.Key)
                Close();
        }

        private static void Changed(string key) => EventBus.Publish(new ContainerChangedEvent(key));

        // ------------------------------------------------------------ сейв / отладка

        /// <summary>Временные контейнеры (persistent: false) забываются при смене зоны.</summary>
        public void ForgetTemporary() => _temporary.Clear();

        public List<ContainerState> Capture() => _persistent.Values.ToList();

        public void SetState(IEnumerable<ContainerState> states)
        {
            Close();
            _persistent.Clear();
            _temporary.Clear();
            if (states != null)
                foreach (var s in states)
                {
                    if (s == null || string.IsNullOrEmpty(s.Key)) continue;
                    s.Slots ??= new List<ItemInstance>();
                    for (int i = 0; i < s.Slots.Count; i++)
                        if (s.Slots[i] != null && _db.Item(s.Slots[i].BaseId) == null) s.Slots[i] = null; // предмет удалили из JSON
                    _persistent[s.Key] = s;
                }
            Changed(null);
        }

        /// <summary>Забыть содержимое (при следующем открытии сгенерируется заново по JSON). key = null — все.</summary>
        public int Reset(string key = null)
        {
            Close();
            int n;
            if (string.IsNullOrEmpty(key))
            {
                n = _persistent.Count + _temporary.Count;
                _persistent.Clear();
                _temporary.Clear();
            }
            else
            {
                n = (_persistent.Remove(key) ? 1 : 0) + (_temporary.Remove(key) ? 1 : 0);
            }
            Changed(key);
            return n;
        }
    }
}
