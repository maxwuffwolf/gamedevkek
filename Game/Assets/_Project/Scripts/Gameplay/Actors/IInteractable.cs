using UnityEngine;

namespace Game.Gameplay.Actors
{
    /// <summary>То, на что можно кликнуть: NPC, лут, сундук, портал...</summary>
    public interface IInteractable
    {
        /// <summary>Текст при наведении (поддерживает rich text).</summary>
        string HoverText { get; }
        /// <summary>Подсказка действия: «Поговорить», «Подобрать».</summary>
        string ActionText { get; }
        Vector3 InteractPosition { get; }
        float InteractRange { get; }
        bool CanInteract { get; }
        void Interact(Actor player);
    }
}
