using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Data;
using Game.Gameplay.Actors;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Герой на настоящей модели. Исходники:
    ///   <see cref="ModelsDir"/>/Hero.fbx (+ текстуры T_Hero_*) — собирается скриптом Tools/Blender/hero_build.py
    ///   из CC0-базы Quaternius Universal Base Characters и наших деталей (волосы, повязка, наруч, меч);
    ///   <see cref="AnimDir"/>/UAL1_Standard.fbx — CC0 Quaternius Universal Animation Library (Humanoid).
    /// Меню Game → Герой → Собрать героя делает материалы (toon), Animator Controller, префаб Hero
    /// и ActorVisual "hero" в Resources/Visuals — ActorFactory сам подхватит модель вместо капсулы-заглушки.
    /// </summary>
    public static class HeroBuilder
    {
        public const string ModelsDir = "Assets/_Project/Art/Models/Characters/Hero";
        public const string HeroFbx = ModelsDir + "/Hero.fbx";
        public const string SwordFbx = ModelsDir + "/Wpn_Sword_Rusty.fbx";
        public const string AnimDir = "Assets/_Project/Art/Animations/UAL";
        public const string UalFbx = AnimDir + "/UAL1_Standard.fbx";
        public const string MaterialsDir = "Assets/_Project/Art/Materials/Characters";
        public const string ControllerPath = "Assets/_Project/Art/Animations/AC_Hero.controller";
        public const string PrefabsDir = "Assets/_Project/Prefabs/Characters";
        public const string VisualPath = "Assets/_Project/Resources/Visuals/AV_Hero.asset";
        public const string ToonShader = "Game/Character Toon";
        public const string CrystalShader = "Game/Bracer Crystal";
        /// <summary>Поворот модели внутри префаба героя (градусы по Y), чтобы лицо смотрело по ходу движения.</summary>
        public const float ModelYaw = 180f;

        [MenuItem("Game/Герой/Собрать героя", priority = 50)]
        public static void BuildMenu()
        {
            string error = Build();
            EditorUtility.DisplayDialog("Герой", error ?? "Готово: префаб Hero собран, ActorVisual \"hero\" обновлён.\nЗапусти сцену Showcase (Play) — герой будет на новой модели.", "OK");
        }

        [MenuItem("Game/Герой/Открыть папку модели", priority = 51)]
        private static void Reveal()
        {
            EnsureFolder(ModelsDir);
            EditorUtility.RevealInFinder(ModelsDir);
        }

        public static string Build()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(HeroFbx) == null)
                return $"Нет {HeroFbx}.\nСобери модель: Tools/Blender/hero_build.py (см. docs/11-hero.md).";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(UalFbx) == null)
                return $"Нет анимаций {UalFbx} (Quaternius Universal Animation Library, файл Unity/UAL1_Standard.fbx).";

            EnsureFolder(MaterialsDir);
            EnsureFolder(PrefabsDir);
            EnsureFolder(Path.GetDirectoryName(VisualPath).Replace('\\', '/'));

            var mats = BuildMaterials();
            Remap(HeroFbx, mats);
            if (File.Exists(SwordFbx)) Remap(SwordFbx, mats);

            var controller = BuildController();
            if (controller == null) return "Не нашёл нужные клипы в UAL1_Standard.fbx (Idle_Loop, Walk_Loop, Sword_Attack…)";
            var prefab = BuildPrefab(controller);
            BuildSwordProp();

            var visual = AssetDatabase.LoadAssetAtPath<ActorVisual>(VisualPath);
            if (visual == null)
            {
                visual = ScriptableObject.CreateInstance<ActorVisual>();
                AssetDatabase.CreateAsset(visual, VisualPath);
            }
            visual.Id = "hero";
            visual.Prefab = prefab;
            visual.Scale = 1f;
            visual.Offset = Vector3.zero;
            EditorUtility.SetDirty(visual);
            AssetDatabase.SaveAssets();
            Debug.Log("[Game] Герой собран: " + PrefabsDir + "/Hero.prefab");
            return null;
        }

        // ------------------------------------------------------------ материалы

        private static Dictionary<string, Material> BuildMaterials()
        {
            var tex = new System.Func<string, Texture2D>(n => AssetDatabase.LoadAssetAtPath<Texture2D>($"{ModelsDir}/{n}.png"));
            var m = new Dictionary<string, Material>();

            var skin = Mat("M_Hero_Skin", ToonShader);
            skin.SetTexture("_BaseMap", tex("T_Hero_Skin"));
            var normal = tex("T_Hero_Skin_Normal");
            Keyword(skin, "_NORMALMAP", "_UseNormalMap", normal != null);
            skin.SetTexture("_BumpMap", normal);
            skin.SetFloat("_BumpScale", 0.8f);
            skin.SetFloat("_Steps", 2);
            skin.SetFloat("_StepSmooth", 0.18f);
            m["M_Hero_Skin"] = skin;

            var eyes = Mat("M_Hero_Eyes", ToonShader);
            eyes.SetTexture("_BaseMap", tex("T_Hero_Eyes"));
            Keyword(eyes, "_EYES", "_Eyes", true);
            eyes.SetFloat("_RimStrength", 0f);
            m["M_Hero_Eyes"] = eyes;

            var brows = Mat("M_Hero_Brows", ToonShader);
            brows.SetTexture("_BaseMap", tex("T_Hero_Brows"));
            brows.SetColor("_BaseColor", new Color(0.75f, 0.54f, 0.23f));
            m["M_Hero_Brows"] = brows;

            m["M_Hero_Hair"] = VertexColored("M_Hero_Hair", rim: 0.35f);
            var cloth = VertexColored("M_Hero_Cloth", rim: 0.1f);
            cloth.SetFloat("_Cull", 0f); // тонкая ткань — видна с обеих сторон
            m["M_Hero_Cloth"] = cloth;
            var bracer = VertexColored("M_Hero_Bracer", rim: 0.5f);
            bracer.SetColor("_RimColor", new Color(0.75f, 0.8f, 1f));
            bracer.SetFloat("_RimPower", 2.2f);
            bracer.SetFloat("_Steps", 3);
            m["M_Hero_Bracer"] = bracer;
            m["M_Sword_Rusty"] = VertexColored("M_Sword_Rusty", rim: 0.2f);

            var crystal = Mat("M_Hero_Crystal", CrystalShader);
            m["M_Hero_Crystal"] = crystal;

            foreach (var mat in m.Values) EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            ApplyReadability();
            return m;
        }

        /// <summary>
        /// Читаемость героя в тёмной сцене: тёмный контур + холодный «лунный» rim по силуэту.
        /// Вызывается и при пересборке сцен (Овраг, Showcase), чтобы не пересобирать героя целиком.
        /// Подкрутить руками: материалы в <see cref="MaterialsDir"/>, секции Outline и Rim.
        /// </summary>
        public static void ApplyReadability()
        {
            var outlineColor = new Color(0.035f, 0.025f, 0.06f);
            var moonRim = new Color(0.62f, 0.76f, 1f);
            void Set(string name, float outline, float rim, Color? rimColor = null, float rimPower = 2.6f)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/{name}.mat");
                if (mat == null || !mat.HasProperty("_OutlineWidth")) return;
                mat.SetFloat("_OutlineWidth", outline);
                mat.SetColor("_OutlineColor", outlineColor);
                mat.SetFloat("_RimStrength", rim);
                mat.SetFloat("_RimPower", rimPower);
                if (rimColor.HasValue) mat.SetColor("_RimColor", rimColor.Value);
                EditorUtility.SetDirty(mat);
            }
            Set("M_Hero_Skin", 1.6f, 0.45f, moonRim);
            Set("M_Hero_Hair", 1.6f, 0.5f, new Color(1f, 0.85f, 0.55f), 2.2f); // золотые волосы ловят свет — главный ориентир «где герой»
            Set("M_Hero_Cloth", 0f, 0.2f, moonRim);                            // тонкая двусторонняя ткань: контур на ней рябит
            Set("M_Hero_Bracer", 1.6f, 0.5f, new Color(0.75f, 0.8f, 1f), 2.2f);
            Set("M_Sword_Rusty", 1.4f, 0.3f, moonRim);                         // он же — меч в земле у скелета: контур подсвечивает «можно взять»
            Set("M_Hero_Brows", 0f, 0.1f);
            AssetDatabase.SaveAssets();
        }

        private static Material VertexColored(string name, float rim)
        {
            var mat = Mat(name, ToonShader);
            Keyword(mat, "_VERTEX_COLORS", "_UseVertexColors", true);
            mat.SetTexture("_BaseMap", null);
            mat.SetFloat("_RimStrength", rim);
            return mat;
        }

        private static Material Mat(string name, string shaderName)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var shader = Shader.Find(shaderName);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader) mat.shader = shader;
            return mat;
        }

        private static void Keyword(Material mat, string keyword, string toggleProperty, bool on)
        {
            mat.SetFloat(toggleProperty, on ? 1f : 0f);
            if (on) mat.EnableKeyword(keyword); else mat.DisableKeyword(keyword);
        }

        private static void Remap(string fbxPath, Dictionary<string, Material> mats)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            foreach (var kv in mats)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------ аниматор

        private static AnimatorController BuildController()
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(UalFbx).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .GroupBy(c => c.name).ToDictionary(g => g.Key, g => g.First());
            AnimationClip Clip(string n) => clips.TryGetValue(n, out var c) ? c : null;

            var idle = Clip("Idle_Loop");
            var walk = Clip("Walk_Loop");
            var jog = Clip("Jog_Fwd_Loop");
            var sprint = Clip("Sprint_Loop");
            var attack = Clip("Sword_Attack");
            var death = Clip("Death01");
            if (idle == null || walk == null || attack == null) return null;

            // Контроллер не удаляем, а очищаем на месте: если удалить и создать заново по тому же пути,
            // префаб в памяти продолжает ссылаться на старый (уничтоженный) объект → «Missing» в игре.
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ac == null)
            {
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
            // длина клипа удара: AnimatorBridge подгоняет скорость анимации под длительность умения
            ac.AddParameter(new AnimatorControllerParameter { name = "AttackClipLength", type = AnimatorControllerParameterType.Float, defaultFloat = attack.length });
            ac.AddParameter(new AnimatorControllerParameter { name = "HeavyAttackClipLength", type = AnimatorControllerParameterType.Float, defaultFloat = attack.length });
            foreach (var t in new[] { "Attack", "HeavyAttack", "Die", "Revive", "PickUp" })
                ac.AddParameter(t, AnimatorControllerParameterType.Trigger);

            var sm = ac.layers[0].stateMachine;
            var loco = ac.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, 1.4f);
            if (jog != null) tree.AddChild(jog, 3.8f);
            if (sprint != null) tree.AddChild(sprint, 6.5f);
            sm.defaultState = loco;

            AnimatorState AddAction(string name, AnimationClip clip, string trigger, bool returns, float speed = 1f)
            {
                var st = sm.AddState(name);
                st.motion = clip;
                st.speed = speed;
                if (name.Contains("Attack"))
                {
                    st.speedParameter = "CastSpeed";
                    st.speedParameterActive = true;
                }
                var tr = sm.AddAnyStateTransition(st);
                tr.AddCondition(AnimatorConditionMode.If, 0, trigger);
                tr.duration = 0.06f;
                tr.canTransitionToSelf = true;
                if (returns)
                {
                    var back = st.AddTransition(loco);
                    back.hasExitTime = true;
                    back.exitTime = 0.85f;
                    back.duration = 0.15f;
                }
                return st;
            }

            AddAction("Attack", attack, "Attack", true);
            AddAction("HeavyAttack", attack, "HeavyAttack", true);
            if (Clip("PickUp_Table") != null) AddAction("PickUp", Clip("PickUp_Table"), "PickUp", true);
            if (death != null)
            {
                var die = AddAction("Die", death, "Die", false);
                sm.anyStateTransitions.Last().canTransitionToSelf = false;
                var revive = die.AddTransition(loco);
                revive.AddCondition(AnimatorConditionMode.If, 0, "Revive");
                revive.hasExitTime = false;
                revive.duration = 0.2f;
            }
            EditorUtility.SetDirty(ac);
            AssetDatabase.SaveAssets();
            return ac;
        }

        // ------------------------------------------------------------ префабы

        private static GameObject BuildPrefab(AnimatorController controller)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(HeroFbx);
            var root = new GameObject("Hero");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            inst.transform.localPosition = Vector3.zero;
            // Hero.fbx после импорта смотрит в -Z, а персонаж в игре ходит по +Z: разворачиваем модель внутри префаба
            inst.transform.localRotation = Quaternion.Euler(0f, ModelYaw, 0f);

            var animator = inst.GetComponent<Animator>() ?? inst.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                if (r.gameObject.name == "Hero_BracerCrystal" || r.gameObject.name == "Hero_Eyes")
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
            }
            root.AddComponent<HeroAppearance>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabsDir}/Hero.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Отдельный ржавый меч для мира (скелет в овраге, лут) — понадобится на этапе 2.</summary>
        private static void BuildSwordProp()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SwordFbx);
            if (model == null) return;
            EnsureFolder("Assets/_Project/Prefabs/Items");
            var root = new GameObject("Wpn_Sword_Rusty");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            inst.transform.localPosition = Vector3.zero;
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/_Project/Prefabs/Items/Wpn_Sword_Rusty.prefab");
            Object.DestroyImmediate(root);
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }

    /// <summary>Настройки импорта героя, его меча и библиотеки анимаций (Humanoid, имена и петли клипов).</summary>
    public sealed class HeroImportRules : AssetPostprocessor
    {
        private bool IsHero => assetPath == HeroBuilder.HeroFbx;
        private bool IsUal => assetPath.StartsWith(HeroBuilder.AnimDir + "/") && assetPath.EndsWith(".fbx");
        private bool InHeroDir => assetPath.StartsWith(HeroBuilder.ModelsDir + "/");

        private void OnPreprocessModel()
        {
            var mi = (ModelImporter)assetImporter;
            if (IsHero)
            {
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.importAnimation = false;
                mi.importBlendShapes = false;
                mi.importCameras = false;
                mi.importLights = false;
            }
            else if (IsUal)
            {
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.importAnimation = true;
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.importCameras = false;
                mi.importLights = false;
            }
            else if (InHeroDir)
            {
                mi.animationType = ModelImporterAnimationType.None;
                mi.importAnimation = false;
            }
        }

        private void OnPreprocessAnimation()
        {
            if (!IsUal) return;
            var mi = (ModelImporter)assetImporter;
            var clips = mi.defaultClipAnimations;
            foreach (var c in clips)
            {
                string n = c.name;
                c.name = n.Substring(n.LastIndexOf('|') + 1);
                bool loop = c.name.EndsWith("_Loop") || c.name == "Sword_Idle";
                c.loopTime = loop;
                c.loopPose = loop;
                // корень не уезжает: всё движение делает CharacterController
                c.lockRootRotation = true;
                c.keepOriginalOrientation = true;
                c.lockRootHeightY = true;
                c.keepOriginalPositionY = true;
                c.lockRootPositionXZ = true;
                c.keepOriginalPositionXZ = true;
            }
            mi.clipAnimations = clips;
        }

        private void OnPreprocessTexture()
        {
            if (!InHeroDir) return;
            var ti = (TextureImporter)assetImporter;
            if (assetPath.EndsWith("_Normal.png"))
            {
                ti.textureType = TextureImporterType.NormalMap;
            }
            else
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
            }
            ti.maxTextureSize = 2048;
        }
    }
}
