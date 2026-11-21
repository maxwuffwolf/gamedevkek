using UnityEngine;

namespace Game.Data
{
    /// <summary>Иконка и модель предмета на земле. Связь с JSON по id предмета.</summary>
    [CreateAssetMenu(menuName = "Game/Item Visual", fileName = "ItemVisual")]
    public sealed class ItemVisual : ScriptableObject
    {
        public string Id;
        public Sprite Icon;
        public GameObject WorldPrefab;
    }

}
