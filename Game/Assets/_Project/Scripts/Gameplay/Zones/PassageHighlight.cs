using Game.Gameplay.World;
using UnityEngine;

namespace Game.Gameplay.Zones
{
    /// <summary>
    /// Подсветка прохода между зонами при наведении курсора (как в PoE): «глубина» прохода
    /// (материал Game/Passage Glow, свойство _Hover) плавно заливается светом, свет внутри прохода
    /// разгорается, название зоны над проходом показывает ZonePortal (LabelOnHoverOnly). Без наведения проход — просто тёмный
    /// просвет в зарослях, часть пейзажа.
    /// Вешается рядом с <see cref="ZonePortal"/> (собирает ZoneGateBuilder, стиль Thicket).
    /// </summary>
    public sealed class PassageHighlight : MonoBehaviour
    {
        public Renderer[] GlowRenderers;
        public Light GlowLight;
        public float LightIdle = 0.5f;
        public float LightHover = 4f;
        [Tooltip("Скорость разгорания/затухания (1/с)")]
        public float FadeSpeed = 6f;

        private static readonly int HoverId = Shader.PropertyToID("_Hover");
        private ZonePortal _portal;
        private MaterialPropertyBlock _mpb;
        private float _t = -1f;

        private void Awake()
        {
            _portal = GetComponentInParent<ZonePortal>();
            _mpb = new MaterialPropertyBlock();
            Apply(0f);
        }

        private void Update()
        {
            var pc = GameWorld.PlayerController;
            bool hovered = pc != null && _portal != null && ReferenceEquals(pc.Hovered, _portal);
            float target = hovered ? 1f : 0f;
            float t = Mathf.MoveTowards(Mathf.Max(_t, 0f), target, FadeSpeed * Time.deltaTime);
            if (!Mathf.Approximately(t, _t)) Apply(t);
        }

        private void Apply(float t)
        {
            _t = t;
            float eased = t * t * (3f - 2f * t);
            if (GlowRenderers != null)
                foreach (var r in GlowRenderers)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(_mpb);
                    _mpb.SetFloat(HoverId, eased);
                    r.SetPropertyBlock(_mpb);
                }
            if (GlowLight != null) GlowLight.intensity = Mathf.Lerp(LightIdle, LightHover, eased);
        }
    }
}
