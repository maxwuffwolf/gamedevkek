using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Пропсы с цветом в вершинах (скелет солдата в овраге и т.п.) из Tools/Blender/ravine_props.py.
    /// FBX лежат в <see cref="ModelsDir"/>, материалы в FBX называются M_Prop_Ravine (обычный)
    /// и M_Prop_Ravine_2S (ткань/тонкое железо — видно с обеих сторон); сюда они подменяются на
    /// toon-материалы (Game/Character Toon + Vertex Colors), как у героя и его меча.
    /// </summary>
    public static class PropModels
    {
        public const string ModelsDir = "Assets/_Project/Art/Models/Props";
        public const string MaterialsDir = "Assets/_Project/Art/Materials/Props";
        public const string SkeletonSoldierFbx = ModelsDir + "/Ravine/Prop_Skeleton_Soldier.fbx";

        public static Dictionary<string, Material> EnsureMaterials()
        {
            HeroBuilder.EnsureFolder(MaterialsDir);
            var m = new Dictionary<string, Material>
            {
                ["M_Prop_Ravine"] = VertexToon("M_Prop_Ravine", twoSided: false),
                ["M_Prop_Ravine_2S"] = VertexToon("M_Prop_Ravine_2S", twoSided: true),
            };
            AssetDatabase.SaveAssets();
            return m;
        }

        private static Material VertexToon(string name, bool twoSided)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var shader = Shader.Find(HeroBuilder.ToonShader);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader) mat.shader = shader;
            mat.SetFloat("_UseVertexColors", 1f);
            mat.EnableKeyword("_VERTEX_COLORS");
            mat.SetTexture("_BaseMap", null);
            mat.SetFloat("_RimStrength", 0.15f);
            mat.SetFloat("_Cull", twoSided ? 0f : 2f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Модель пропа с нашими материалами или null, если FBX ещё не положили в проект.</summary>
        public static GameObject Load(string fbxPath)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null) return null;
            var mats = EnsureMaterials();
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
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
                model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            }
            return model;
        }
    }

    /// <summary>Импорт пропсов: без анимаций и риг-аватаров, метры как в Blender.</summary>
    public sealed class PropImportRules : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(PropModels.ModelsDir + "/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
        }
    }
}
