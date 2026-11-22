using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Делает Image «таблеткой» или кругом: радиус скругления = половина меньшей стороны.
    /// 9-slice сам по себе ужимает углы только по одной оси (получался эллипс на тонких полосах),
    /// поэтому при каждом изменении размера подбираем спрайт с нужным радиусом.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class PillShape : MonoBehaviour
    {
        private int _ring;

        /// <summary>0 — залитая форма, иначе толщина обводки в пикселях.</summary>
        public int RingThickness
        {
            get => _ring;
            set
            {
                _ring = value;
                _radius = -1;
                Apply();
            }
        }

        private Image _image;
        private int _radius = -1;

        private void Awake() => Apply();
        private void OnEnable() => Apply();
        private void Start() => Apply();
        private void OnRectTransformDimensionsChange() => Apply();
        // страховка: размер мог поменяться без события (родитель растянулся в том же кадре и т.п.)
        private void LateUpdate() => Apply();

        public void Apply()
        {
            if (_image == null) _image = GetComponent<Image>();
            if (_image == null) return;
            var size = ((RectTransform)transform).rect.size;
            int r = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(size.x, size.y) * 0.5f), 2, 128);
            if (r == _radius && _image.sprite != null) return;
            _radius = r;
            _image.sprite = _ring > 0 ? UiKit.RoundedRing(r, Mathf.Min(_ring, r)) : UiKit.Rounded(r);
            _image.type = Image.Type.Sliced;
        }
    }
}
