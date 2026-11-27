using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Data;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Падальщик (гуль) — первый враг оврага на настоящей модели.
    /// Исходник: <see cref="GhoulFbx"/> из Tools/Blender/ghoul_build.py (свой Generic-скелет и клипы
    /// Idle / Walk / Run / Attack / Hit / Death прямо в FBX, цвет в вершинах).
    /// Меню Game → Монстры → Собрать падальщика: материалы (Character Toon), Animator Controller,
    /// префаб и ActorVisual "scavenger" в Resources/Visuals — ActorFactory сам заменит заглушку на модель.
    /// Пересборка оврага (Game → Зоны → Пересобрать) тоже вызывает сборку, если FBX на месте.
    /// </summary>
    public static class GhoulBuilder
    {
        public const string ModelsDir = "Assets/_Project/Art/Models/Characters/Monsters/Ghoul";
        public const string GhoulFbx = ModelsDir + "/Mon_Ghoul.fbx";
        public const string MaterialsDir = "Assets/_Project/Art/Materials/Characters";
        public const string ControllerPath = "Assets/_Project/Art/Animations/AC_Ghoul.controller";
        public const string PrefabPath = "Assets/_Project/Prefabs/Characters/Mon_Ghoul.prefab";
        public const string VisualPath = "Assets/_Project/Resources/Visuals/AV_Scavenger.asset";
        public const string VisualId = "scavenger";
        /// <summary>Поворот модели внутри префаба. Гуль из ghoul_build.py после импорта уже смотрит в +Z (проверено в игре: 180 — ходил спиной).</summary>
        public const float ModelYaw = 0f;

        [MenuItem("Game/Монстры/Собрать падальщика (гуль)", priority = 60)]
        public static void BuildMenu()
        {
            string error = Build();
            EditorUtility.DisplayDialog("Падальщик", error ?? "Готово: префаб Mon_Ghoul собран, ActorVisual \"scavenger\" обновлён.\nЗапусти овраг — падальщики будут на новой модели.", "OK");
        }

        /// <summary>Тихая сборка при пересборке сцены: только если FBX уже лежит в проекте.</summary>
        public static void BuildIfPresent()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(GhoulFbx) == null) return;
            string error = Build();
            if (error != null) Debug.LogWarning("[Game] Гуль: " + error);
        }

        public static string Build()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(GhoulFbx) == null)
                return $"Нет {GhoulFbx}.\nСобери модель: Tools/Blender/ghoul_build.py (см. docs/14-ghoul.md).";
            HeroBuilder.EnsureFolder(MaterialsDir);
            HeroBuilder.EnsureFolder(Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
            HeroBuilder.EnsureFolder(Path.GetDirectoryName(VisualPath).Replace('\\', '/'));

            var mats = BuildMaterials();
            Remap(mats);
            var controller = BuildController();
            if (controller == null) return "В Mon_Ghoul.fbx нет клипов Idle / Run / Attack — переэкспортируй ghoul_build.py.";
            var prefab = BuildPrefab(controller);

            var visual = AssetDatabase.LoadAssetAtPath<ActorVisual>(VisualPath);
            if (visual == null)
            {
                visual = ScriptableObject.CreateInstance<ActorVisual>();
                AssetDatabase.CreateAsset(visual, VisualPath);
            }
            visual.Id = VisualId;
            visual.Prefab = prefab;
            visual.Scale = 1f;
            visual.Offset = Vector3.zero;
            EditorUtility.SetDirty(visual);
            AssetDatabase.SaveAssets();
            Debug.Log("[Game] Падальщик собран: " + PrefabPath);
            return null;
        }

        // ------------------------------------------------------------ материалы

        private static Dictionary<string, Material> BuildMaterials()
        {
            var outline = new Color(0.035f, 0.025f, 0.06f);
            var moonRim = new Color(0.62f, 0.76f, 1f);
            Material Vc(string name, bool twoSided, float outlineWidth, float rim)
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
                mat.SetFloat("_Cull", twoSided ? 0f : 2f);
                // гладкая кожа: мягкий переход света в тень, как на рисованных моделях (а не жёсткий cel)
                mat.SetFloat("_Steps", 2);
                mat.SetFloat("_StepSmooth", 0.32f);
                // холодная тень — гуль «сливается» с сумерками, но силуэт держат контур и лунный rim
                mat.SetColor("_ShadowColor", new Color(0.38f, 0.4f, 0.58f));
                if (mat.HasProperty("_OutlineWidth"))
                {
                    mat.SetFloat("_OutlineWidth", outlineWidth);
                    mat.SetColor("_OutlineColor", outline);
                }
                mat.SetFloat("_RimStrength", rim);
                mat.SetColor("_RimColor", moonRim);
                mat.SetFloat("_RimPower", 2.6f);
                if (mat.HasProperty("_EmissionFromAlbedo")) mat.SetFloat("_EmissionFromAlbedo", 0f);
                EditorUtility.SetDirty(mat);
                return mat;
            }

            var m = new Dictionary<string, Material>
            {
                ["M_Ghoul"] = Vc("M_Ghoul", false, 1.5f, 0.4f),
                ["M_Ghoul_2S"] = Vc("M_Ghoul_2S", true, 0f, 0.25f), // тонкая ткань и уши: контур на них рябит
            };
            var glow = Vc("M_Ghoul_Glow", false, 0f, 0f);
            glow.SetFloat("_EmissionFromAlbedo", 3.5f); // глаза-угольки и споровые грибы светятся через Bloom
            glow.SetFloat("_AmbientStrength", 0f);
            m["M_Ghoul_Glow"] = glow;
            AssetDatabase.SaveAssets();
            return m;
        }

        private static void Remap(Dictionary<string, Material> mats)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(GhoulFbx);
            var existing = importer.GetExternalObjectMap();
            bool changed = false;
            foreach (var kv in mats)
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key);
                if (existing.TryGetValue(id, out var cur) && cur == kv.Value) continue;
                importer.AddRemap(id, kv.Value);
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
        }

        // ------------------------------------------------------------ аниматор

        private static AnimatorController BuildController()
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(GhoulFbx).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .GroupBy(c => c.name).ToDictionary(g => g.Key, g => g.First());
            AnimationClip Clip(string n) => clips.TryGetValue(n, out var c) ? c : null;
            var idle = Clip("Idle");
            var walk = Clip("Walk");
            var run = Clip("Run");
            var attack = Clip("Attack");
            var hit = Clip("Hit");
            var death = Clip("Death");
            if (idle == null || run == null || attack == null) return null;

            // очищаем на месте, а не пересоздаём — иначе префаб в памяти ссылается на удалённый контроллер
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ac == null)
            {
                HeroBuilder.EnsureFolder(Path.GetDirectoryName(ControllerPath).Replace('\\', '/'));
                ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }
            else
            {
                foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
                    if (sub != null && sub != ac) AssetDatabase.RemoveObjectFromAsset(sub);
                ac.parameters = new AnimatorControllerParameter[0];
                ac.layers = new AnimatorControllerLayer[0];
                ac.AddLayer("Base Layer");
            }
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter(new AnimatorControllerParameter { name = "CastSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            // AnimatorBridge растягивает клип удара ровно на длительность умения (удар на 55 % — как hitFrame в ravine.json)
            ac.AddParameter(new AnimatorControllerParameter { name = "AttackClipLength", type = AnimatorControllerParameterType.Float, defaultFloat = attack.length });
            foreach (var t in new[] { "Attack", "Hit", "Die", "Revive" })
                ac.AddParameter(t, AnimatorControllerParameterType.Trigger);

            var sm = ac.layers[0].stateMachine;
            var loco = ac.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            if (walk != null) tree.AddChild(walk, 1.3f);
            tree.AddChild(run, 3.9f); // MoveSpeed падальщика в ravine.json
            sm.defaultState = loco;

            var atk = sm.AddState("Attack");
            atk.motion = attack;
            atk.speedParameter = "CastSpeed";
            atk.speedParameterActive = true;
            var toAtk = sm.AddAnyStateTransition(atk);
            toAtk.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            toAtk.duration = 0.1f;
            toAtk.canTransitionToSelf = true;
            var back = atk.AddTransition(loco);
            back.hasExitTime = true;
            back.exitTime = 0.92f;
            back.duration = 0.12f;

            if (death != null)
            {
                var die = sm.AddState("Die");
                die.motion = death;
                var toDie = sm.AddAnyStateTransition(die);
                toDie.AddCondition(AnimatorConditionMode.If, 0, "Die");
                toDie.duration = 0.15f;
                toDie.canTransitionToSelf = false;
                var revive = die.AddTransition(loco);
                revive.AddCondition(AnimatorConditionMode.If, 0, "Revive");
                revive.hasExitTime = false;
                revive.duration = 0.2f;
            }

            // вздрог от удара — аддитивный слой поверх бега и атаки, ничего не прерывает
            if (hit != null)
            {
                var layer = new AnimatorControllerLayer
                {
                    name = "Hit",
                    defaultWeight = 1f,
                    blendingMode = AnimatorLayerBlendingMode.Additive,
                    stateMachine = new AnimatorStateMachine { name = "Hit", hideFlags = HideFlags.HideInHierarchy },
                };
                AssetDatabase.AddObjectToAsset(layer.stateMachine, ac);
                var hsm = layer.stateMachine;
                var empty = hsm.AddState("Empty");
                hsm.defaultState = empty;
                var hs = hsm.AddState("Hit");
                hs.motion = hit;
                var toHit = hsm.AddAnyStateTransition(hs);
                toHit.AddCondition(AnimatorConditionMode.If, 0, "Hit");
                toHit.duration = 0.02f;
                toHit.canTransitionToSelf = true;
                var hb = hs.AddTransition(empty);
                hb.hasExitTime = true;
                hb.exitTime = 0.95f;
                hb.duration = 0.05f;
                ac.AddLayer(layer);
            }
            EditorUtility.SetDirty(ac);
            AssetDatabase.SaveAssets();
            return ac;
        }

        // ------------------------------------------------------------ префаб

        private static GameObject BuildPrefab(AnimatorController controller)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(GhoulFbx);
            var root = new GameObject("Mon_Ghoul");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.Euler(0f, ModelYaw, 0f);
            var animator = inst.GetComponent<Animator>() ?? inst.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            foreach (var r in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                r.updateWhenOffscreen = false;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }

    /// <summary>Импорт гуля: Generic-скелет из FBX, клипы без префикса «Ghoul_Rig|», петли Idle/Walk/Run.</summary>
    public sealed class GhoulImportRules : AssetPostprocessor
    {
        private bool IsGhoul => assetPath == GhoulBuilder.GhoulFbx;

        private void OnPreprocessModel()
        {
            if (!IsGhoul) return;
            var mi = (ModelImporter)assetImporter;
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = true;
            mi.importBlendShapes = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            // без сжатия: клипы короткие, а сжатие ключей давало «шероховатость» в падении
            mi.animationCompression = ModelImporterAnimationCompression.Off;
        }

        private void OnPreprocessAnimation()
        {
            if (!IsGhoul) return;
            var mi = (ModelImporter)assetImporter;
            var clips = mi.defaultClipAnimations;
            foreach (var c in clips)
            {
                string n = c.name;
                c.name = n.Substring(n.LastIndexOf('|') + 1);
                bool loop = c.name == "Idle" || c.name == "Walk" || c.name == "Run";
                c.loopTime = loop;
                c.loopPose = false; // первый и последний кадр в Blender уже совпадают
            }
            mi.clipAnimations = clips;
        }
    }
}
