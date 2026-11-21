using UnityEngine;

namespace Game.Core
{
    /// <summary>Обёртка над Debug.Log с категориями: [Content] ..., [Loot] ... (фильтруется в дев-консоли).</summary>
    public static class Log
    {
        public static void Info(string category, string message) => Debug.Log($"[{category}] {message}");
        public static void Warn(string category, string message) => Debug.LogWarning($"[{category}] {message}");
        public static void Error(string category, string message) => Debug.LogError($"[{category}] {message}");
    }
}
