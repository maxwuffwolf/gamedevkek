using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Внешний вид актора (игрок, моб, NPC). Связь с JSON по полю "prefab".
    /// Создать: ПКМ в Project → Create → Game → Actor Visual, положить в любую папку Resources/Visuals.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Actor Visual", fileName = "ActorVisual")]
    public sealed class ActorVisual : ScriptableObject
    {
        public string Id;
        public GameObject Prefab;
        public float Scale = 1f;
        [Tooltip("Смещение модели относительно капсулы персонажа")]
        public Vector3 Offset;
    }

}
