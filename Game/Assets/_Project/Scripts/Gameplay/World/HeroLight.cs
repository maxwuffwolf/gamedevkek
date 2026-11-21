using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// Мягкий свет вокруг героя («радиус света»): ночью игрок всегда видит ближайшее окружение.
    /// Следует за целью камеры (<see cref="CameraRig.Target"/>), висит над ней на <see cref="Height"/> метров.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class HeroLight : MonoBehaviour
    {
        public float Height = 3f;
        public float Sharpness = 12f;

        private void LateUpdate()
        {
            var rig = CameraRig.Instance;
            if (rig == null || rig.Target == null) return;
            var target = rig.Target.position + Vector3.up * Height;
            transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-Sharpness * Time.deltaTime));
        }
    }
}
