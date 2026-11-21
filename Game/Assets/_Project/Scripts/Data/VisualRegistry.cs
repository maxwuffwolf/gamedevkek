using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    /// <summary>Реестр визуалов из Resources/Visuals. Нет визуала → используем заглушку.</summary>
    public sealed class VisualRegistry
    {
        private readonly Dictionary<string, ActorVisual> _actors = new();
        private readonly Dictionary<string, ItemVisual> _items = new();

        public VisualRegistry() => Reload();

        public void Reload()
        {
            _actors.Clear();
            _items.Clear();
            foreach (var v in Resources.LoadAll<ActorVisual>("Visuals"))
                if (!string.IsNullOrEmpty(v.Id)) _actors[v.Id] = v;
            foreach (var v in Resources.LoadAll<ItemVisual>("Visuals"))
                if (!string.IsNullOrEmpty(v.Id)) _items[v.Id] = v;
        }

        public ActorVisual Actor(string id) => id != null && _actors.TryGetValue(id, out var v) ? v : null;
        public ItemVisual Item(string id) => id != null && _items.TryGetValue(id, out var v) ? v : null;
    }
}
