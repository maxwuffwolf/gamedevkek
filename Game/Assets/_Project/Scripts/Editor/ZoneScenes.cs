using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Связь zones/*.json ↔ сцены проекта. Unity умеет загружать во время игры только сцены из Build Settings,
    /// поэтому список сцен собирается отсюда: стартовая зона первой, дальше остальные в порядке JSON.
    /// </summary>
    public static class ZoneScenes
    {
        [MenuItem("Game/Зоны/Обновить список сцен (Build Settings)", priority = 30)]
        private static void SyncMenu()
        {
            string report = SyncBuildSettings();
            Debug.Log("[Game] " + report);
            EditorUtility.DisplayDialog("Зоны", report, "OK");
        }

        [MenuItem("Game/Зоны/Открыть сцену Овраг", priority = 10)]
        private static void OpenRavine()
        {
            if (!File.Exists(RavineSceneBuilder.ScenePath)) RavineSceneBuilder.Build();
            else if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(RavineSceneBuilder.ScenePath);
        }

        [MenuItem("Game/Зоны/Открыть сцену Лес (Showcase)", priority = 11)]
        private static void OpenForest()
        {
            if (!File.Exists(ShowcaseSceneBuilder.ScenePath)) ShowcaseSceneBuilder.Build();
            else if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ShowcaseSceneBuilder.ScenePath);
        }

        /// <summary>
        /// Переписать Build Settings: сцены всех зон (стартовая первой) + <paramref name="alsoInclude"/> + прочие уже добавленные.
        /// Возвращает отчёт (каких сцен не хватает).
        /// </summary>
        public static string SyncBuildSettings(string alsoInclude = null)
        {
            var db = new ContentDatabase();
            db.LoadAll(ContentDatabase.DefaultRoot);

            var zones = db.Zones.Values.OrderByDescending(z => z.Start).ToList(); // OrderBy стабилен: порядок JSON сохраняется
            var result = new List<EditorBuildSettingsScene>();
            var sb = new StringBuilder();
            var missing = new List<string>();

            void Add(string path)
            {
                if (string.IsNullOrEmpty(path) || result.Any(s => s.path == path)) return;
                result.Add(new EditorBuildSettingsScene(path, true));
            }

            foreach (var z in zones)
            {
                string path = FindScene(z.Scene);
                if (path == null) missing.Add($"{z.Id} → {z.Scene}.unity");
                Add(path);
            }
            Add(alsoInclude);
            foreach (var s in EditorBuildSettings.scenes) Add(s.path);
            EditorBuildSettings.scenes = result.ToArray();

            sb.Append($"Build Settings: {string.Join(", ", result.Select(s => Path.GetFileNameWithoutExtension(s.path)))}.");
            if (missing.Count > 0)
                sb.Append($"\nНет сцен для зон: {string.Join(", ", missing)}. Собери их (меню Game → Зоны) или поправь \"scene\" в zones.json.");
            return sb.ToString();
        }

        /// <summary>Путь к сцене по имени файла (без .unity), или null.</summary>
        public static string FindScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) return null;
            return AssetDatabase.FindAssets($"t:Scene {sceneName}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => string.Equals(Path.GetFileNameWithoutExtension(p), sceneName, System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
