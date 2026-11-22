using System.Collections.Generic;
using Game.Core;
using Game.Data;
using Game.Gameplay.Items;
using Game.Gameplay.World;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Окно контейнера (сундук, труп, меч в земле): слева экрана, рядом сам открывается инвентарь.
    /// ПКМ по предмету — взять в рюкзак, можно перетаскивать в рюкзак и прямо на слот экипировки.
    /// Если в JSON у контейнера "allowPut": true — можно класть свои вещи обратно (ПКМ в рюкзаке / перетаскивание).
    /// </summary>
    public sealed class ContainerView : MonoBehaviour
    {
        public static ContainerView Instance { get; private set; }

        private const float Cell = 62f;
        private const float Gap = 6f;
        private const int Columns = 4;
        private const float Width = Columns * Cell + (Columns - 1) * Gap + 40f;

        private ContentDatabase _db;
        private VisualRegistry _visuals;
        private ContainerService _service;
        private InventoryView _inventory;
        private RectTransform _window, _grid;
        private Text _title, _description, _hint;
        private Button _takeAll;
        private readonly List<ItemSlotView> _slots = new();
        private bool _dirty;
        private bool _openedInventory;

        public bool IsOpen => gameObject.activeSelf;

        public static ContainerView Create(RectTransform canvas, InventoryView inventory)
        {
            var window = UiKit.Panel(canvas, "Container", UiTheme.Panel, UiTheme.RadiusWindow, UiTheme.Line).rectTransform;
            var view = window.gameObject.AddComponent<ContainerView>();
            view._window = window;
            view._inventory = inventory;
            view.Build();
            window.gameObject.SetActive(false);
            Instance = view;
            return view;
        }

        private void Build()
        {
            _db = Services.Get<ContentDatabase>();
            _visuals = Services.Get<VisualRegistry>();
            _service = Services.Get<ContainerService>();

            _title = UiKit.Text(_window, "Title", "", 22, UiTheme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            _title.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -14f), new Vector2(Width - 70f, 30f));
            UiKit.CloseButton(_window, () => _service.Close());

            _description = UiKit.Text(_window, "Description", "", 14, UiTheme.Dim, TextAnchor.UpperLeft);
            _description.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(Width - 40f, 40f));

            _grid = UiKit.Rect("Grid", _window);

            _takeAll = UiKit.Button(_window, "TakeAll", "Взять всё", OnTakeAll, 16, primary: true);
            _takeAll.GetComponent<RectTransform>().Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 38f), new Vector2(140f, 36f));
            _hint = UiKit.Text(_window, "Hint", "", 12, UiTheme.Dim, TextAnchor.MiddleLeft);
            _hint.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 10f), new Vector2(Width - 40f, 22f));

            EventBus.Subscribe<ContainerOpenedEvent>(OnOpened);
            EventBus.Subscribe<ContainerClosedEvent>(OnClosed);
            EventBus.Subscribe<ContainerChangedEvent>(OnChanged);
            EventBus.Subscribe<InventoryChangedEvent>(OnInventoryChanged);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<ContainerOpenedEvent>(OnOpened);
            EventBus.Unsubscribe<ContainerClosedEvent>(OnClosed);
            EventBus.Unsubscribe<ContainerChangedEvent>(OnChanged);
            EventBus.Unsubscribe<InventoryChangedEvent>(OnInventoryChanged);
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------ открытие / закрытие

        private void OnOpened(ContainerOpenedEvent e)
        {
            var def = _service.CurrentDef;
            var state = _service.CurrentState;
            if (def == null || state == null) return;

            _title.text = string.IsNullOrEmpty(def.Title) ? def.Name : def.Title;
            _description.text = def.Description ?? "";
            _description.gameObject.SetActive(!string.IsNullOrEmpty(def.Description));
            _hint.text = def.AllowPut ? "ПКМ — взять · ПКМ в рюкзаке — положить сюда" : "ПКМ — взять · перетащи в рюкзак или на героя";
            ItemSlotView.ContainerHint = def.AllowPut ? "ПКМ — положить в контейнер · перетащи" : null;

            BuildSlots(state.Slots.Count);
            Layout(!string.IsNullOrEmpty(def.Description), state.Slots.Count);

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _openedInventory = !_inventory.IsOpen;
            if (_openedInventory) _inventory.Toggle(true);
            _dirty = true;
        }

        private void OnClosed(ContainerClosedEvent e)
        {
            ItemSlotView.ContainerHint = null;
            if (!gameObject.activeSelf) return;
            gameObject.SetActive(false);
            if (_openedInventory && _inventory.IsOpen) _inventory.Toggle(false);
            _openedInventory = false;
        }

        private void OnChanged(ContainerChangedEvent e) => _dirty = true;
        private void OnInventoryChanged(InventoryChangedEvent e) => _dirty = true;

        private void Layout(bool hasDescription, int slotCount)
        {
            int rows = Mathf.Max(1, Mathf.CeilToInt(slotCount / (float)Columns));
            float gridH = rows * Cell + (rows - 1) * Gap;
            float top = hasDescription ? 100f : 58f;
            float height = top + gridH + 94f;
            _window.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(60f, 40f), new Vector2(Width, height));
            _grid.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -top), new Vector2(Width - 40f, gridH));
        }

        private void BuildSlots(int count)
        {
            if (_slots.Count == count) return;
            foreach (var s in _slots) Destroy(s.gameObject);
            _slots.Clear();
            for (int i = 0; i < count; i++)
            {
                int index = i;
                var slot = ItemSlotView.Create(_grid, $"Slot{i}", new Vector2(Cell, Cell), "");
                int col = i % Columns, row = i / Columns;
                slot.GetComponent<RectTransform>().Place(new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(col * (Cell + Gap), -row * (Cell + Gap)), new Vector2(Cell, Cell));
                slot.ContainerIndex = index;
                slot.GetItem = () =>
                {
                    var st = _service.CurrentState;
                    return st != null && index < st.Slots.Count ? st.Slots[index] : null;
                };
                slot.RightClicked = s => Report(_service.Take(s.ContainerIndex, out var err) ? null : err);
                slot.DroppedOn = OnDropOnContainer;
                _slots.Add(slot);
            }
        }

        // ------------------------------------------------------------ действия

        private void OnTakeAll()
        {
            _service.TakeAll(out var error);
            Report(error);
        }

        /// <summary>Перетащили что-то на ячейку контейнера.</summary>
        private void OnDropOnContainer(ItemSlotView from, ItemSlotView to)
        {
            string error = null;
            if (from.IsContainer) _service.Move(from.ContainerIndex, to.ContainerIndex);
            else if (from.IsEquipment) error = "Сначала сними предмет в рюкзак";
            else _service.Put(from.BackpackIndex, to.ContainerIndex, out error);
            Report(error);
        }

        /// <summary>Перетащили из контейнера в окно инвентаря (вызывает InventoryView).</summary>
        public void TakeInto(ItemSlotView from, ItemSlotView to)
        {
            string error;
            if (to.IsEquipment) _service.TakeAndEquip(from.ContainerIndex, to.EquipSlot, out error);
            else _service.Take(from.ContainerIndex, out error, to.BackpackIndex);
            Report(error);
        }

        /// <summary>ПКМ по вещи в рюкзаке, пока открыт контейнер, в который можно класть.</summary>
        public bool TryPutFromBackpack(int backpackIndex)
        {
            if (!IsOpen || _service.CurrentDef == null || !_service.CurrentDef.AllowPut) return false;
            _service.Put(backpackIndex, -1, out var error);
            Report(error);
            return true;
        }

        private static void Report(string error)
        {
            if (!string.IsNullOrEmpty(error)) EventBus.Publish(new UiMessageEvent(error, new Color(1f, 0.45f, 0.4f)));
        }

        // ------------------------------------------------------------ обновление

        private void Update()
        {
            var current = _service.Current;
            if (current == null)
            {
                _service.Close(); // объект уничтожен (смена сцены) — закрываемся
                OnClosed(default);
                return;
            }
            // отошёл от сундука — закрыли
            var player = GameWorld.Player;
            if (player != null)
            {
                Vector3 d = player.transform.position - current.InteractPosition;
                d.y = 0f;
                if (d.magnitude > current.InteractRange + 2.5f)
                {
                    _service.Close();
                    return;
                }
            }

            if (!_dirty) return;
            _dirty = false;
            var state = _service.CurrentState;
            if (state != null && state.Slots.Count != _slots.Count)
            {
                BuildSlots(state.Slots.Count);
                Layout(_description.gameObject.activeSelf, state.Slots.Count);
            }
            foreach (var s in _slots) s.Refresh(_db, _visuals);
            bool any = state != null && state.Slots.Exists(i => i != null);
            _takeAll.interactable = any;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;
    }
}
