using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Простой сервис-локатор. Сервисы регистрирует бутстрап (Game.App.GameRoot).
    /// Статика сбрасывается при входе в Play Mode (Domain Reload отключён в настройках проекта).
    /// </summary>
    public static class Services
    {
        private static readonly Dictionary<Type, object> _map = new();

        public static void Register<T>(T service) where T : class => _map[typeof(T)] = service;

        public static T Get<T>() where T : class
        {
            if (_map.TryGetValue(typeof(T), out var s)) return (T)s;
            throw new InvalidOperationException($"Сервис {typeof(T).Name} не зарегистрирован");
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (_map.TryGetValue(typeof(T), out var s))
            {
                service = (T)s;
                return true;
            }
            service = null;
            return false;
        }

        public static bool Has<T>() where T : class => _map.ContainsKey(typeof(T));

        public static void Clear() => _map.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _map.Clear();
    }

    /// <summary>Флаг готовности игры: контент загружен, сервисы подняты, игрок создан.</summary>
    public static class GameState
    {
        public static bool IsReady { get; private set; }
        public static event Action Ready;

        public static void SetReady()
        {
            IsReady = true;
            Ready?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsReady = false;
            Ready = null;
        }
    }
}
