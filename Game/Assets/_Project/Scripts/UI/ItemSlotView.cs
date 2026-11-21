using System;
using Game.Core;
using Game.Data;
using Game.Gameplay.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Ячейка с предметом (рюкзак или экипировка). ПКМ — надеть/снять, перетаскивание — переложить/надеть.
    /// Логика действий в InventoryView, ячейка только сообщает о событиях.
    /// </summary>
    public sealed class ItemSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        public bool IsEquipment;
        public int BackpackIndex = -1;
        public EquipSlot EquipSlot;

        public Func<ItemInstance> GetItem;
        public Func<bool> IsLocked;
        public Action<ItemSlotView> RightClicked;
        public Action<ItemSlotView, ItemSlotView> DroppedOn; // (откуда, куда)

        private Image _bg;
        private Image _rarityFrame;
        private Image _icon;
        private Text _iconText;
        private Text _placeholder;
        private bool _hovered;
        private string _placeholderLabel;

        private static ItemSlotView _dragSource;
        private static Image _dragGhost;

        public static ItemSlotView Create(Transform parent, string name, Vector2 size, string placeholder)
        {
            var frame = UiKit.Image(parent, name, new Color(0.3f, 0.27f, 0.22f), true);
            frame.rectTransform.sizeDelta = size;
            var view = frame.gameObject.AddComponent<ItemSlotView>();
            view._rarityFrame = frame;
            view._bg = UiKit.Image(frame.transform, "Bg", UiKit.SlotColor);
            view._bg.rectTransform.Stretch(2, 2, 2, 2);
            view._placeholder = UiKit.Text(frame.transform, "Placeholder", placeholder, 11, new Color(1f, 1f, 1f, 0.25f));
            view._placeholder.rectTransform.Stretch(2, 2, 2, 2);
            view._placeholderLabel = placeholder;
            view._icon = UiKit.Image(frame.transform, "Icon", Color.clear);
            view._icon.rectTransform.Stretch(6, 6, 6, 6);
            view._iconText = UiKit.Text(view._icon.transform, "Abbr", "", 15, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            view._iconText.rectTransform.Stretch();
            return view;
        }

        public void Refresh(ContentDatabase db, VisualRegistry visuals)
        {
            bool locked = IsLocked != null && IsLocked();
            var item = GetItem?.Invoke();
            _bg.color = locked ? new Color(0.05f, 0.05f, 0.05f, 0.95f) : UiKit.SlotColor;
            if (locked)
            {
                _rarityFrame.color = new Color(0.15f, 0.14f, 0.12f);
                _icon.color = Color.clear;
                _icon.sprite = UiKit.White;
                _iconText.text = "";
                _placeholder.enabled = true;
                _placeholder.text = "×";
                _placeholder.fontSize = 22;
                return;
            }
            _placeholder.text = _placeholderLabel;
            _placeholder.fontSize = 11;

            if (item == null)
            {
                _rarityFrame.color = new Color(0.3f, 0.27f, 0.22f);
                _icon.color = Color.clear;
                _iconText.text = "";
                _placeholder.enabled = true;
                return;
            }

            var def = db.Item(item.BaseId);
            var visual = visuals.Item(item.BaseId);
            _placeholder.enabled = false;
            _rarityFrame.color = db.RarityColor(item.Rarity);
            if (visual != null && visual.Icon != null)
            {
                _icon.sprite = visual.Icon;
                _icon.color = Color.white;
                _iconText.text = "";
            }
            else
            {
                _icon.sprite = UiKit.White;
                _icon.color = ColorUtil.Parse(def?.Icon?.Color, Color.gray);
                _iconText.text = def?.Icon?.Text ?? "?";
            }
            if (_hovered) ShowTooltip();
        }

        private void ShowTooltip()
        {
            var item = GetItem?.Invoke();
            if (item == null || TooltipView.Instance == null)
            {
                TooltipView.Instance?.Hide(this);
                return;
            }
            var db = Services.Get<ContentDatabase>();
            string hint = IsEquipment ? "ПКМ — снять" : "ПКМ — надеть · перетащи, чтобы переложить";
            TooltipView.Instance.Show(this, TooltipView.ItemText(db, item, hint), db.RarityColor(item.Rarity));
        }

        public void OnPointerEnter(PointerEventData e)
        {
            _hovered = true;
            ShowTooltip();
        }

        public void OnPointerExit(PointerEventData e)
        {
            _hovered = false;
            TooltipView.Instance?.Hide(this);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Right) RightClicked?.Invoke(this);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || GetItem?.Invoke() == null) return;
            _dragSource = this;
            var canvas = GetComponentInParent<Canvas>().rootCanvas;
            _dragGhost = UiKit.Image(canvas.transform, "DragGhost", _icon.color);
            _dragGhost.sprite = _icon.sprite;
            _dragGhost.rectTransform.sizeDelta = _icon.rectTransform.rect.size;
            var t = UiKit.Text(_dragGhost.transform, "Abbr", _iconText.text, 15, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Stretch();
            var group = _dragGhost.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.alpha = 0.85f;
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (_dragGhost != null) _dragGhost.transform.position = e.position;
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (_dragGhost != null) Destroy(_dragGhost.gameObject);
            _dragGhost = null;
            _dragSource = null;
        }

        public void OnDrop(PointerEventData e)
        {
            if (_dragSource != null && _dragSource != this) DroppedOn?.Invoke(_dragSource, this);
        }

        private void OnDisable()
        {
            if (_hovered) TooltipView.Instance?.Hide(this);
            _hovered = false;
            if (_dragSource == this && _dragGhost != null)
            {
                Destroy(_dragGhost.gameObject);
                _dragGhost = null;
                _dragSource = null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _dragSource = null;
            _dragGhost = null;
        }
    }
}
