using Game.Gameplay.Actors;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>Быстрый доступ к ключевым объектам текущей сцены.</summary>
    public static class GameWorld
    {
        public static Actor Player;
        public static PlayerController PlayerController;

        public static bool PlayerAlive => Player != null && !Player.IsDead;

        /// <summary>Точка на земле под позицией (луч сверху вниз).</summary>
        public static Vector3 SnapToGround(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 20f, Vector3.down, out var hit, 60f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point;
            p.y = 0f;
            return p;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Player = null;
            PlayerController = null;
        }
    }
}
