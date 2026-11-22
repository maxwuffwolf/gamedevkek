using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Zones
{
    /// <summary>
    /// Точка прибытия в зону. Портал в другой сцене ссылается на неё по <see cref="Id"/>
    /// (например, портал «в лес» из оврага ведёт на вход "from_ravine" в лесу).
    /// Синяя стрелка в окне Scene показывает, куда игрок будет смотреть.
    /// </summary>
    public sealed class ZoneEntrance : MonoBehaviour
    {
        public string Id = "from_somewhere";

        private static readonly List<ZoneEntrance> _all = new();
        public static IReadOnlyList<ZoneEntrance> All => _all;

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);

        public static ZoneEntrance Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var e in _all)
                if (e != null && string.Equals(e.Id, id, System.StringComparison.OrdinalIgnoreCase)) return e;
            return null;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireCube(transform.position + Vector3.up, new Vector3(1f, 2f, 1f));
            Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.up + transform.forward * 1.8f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _all.Clear();
    }
}
