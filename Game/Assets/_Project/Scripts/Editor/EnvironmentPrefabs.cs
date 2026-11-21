using System.IO;
using System.Linq;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Модели окружения → префабы. Кладёшь FBX `Env_*` в <see cref="ModelsDir"/>, и после импорта
    /// для каждой модели сам собирается префаб в <see cref="PrefabsDir"/>:
    /// общий материал M_Env_Forest (палитра), коллайдер по типу из имени.
    ///   Env_Tree_*  → капсула на ствол;  Env_Rock_* → выпуклый MeshCollider;  Env_Bush_*, Env_Grass_* → без коллайдера.
    /// Генератор сцены Showcase потом расставляет эти префабы вместо плейсхолдеров.
    /// Меню: Game → Окружение → Собрать префабы из моделей.
    /// </summary>
    public static class EnvironmentPrefabs
    {
        public const string ModelsDir = "Assets/_Project/Art/Models/Environment/Forest";
        public const string PrefabsDir = "Assets/_Project/Prefabs/Environment/Forest";
        public const string MaterialPath = "Assets/_Project/Art/Materials/M_Env_Forest.mat";
        public const string PalettePath = "Assets/_Project/Art/Textures/Environment/T_Palette_Forest.png";

        [MenuItem("Game/Окружение/Собрать префабы из моделей", priority = 40)]
        public static void BuildAllMenu()
        {
            int n = BuildAll();
            EditorUtility.DisplayDialog("Префабы окружения",
                n > 0
                    ? $"Готово: {n} префабов в {PrefabsDir}.\nЧтобы они появились в лесу, пересобери сцену: Game → Пересобрать сцену Showcase."
                    : $"В {ModelsDir} нет моделей Env_*.fbx.", "OK");
        }

        [MenuItem("Game/Окружение/Открыть папку моделей", priority = 41)]
        private static void RevealModels()
        {
            EnsureFolder(ModelsDir);
            EditorUtility.RevealInFinder(ModelsDir);
        }

        public static int BuildAll()
        {
            if (!AssetDatabase.IsValidFolder(ModelsDir)) return 0;
            EnsureFolder(PrefabsDir);
            var material = GetOrCreateMaterial();

            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                if (!name.StartsWith("Env_")) continue;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;
                BuildPrefab(name, model, material);
                count++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Game] Префабы окружения: {count} шт. в {PrefabsDir}");
            return count;
        }

        /// <summary>Префабы вида Env_{kind}_* (kind: Tree, Rock, Bush, Grass). Пусто, если моделей ещё нет.</summary>
        public static GameObject[] Load(string kind)
        {
            if (!AssetDatabase.IsValidFolder(PrefabsDir)) return new GameObject[0];
            return AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileNameWithoutExtension(p).StartsWith($"Env_{kind}_"))
                .OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(g => g != null)
                .ToArray();
        }

        private static void BuildPrefab(string name, GameObject model, Material material)
        {
            var root = new GameObject(name);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            visual.name = "Model";
            visual.transform.localPosition = Vector3.zero;

            foreach (var r in visual.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = material;
                r.sharedMaterials = mats;
            }

            var bounds = CalcBounds(visual);
            if (name.StartsWith("Env_Tree_"))
            {
                // ствол: тонкая капсула снизу, чтобы крона не цеплялась за героя
                var cap = root.AddComponent<CapsuleCollider>();
                cap.radius = 0.35f;
                cap.height = Mathf.Min(2.5f, bounds.size.y);
                cap.center = new Vector3(0f, cap.height * 0.5f, 0f);
            }
            else if (name.StartsWith("Env_Rock_"))
            {
                foreach (var mf in visual.GetComponentsInChildren<MeshFilter>())
                {
                    var mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = true;
                }
            }

            GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabsDir}/{name}.prefab");
            Object.DestroyImmediate(root);
        }

        private static Bounds CalcBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        public static Material GetOrCreateMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat != null) return mat;

            EnsureFolder(Path.GetDirectoryName(MaterialPath).Replace('\\', '/'));
            var shader = Shader.Find("Game/Stylized Toon");
            if (shader == null) shader = MaterialFactory.LitShader;
            mat = new Material(shader) { name = "M_Env_Forest" };
            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath);
            if (palette != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", palette);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", palette);
            }
            else Debug.LogWarning($"[Game] Не нашёл палитру {PalettePath}, материал будет без текстуры.");
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0f);
            AssetDatabase.CreateAsset(mat, MaterialPath);
            return mat;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>Новые/изменённые FBX в папке моделей → пересобрать префабы автоматически.</summary>
        internal class AutoBuild : AssetPostprocessor
        {
            private static bool _queued;

            private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                if (_queued || Application.isBatchMode) return;
                bool touched = imported.Concat(moved).Any(p =>
                    p.StartsWith(ModelsDir + "/") && p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase));
                if (!touched) return;
                _queued = true;
                EditorApplication.delayCall += () =>
                {
                    _queued = false;
                    BuildAll();
                };
            }
        }
    }
}
