using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    /// <summary>Показывает тултип при наведении на элемент UI. Текст берётся из делегата в момент наведения.</summary>
    public sealed class HoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Func<string> GetText;
        public Func<Color> GetBorder;
        public float Width = 320f;
        private bool _hovered;

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            Refresh();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            if (TooltipView.Instance != null) TooltipView.Instance.Hide(this);
        }

        public void Refresh()
        {
            if (!_hovered || TooltipView.Instance == null) return;
            string text = GetText?.Invoke();
            if (string.IsNullOrEmpty(text))
            {
                TooltipView.Instance.Hide(this);
                return;
            }
            TooltipView.Instance.Show(this, text, GetBorder?.Invoke() ?? UiKit.FrameColor, Width);
        }

        private void OnDisable()
        {
            if (_hovered && TooltipView.Instance != null) TooltipView.Instance.Hide(this);
            _hovered = false;
        }
    }
}
