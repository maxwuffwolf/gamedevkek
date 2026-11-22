using System.IO;
using Game.Gameplay.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Генерирует стартовую зону «Овраг Упавшей звезды» (зона "ravine" в zones.json, сцена Ravine):
    /// извилистое ущелье с юга на север. На юге — дымящийся кратер, куда упал герой (тут PlayerSpawn),
    /// дальше павший солдат с ржавым мечом (меч — контейнер, его забирают через окно), падальщики,
    /// на севере — арка-переход в Сумеречный лес.
    /// Меню: Game → Зоны → Пересобрать сцену Овраг. Сцену потом можно спокойно править руками.
    /// </summary>
    public static class RavineSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Ravine.unity";

        private const float ZMin = -46f;
        private const float ZMax = 46f;
        private const float CraterZ = -34f;
        private const float SkeletonZ = -22f;
        /// <summary>Скелет между тропой и левой стеной — на открытом месте, не в камнях у обрыва.</summary>
        private static Vector3 SkeletonPos => Floor(SkeletonZ, -0.45f);
        private const float ExitZ = 42f;

        private static System.Random _rng;

        [MenuItem("Game/Зоны/Пересобрать сцену Овраг", priority = 12)]
        private static void RebuildMenu()
        {
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Пересобрать овраг?",
                    "Сцена Ravine будет создана заново, ручные правки в ней пропадут.", "Пересобрать", "Отмена"))
                return;
            Build();
        }

        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            _rng = new System.Random(777);
            AtmosphereBuilder.BeginBuild();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var env = new GameObject("Environment").transform;
            var gameplay = new GameObject("Gameplay").transform;

            AtmosphereBuilder.BuildLighting(env); // те же сумерки, что в лесу: зоны должны выглядеть одним миром
            ShowcaseSceneBuilder.BuildCamera();

            EnvironmentPrefabs.BuildAll();
            BuildFloor(env);
            BuildCliffs(env);
            BuildCrater(env);
            BuildDebris(env);
            BuildGlowShrooms(env);
            AtmosphereBuilder.BuildEffects(env, _rng);

            // --- геймплей (эти объекты и стоит двигать в инспекторе)
            var spawn = new GameObject("PlayerSpawn", typeof(PlayerSpawnPoint)).transform;
            spawn.SetParent(gameplay);
            spawn.position = Floor(CraterZ);
            spawn.rotation = Quaternion.identity; // лицом на север, к выходу

            BuildSkeleton(env, gameplay); // павший солдат и ржавый меч-контейнер

            AddSpawner(gameplay, "Spawner_Scavengers_Skeleton", Floor(SkeletonZ + 6f, 0.2f), 2, 2.5f, new Vector2Int(1, 1), 45f);
            AddSpawner(gameplay, "Spawner_Scavengers_Mid", Floor(6f, -0.2f), 3, 4f, new Vector2Int(1, 2), 45f);
            AddSpawner(gameplay, "Spawner_Scavengers_Exit", Floor(27f, 0.15f), 3, 4f, new Vector2Int(2, 2), 60f);

            // --- выход в лес: портал ведёт на вход "from_ravine" в лесу, рядом с ним наш вход "from_forest"
            ZoneGateBuilder.Build(gameplay, Floor(ExitZ), 180f, targetZone: "forest", targetEntrance: "from_ravine",
                localEntranceId: "from_forest", style: ZoneGateBuilder.Style.Ascent);

            AtmosphereBuilder.EndBuild();
            EditorSceneManager.SaveScene(scene, ScenePath);
            string report = ZoneScenes.SyncBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Game] Сцена собрана: {ScenePath}. {report} Жми Play!");
        }

        // ------------------------------------------------------------------ форма ущелья

        /// <summary>Середина дна ущелья по X (ущелье плавно виляет).</summary>
        private static float CenterX(float z) => 3.5f * Mathf.Sin(z * 0.055f);

        /// <summary>Полуширина дна. У кратера расширяется в «чашу».</summary>
        private static float HalfWidth(float z)
        {
            float w = 6.5f + 1.8f * Mathf.Sin(z * 0.13f + 1.3f);
            w = Mathf.Max(w, 10.5f - Mathf.Abs(z - CraterZ) * 0.45f);
            if (z > ExitZ - 6f) w = Mathf.Max(w, 6f);
            return w;
        }

        /// <summary>Точка на дне: across = -1 (левая стена) .. 0 (середина) .. 1 (правая стена).</summary>
        private static Vector3 Floor(float z, float across = 0f) => new(CenterX(z) + across * HalfWidth(z), 0f, z);

        // ------------------------------------------------------------------ окружение

        private static void BuildFloor(Transform parent)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(parent);
            ground.transform.localScale = new Vector3(12f, 1f, 12f);
            ground.GetComponent<Renderer>().sharedMaterial = ShowcaseSceneBuilder.Mat("RavineFloor", new Color(0.31f, 0.28f, 0.27f));

            // тропа от кратера к выходу
            var pathMat = ShowcaseSceneBuilder.Mat("RavinePath", new Color(0.42f, 0.35f, 0.28f));
            for (float z = CraterZ + 5f; z < ExitZ - 1f; z += 2.6f)
            {
                var p = ShowcaseSceneBuilder.Prop(PrimitiveType.Cylinder, parent, "Path", Floor(z, 0.08f * Mathf.Sin(z * 0.4f)) + Vector3.up * 0.005f,
                    new Vector3(Range(2f, 2.8f), 0.01f, Range(2.2f, 3f)), pathMat, keepCollider: false);
                p.transform.rotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
            }

            // тропа уходит вверх за аркой — намёк «там выход наверх»
            var ramp = ShowcaseSceneBuilder.Prop(PrimitiveType.Cube, parent, "ExitRamp", Floor(ExitZ + 5f) + Vector3.up * 1.2f,
                new Vector3(5f, 0.4f, 8f), pathMat, keepCollider: false);
            ramp.transform.rotation = Quaternion.Euler(-16f, 0f, 0f);
        }

        private static void BuildCliffs(Transform parent)
        {
            var root = new GameObject("Cliffs").transform;
            root.SetParent(parent);
            var cliffMats = new[]
            {
                ShowcaseSceneBuilder.Mat("CliffA", new Color(0.37f, 0.35f, 0.4f)),
                ShowcaseSceneBuilder.Mat("CliffB", new Color(0.42f, 0.37f, 0.35f)),
            };
            var strataMat = ShowcaseSceneBuilder.Mat("CliffStrata", new Color(0.27f, 0.25f, 0.3f));
            var mossMat = ShowcaseSceneBuilder.Mat("CliffMoss", new Color(0.23f, 0.32f, 0.24f));
            var rockMat = ShowcaseSceneBuilder.Mat("CliffRock", new Color(0.33f, 0.32f, 0.36f));
            var treePrefabs = EnvironmentPrefabs.Load("Tree");
            var cone = ShowcaseSceneBuilder.ConeMesh();

            for (float z = ZMin - 6f; z <= ZMax + 6f; z += 3f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float edge = CenterX(z) + side * HalfWidth(z);
                    float h = Range(7f, 10.5f);
                    float cx = edge + side * 15f;

                    // сплошной блок стены (коллайдер держит игрока в ущелье)
                    ShowcaseSceneBuilder.Prop(PrimitiveType.Cube, root, "Cliff", new Vector3(cx, h * 0.5f - 0.3f, z),
                        new Vector3(30f, h, 3.3f), cliffMats[_rng.Next(cliffMats.Length)], keepCollider: true);
                    // мох на плато сверху
                    ShowcaseSceneBuilder.Prop(PrimitiveType.Cube, root, "Moss", new Vector3(cx, h - 0.15f, z),
                        new Vector3(30f, 0.3f, 3.32f), mossMat, keepCollider: false);
                    // слои породы на лицевой стороне
                    ShowcaseSceneBuilder.Prop(PrimitiveType.Cube, root, "Strata", new Vector3(edge, Range(1.8f, h - 1.5f), z),
                        new Vector3(0.5f, Range(0.2f, 0.4f), 3.4f), strataMat, keepCollider: false);

                    // выступающие валуны ломают ровную линию стены
                    int chunks = _rng.Next(1, 3);
                    for (int i = 0; i < chunks; i++)
                    {
                        var s = new Vector3(Range(1.2f, 2.6f), Range(1.5f, 4f), Range(1.5f, 3f));
                        var rock = ShowcaseSceneBuilder.Prop(PrimitiveType.Cube, root, "WallRock",
                            new Vector3(edge + side * Range(-0.2f, 0.8f), s.y * 0.4f, z + Range(-1.2f, 1.2f)), s, rockMat, keepCollider: true);
                        rock.transform.rotation = Quaternion.Euler(Range(-12f, 12f), Range(0f, 360f), Range(-12f, 12f));
                    }

                    // деревья по краю обрыва — силуэт леса наверху
                    if (_rng.NextDouble() < 0.75)
                    {
                        var p = new Vector3(edge + side * Range(1.5f, 10f), h, z + Range(-1f, 1f));
                        if (treePrefabs.Length > 0) PlacePrefab(root, treePrefabs, p, 0.9f, 1.35f);
                        else ConeTree(root, p, cone);
                    }
                }
            }

            // торцы ущелья
            ShowcaseSceneBuilder.Prop(PrimitiveType.Cube, root, "Cap_South", new Vector3(0f, 5f, ZMin - 7f), new Vector3(100f, 10f, 6f), cliffMats[0], keepCollider: true);
            ShowcaseSceneBuilder.Prop(PrimitiveType.Cube, root, "Cap_North", new Vector3(0f, 5f, ZMax + 6f), new Vector3(100f, 10f, 6f), cliffMats[1], keepCollider: true);
        }

        private static void BuildCrater(Transform parent)
        {
            var root = new GameObject("Crater").transform;
            root.SetParent(parent);
            Vector3 c = Floor(CraterZ);
            root.position = c;

            ShowcaseSceneBuilder.Prop(PrimitiveType.Cylinder, root, "Scorch", c + Vector3.up * 0.01f, new Vector3(11f, 0.01f, 11f),
                ShowcaseSceneBuilder.Mat("Scorch", new Color(0.11f, 0.09f, 0.1f)), keepCollider: false);
            ShowcaseSceneBuilder.Prop(PrimitiveType.Cylinder, root, "ImpactGlow", c + Vector3.up * 0.016f, new Vector3(2.4f, 0.01f, 2.4f),
                ShowcaseSceneBuilder.UnlitMat("ImpactGlow", new Color(2.4f, 0.9f, 0.3f)), keepCollider: false);

            // раскалённые трещины от центра
            var crackMat = ShowcaseSceneBuilder.UnlitMat("Crack", new Color(1.8f, 0.6f, 0.15f));
            for (int i = 0; i < 9; i++)
            {
                float yaw = i * 40f + Range(-12f, 12f);
                float len = Range(2f, 4.5f);
                Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                var crack = ShowcaseSceneBuilder.Prop(PrimitiveType.Cube, root, "Crack", c + dir * (len * 0.5f + 0.8f) + Vector3.up * 0.02f,
                    new Vector3(Range(0.08f, 0.16f), 0.02f, len), crackMat, keepCollider: false);
                crack.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }

            // вал из выброшенных камней, с проходом на север
            var rimMat = ShowcaseSceneBuilder.Mat("ScorchedStone", new Color(0.23f, 0.21f, 0.23f));
            for (int i = 0; i < 18; i++)
            {
                float a = i * 20f + Range(-6f, 6f);
                if (Mathf.Abs(Mathf.DeltaAngle(a, 0f)) < 28f) continue; // проход
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                var s = new Vector3(Range(1f, 2f), Range(0.6f, 1.4f), Range(1f, 1.8f));
                var rock = ShowcaseSceneBuilder.Prop(PrimitiveType.Cube, root, "RimRock", c + dir * Range(5.2f, 6.2f) + Vector3.up * s.y * 0.3f,
                    s, rimMat, keepCollider: true);
                rock.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(Range(-25f, -10f), Range(-20f, 20f), Range(-10f, 10f));
            }

            var lightGo = new GameObject("ImpactLight");
            lightGo.transform.SetParent(root);
            lightGo.transform.position = c + Vector3.up * 1.5f;
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.55f, 0.25f);
            l.range = 12f;
            l.intensity = 5f;
            l.shadows = LightShadows.None;
            var flicker = lightGo.AddComponent<LightFlicker>();
            flicker.BaseIntensity = 5f;
            flicker.Amount = 1f;
            flicker.Speed = 3f;

            AtmosphereBuilder.BuildEmbers(root, c + Vector3.up * 0.2f);
        }

        /// <summary>Солдат чуть крупнее «реального» — с камеры сверху так лучше читается.</summary>
        private const float ModelScale = 1.15f;

        /// <summary>Где воткнут меч относительно скелета (локально, метры). SWORD_POS из Tools/Blender/ravine_props.py:
        /// Blender (x, y) → Unity (-x, -y), умножено на масштаб модели.</summary>
        private static readonly Vector3 SwordLocal = new(-0.62f * ModelScale, 0f, 0.12f * ModelScale);

        /// <summary>
        /// Павший солдат спиной к валуну (модель Prop_Skeleton_Soldier.fbx из Tools/Blender/ravine_props.py, без неё — заглушка
        /// из примитивов) и воткнутый рядом ржавый меч — контейнер "ravine_rusty_sword" (клик → окно → взять меч).
        /// </summary>
        private static void BuildSkeleton(Transform parent, Transform gameplay)
        {
            var root = new GameObject("Skeleton_Soldier").transform;
            root.SetParent(parent);
            root.position = SkeletonPos;
            // сидит спиной к валуну на открытом месте у тропы, лицом на юг — к кратеру и к камере,
            // чтобы игрок видел его спереди, а меч не терялся среди камней у стены
            root.rotation = Quaternion.Euler(0f, 180f, 0f);

            var model = PropModels.Load(PropModels.SkeletonSoldierFbx);
            if (model != null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, root);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one * ModelScale;
                foreach (var r in inst.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            else
            {
                BuildSkeletonPlaceholder(root);
            }
            // твёрдый коллайдер, чтобы герой не проходил сквозь скелет и щит
            var body = root.gameObject.AddComponent<BoxCollider>();
            body.center = new Vector3(0f, 0.4f, -0.25f) * ModelScale; // скелет + валун за спиной (он в той же модели)
            body.size = new Vector3(2.0f, 0.8f, 1.0f) * ModelScale;

            // мягкий холодный свет сверху: в сумерках солдат должен читаться силуэтом
            var lightGo = new GameObject("SoldierLight");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = new Vector3(0f, 2.2f, 1.6f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.78f, 0.84f, 1f);
            l.range = 5.5f;
            l.intensity = 3f;
            l.shadows = LightShadows.None;

            BuildRustySword(gameplay, root);
        }

        /// <summary>Меч в земле = контейнер. Пропадает целиком, когда меч забрали (whenEmpty: Hide в containers.json).</summary>
        private static void BuildRustySword(Transform gameplay, Transform skeleton)
        {
            var go = new GameObject("Container_RustySword");
            go.transform.SetParent(gameplay);
            go.transform.position = skeleton.TransformPoint(SwordLocal);
            go.transform.rotation = skeleton.rotation;

            var trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.55f, 0f);
            trigger.size = new Vector3(0.7f, 1.2f, 0.7f);

            var container = go.AddComponent<Game.Gameplay.Items.WorldContainer>();
            container.ContainerId = "ravine_rusty_sword";
            container.Range = 2f;

            // модель: остриём в землю на 20 см, стоит рядом с солдатом
            const float tip = 0.903f; // от центра рукояти до острия (lowpoly_kit.SWORD_TIP)
            var visual = new GameObject("Model").transform;
            visual.SetParent(go.transform, false);
            visual.localRotation = Quaternion.Euler(-3f, 0f, 4f); // почти вертикально, как воткнули
            visual.localPosition = visual.localRotation * new Vector3(0f, tip - 0.2f, 0f);

            var swordModel = AssetDatabase.LoadAssetAtPath<GameObject>(HeroBuilder.SwordFbx);
            if (swordModel != null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(swordModel, visual);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.Euler(180f, 0f, 0f); // в FBX клинок смотрит вверх
                var mat = AssetDatabase.LoadAssetAtPath<Material>(HeroBuilder.MaterialsDir + "/M_Sword_Rusty.mat");
                if (mat != null)
                    foreach (var r in inst.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
            }
            else
            {
                var rust = ShowcaseSceneBuilder.Mat("Rust", new Color(0.48f, 0.3f, 0.2f));
                var grip = ShowcaseSceneBuilder.Mat("Wood", new Color(0.4f, 0.27f, 0.15f));
                void Part(PrimitiveType t, string n, Vector3 local, Vector3 scale, Material m)
                {
                    var p = ShowcaseSceneBuilder.Prop(t, visual, n, Vector3.zero, scale, m, keepCollider: false);
                    p.transform.localPosition = local;
                    p.transform.localRotation = Quaternion.identity;
                }
                Part(PrimitiveType.Cube, "Sword_Blade", new Vector3(0f, -0.45f, 0f), new Vector3(0.07f, 0.8f, 0.015f), rust);
                Part(PrimitiveType.Cube, "Sword_Guard", new Vector3(0f, -0.04f, 0f), new Vector3(0.22f, 0.03f, 0.03f), rust);
                Part(PrimitiveType.Cylinder, "Sword_Grip", new Vector3(0f, 0.04f, 0f), new Vector3(0.03f, 0.09f, 0.03f), grip);
            }

            // подсветка «тут лут» — столб света вешает сам WorldContainer ("lootBeam": true в containers.json)
        }

        /// <summary>Скелет из примитивов — если модели Prop_Skeleton_Soldier.fbx ещё нет в проекте.</summary>
        private static void BuildSkeletonPlaceholder(Transform root)
        {
            var bone = ShowcaseSceneBuilder.Mat("Bone", new Color(0.82f, 0.78f, 0.66f));

            void Part(PrimitiveType t, string n, Vector3 local, Vector3 scale, Vector3 euler, Material m)
            {
                var go = ShowcaseSceneBuilder.Prop(t, root, n, Vector3.zero, scale, m, keepCollider: false);
                go.transform.localPosition = local;
                go.transform.localRotation = Quaternion.Euler(euler);
            }

            Part(PrimitiveType.Sphere, "Skull", new Vector3(0f, 0.75f, -0.15f), new Vector3(0.3f, 0.27f, 0.34f), new Vector3(20f, 25f, 0f), bone);
            Part(PrimitiveType.Cylinder, "Spine", new Vector3(0f, 0.38f, -0.2f), new Vector3(0.06f, 0.3f, 0.06f), new Vector3(-15f, 0f, 0f), bone);
            for (int i = 0; i < 4; i++)
                Part(PrimitiveType.Cylinder, "Rib", new Vector3(0f, 0.28f + i * 0.09f, -0.12f), new Vector3(0.04f, 0.17f, 0.04f), new Vector3(0f, 0f, 90f), bone);
            Part(PrimitiveType.Cylinder, "Leg_L", new Vector3(-0.15f, 0.06f, 0.35f), new Vector3(0.06f, 0.4f, 0.06f), new Vector3(90f, -8f, 0f), bone);
            Part(PrimitiveType.Cylinder, "Leg_R", new Vector3(0.17f, 0.06f, 0.3f), new Vector3(0.06f, 0.38f, 0.06f), new Vector3(90f, 14f, 0f), bone);
            Part(PrimitiveType.Cylinder, "Arm", new Vector3(-0.28f, 0.25f, 0.05f), new Vector3(0.045f, 0.28f, 0.045f), new Vector3(60f, 0f, -30f), bone);
        }

        private static void BuildDebris(Transform parent)
        {
            var root = new GameObject("Debris").transform;
            root.SetParent(parent);
            var rockPrefabs = EnvironmentPrefabs.Load("Rock");
            var bushPrefabs = EnvironmentPrefabs.Load("Bush");
            var grassPrefabs = EnvironmentPrefabs.Load("Grass");
            var rockMat = ShowcaseSceneBuilder.Mat("Rock", new Color(0.48f, 0.48f, 0.5f));
            var logMat = ShowcaseSceneBuilder.Mat("DeadWood", new Color(0.33f, 0.25f, 0.2f));

            bool Reserved(Vector3 p, float across)
            {
                if (Vector3.Distance(p, Floor(CraterZ)) < 7.5f) return true;          // кратер
                if (Mathf.Abs(across) < 0.25f) return true;                         // тропа
                if (p.z > ExitZ - 6f && Mathf.Abs(across) < 0.6f) return true;      // перед аркой
                if (Vector3.Distance(p, SkeletonPos) < 3f) return true;            // скелет и меч
                return false;
            }

            for (int i = 0; i < 60; i++)
            {
                float z = Range(ZMin + 2f, ZMax - 2f), across = Range(-0.9f, 0.9f);
                var p = Floor(z, across);
                if (Reserved(p, across)) continue;
                if (rockPrefabs.Length > 0)
                {
                    PlacePrefab(root, rockPrefabs, p, 0.5f, 1.2f);
                    continue;
                }
                float s = Range(0.4f, 1.3f);
                var rock = ShowcaseSceneBuilder.Prop(PrimitiveType.Cube, root, "Rock", p + Vector3.up * s * 0.25f, new Vector3(s, s * 0.7f, s * 1.2f), rockMat, keepCollider: true);
                rock.transform.rotation = Quaternion.Euler(Range(-15f, 15f), Range(0f, 360f), Range(-15f, 15f));
            }

            for (int i = 0; bushPrefabs.Length > 0 && i < 50; i++)
            {
                float z = Range(ZMin + 2f, ZMax - 2f), across = (_rng.NextDouble() < 0.5 ? -1f : 1f) * Range(0.6f, 0.95f);
                var p = Floor(z, across);
                if (!Reserved(p, across)) PlacePrefab(root, bushPrefabs, p, 0.7f, 1.2f);
            }
            for (int i = 0; grassPrefabs.Length > 0 && i < 160; i++)
            {
                float z = Range(ZMin + 2f, ZMax - 2f), across = Range(-0.95f, 0.95f);
                var p = Floor(z, across);
                if (Vector3.Distance(p, Floor(CraterZ)) > 7f) PlacePrefab(root, grassPrefabs, p, 0.7f, 1.2f);
            }

            // поваленные стволы, скатившиеся с обрыва
            for (int i = 0; i < 6; i++)
            {
                float z = Range(CraterZ + 9f, ExitZ - 8f), across = (_rng.NextDouble() < 0.5 ? -1f : 1f) * Range(0.55f, 0.8f);
                var p = Floor(z, across);
                if (Reserved(p, across)) continue;
                var log = ShowcaseSceneBuilder.Prop(PrimitiveType.Cylinder, root, "FallenLog", p + Vector3.up * 0.3f,
                    new Vector3(0.55f, Range(1.6f, 2.6f), 0.55f), logMat, keepCollider: true);
                log.transform.rotation = Quaternion.Euler(0f, Range(0f, 360f), 90f);
            }
        }

        /// <summary>Светящиеся грибы у стен: холодные акценты, по которым глаз читает форму ущелья в темноте.</summary>
        private static void BuildGlowShrooms(Transform parent)
        {
            var root = new GameObject("GlowShrooms").transform;
            root.SetParent(parent);
            var capMat = ShowcaseSceneBuilder.UnlitMat("ShroomGlow", new Color(0.3f, 1.4f, 1.2f));
            for (int i = 0; i < 24; i++)
            {
                float z = Range(CraterZ + 6f, ExitZ - 3f);
                int side = _rng.NextDouble() < 0.5 ? -1 : 1;
                var p = Floor(z, side * 0.93f);
                for (int k = 0; k < 3; k++)
                {
                    float s = Range(0.12f, 0.26f);
                    var cap = ShowcaseSceneBuilder.Prop(PrimitiveType.Sphere, root, "Shroom",
                        p + new Vector3(Range(-0.4f, 0.4f), s * 0.4f, Range(-0.4f, 0.4f)), new Vector3(s, s * 0.6f, s), capMat, keepCollider: false);
                    cap.transform.rotation = Quaternion.Euler(Range(-10f, 10f), 0f, Range(-10f, 10f));
                }
            }
        }

        // ------------------------------------------------------------------ утилиты

        private static void AddSpawner(Transform parent, string name, Vector3 pos, int count, float radius, Vector2Int levels, float respawn)
        {
            var go = new GameObject(name, typeof(MonsterSpawner));
            go.transform.SetParent(parent);
            go.transform.position = pos;
            var s = go.GetComponent<MonsterSpawner>();
            s.MonsterId = "scavenger";
            s.Count = count;
            s.Radius = radius;
            s.LevelRange = levels;
            s.RespawnSeconds = respawn;
        }

        private static GameObject PlacePrefab(Transform parent, GameObject[] prefabs, Vector3 pos, float minScale, float maxScale)
        {
            var prefab = prefabs[_rng.Next(prefabs.Length)];
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
            go.transform.localScale = Vector3.one * Range(minScale, maxScale);
            return go;
        }

        /// <summary>Ёлка-заглушка из конусов, если своих моделей деревьев ещё нет.</summary>
        private static void ConeTree(Transform parent, Vector3 pos, Mesh cone)
        {
            float s = Range(0.9f, 1.4f);
            var root = new GameObject("Tree").transform;
            root.SetParent(parent);
            root.position = pos;
            root.localScale = Vector3.one * s;
            var trunk = ShowcaseSceneBuilder.Prop(PrimitiveType.Cylinder, root, "Trunk", Vector3.zero, Vector3.one,
                ShowcaseSceneBuilder.Mat("Trunk", new Color(0.36f, 0.25f, 0.16f)), keepCollider: false);
            trunk.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            trunk.transform.localScale = new Vector3(0.35f, 0.9f, 0.35f);
            var leaf = ShowcaseSceneBuilder.Mat("Leaves_C", new Color(0.18f, 0.36f, 0.26f));
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("Leaves");
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3(0f, 1.4f + i * 0.85f, 0f);
                go.transform.localScale = new Vector3(2.2f - i * 0.55f, 1.6f, 2.2f - i * 0.55f);
                go.AddComponent<MeshFilter>().sharedMesh = cone;
                go.AddComponent<MeshRenderer>().sharedMaterial = leaf;
            }
        }

        private static float Range(float a, float b) => a + (float)_rng.NextDouble() * (b - a);
    }
}
