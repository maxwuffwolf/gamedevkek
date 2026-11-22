using Game.Core;
using UnityEngine;

namespace Game.Gameplay.Items
{
    /// <summary>
    /// Столб света «тут лежит лут» — такой же, как у выпавших вещей (LootDrop): луч, ореол, диск на земле, свет.
    /// Вешается на контейнеры с "lootBeam": true, чтобы игрок сразу видел, куда кликать.
    /// </summary>
    public sealed class LootBeam : MonoBehaviour
    {
        private Material _beamMat, _glowMat, _discMat;
        private Light _light;
        private Color _color;
        private float _phase;

        public static LootBeam Create(Transform parent, Color color)
        {
            var go = new GameObject("LootBeam");
            go.transform.SetParent(parent, false);
            var beam = go.AddComponent<LootBeam>();
            beam.Build(color);
            return beam;
        }

        public void SetColor(Color color)
        {
            _color = color;
            if (_light != null) _light.color = color;
        }

        private void Build(Color color)
        {
            _color = color;
            _phase = Random.value * 10f;
            _beamMat = MaterialFactory.CreateTransparent(WithAlpha(color, 0.75f));
            _glowMat = MaterialFactory.CreateTransparent(WithAlpha(color, 0.18f));
            _discMat = MaterialFactory.CreateTransparent(WithAlpha(color, 0.35f));
            MakePart(new Vector3(0f, 3f, 0f), new Vector3(0.09f, 3f, 0.09f), _beamMat);
            MakePart(new Vector3(0f, 2.5f, 0f), new Vector3(0.35f, 2.5f, 0.35f), _glowMat);
            MakePart(new Vector3(0f, 0.02f, 0f), new Vector3(1.1f, 0.01f, 1.1f), _discMat);

            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = color;
            _light.range = 4f;
            _light.intensity = 2.5f;
            _light.shadows = LightShadows.None;
        }

        private void MakePart(Vector3 localPos, Vector3 scale, Material mat)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(p.GetComponent<Collider>());
            p.transform.SetParent(transform, false);
            p.transform.localPosition = localPos;
            p.transform.localScale = scale;
            var r = p.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        private void Update()
        {
            float pulse = 0.8f + 0.2f * Mathf.Sin((Time.time + _phase) * 3f);
            if (_beamMat != null) _beamMat.color = WithAlpha(_color, 0.75f * pulse);
            if (_glowMat != null) _glowMat.color = WithAlpha(_color, 0.18f * pulse);
            if (_discMat != null) _discMat.color = WithAlpha(_color, 0.35f * pulse);
            if (_light != null) _light.intensity = 2.5f * pulse;
        }

        private void OnDestroy()
        {
            if (_beamMat != null) Destroy(_beamMat);
            if (_glowMat != null) Destroy(_glowMat);
            if (_discMat != null) Destroy(_discMat);
        }
    }
}
