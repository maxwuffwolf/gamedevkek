using System.Collections.Generic;
using System.IO;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Мелкая детализация леса из Tools/Blender/forest_details.py: грибы, ягоды, кусты, папоротники, трава,
    /// цветы, корни, ветки, пень, бревно, камешки, кристаллы и живность (птичка, мотылёк, жук).
    /// FBX лежат в <see cref="ModelsDir"/> (Det_*.fbx), цвет — в вершинах. Материалы из FBX
    /// (M_Detail / M_Detail_Leaf / M_Detail_Glow) подменяются на toon-материалы Game/Stylized Toon.
    /// Если моделей нет (не скопировали FBX) — <see cref="Load"/> вернёт null, и сцена соберётся по-старому.
    /// </summary>
    public static class ForestDetails
    {
        public const string ModelsDir = "Assets/_Project/Art/Models/Props/Forest";
        public const string MaterialsDir = "Assets/_Project/Art/Materials/Props";
        private const string ToonShader = "Game/Stylized Toon";

        /// <summary>Лунный контровой свет по силуэту: отделяет мелочь от земли сверху.</summary>
        public static readonly Color MoonRim = new(0.55f, 0.72f, 1f);

        private static Dictionary<string, Material> _mats;

        public static bool Available => File.Exists(ModelsDir + "/Det_Fern_A.fbx");

        public static Dictionary<string, Material> EnsureMaterials()
        {
            if (_mats != null && AllAlive(_mats.Values)) return _mats;
            EnvironmentPrefabs.EnsureFolder(MaterialsDir);
            _mats = new Dictionary<string, Material>
            {
                ["M_Detail"] = Toon("M_Detail", leaf: false, glow: 0f),
                ["M_Detail_Leaf"] = Toon("M_Detail_Leaf", leaf: true, glow: 0f),
                ["M_Detail_Glow"] = Toon("M_Detail_Glow", leaf: false, glow: 1.7f),
            };
            AssetDatabase.SaveAssets();
            return _mats;
        }

        private static bool AllAlive(IEnumerable<Material> mats)
        {
            foreach (var m in mats)
                if (m == null) return false;
            return true;
        }

        private static Material Toon(string name, bool leaf, float glow)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var shader = Shader.Find(ToonShader);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created)
            {
                mat = new Material(shader != null ? shader : MaterialFactory.LitShader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            if (!created && mat.shader == shader && !AtmosphereBuilder.RefreshAssets) return mat;
            if (shader != null) mat.shader = shader;

            mat.SetTexture("_BaseMap", null);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_UseVertexColors", 1f);
            mat.EnableKeyword("_VERTEX_COLORS");
            mat.SetFloat("_Variation", 0.06f);
            mat.SetColor("_GradientBottom", new Color(0.78f, 0.8f, 0.92f));
            mat.SetColor("_GradientTop", Color.white);
            mat.SetFloat("_GradientHeight", 0.6f);
            mat.SetColor("_ShadowColor", new Color(0.5f, 0.52f, 0.8f));
            mat.SetFloat("_Steps", 2f);
            mat.SetFloat("_StepSmooth", 0.12f);
            mat.SetFloat("_PointSmooth", 1f);
            mat.SetFloat("_AmbientStrength", 0.9f);
            mat.SetColor("_RimColor", MoonRim);
            mat.SetFloat("_RimStrength", glow > 0f ? 0.15f : 0.38f);
            mat.SetFloat("_RimPower", 2.6f);
            mat.SetColor("_EmissionColor", Color.white * glow);
            mat.SetFloat("_EmissionFromAlbedo", glow > 0f ? 1f : 0f);
            mat.SetFloat("_WindStrength", leaf ? 0.05f : 0f);
            mat.SetFloat("_WindSpeed", 1.5f);
            mat.SetFloat("_WindScale", 0.4f);
            mat.SetFloat("_Cull", leaf ? 0f : 2f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Модель Det_&lt;name&gt; с нашими материалами или null.</summary>
        public static GameObject Load(string name)
        {
            string path = $"{ModelsDir}/{name}.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) return null;
            var mats = EnsureMaterials();
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            bool changed = false;
            var existing = importer.GetExternalObjectMap();
            foreach (var kv in mats)
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key);
                if (existing.TryGetValue(id, out var cur) && cur == kv.Value) continue;
                importer.AddRemap(id, kv.Value);
                changed = true;
            }
            if (changed)
            {
                importer.SaveAndReimport();
                model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return model;
        }

        /// <summary>Несколько моделей сразу (пропускает отсутствующие).</summary>
        public static GameObject[] LoadAll(params string[] names)
        {
            var list = new List<GameObject>();
            foreach (var n in names)
            {
                var m = Load(n);
                if (m != null) list.Add(m);
            }
            return list.ToArray();
        }

        /// <summary>Поставить модель: случайный поворот вокруг оси Y, без коллайдеров и теней от мелочи.</summary>
        public static GameObject Place(GameObject model, Transform parent, Vector3 pos, float yaw, float scale, bool castShadows = false)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
