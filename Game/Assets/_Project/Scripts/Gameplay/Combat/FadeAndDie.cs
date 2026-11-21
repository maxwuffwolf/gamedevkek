using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>Плавно гасит прозрачный материал и удаляет объект вместе с временными ассетами.</summary>
    public sealed class FadeAndDie : MonoBehaviour
    {
        private float _duration = 0.3f;
        private float _t;
        private Material _material;
        private Mesh _mesh;
        private float _startAlpha;

        public void Init(float duration, Material material, Mesh ownedMesh = null)
        {
            _duration = duration;
            _material = material;
            _mesh = ownedMesh;
            _startAlpha = material != null ? material.color.a : 1f;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / _duration);
            if (_material != null)
            {
                var c = _material.color;
                c.a = _startAlpha * (1f - k);
                _material.color = c;
            }
            transform.localScale = Vector3.one * (1f + 0.15f * k);
            if (k >= 1f) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
