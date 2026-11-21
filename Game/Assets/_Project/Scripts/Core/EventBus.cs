using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Шина событий. События это обычные struct/class.
    /// EventBus.Subscribe&lt;MonsterKilledEvent&gt;(OnKilled); EventBus.Publish(new MonsterKilledEvent(...));
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> _handlers = new();

        public static void Subscribe<T>(Action<T> handler)
        {
            _handlers.TryGetValue(typeof(T), out var existing);
            _handlers[typeof(T)] = Delegate.Combine(existing, handler);
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            if (!_handlers.TryGetValue(typeof(T), out var existing)) return;
            var result = Delegate.Remove(existing, handler);
            if (result == null) _handlers.Remove(typeof(T));
            else _handlers[typeof(T)] = result;
        }

        public static void Publish<T>(T evt)
        {
            if (!_handlers.TryGetValue(typeof(T), out var d)) return;
            foreach (var h in d.GetInvocationList())
            {
                try
                {
                    ((Action<T>)h).Invoke(evt);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _handlers.Clear();
    }

    /// <summary>Сообщение для игрока (всплывающая строка по центру экрана).</summary>
    public readonly struct UiMessageEvent
    {
        public readonly string Text;
        public readonly Color Color;
        public UiMessageEvent(string text, Color color) { Text = text; Color = color; }
        public UiMessageEvent(string text) : this(text, Color.white) { }
    }
}
