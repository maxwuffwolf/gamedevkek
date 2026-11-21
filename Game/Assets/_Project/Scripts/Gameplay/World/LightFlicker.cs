using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>Мерцание света (костёр, факел).</summary>
    [RequireComponent(typeof(Light))]
    public sealed class LightFlicker : MonoBehaviour
    {
        public float BaseIntensity = 3f;
        public float Amount = 0.6f;
        public float Speed = 6f;

        private Light _light;
        private float _seed;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _seed = Random.value * 100f;
        }

        private void Update()
        {
            float n = Mathf.PerlinNoise(_seed, Time.time * Speed) - 0.5f;
            _light.intensity = BaseIntensity + n * 2f * Amount;
        }
    }
}
