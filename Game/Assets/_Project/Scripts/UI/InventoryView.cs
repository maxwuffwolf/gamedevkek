using System.Collections.Generic;
using System.Text;
using Game.Core;
using Game.Data;
using Game.Gameplay.Items;
using Game.Gameplay.Progression;
using Game.Gameplay.World;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Окно инвентаря (B): слева экипировка героя, справа рюкзак (часть ячеек закрыта), статы и золото.</summary>
    public sealed class InventoryView : MonoBehaviour
    {
        private const float Cell = 62f;
        private const float Gap = 6f;
        private const int Columns = 8;

        private ContentDatabase _db;
        private VisualRegistry _visuals;
        private Inventory _inventory;
        private PlayerProfile _profile;
        private readonly List<ItemSlotView> _backpackSlots = new();
        private readonly List<ItemSlotView> _equipSlots = new();
        private Text _stats, _gold, _capacity, _heroName;
        private bool _dirty = true;
        private RectTransform _backpackGrid;

        public bool IsOpen => gameObject.activeSelf;

        public static InventoryView Create(RectTransform canvas)
        {
            var window = UiKit.Image(canvas, "Inventory", UiKit.PanelColor, true).rectTransform;
            window.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1010f, 640f));
            UiKit.Frame(window, UiKit.FrameColor, 3f);
            var view = window.gameObject.AddComponent<InventoryView>();
            view.Build(window);
            window.gameObject.SetActive(false);
            return view;
        }

        private void Build(RectTransform window)
        {
            _db = Services.Get<ContentDatabase>();
            _visuals = Services.Get<VisualRegistry>();
            _inventory = Services.Get<Inventory>();
            _profile = Services.Get<PlayerProfile>();
            _inventory.Changed += MarkDirty;
            _profile.Changed += MarkDirty;

            var title = UiKit.Text(window, "Title", "Инвентарь", 26, UiKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(400f, 34f));
            var close = UiKit.Button(window, "Close", "×", () => Toggle(false), 24);
            close.GetComponent<RectTransform>().Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(36f, 36f));

            // ------------------------------------------------ экипировка
            var equip = UiKit.Image(window, "Equipment", new Color(0f, 0f, 0f, 0.25f)).rectTransform;
            equip.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -56f), new Vector2(400f, 560f));
            UiKit.Frame(equip, new Color(0.35f, 0.3f, 0.22f), 1f);
            var eqTitle = UiKit.Text(equip, "Title", "Снаряжение", 18, UiKit.TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            eqTitle.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(300f, 24f));

            // силуэт героя по центру
            var doll = UiKit.Image(equip, "Doll", new Color(0.12f, 0.11f, 0.1f, 0.9f)).rectTransform;
            doll.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(170f, 300f));
            var head = UiKit.Image(doll, "SilHead", new Color(0.2f, 0.19f, 0.17f)).rectTransform;
            head.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(54f, 54f));
            var torso = UiKit.Image(doll, "SilBody", new Color(0.2f, 0.19f, 0.17f)).rectTransform;
            torso.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(90f, 120f));
            var legs = UiKit.Image(doll, "SilLegs", new Color(0.2f, 0.19f, 0.17f)).rectTransform;
            legs.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -218f), new Vector2(70f, 70f));
            _heroName = UiKit.Text(doll, "Name", "", 16, UiKit.TextColor, TextAnchor.LowerCenter);
            _heroName.rectTransform.Stretch(4, 4, 4, 4);

            AddEquip(equip, EquipSlot.Head, "Голова", new Vector2(-140f, 150f));
            AddEquip(equip, EquipSlot.Shoulders, "Плечи", new Vector2(-140f, 80f));
            AddEquip(equip, EquipSlot.Chest, "Торс", new Vector2(-140f, 10f));
            AddEquip(equip, EquipSlot.Hands, "Руки", new Vector2(-140f, -60f));
            AddEquip(equip, EquipSlot.Neck, "Шея", new Vector2(140f, 150f));
            AddEquip(equip, EquipSlot.Waist, "Пояс", new Vector2(140f, 80f));
            AddEquip(equip, EquipSlot.Legs, "Ноги", new Vector2(140f, 10f));
            AddEquip(equip, EquipSlot.Feet, "Ступни", new Vector2(140f, -60f));
            AddEquip(equip, EquipSlot.MainHand, "Оружие", new Vector2(-120f, -190f));
            AddEquip(equip, EquipSlot.Ring1, "Кольцо", new Vector2(-40f, -190f));
            AddEquip(equip, EquipSlot.Ring2, "Кольцо", new Vector2(40f, -190f));
            AddEquip(equip, EquipSlot.OffHand, "Л. рука", new Vector2(120f, -190f));

            // ------------------------------------------------ рюкзак
            var bag = UiKit.Image(window, "Backpack", new Color(0f, 0f, 0f, 0.25f)).rectTransform;
            bag.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -56f), new Vector2(560f, 560f));
            UiKit.Frame(bag, new Color(0.35f, 0.3f, 0.22f), 1f);
            var bagTitle = UiKit.Text(bag, "Title", "Рюкзак", 18, UiKit.TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            bagTitle.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(300f, 24f));
            _capacity = UiKit.Text(bag, "Capacity", "", 14, UiKit.DimText, TextAnchor.MiddleRight);
            _capacity.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -6f), new Vector2(200f, 24f));

            _backpackGrid = UiKit.Rect("Grid", bag);
            _backpackGrid.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(Columns * Cell + (Columns - 1) * Gap, 340f));
            BuildBackpackSlots();

            _stats = UiKit.Text(bag, "Stats", "", 15, UiKit.TextColor, TextAnchor.UpperLeft);
            _stats.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 12f), new Vector2(380f, 160f));
            _gold = UiKit.Text(bag, "Gold", "", 18, UiKit.Gold, TextAnchor.LowerRight, FontStyle.Bold).Shadowed();
            _gold.rectTransform.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 12f), new Vector2(200f, 30f));
            var help = UiKit.Text(bag, "Help", "ПКМ — надеть/снять\nПеретаскивай предметы\nЗакрытые ячейки открываются\nнаградами за задания",
                12, UiKit.DimText, TextAnchor.LowerRight);
            help.rectTransform.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 48f), new Vector2(220f, 80f));
        }

        private void BuildBackpackSlots()
        {
            foreach (var s in _backpackSlots) Destroy(s.gameObject);
            _backpackSlots.Clear();
            for (int i = 0; i < _inventory.Capacity; i++)
            {
                int index = i;
                var slot = ItemSlotView.Create(_backpackGrid, $"Bag{i}", new Vector2(Cell, Cell), "");
                int col = i % Columns, row = i / Columns;
                slot.GetComponent<RectTransform>().Place(new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(col * (Cell + Gap), -row * (Cell + Gap)), new Vector2(Cell, Cell));
                slot.BackpackIndex = index;
                slot.GetItem = () => _inventory.GetBackpack(index);
                slot.IsLocked = () => !_inventory.IsUnlocked(index);
                slot.RightClicked = OnRightClick;
                slot.DroppedOn = OnDrop;
                _backpackSlots.Add(slot);
            }
        }

        private void AddEquip(RectTransform parent, EquipSlot slotId, string label, Vector2 pos)
        {
            var slot = ItemSlotView.Create(parent, $"Eq_{slotId}", new Vector2(Cell, Cell), label);
            slot.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(Cell, Cell));
            slot.IsEquipment = true;
            slot.EquipSlot = slotId;
            slot.GetItem = () => _inventory.GetEquipped(slotId);
            slot.RightClicked = OnRightClick;
            slot.DroppedOn = OnDrop;
            _equipSlots.Add(slot);
        }

        private void OnDestroy()
        {
            if (_inventory != null) _inventory.Changed -= MarkDirty;
            if (_profile != null) _profile.Changed -= MarkDirty;
        }

        private void MarkDirty() => _dirty = true;

        public void Toggle(bool? open = null)
        {
            bool target = open ?? !gameObject.activeSelf;
            gameObject.SetActive(target);
            if (target)
            {
                transform.SetAsLastSibling();
                _dirty = true;
            }
        }

        // ------------------------------------------------ действия

        private void OnRightClick(ItemSlotView slot)
        {
            string error;
            if (slot.IsEquipment) _inventory.Unequip(slot.EquipSlot, out error);
            else if (_inventory.GetBackpack(slot.BackpackIndex) != null) _inventory.Equip(slot.BackpackIndex, out error);
            else return;
            Report(error);
        }

        private void OnDrop(ItemSlotView from, ItemSlotView to)
        {
            string error = null;
            if (!from.IsEquipment && !to.IsEquipment) _inventory.Move(from.BackpackIndex, to.BackpackIndex);
            else if (!from.IsEquipment && to.IsEquipment) _inventory.Equip(from.BackpackIndex, out error, to.EquipSlot);
            else if (from.IsEquipment && !to.IsEquipment) _inventory.Unequip(from.EquipSlot, out error, to.BackpackIndex);
            else if (from.IsEquipment && to.IsEquipment) error = SwapRings(from.EquipSlot, to.EquipSlot);
            Report(error);
        }

        private string SwapRings(EquipSlot a, EquipSlot b)
        {
            bool rings = (a == EquipSlot.Ring1 || a == EquipSlot.Ring2) && (b == EquipSlot.Ring1 || b == EquipSlot.Ring2);
            if (!rings) return "Не подходит в этот слот";
            var ia = _inventory.GetEquipped(a);
            var ib = _inventory.GetEquipped(b);
            _inventory.ForceEquip(a, ib);
            _inventory.ForceEquip(b, ia);
            return null;
        }

        private static void Report(string error)
        {
            if (!string.IsNullOrEmpty(error)) EventBus.Publish(new UiMessageEvent(error, new Color(1f, 0.45f, 0.4f)));
        }

        // ------------------------------------------------ обновление

        private void Update()
        {
            if (_backpackSlots.Count != _inventory.Capacity)
            {
                BuildBackpackSlots();
                _dirty = true;
            }
            if (!_dirty) return;
            _dirty = false;

            foreach (var s in _backpackSlots) s.Refresh(_db, _visuals);
            foreach (var s in _equipSlots) s.Refresh(_db, _visuals);
            _capacity.text = $"Открыто {_inventory.Unlocked}/{_inventory.Capacity} · свободно {_inventory.FreeSlots}";
            _gold.text = $"{_profile.Gold} золота";
            _heroName.text = $"{_profile.Character?.Name ?? "Герой"}\nУровень {_profile.Level}";
            _stats.text = BuildStats();
        }

        private string BuildStats()
        {
            var p = GameWorld.Player;
            if (p == null) return "";
            var sb = new StringBuilder();
            void Line(string name, string value) => sb.AppendLine($"<color=#9a9080>{name}:</color> {value}");
            Line("Здоровье", $"{p.MaxHealth:0}");
            Line("Мана", $"{p.MaxMana:0}");
            Line("Урон", $"{p.Stats.Get(StatId.PhysDamage):0.#}");
            Line("Скорость атаки", $"{p.Stats.Get(StatId.AttackSpeed):0.00}");
            Line("Броня", $"{p.Stats.Get(StatId.Armor):0}");
            Line("Крит", $"{p.Stats.Get(StatId.CritChance):0.#}% × {p.Stats.Get(StatId.CritMulti):0}%");
            Line("Скорость бега", $"{p.Stats.Get(StatId.MoveSpeed):0.0}");
            return sb.ToString().TrimEnd();
        }

        private void LateUpdate()
        {
            // статы меняются и от уровня/баффов — обновляем раз в полсекунды, пока окно открыто
            if (Time.frameCount % 30 == 0) _stats.text = BuildStats();
        }
    }
}
