using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>Где появляется игрок (и где воскресает). Одна на сцену.</summary>
    public sealed class PlayerSpawnPoint : MonoBehaviour
    {
        public static PlayerSpawnPoint Current { get; private set; }

        private void Awake() => Current = this;

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.5f);
            Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.up + transform.forward * 1.5f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Current = null;
    }
}
