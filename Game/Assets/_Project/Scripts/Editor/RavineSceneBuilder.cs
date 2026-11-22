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

            HeroBuilder.ApplyReadability();
            AtmosphereBuilder.BuildLighting(env); // те же сумерки, что в лесу: зоны должны выглядеть одним миром
            ShowcaseSceneBuilder.BuildCamera();

            EnvironmentPrefabs.BuildAll();
            BuildFloor(env);
            BuildCliffs(env);
            BuildCrater(env);
            BuildDebris(env);
            BuildGlowShrooms(env);
            BuildCrystals(env);
            BuildGlowFlowers(env);
            BuildGroundDetail(env);
            // частицы (светлячки, пылинки, дымка) только над ущельем: так они гуще там, где их видно
            AtmosphereBuilder.BuildEffects(env, _rng, new Vector3(0f, 0f, 0f), new Vector2(28f, ZMax - ZMin));

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
            ground.GetComponent<Renderer>().sharedMaterial = GroundMaterial();

            // тропа рисуется прямо шейдером земли (Game/Ground Toon), здесь только подъём за аркой — намёк «там выход наверх»
            var pathMat = ShowcaseSceneBuilder.Mat("RavinePath", new Color(0.42f, 0.35f, 0.28f));
            var ramp = ShowcaseSceneBuilder.Prop(PrimitiveType.Cube, parent, "ExitRamp", Floor(ExitZ + 5f) + Vector3.up * 1.2f,
                new Vector3(5f, 0.4f, 8f), pathMat, keepCollider: false);
            ramp.transform.rotation = Quaternion.Euler(-16f, 0f, 0f);
        }

        /// <summary>
        /// Материал земли: процедурный шейдер (земля пятнами, мох, тропа, камешки, листья). Тропа повторяет
        /// <see cref="CenterX"/>. Создаётся один раз — потом цвета можно крутить в инспекторе, параметры тропы
        /// переписываются при каждой пересборке (они должны совпадать с формой ущелья).
        /// </summary>
        private static Material GroundMaterial()
        {
            const string path = "Assets/_Project/Art/Generated/Materials/RavineGround.mat";
            var shader = Shader.Find("Game/Ground Toon");
            if (shader == null)
            {
                Debug.LogWarning("[Game] Нет шейдера Game/Ground Toon — земля будет однотонной. Проверь Console на ошибки шейдеров.");
                return ShowcaseSceneBuilder.Mat("RavineFloor", new Color(0.31f, 0.28f, 0.27f));
            }
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = "RavineGround" };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetFloat("_PathAmp", 3.5f);       // = CenterX
            m.SetFloat("_PathFreq", 0.055f);
            m.SetFloat("_PathOffsetX", 0f);
            m.SetFloat("_PathWidth", 1.5f);
            m.SetFloat("_PathZMin", CraterZ + 4f);
            m.SetFloat("_PathZMax", ExitZ + 10f);
            m.SetFloat("_PebbleDensity", 0.12f); // крупные камешки — это модели в BuildGroundDetail, в шейдере только мелкая крошка
            m.SetFloat("_LeafDensity", 0.15f);
            EditorUtility.SetDirty(m);
            return m;
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
                ShowcaseSceneBuilder.UnlitMat("ImpactGlow", new Color(1.25f, 0.48f, 0.16f)), keepCollider: false);

            // раскалённые трещины от центра
            var crackMat = ShowcaseSceneBuilder.UnlitMat("Crack", new Color(1.05f, 0.36f, 0.1f));
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
            l.range = 10f;
            l.intensity = 2.4f;
            l.shadows = LightShadows.None;
            var flicker = lightGo.AddComponent<LightFlicker>();
            flicker.BaseIntensity = 2.4f;
            flicker.Amount = 0.5f;
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

        // ------------------------------------------------------------------ локальные источники света
        // Сцена специально тёмная: глаз цепляется за яркие пятна. Каждая группа светящихся объектов
        // даёт мягкий «бассейн» цветного света на землю и стены вокруг себя.

        /// <summary>Не ставим декор на тропу, в кратер, на скелета и перед аркой.</summary>
        private static bool Reserved(Vector3 p, float across)
        {
            if (Vector3.Distance(p, Floor(CraterZ)) < 7.5f) return true;
            if (Mathf.Abs(across) < 0.28f) return true;
            if (p.z > ExitZ - 6f && Mathf.Abs(across) < 0.6f) return true;
            if (Vector3.Distance(p, SkeletonPos) < 3.2f) return true;
            return false;
        }

        /// <summary>Toon-материал со свечением: грани остаются видны, а Bloom даёт ореол.</summary>
        private static Material GlowMat(string name, Color albedo, Color emission)
        {
            var m = ShowcaseSceneBuilder.Mat(name, albedo);
            if (m.HasProperty("_EmissionColor"))
            {
                m.SetColor("_EmissionColor", emission);
                m.EnableKeyword("_EMISSION"); // для URP Lit, если toon-шейдера нет
            }
            m.SetFloat("_RimStrength", 0.4f);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void AddPointLight(Transform parent, string name, Vector3 pos, Color color, float range, float intensity, float flicker = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            if (flicker > 0f)
            {
                var f = go.AddComponent<LightFlicker>();
                f.BaseIntensity = intensity;
                f.Amount = flicker;
                f.Speed = 0.8f;
            }
        }

        /// <summary>Светящиеся грибы у стен: бирюзовые и синие кучки на ножках, у каждой кучки свой свет.</summary>
        private static void BuildGlowShrooms(Transform parent)
        {
            var root = new GameObject("GlowShrooms").transform;
            root.SetParent(parent);
            var kinds = new[]
            {
                (cap: GlowMat("ShroomCapTeal", new Color(0.3f, 0.75f, 0.7f), new Color(0.25f, 1.6f, 1.35f)), light: new Color(0.35f, 1f, 0.85f)),
                (cap: GlowMat("ShroomCapBlue", new Color(0.35f, 0.5f, 0.9f), new Color(0.35f, 0.75f, 2.2f)), light: new Color(0.45f, 0.65f, 1f)),
            };
            var stemMat = ShowcaseSceneBuilder.Mat("ShroomStem", new Color(0.62f, 0.66f, 0.74f));

            for (int i = 0; i < 26; i++)
            {
                float z = Range(CraterZ + 6f, ExitZ - 3f);
                float across = (_rng.NextDouble() < 0.5 ? -1f : 1f) * Range(0.78f, 0.96f);
                var p = Floor(z, across);
                if (Reserved(p, across)) continue;
                var kind = kinds[_rng.Next(kinds.Length)];
                var cluster = new GameObject("ShroomCluster").transform;
                cluster.SetParent(root);
                cluster.position = p;

                int n = _rng.Next(3, 7);
                for (int k = 0; k < n; k++)
                {
                    var at = p + new Vector3(Range(-0.5f, 0.5f), 0f, Range(-0.5f, 0.5f));
                    float h = Range(0.1f, 0.38f), s = Range(0.14f, 0.32f);
                    var tilt = Quaternion.Euler(Range(-14f, 14f), Range(0f, 360f), Range(-14f, 14f));
                    var stem = ShowcaseSceneBuilder.Prop(PrimitiveType.Cylinder, cluster, "Stem", at + tilt * Vector3.up * h * 0.5f,
                        new Vector3(0.05f + s * 0.12f, h * 0.5f, 0.05f + s * 0.12f), stemMat, keepCollider: false);
                    stem.transform.rotation = tilt;
                    var cap = ShowcaseSceneBuilder.Prop(PrimitiveType.Sphere, cluster, "Cap", at + tilt * Vector3.up * h,
                        new Vector3(s, s * 0.45f, s), kind.cap, keepCollider: false);
                    cap.transform.rotation = tilt;
                }
                AddPointLight(cluster, "ShroomLight", p + Vector3.up * 0.7f, kind.light, Range(3.2f, 4.2f), Range(1.4f, 2f), flicker: 0.15f);
            }
        }

        /// <summary>
        /// Кристаллы: осколки чужого мира, вбитые в землю падением героя. У кратера — золотые и фиолетовые
        /// (две ветви браслета), дальше по ущелью — только фиолетовые, всё реже к выходу.
        /// </summary>
        private static void BuildCrystals(Transform parent)
        {
            var root = new GameObject("Crystals").transform;
            root.SetParent(parent);
            var mesh = CrystalMesh();
            var violet = (mat: GlowMat("CrystalViolet", new Color(0.55f, 0.3f, 0.95f), new Color(0.55f, 0.2f, 1.3f)), light: new Color(0.7f, 0.4f, 1f));
            var gold = (mat: GlowMat("CrystalGold", new Color(1f, 0.75f, 0.35f), new Color(1.25f, 0.7f, 0.18f)), light: new Color(1f, 0.7f, 0.35f));

            void Cluster(Vector3 p, (Material mat, Color light) kind, float scale)
            {
                var cluster = new GameObject("CrystalCluster").transform;
                cluster.SetParent(root);
                cluster.position = p;
                int n = _rng.Next(3, 7);
                for (int k = 0; k < n; k++)
                {
                    var go = new GameObject("Shard");
                    go.transform.SetParent(cluster);
                    go.transform.position = p + new Vector3(Range(-0.35f, 0.35f), -0.08f, Range(-0.35f, 0.35f)) * scale;
                    go.transform.rotation = Quaternion.Euler(Range(-32f, 32f), Range(0f, 360f), Range(-32f, 32f));
                    float w = Range(0.16f, 0.32f) * scale;
                    go.transform.localScale = new Vector3(w, Range(0.45f, 1.3f) * scale, w);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = kind.mat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                AddPointLight(cluster, "CrystalLight", p + Vector3.up * 1f, kind.light, 4.5f * Mathf.Sqrt(scale), 1.6f, flicker: 0.2f);
            }

            // у кратера: кольцо осколков, золото и фиолет вперемешку
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f + Range(-15f, 15f);
                if (Mathf.Abs(Mathf.DeltaAngle(a, 0f)) < 30f) a += 60f; // не перекрываем проход на север
                var p = Floor(CraterZ) + Quaternion.Euler(0f, a, 0f) * Vector3.forward * Range(6.8f, 8.2f);
                Cluster(p, i % 2 == 0 ? gold : violet, Range(0.9f, 1.3f));
            }
            // дальше по ущелью у стен
            for (float z = CraterZ + 12f; z < ExitZ - 4f; z += Range(9f, 14f))
            {
                float across = (_rng.NextDouble() < 0.5 ? -1f : 1f) * Range(0.82f, 0.95f);
                var p = Floor(z, across);
                if (Reserved(p, across)) continue;
                Cluster(p, violet, Range(0.7f, 1.1f));
            }
        }

        /// <summary>Мелкие светящиеся цветы в траве: тёплые точки без собственного света — их «добивает» Bloom.</summary>
        private static void BuildGlowFlowers(Transform parent)
        {
            var root = new GameObject("GlowFlowers").transform;
            root.SetParent(parent);
            var mats = new[]
            {
                ShowcaseSceneBuilder.UnlitMat("FlowerGlowPink", new Color(2.4f, 0.8f, 1.7f)),
                ShowcaseSceneBuilder.UnlitMat("FlowerGlowYellow", new Color(2.6f, 1.9f, 0.55f)),
                ShowcaseSceneBuilder.UnlitMat("FlowerGlowWhite", new Color(1.6f, 1.8f, 2.4f)),
            };
            for (int i = 0; i < 45; i++)
            {
                float z = Range(ZMin + 4f, ZMax - 3f), across = Range(-0.92f, 0.92f);
                var p = Floor(z, across);
                if (Reserved(p, across)) continue;
                var mat = mats[_rng.Next(mats.Length)];
                int n = _rng.Next(3, 8);
                for (int k = 0; k < n; k++)
                {
                    float s = Range(0.05f, 0.09f);
                    ShowcaseSceneBuilder.Prop(PrimitiveType.Sphere, root, "Flower",
                        p + new Vector3(Range(-0.6f, 0.6f), Range(0.08f, 0.22f), Range(-0.6f, 0.6f)), Vector3.one * s, mat, keepCollider: false);
                }
            }
        }

        // ------------------------------------------------------------------ мелкая детализация
        // Между крупными объектами не должно быть «пустого пола»: корни у стен, ветки, камешки, кучки листьев, кости.

        private static void BuildGroundDetail(Transform parent)
        {
            var root = new GameObject("GroundDetail").transform;
            root.SetParent(parent);
            var rootMat = ShowcaseSceneBuilder.Mat("Roots", new Color(0.3f, 0.22f, 0.18f));
            var twigMat = ShowcaseSceneBuilder.Mat("Twigs", new Color(0.36f, 0.27f, 0.2f));
            var pebbleMat = ShowcaseSceneBuilder.Mat("Pebbles", new Color(0.44f, 0.42f, 0.47f));
            var boneMat = ShowcaseSceneBuilder.Mat("Bone", new Color(0.82f, 0.78f, 0.66f));
            var leafMats = new[]
            {
                ShowcaseSceneBuilder.Mat("LeafRust", new Color(0.62f, 0.32f, 0.18f)),
                ShowcaseSceneBuilder.Mat("LeafOchre", new Color(0.66f, 0.5f, 0.22f)),
                ShowcaseSceneBuilder.Mat("LeafDry", new Color(0.45f, 0.36f, 0.26f)),
            };
            var rockPrefabs = EnvironmentPrefabs.Load("Rock");
            var grassPrefabs = EnvironmentPrefabs.Load("Grass");
            var bushPrefabs = EnvironmentPrefabs.Load("Bush");

            GameObject Piece(PrimitiveType t, string name, Vector3 pos, Vector3 scale, Quaternion rot, Material m)
            {
                var go = ShowcaseSceneBuilder.Prop(t, root, name, pos, scale, m, keepCollider: false);
                go.transform.rotation = rot;
                var r = go.GetComponent<Renderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                return go;
            }

            // корни: выползают из-под стен на пол, по 2–4 изогнутых сегмента
            for (float z = ZMin + 3f; z < ZMax - 3f; z += Range(2.2f, 4f))
            {
                int side = _rng.NextDouble() < 0.5 ? -1 : 1;
                var start = Floor(z, side * 1.02f);
                if (Reserved(Floor(z, side * 0.9f), side * 0.9f)) continue;
                float yaw = side > 0 ? 270f : 90f; // от стены к центру
                yaw += Range(-35f, 35f);
                var p = start;
                float thick = Range(0.09f, 0.16f);
                int segs = _rng.Next(2, 5);
                for (int k = 0; k < segs; k++)
                {
                    float len = Range(0.6f, 1.2f);
                    var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                    var mid = p + dir * len * 0.5f + Vector3.up * thick * 0.25f;
                    Piece(PrimitiveType.Cylinder, "Root", mid, new Vector3(thick, len * 0.5f, thick),
                        Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f), rootMat);
                    p += dir * len;
                    yaw += Range(-30f, 30f);
                    thick *= 0.75f;
                }
            }

            // ветки с сучками
            for (int i = 0; i < 40; i++)
            {
                float z = Range(ZMin + 3f, ZMax - 3f), across = Range(-0.9f, 0.9f);
                var p = Floor(z, across);
                if (Reserved(p, across)) continue;
                float yaw = Range(0f, 360f), len = Range(0.6f, 1.4f);
                var rot = Quaternion.Euler(0f, yaw, 0f);
                Piece(PrimitiveType.Cylinder, "Branch", p + Vector3.up * 0.03f, new Vector3(0.05f, len * 0.5f, 0.05f), rot * Quaternion.Euler(90f, 0f, 0f), twigMat);
                for (int k = 0; k < 2; k++)
                {
                    var at = p + rot * Vector3.forward * Range(-len * 0.35f, len * 0.35f) + Vector3.up * 0.03f;
                    var twigRot = rot * Quaternion.Euler(0f, (k == 0 ? 1 : -1) * Range(30f, 60f), 0f);
                    float tl = Range(0.2f, 0.4f);
                    Piece(PrimitiveType.Cylinder, "Twig", at + twigRot * Vector3.forward * tl * 0.5f, new Vector3(0.025f, tl * 0.5f, 0.025f),
                        twigRot * Quaternion.Euler(90f, 0f, 0f), twigMat);
                }
            }

            // камешки: мелкие версии камней из набора (или кубики)
            for (int i = 0; i < 140; i++)
            {
                float z = Range(ZMin + 2f, ZMax - 2f), across = Range(-0.97f, 0.97f);
                var p = Floor(z, across);
                if (Vector3.Distance(p, SkeletonPos) < 2f) continue;
                if (rockPrefabs.Length > 0)
                {
                    var go = PlacePrefab(root, rockPrefabs, p, 0.12f, 0.28f);
                    foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false; // камешки не мешают ходить
                }
                else
                {
                    float s = Range(0.08f, 0.2f);
                    Piece(PrimitiveType.Cube, "Pebble", p + Vector3.up * s * 0.3f, new Vector3(s, s * 0.6f, s * 1.2f),
                        Quaternion.Euler(Range(-20f, 20f), Range(0f, 360f), Range(-20f, 20f)), pebbleMat);
                }
            }

            // кучки сухих листьев: плоские «пластинки» тёплых цветов, гуще у стен
            for (int i = 0; i < 38; i++)
            {
                float z = Range(ZMin + 3f, ZMax - 3f);
                float across = (_rng.NextDouble() < 0.5 ? -1f : 1f) * Range(0.4f, 0.95f);
                var c = Floor(z, across);
                if (Reserved(c, across)) continue;
                int n = _rng.Next(6, 14);
                for (int k = 0; k < n; k++)
                {
                    var at = c + new Vector3(Range(-0.7f, 0.7f), 0.012f + k * 0.002f, Range(-0.7f, 0.7f));
                    Piece(PrimitiveType.Cube, "Leaf", at, new Vector3(Range(0.1f, 0.16f), 0.006f, Range(0.07f, 0.11f)),
                        Quaternion.Euler(Range(-8f, 8f), Range(0f, 360f), Range(-8f, 8f)), leafMats[_rng.Next(leafMats.Length)]);
                }
            }

            // кости — объедки падальщиков, недалеко от их логова на середине ущелья
            for (int i = 0; i < 10; i++)
            {
                float z = Range(-6f, 30f), across = Range(-0.8f, 0.8f);
                var p = Floor(z, across);
                if (Reserved(p, across)) continue;
                Piece(PrimitiveType.Capsule, "Bone", p + Vector3.up * 0.04f, new Vector3(0.06f, Range(0.15f, 0.25f), 0.06f),
                    Quaternion.Euler(90f, Range(0f, 360f), 0f), boneMat);
                if (_rng.NextDouble() < 0.4)
                    Piece(PrimitiveType.Sphere, "Skull", p + new Vector3(Range(-0.3f, 0.3f), 0.08f, Range(-0.3f, 0.3f)), new Vector3(0.16f, 0.14f, 0.19f),
                        Quaternion.Euler(Range(-20f, 20f), Range(0f, 360f), Range(-30f, 30f)), boneMat);
            }

            // больше травы и кустов у стен (основная трава — в BuildDebris)
            for (int i = 0; grassPrefabs.Length > 0 && i < 220; i++)
            {
                float z = Range(ZMin + 2f, ZMax - 2f);
                float across = (_rng.NextDouble() < 0.5 ? -1f : 1f) * Range(0.45f, 0.98f);
                var p = Floor(z, across);
                if (!Reserved(p, across)) PlacePrefab(root, grassPrefabs, p, 0.6f, 1.1f);
            }
            for (int i = 0; bushPrefabs.Length > 0 && i < 30; i++)
            {
                float z = Range(ZMin + 2f, ZMax - 2f);
                float across = (_rng.NextDouble() < 0.5 ? -1f : 1f) * Range(0.85f, 1f);
                var p = Floor(z, across);
                if (!Reserved(p, across)) PlacePrefab(root, bushPrefabs, p, 0.5f, 0.9f);
            }
        }

        /// <summary>Шестигранный кристалл с острой верхушкой (высота 1, ширина 1), плоские грани. Сохраняется ассетом.</summary>
        private static Mesh CrystalMesh()
        {
            const string path = "Assets/_Project/Art/Generated/Crystal6.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            mesh = new Mesh { name = "Crystal6" };

            const int sides = 6;
            const float body = 0.72f;
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                int o = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                tris.Add(o); tris.Add(o + 1); tris.Add(o + 2);
            }
            var top = new Vector3(0f, 1f, 0f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                var b0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f);
                var b1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f);
                var t0 = b0 * 0.9f + Vector3.up * body;
                var t1 = b1 * 0.9f + Vector3.up * body;
                // бок (по часовой, если смотреть снаружи — так Unity считает лицевой стороной)
                Tri(b0, t1, b1);
                Tri(b0, t0, t1);
                // верхушка
                Tri(t0, top, t1);
            }
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
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
