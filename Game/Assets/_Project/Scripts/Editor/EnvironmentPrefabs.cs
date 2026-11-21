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
    /// материал M_Env_Forest или M_Env_Forest_Foliage (toon-шейдер + палитра, листва качается), коллайдер по типу из имени.
    ///   Env_Tree_*  → капсула на ствол;  Env_Rock_* → выпуклый MeshCollider;  Env_Bush_*, Env_Grass_* → без коллайдера.
    /// Генератор сцены Showcase потом расставляет эти префабы вместо плейсхолдеров.
    /// Меню: Game → Окружение → Собрать префабы из моделей.
    /// </summary>
    public static class EnvironmentPrefabs
    {
        public const string ModelsDir = "Assets/_Project/Art/Models/Environment/Forest";
        public const string PrefabsDir = "Assets/_Project/Prefabs/Environment/Forest";
        public const string MaterialPath = "Assets/_Project/Art/Materials/M_Env_Forest.mat";
        public const string FoliageMaterialPath = "Assets/_Project/Art/Materials/M_Env_Forest_Foliage.mat";
        public const string ToonShaderName = "Game/Stylized Toon";
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
            var foliage = GetOrCreateFoliageMaterial();

            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                if (!name.StartsWith("Env_")) continue;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;
                bool sways = name.StartsWith("Env_Tree_") || name.StartsWith("Env_Bush_") || name.StartsWith("Env_Grass_");
                BuildPrefab(name, model, sways ? foliage : material);
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

        /// <summary>Материал камней и всего неподвижного: toon-шейдер + палитра.</summary>
        public static Material GetOrCreateMaterial() => GetOrCreate(MaterialPath, "M_Env_Forest", wind: 0f);

        /// <summary>Материал листвы (деревья, кусты, трава): то же самое + лёгкое покачивание на ветру.</summary>
        public static Material GetOrCreateFoliageMaterial() => GetOrCreate(FoliageMaterialPath, "M_Env_Forest_Foliage", wind: 0.05f);

        /// <summary>
        /// Создаёт материал или переводит существующий (например, старый на URP Lit) на toon-шейдер.
        /// Настройки задаются только при создании/переводе, так что ручные правки в инспекторе не затираются.
        /// </summary>
        private static Material GetOrCreate(string path, string name, float wind)
        {
            var shader = Shader.Find(ToonShaderName);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
            {
                if (shader == null || (mat.shader == shader && !AtmosphereBuilder.RefreshAssets)) return mat;
                mat.shader = shader;
                ApplyToonDefaults(mat, wind);
                EditorUtility.SetDirty(mat);
                Debug.Log($"[Game] {name}: переведён на шейдер {ToonShaderName}");
                return mat;
            }

            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            mat = new Material(shader != null ? shader : MaterialFactory.LitShader) { name = name };
            ApplyToonDefaults(mat, wind);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void ApplyToonDefaults(Material mat, float wind)
        {
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

            if (mat.shader.name != ToonShaderName) return;
            // у моделей из Blender нет вершинных цветов: цвет берётся только из палитры
            mat.SetFloat("_UseVertexColors", 0f);
            mat.DisableKeyword("_VERTEX_COLORS");
            mat.SetFloat("_Variation", 0.07f);
            // сумеречные настройки: тени в фиолет, тёплый контровой обвод от низкого солнца
            mat.SetColor("_ShadowColor", new Color(0.38f, 0.4f, 0.66f));
            mat.SetColor("_GradientBottom", new Color(0.62f, 0.64f, 0.82f));
            mat.SetFloat("_GradientHeight", wind > 0f ? 3.5f : 1.5f);
            mat.SetColor("_RimColor", new Color(1f, 0.6f, 0.4f));
            mat.SetFloat("_RimStrength", 0.25f);
            mat.SetFloat("_RimPower", 3f);
            mat.SetFloat("_AmbientStrength", 0.75f);
            mat.SetFloat("_WindStrength", wind);
            mat.SetFloat("_WindSpeed", 1.1f);
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
