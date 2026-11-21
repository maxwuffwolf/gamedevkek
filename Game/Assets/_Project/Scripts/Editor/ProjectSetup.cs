using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Первое открытие проекта: если сцены Showcase ещё нет — генерирует её и открывает.
    /// Плюс меню Game с полезными ярлыками.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        private const string DoneKey = "Gamedevkek.ShowcaseAutoBuilt";

        static ProjectSetup()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += AutoSetup;
        }

        private static void AutoSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoSetup;
                return;
            }
            if (File.Exists(ShowcaseSceneBuilder.ScenePath))
            {
                // первое открытие на этом компьютере: сразу открыть шоукейс вместо пустой Untitled
                if (!SessionState.GetBool(DoneKey, false) && string.IsNullOrEmpty(EditorSceneManager.GetActiveScene().path))
                    EditorSceneManager.OpenScene(ShowcaseSceneBuilder.ScenePath);
                SessionState.SetBool(DoneKey, true);
                return;
            }
            SessionState.SetBool(DoneKey, true);
            Debug.Log("[Game] Первый запуск: собираю сцену Showcase...");
            ShowcaseSceneBuilder.Build();
        }

        [MenuItem("Game/Открыть сцену Showcase", priority = 0)]
        private static void OpenShowcase()
        {
            if (!File.Exists(ShowcaseSceneBuilder.ScenePath)) ShowcaseSceneBuilder.Build();
            else if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ShowcaseSceneBuilder.ScenePath);
        }

        [MenuItem("Game/Папка с данными (JSON)", priority = 20)]
        private static void RevealData() => EditorUtility.RevealInFinder(Path.Combine(Application.streamingAssetsPath, "Data", "items"));

        [MenuItem("Game/Папка с сохранениями", priority = 21)]
        private static void RevealSaves()
        {
            string dir = Path.Combine(Application.persistentDataPath, "saves");
            Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir + Path.DirectorySeparatorChar);
        }

        [MenuItem("Game/Удалить сохранение (новая игра)", priority = 22)]
        private static void DeleteSave()
        {
            string dir = Path.Combine(Application.persistentDataPath, "saves");
            if (!Directory.Exists(dir)) return;
            if (!EditorUtility.DisplayDialog("Удалить сохранения?", dir, "Удалить", "Отмена")) return;
            foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
            Debug.Log("[Game] Сохранения удалены");
        }
    }
}
