using System.Collections;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay.Actors
{
    /// <summary>
    /// Труп падает на бок, ждёт и растворяется (прозрачность + подъём + сжатие), затем удаляется.
    /// Модели со своей анимацией смерти (Animator с триггером Die, напр. гуль) не опрокидываются и не
    /// перекрашиваются: лежат, как упали, а потом уходят в землю.
    /// </summary>
    public sealed class CorpseDissolver : MonoBehaviour
    {
        public static void Begin(GameObject target, Transform model, float delay, float duration)
        {
            var d = target.AddComponent<CorpseDissolver>();
            d.StartCoroutine(d.Run(model, delay, duration));
        }

        private readonly List<Material> _materials = new();

        private static bool HasDeathAnimation(Transform model)
        {
            var anim = model != null ? model.GetComponentInChildren<Animator>() : null;
            if (anim == null || anim.runtimeAnimatorController == null) return false;
            foreach (var p in anim.parameters)
                if (p.name == "Die" && p.type == AnimatorControllerParameterType.Trigger) return true;
            return false;
        }

        private IEnumerator SinkIntoGround(float delay, float duration)
        {
            yield return new WaitForSeconds(delay + 0.8f); // + время самой анимации падения
            foreach (var r in GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Vector3 startPos = transform.position;
            Vector3 startScale = transform.localScale;
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.1f, duration * 1.5f))
            {
                transform.position = startPos + Vector3.down * (0.7f * t * t);
                transform.localScale = startScale * (1f - 0.25f * t);
                yield return null;
            }
            Destroy(gameObject);
        }

        private IEnumerator Run(Transform model, float delay, float duration)
        {
            if (HasDeathAnimation(model))
            {
                yield return SinkIntoGround(delay, duration);
                yield break;
            }
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
