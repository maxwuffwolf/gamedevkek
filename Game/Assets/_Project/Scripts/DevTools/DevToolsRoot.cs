using System.Collections;
using Game.Core;
using UnityEngine;

namespace Game.DevTools
{
    /// <summary>
    /// Поднимает дев-инструменты после старта игры. Сборка Game.DevTools компилируется только в редакторе
    /// и Development Build, поэтому в релизном билде консоли и читов просто нет.
    /// </summary>
    public sealed class DevToolsRoot : MonoBehaviour
    {
        private static DevToolsRoot _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (_instance != null) return;
            var go = new GameObject("[DevTools]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<DevToolsRoot>();
            go.AddComponent<DevConsole>();
        }

        private IEnumerator Start()
        {
            while (!GameState.IsReady) yield return null;
            gameObject.AddComponent<HotReloader>();
            gameObject.AddComponent<CheatMenu>();
            gameObject.AddComponent<DebugOverlay>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;
    }
}
