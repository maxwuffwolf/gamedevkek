using System.Collections;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay.Actors
{
    /// <summary>Труп падает на бок, ждёт и растворяется (прозрачность + подъём + сжатие), затем удаляется.</summary>
    public sealed class CorpseDissolver : MonoBehaviour
    {
        public static void Begin(GameObject target, Transform model, float delay, float duration)
        {
            var d = target.AddComponent<CorpseDissolver>();
            d.StartCoroutine(d.Run(model, delay, duration));
        }

        private readonly List<Material> _materials = new();

        private IEnumerator Run(Transform model, float delay, float duration)
        {
            if (model != null)
            {
                // падение на бок
                Quaternion from = model.localRotation;
                Quaternion to = from * Quaternion.Euler(0f, 0f, 90f);
                Vector3 p0 = model.localPosition;
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
                {
                    model.localRotation = Quaternion.Slerp(from, to, t * t);
                    model.localPosition = p0 + new Vector3(0f, 0.25f * t, 0f);
                    yield return null;
                }
            }

            yield return new WaitForSeconds(delay);

            // переключаем материалы на прозрачные того же цвета
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                var src = r.sharedMaterial;
                Color c = src != null && src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor")
                    : src != null && src.HasProperty("_Color") ? src.color : Color.gray;
                c.a = 1f;
                var m = MaterialFactory.CreateTransparent(c);
                r.sharedMaterial = m;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _materials.Add(m);
            }

            Vector3 startScale = transform.localScale;
            Vector3 startPos = transform.position;
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.1f, duration))
            {
                foreach (var m in _materials)
                {
                    var c = m.color;
                    c.a = 1f - t;
                    m.color = c;
                }
                transform.localScale = startScale * (1f - 0.4f * t);
                transform.position = startPos + Vector3.up * (0.6f * t);
                yield return null;
            }
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            foreach (var m in _materials)
                if (m != null) Destroy(m);
        }
    }
}
