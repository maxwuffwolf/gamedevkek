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
        /// <summary>Радиус чаши кратера (шейдер земли рисует плиты до него, пепел и лучи — до ~2 радиусов).</summary>
        private const float CraterRadius = 5.5f;

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
            BuildDressing(env, gameplay); // вся детализация — по «главам» ущелья, см. ниже
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

            // --- выход в лес (проход в зарослях, как в PoE): ведёт на вход "from_ravine" в лесу, рядом с ним наш вход "from_forest"
            ZoneGateBuilder.Build(gameplay, Floor(ExitZ), 180f, targetZone: "forest", targetEntrance: "from_ravine",
                localEntranceId: "from_forest", style: ZoneGateBuilder.Style.Thicket, wallHalfWidth: HalfWidth(ExitZ) + 1.5f);

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
            bool created = m == null;
            if (created)
            {
                m = new Material(shader) { name = "RavineGround" };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetFloat("_PathAmp", 3.5f);       // = CenterX
            m.SetFloat("_PathFreq", 0.055f);
            m.SetFloat("_PathOffsetX", 0f);
            m.SetFloat("_PathWidth", 1.5f);
            m.SetFloat("_PathZMin", CraterZ + CraterRadius + 1f); // тропа начинается за валом кратера
            // форма ущелья (= HalfWidth) и кратер
            m.SetVector("_RavineWidth", new Vector4(6.5f, 1.8f, 0.13f, 1.3f));
            var cc = Floor(CraterZ);
            m.SetVector("_CraterCenter", new Vector4(cc.x, cc.z, 0f, 0f));
            m.SetFloat("_CraterRadius", CraterRadius);
            // Новые параметры шейдера задаём явно при каждой пересборке: у материала, созданного старой версией
            // шейдера, их нет, и Unity подставляет нули (огромные «комья» на всю землю, чёрная земля у стен).
            m.SetFloat("_NoiseScale", 0.16f);
            m.SetFloat("_ClodScale", 1.6f);
            m.SetFloat("_Bevel", 0.5f);
            m.SetFloat("_MossAmount", 0.5f);
            m.SetFloat("_WallBand", 0.62f);
            m.SetColor("_DampColor", new Color(0.19f, 0.17f, 0.2f));
            m.SetColor("_CharColor", new Color(0.13f, 0.11f, 0.14f));
            m.SetColor("_CharColor2", new Color(0.2f, 0.16f, 0.18f));
            m.SetColor("_AshColor", new Color(0.36f, 0.33f, 0.38f));
            m.SetColor("_MagmaColor", new Color(2.2f, 0.6f, 0.14f));
            m.SetColor("_CoreColor", new Color(2.6f, 1.3f, 0.5f));
            m.SetFloat("_Heat", 0.8f);
            m.SetFloat("_PulseSpeed", 1.3f);
            m.SetFloat("_Steps", 2f);
            m.SetFloat("_StepSmooth", 0.06f);
            m.SetFloat("_PointSmooth", 1f);
            m.SetFloat("_PathZMax", ExitZ + 10f);
            if (created || AtmosphereBuilder.RefreshAssets)
            {
                m.SetColor("_DirtColor", new Color(0.25f, 0.21f, 0.22f));
                m.SetColor("_DirtColor2", new Color(0.33f, 0.27f, 0.24f));
                m.SetColor("_MossColor", new Color(0.15f, 0.25f, 0.21f));
                m.SetColor("_MossColor2", new Color(0.24f, 0.34f, 0.24f));
                m.SetColor("_PathColor", new Color(0.45f, 0.36f, 0.27f));
                m.SetColor("_PebbleColor", new Color(0.46f, 0.44f, 0.48f));
                m.SetColor("_LeafColor", new Color(0.62f, 0.36f, 0.2f));
                m.SetColor("_CrackColor", new Color(0.12f, 0.1f, 0.12f));
                m.SetColor("_ShadowColor", new Color(0.38f, 0.4f, 0.66f));
                m.SetFloat("_AmbientStrength", 0.75f);
            }
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

            // сам кратер (плиты, светящиеся швы, пепел, лучи выброса) рисует шейдер земли — см. GroundMaterial.
            // Здесь только объём: вал из выброшенных обломков, с проходом на север.
            var rimMat = ShowcaseSceneBuilder.Mat("ScorchedStone", new Color(0.24f, 0.21f, 0.24f));
            var rockPrefabs = EnvironmentPrefabs.Load("Rock");
            for (int i = 0; i < 16; i++)
            {
                float a = i * 22.5f + Range(-7f, 7f);
                if (Mathf.Abs(Mathf.DeltaAngle(a, 0f)) < 30f) continue; // проход
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                var p = c + dir * Range(CraterRadius * 0.95f, CraterRadius * 1.15f);
                GameObject rock;
                if (rockPrefabs.Length > 0)
                {
                    rock = PlacePrefab(root, rockPrefabs, p, Range(0.55f, 1.0f), 1f);
                    rock.transform.localScale = Vector3.one * Range(0.55f, 1.05f);
                    foreach (var r in rock.GetComponentsInChildren<Renderer>()) r.sharedMaterial = rimMat;
                }
                else
                {
                    var sz = new Vector3(Range(1f, 2f), Range(0.6f, 1.4f), Range(1f, 1.8f));
                    rock = ShowcaseSceneBuilder.Prop(PrimitiveType.Cube, root, "RimRock", p + Vector3.up * sz.y * 0.3f, sz, rimMat, keepCollider: true);
                }
                // обломки наклонены наружу — их выбросило ударом
                rock.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(Range(-25f, -10f), Range(-30f, 30f), Range(-10f, 10f));
            }

            var lightGo = new GameObject("ImpactLight");
            lightGo.transform.SetParent(root);
            lightGo.transform.position = c + Vector3.up * 1.5f;
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.55f, 0.25f);
            l.range = 9f;
            l.intensity = 2f;
            l.shadows = LightShadows.None;
            var flicker = lightGo.AddComponent<LightFlicker>();
            flicker.BaseIntensity = 2f;
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

        // ------------------------------------------------------------------ композиция
        // Ущелье читается как четыре «главы» с юга на север, у каждой — свой небольшой набор деталей:
        //   1. Пепелище (кратер): ничего живого — плиты, пепел, обугленные ветки, кристаллы-осколки по валу.
        //   2. Привал павшего: сухая трава, камни, кости, цепочка мелких кристаллов уводит на север.
        //   3. Грибная лощина: зелень возвращается; у стен три «сценки» со светящимися грибами (у каждой свой цвет)
        //      и логово падальщиков.
        //   4. Опушка: густой подлесок у стен, пни, ягоды, птицы; светящиеся колокольчики ведут к выходу.
        // Правила: тропа (|across| < 0.3) чистая; крупное и тёмное — только у стен; мелочь не рассыпается
        // поодиночке, а сидит группами вокруг «якоря» (камня, пня, бревна, корня).

        private enum Chapter { Ashes, Fallen, Grove, Edge }

        private const float FallenStartZ = CraterZ + 9f;   // -25
        private const float GroveStartZ = SkeletonZ + 8f;  // -14
        private const float EdgeStartZ = 13f;

        private static Chapter ChapterAt(float z) =>
            z < FallenStartZ ? Chapter.Ashes : z < GroveStartZ ? Chapter.Fallen : z < EdgeStartZ ? Chapter.Grove : Chapter.Edge;

        /// <summary>Модели Det_* (Tools/Blender/forest_details.py) и камни из набора леса. Любое поле может быть null.</summary>
        private sealed class Kit
        {
            public GameObject FernA, FernB, BushRound, BushBlue, BushFlower, BerryBlue, BerryRed;
            public GameObject GrassTall, GrassBlue, BellsWhite, BellsPink;
            public GameObject GlowA, GlowB, GlowTall, Toadstool, Shelf;
            public GameObject RootsBig, RootsSmall, StickA, StickB, StickPile, Leaves, Pebbles, Stump, Log;
            public GameObject CrystalViolet, CrystalSmall, CrystalGold;
            public GameObject Bird, Moth, Beetle;
            public GameObject[] Rocks;
            public System.Collections.Generic.Dictionary<Material, Material> Charred, Dry;
        }

        private static Kit LoadKit()
        {
            var k = new Kit
            {
                FernA = ForestDetails.Load("Det_Fern_A"), FernB = ForestDetails.Load("Det_Fern_B"),
                BushRound = ForestDetails.Load("Det_Bush_Round"), BushBlue = ForestDetails.Load("Det_Bush_Blue"),
                BushFlower = ForestDetails.Load("Det_Bush_Flower"),
                BerryBlue = ForestDetails.Load("Det_Berry_Blue"), BerryRed = ForestDetails.Load("Det_Berry_Red"),
                GrassTall = ForestDetails.Load("Det_Grass_Tall"), GrassBlue = ForestDetails.Load("Det_Grass_Blue"),
                BellsWhite = ForestDetails.Load("Det_Flower_Bells"), BellsPink = ForestDetails.Load("Det_Flower_Bells_Pink"),
                GlowA = ForestDetails.Load("Det_Shroom_Glow_A"), GlowB = ForestDetails.Load("Det_Shroom_Glow_B"),
                GlowTall = ForestDetails.Load("Det_Shroom_Tall"), Toadstool = ForestDetails.Load("Det_Shroom_Toadstool"),
                Shelf = ForestDetails.Load("Det_Shroom_Shelf"),
                RootsBig = ForestDetails.Load("Det_Roots_Big"), RootsSmall = ForestDetails.Load("Det_Roots_Small"),
                StickA = ForestDetails.Load("Det_Stick_A"), StickB = ForestDetails.Load("Det_Stick_B"),
                StickPile = ForestDetails.Load("Det_Stick_Pile"), Leaves = ForestDetails.Load("Det_Leaves"),
                Pebbles = ForestDetails.Load("Det_Pebbles"), Stump = ForestDetails.Load("Det_Stump"), Log = ForestDetails.Load("Det_Log_Mossy"),
                CrystalViolet = ForestDetails.Load("Det_Crystal_Violet"), CrystalSmall = ForestDetails.Load("Det_Crystal_Small"),
                CrystalGold = ForestDetails.Load("Det_Crystal_Gold"),
                Bird = ForestDetails.Load("Det_Bird"), Moth = ForestDetails.Load("Det_Moth"), Beetle = ForestDetails.Load("Det_Beetle"),
                Rocks = EnvironmentPrefabs.Load("Rock"),
            };
            k.Charred = TintedDetailMaterials("Charred", new Color(0.32f, 0.27f, 0.28f));
            k.Dry = TintedDetailMaterials("Dry", new Color(1.15f, 0.95f, 0.55f));
            return k;
        }

        /// <summary>
        /// Перекрашенные копии M_Detail / M_Detail_Leaf (обугленные ветки у кратера, сухая трава у скелета):
        /// исходный материал → копия с другим оттенком. Листва остаётся двусторонней и с ветром.
        /// </summary>
        private static System.Collections.Generic.Dictionary<Material, Material> TintedDetailMaterials(string suffix, Color tint)
        {
            var map = new System.Collections.Generic.Dictionary<Material, Material>();
            foreach (var kv in ForestDetails.EnsureMaterials())
            {
                if (kv.Value == null || kv.Key == "M_Detail_Glow") continue;
                string path = $"{ForestDetails.MaterialsDir}/{kv.Key}_{suffix}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(kv.Value) { name = $"{kv.Key}_{suffix}" };
                    AssetDatabase.CreateAsset(m, path);
                }
                else m.CopyPropertiesFromMaterial(kv.Value);
                m.SetColor("_BaseColor", tint);
                EditorUtility.SetDirty(m);
                map[kv.Value] = m;
            }
            return map;
        }

        private static GameObject Put(GameObject model, Transform parent, Vector3 p, float scale, float yaw = float.NaN,
            bool shadows = false, System.Collections.Generic.Dictionary<Material, Material> recolor = null)
        {
            if (model == null) return null;
            var go = ForestDetails.Place(model, parent, p, float.IsNaN(yaw) ? Range(0f, 360f) : yaw, scale, shadows);
            if (recolor != null)
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    var arr = r.sharedMaterials;
                    for (int i = 0; i < arr.Length; i++)
                        if (arr[i] != null && recolor.TryGetValue(arr[i], out var tinted)) arr[i] = tinted;
                    r.sharedMaterials = arr;
                }
            return go;
        }

        private static GameObject PickOf(params GameObject[] arr)
        {
            int n = 0;
            foreach (var a in arr) if (a != null) n++;
            if (n == 0) return null;
            int k = _rng.Next(n);
            foreach (var a in arr)
                if (a != null && k-- == 0) return a;
            return null;
        }

        /// <summary>Точка рядом с якорем (кольцо радиусом rMin..rMax), не залезающая на тропу.</summary>
        private static Vector3 Around(Vector3 anchor, float rMin, float rMax)
        {
            for (int i = 0; i < 6; i++)
            {
                var p = anchor + Quaternion.Euler(0f, Range(0f, 360f), 0f) * Vector3.forward * Range(rMin, rMax);
                if (Mathf.Abs(Across(p)) >= 0.3f) return p;
            }
            return anchor;
        }

        /// <summary>Обратное к <see cref="Floor"/>: -1 у левой стены, 0 по центру, 1 у правой.</summary>
        private static float Across(Vector3 p) => (p.x - CenterX(p.z)) / HalfWidth(p.z);

        private static void BuildDressing(Transform env, Transform gameplay)
        {
            _glowSpots.Clear();
            if (!ForestDetails.Available)
            {
                Debug.LogWarning("[Game] Нет моделей Det_*.fbx (Tools/Blender/forest_details.py) — овраг соберётся без детализации.");
                return;
            }
            var kit = LoadKit();
            var root = new GameObject("Dressing").transform;
            root.SetParent(env);
            Transform Group(string name)
            {
                var t = new GameObject(name).transform;
                t.SetParent(root);
                return t;
            }

            BuildCraterDressing(Group("1_Ashes"), kit);
            BuildFallenDressing(Group("2_Fallen"), kit);
            BuildGroveDressing(Group("3_Grove"), kit);
            BuildEdgeDressing(Group("4_Edge"), kit);
            BuildWallSkirt(Group("WallSkirt"), kit);
            BuildPathPebbles(Group("PathPebbles"), kit);
            BuildCritters(gameplay, kit);
        }

        // ---------------------------------------------------------------- 1. Пепелище

        private static void BuildCraterDressing(Transform root, Kit kit)
        {
            var c = Floor(CraterZ);
            // кристаллы-осколки на валу: золото и фиолет (две ветви наруча), проход на север свободен
            for (int i = 0; i < 5; i++)
            {
                float a = 36f + i * 72f + Range(-10f, 10f);
                var p = c + Quaternion.Euler(0f, a, 0f) * Vector3.forward * Range(6.6f, 7.6f);
                CrystalCluster(root, kit, p, gold: i % 2 == 0, scale: Range(1.0f, 1.3f), light: true);
            }
            // обугленные ветки и пеньки, отброшенные ударом: лежат по радиусу, наружу от центра
            for (int i = 0; i < 9; i++)
            {
                float a = i * 40f + Range(-12f, 12f);
                if (Mathf.Abs(Mathf.DeltaAngle(a, 0f)) < 25f) continue;
                var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                var p = c + dir * Range(4.2f, 6f);
                Put(PickOf(kit.StickA, kit.StickB), root, p, Range(1.2f, 1.7f), a + Range(-20f, 20f), recolor: kit.Charred);
            }
            var stumpAt = c + Quaternion.Euler(0f, 250f, 0f) * Vector3.forward * 8.6f;
            var stump = Put(kit.Stump, root, stumpAt, 1.25f, Range(0f, 360f), shadows: true, recolor: kit.Charred);
            if (stump != null) AddCapsule(stump, 0.45f, 0.8f);
            Put(kit.StickPile, root, Around(stumpAt, 0.8f, 1.2f), 1.1f, recolor: kit.Charred);
            // мелкие камешки у вала
            for (int i = 0; i < 6; i++)
                Put(kit.Pebbles, root, c + Quaternion.Euler(0f, Range(40f, 320f), 0f) * Vector3.forward * Range(5f, 6.5f), Range(1f, 1.4f));

            // за валом, в пепле: россыпь опалённых обломков группами по 2–3 (выброшены ударом), проход на север свободен
            var scorched = ShowcaseSceneBuilder.Mat("ScorchedStone", new Color(0.24f, 0.21f, 0.24f));
            for (int i = 0; i < 10 && kit.Rocks.Length > 0; i++)
            {
                float a = i * 36f + Range(-12f, 12f);
                if (Mathf.Abs(Mathf.DeltaAngle(a, 0f)) < 28f) continue;
                var at = c + Quaternion.Euler(0f, a, 0f) * Vector3.forward * Range(7.2f, 9.2f);
                if (Mathf.Abs(Across(at)) > 0.9f) continue;
                for (int k = _rng.Next(1, 4); k > 0; k--)
                {
                    var rock = PlacePrefab(root, kit.Rocks, at + new Vector3(Range(-0.7f, 0.7f), 0f, Range(-0.7f, 0.7f)), 0.18f, 0.4f);
                    foreach (var r in rock.GetComponentsInChildren<Renderer>()) r.sharedMaterial = scorched;
                    foreach (var col in rock.GetComponentsInChildren<Collider>()) col.enabled = false;
                }
                if (_rng.NextDouble() < 0.5) Put(PickOf(kit.StickA, kit.StickB), root, Around(at, 0.4f, 0.9f), Range(1f, 1.4f), recolor: kit.Charred);
            }
            // дальше — первая сухая трава: жизнь начинается там, где кончается пепел
            for (int i = 0; i < 14; i++)
            {
                float a = Range(30f, 330f);
                var at = c + Quaternion.Euler(0f, a, 0f) * Vector3.forward * Range(9.5f, 11.5f);
                float ac = Across(at);
                if (Mathf.Abs(ac) > 0.95f || Mathf.Abs(ac) < 0.3f) continue;
                for (int k = _rng.Next(2, 4); k > 0; k--)
                    Put(kit.GrassTall, root, at + new Vector3(Range(-0.5f, 0.5f), 0f, Range(-0.5f, 0.5f)), Range(0.8f, 1.1f), recolor: kit.Dry);
            }
        }

        private static void CrystalCluster(Transform parent, Kit kit, Vector3 p, bool gold, float scale, bool light)
        {
            var main = gold ? kit.CrystalGold : kit.CrystalViolet;
            if (main == null) return;
            var cluster = new GameObject(gold ? "CrystalGold" : "CrystalViolet").transform;
            cluster.SetParent(parent);
            cluster.position = p;
            _glowSpots.Add(p);
            Put(main, cluster, p, scale * Range(1.3f, 1.6f), shadows: true);
            for (int e = _rng.Next(1, 3); e > 0; e--)
            {
                var off = Quaternion.Euler(0f, Range(0f, 360f), 0f) * Vector3.forward * Range(0.7f, 1.1f) * scale;
                Put(gold && _rng.NextDouble() < 0.5 ? kit.CrystalGold : kit.CrystalSmall, cluster, p + off, scale * Range(0.6f, 0.9f));
            }
            if (light)
                AddPointLight(cluster, "CrystalLight", p + Vector3.up * 1f,
                    gold ? new Color(1f, 0.7f, 0.35f) : new Color(0.7f, 0.4f, 1f), 4.8f * Mathf.Sqrt(scale), 1.8f, flicker: 0.2f);
        }

        // ---------------------------------------------------------------- 2. Привал павшего

        private static void BuildFallenDressing(Transform root, Kit kit)
        {
            // цепочка мелких кристаллов от кратера на север — «след» осколков, ведёт взгляд по тропе
            for (int i = 0; i < 6; i++)
            {
                float z = FallenStartZ - 1f + i * 3.2f;
                float side = i % 2 == 0 ? 1f : -1f;
                var p = Floor(z, side * Range(0.36f, 0.5f));
                if (Vector3.Distance(p, SkeletonPos) < 2.5f) continue;
                Put(kit.CrystalSmall, root, p, Range(1.1f, 1.5f) * (1f - i * 0.08f));
            }

            // скелет: вокруг — кости, сухая трава и пара камней, позади у стены — бревно
            var s = SkeletonPos;
            var boneMat = ShowcaseSceneBuilder.Mat("Bone", new Color(0.82f, 0.78f, 0.66f));
            for (int i = 0; i < 4; i++)
            {
                var p = s + Quaternion.Euler(0f, Range(100f, 260f), 0f) * Vector3.forward * Range(1.6f, 2.4f);
                var b = ShowcaseSceneBuilder.Prop(PrimitiveType.Capsule, root, "Bone", p + Vector3.up * 0.04f,
                    new Vector3(0.06f, Range(0.14f, 0.22f), 0.06f), boneMat, keepCollider: false);
                b.transform.rotation = Quaternion.Euler(90f, Range(0f, 360f), 0f);
            }
            for (int i = 0; i < 7; i++)
                Put(kit.GrassTall, root, Around(s, 1.7f, 2.8f), Range(0.9f, 1.3f), recolor: kit.Dry);
            for (int i = 0; i < 2 && kit.Rocks.Length > 0; i++)
            {
                var rock = PlacePrefab(root, kit.Rocks, Around(s, 2.2f, 3f), 0.35f, 0.55f);
                foreach (var col in rock.GetComponentsInChildren<Collider>()) col.enabled = false;
            }
            var logAt = Floor(SkeletonZ + 3.5f, -0.82f);
            var log = Put(kit.Log, root, logAt, 1.1f, Range(-25f, 25f), shadows: true);
            if (log != null) AddBox(log, new Vector3(2.4f, 0.5f, 0.5f));
            Put(kit.Shelf, root, logAt + Vector3.up * 0.1f, 1.0f);
            Put(kit.Leaves, root, Around(logAt, 0.6f, 1.2f), 1.4f);

            // напротив, у правой стены — валун с сухой травой (балансирует бревно слева)
            var rockAt = Floor(SkeletonZ - 1.5f, 0.78f);
            if (kit.Rocks.Length > 0) PlacePrefab(root, kit.Rocks, rockAt, 0.9f, 1.1f);
            for (int i = 0; i < 4; i++) Put(kit.GrassTall, root, Around(rockAt, 1f, 1.6f), Range(0.9f, 1.2f), recolor: kit.Dry);
        }

        // ---------------------------------------------------------------- 3. Грибная лощина

        private static void BuildGroveDressing(Transform root, Kit kit)
        {
            // три сценки со светящимися грибами — у каждой один цвет: бирюза, синий, фиолет
            GlowVignette(root, kit, Floor(-9f, -0.8f), kit.GlowA, new Color(0.35f, 1f, 0.85f), anchor: kit.Stump);
            GlowVignette(root, kit, Floor(0f, 0.8f), kit.GlowB, new Color(0.45f, 0.65f, 1f), anchor: kit.RootsBig);
            GlowVignette(root, kit, Floor(10f, -0.78f), kit.GlowTall, new Color(0.75f, 0.45f, 1f), anchor: kit.Log);

            // логово падальщиков (между сценками, у правой стены): хворост, кости, листья — тёмное и без света
            var den = Floor(6f, 0.62f);
            Put(kit.StickPile, root, den, 1.4f, shadows: true);
            Put(kit.StickPile, root, Around(den, 0.9f, 1.3f), 1.1f);
            Put(kit.Leaves, root, Around(den, 0.4f, 1f), 1.6f);
            var boneMat = ShowcaseSceneBuilder.Mat("Bone", new Color(0.82f, 0.78f, 0.66f));
            for (int i = 0; i < 5; i++)
            {
                var p = Around(den, 0.6f, 1.8f);
                var b = ShowcaseSceneBuilder.Prop(PrimitiveType.Capsule, root, "Bone", p + Vector3.up * 0.04f,
                    new Vector3(0.06f, Range(0.15f, 0.25f), 0.06f), boneMat, keepCollider: false);
                b.transform.rotation = Quaternion.Euler(90f, Range(0f, 360f), 0f);
            }

            // островки синей травы между сценками
            GrassPatches(root, kit.GrassBlue, GroveStartZ, EdgeStartZ, 6, 0.42f, 0.7f);
        }

        /// <summary>Якорь (пень/корни/бревно) + грибы одного вида + папоротники + свой цветной свет.</summary>
        private static void GlowVignette(Transform root, Kit kit, Vector3 at, GameObject shroom, Color light, GameObject anchor)
        {
            var g = new GameObject("GlowVignette").transform;
            g.SetParent(root);
            g.position = at;
            float side = Mathf.Sign(Across(at));
            float yawToCenter = side > 0 ? 270f : 90f;
            if (anchor != null)
            {
                var a = Put(anchor, g, at + Vector3.right * side * 0.4f, anchor == kit.RootsBig ? 1.5f : 1.15f,
                    yawToCenter + Range(-30f, 30f), shadows: true);
                if (anchor == kit.Stump) AddCapsule(a, 0.45f, 0.8f);
                if (anchor == kit.Log) AddBox(a, new Vector3(2.4f, 0.5f, 0.5f));
            }
            // грибы — дугой со стороны тропы, крупные ближе к якорю
            for (int i = 0; i < 4; i++)
            {
                float ang = yawToCenter + Range(-70f, 70f);
                var p = at + Quaternion.Euler(0f, ang, 0f) * Vector3.forward * Range(0.7f, 1.5f);
                Put(shroom, g, p, Range(1.0f, 1.7f) * (i == 0 ? 1.2f : 1f));
            }
            Put(kit.FernA, g, at + Vector3.right * side * 1.1f + Vector3.forward * Range(-1f, 1f), Range(1.6f, 2f));
            Put(kit.FernB, g, Around(at, 1.4f, 2f), Range(1.3f, 1.7f));
            Put(kit.Leaves, g, Around(at, 0.5f, 1.2f), 1.3f);
            AddPointLight(g, "GlowLight", at + Vector3.up * 0.8f - Vector3.right * side * 0.6f, light, 5f, 2.2f, flicker: 0.12f);
            _glowSpots.Add(at);
        }

        // ---------------------------------------------------------------- 4. Опушка

        private static void BuildEdgeDressing(Transform root, Kit kit)
        {
            // бревно с грибами-трутовиками у правой стены
            var logAt = Floor(17f, 0.8f);
            var log = Put(kit.Log, root, logAt, 1.2f, Range(-20f, 20f), shadows: true);
            if (log != null) AddBox(log, new Vector3(2.4f, 0.5f, 0.5f));
            Put(kit.FernA, root, Around(logAt, 1.2f, 1.6f), 1.7f);

            // пень в кольце мухоморов — «ведьмин круг» у левой стены
            var ring = Floor(24f, -0.72f);
            var stump = Put(kit.Stump, root, ring, 1.2f, shadows: true);
            if (stump != null) AddCapsule(stump, 0.45f, 0.8f);
            for (int i = 0; i < 6; i++)
                Put(kit.Toadstool, root, ring + Quaternion.Euler(0f, i * 60f + Range(-12f, 12f), 0f) * Vector3.forward * Range(1.2f, 1.5f), Range(0.9f, 1.2f));

            // ягодная полянка у правой стены
            var berries = Floor(31f, 0.7f);
            for (int i = 0; i < 4; i++) Put(PickOf(kit.BerryBlue, kit.BerryRed), root, Around(berries, 0.3f, 1.4f), Range(1.2f, 1.6f));
            Put(kit.BushRound, root, berries + Vector3.right * 1.2f, 1.8f);

            // светящиеся колокольчики вдоль тропы перед выходом — «маяк», куда идти
            for (float z = ExitZ - 9f; z < ExitZ - 1.5f; z += Range(1.6f, 2.4f))
                for (int side = -1; side <= 1; side += 2)
                    Put(_rng.NextDouble() < 0.7 ? kit.BellsWhite : kit.BellsPink, root, Floor(z, side * Range(0.42f, 0.56f)), Range(1.2f, 1.5f));
            _glowSpots.Add(Floor(ExitZ - 5f, 0.5f));
            _glowSpots.Add(Floor(ExitZ - 5f, -0.5f));

            GrassPatches(root, kit.GrassTall, EdgeStartZ, ExitZ - 8f, 7, 0.4f, 0.72f);
        }

        /// <summary>Трава островками по 5–8 кустиков, только одного вида на главу.</summary>
        private static void GrassPatches(Transform root, GameObject grass, float z0, float z1, int count, float aMin, float aMax)
        {
            if (grass == null) return;
            for (int i = 0; i < count; i++)
            {
                float z = Mathf.Lerp(z0, z1, (i + Range(0.2f, 0.8f)) / count);
                float side = _rng.NextDouble() < 0.5 ? -1f : 1f;
                var c = Floor(z, side * Range(aMin, aMax));
                if (Reserved(c, Across(c))) continue;
                for (int k = _rng.Next(5, 9); k > 0; k--)
                {
                    var p = c + new Vector3(Range(-0.8f, 0.8f), 0f, Range(-0.8f, 0.8f));
                    if (Mathf.Abs(Across(p)) < 0.3f) continue;
                    Put(grass, root, p, Range(0.9f, 1.35f));
                }
            }
        }

        // ---------------------------------------------------------------- рамка: подлесок у стен, камешки у тропы

        /// <summary>
        /// Непрерывная «юбка» у подножия стен — рамка, в которую вписаны сценки. Кустики идут группами
        /// с паузами (ритм), состав зависит от главы, в кратере юбки нет.
        /// </summary>
        private static void BuildWallSkirt(Transform root, Kit kit)
        {
            for (int sideI = 0; sideI < 2; sideI++)
            {
                float side = sideI == 0 ? -1f : 1f;
                float z = ZMin + 3f + Range(0f, 2f);
                float nextRoots = z + Range(3f, 6f);
                while (z < ExitZ - 7f)
                {
                    var chapter = ChapterAt(z);
                    float step = chapter switch { Chapter.Fallen => Range(3.5f, 5.5f), Chapter.Grove => Range(2.4f, 3.4f), _ => Range(1.8f, 2.6f) };
                    var p = Floor(z, side * Range(0.86f, 0.97f));
                    bool busy = (chapter != Chapter.Ashes && Reserved(p, side * 0.9f)) || NearVignette(p)
                                || Vector3.Distance(p, Floor(CraterZ)) < CraterRadius * 1.3f;
                    if (!busy && _rng.NextDouble() > 0.18)
                    {
                        switch (chapter)
                        {
                            case Chapter.Ashes:
                                // у кратера всё опалено: камни, обугленные ветки, редкая сухая трава
                                if (kit.Rocks.Length > 0) PlacePrefab(root, kit.Rocks, p, 0.5f, 0.85f);
                                if (_rng.NextDouble() < 0.6)
                                    Put(PickOf(kit.StickA, kit.StickB), root, p - Vector3.right * side * Range(0.6f, 1.1f), Range(1.1f, 1.5f), recolor: kit.Charred);
                                else
                                    Put(kit.GrassTall, root, p - Vector3.right * side * Range(0.5f, 0.9f), Range(0.8f, 1.1f), recolor: kit.Dry);
                                break;
                            case Chapter.Fallen:
                                if (kit.Rocks.Length > 0 && _rng.NextDouble() < 0.5) PlacePrefab(root, kit.Rocks, p, 0.6f, 0.9f);
                                Put(kit.GrassTall, root, p - Vector3.right * side * Range(0.5f, 0.9f), Range(0.9f, 1.2f), recolor: kit.Dry);
                                break;
                            case Chapter.Grove:
                                Put(_rng.NextDouble() < 0.6 ? kit.FernA : kit.FernB, root, p, Range(1.5f, 2.1f));
                                if (_rng.NextDouble() < 0.5) Put(kit.BushBlue, root, p + Vector3.forward * Range(-0.9f, 0.9f) + Vector3.right * side * 0.4f, Range(1.4f, 1.9f));
                                break;
                            default:
                                Put(PickOf(kit.BushRound, kit.BushRound, kit.BushFlower), root, p + Vector3.right * side * 0.3f, Range(1.5f, 2.1f));
                                Put(_rng.NextDouble() < 0.5 ? kit.FernA : kit.FernB, root, p - Vector3.right * side * Range(0.6f, 1f) + Vector3.forward * Range(-0.6f, 0.6f), Range(1.3f, 1.8f));
                                break;
                        }
                    }
                    // корни из-под стены — реже, «тут наверху растут большие деревья»
                    if (z >= nextRoots)
                    {
                        nextRoots = z + Range(7f, 11f);
                        var rp = Floor(z, side * Range(0.93f, 1f));
                        if (!Reserved(Floor(z, side * 0.85f), side * 0.85f) && !NearVignette(rp))
                            Put(_rng.NextDouble() < 0.5 && kit.RootsBig != null ? kit.RootsBig : kit.RootsSmall, root, rp,
                                Range(1.1f, 1.6f), (side > 0 ? 270f : 90f) + Range(-40f, 40f), shadows: true,
                                recolor: ChapterAt(z) == Chapter.Ashes ? kit.Charred : null);
                    }
                    z += step;
                }
            }
        }

        /// <summary>Камешки по краям тропы: редкие, в шахматном порядке — подчёркивают её линию.</summary>
        private static void BuildPathPebbles(Transform root, Kit kit)
        {
            int i = 0;
            for (float z = FallenStartZ; z < ExitZ - 4f; z += Range(4f, 6.5f), i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var p = Floor(z, side * Range(0.3f, 0.38f));
                if (Vector3.Distance(p, SkeletonPos) < 2.5f) continue;
                Put(kit.Pebbles, root, p, Range(1.1f, 1.5f));
                if (ChapterAt(z) != Chapter.Fallen && _rng.NextDouble() < 0.4)
                    Put(PickOf(kit.StickA, kit.StickB), root, p + new Vector3(Range(-0.5f, 0.5f), 0f, Range(0.3f, 0.8f)), Range(0.9f, 1.2f));
            }
        }

        private static bool NearVignette(Vector3 p)
        {
            foreach (var g in _glowSpots)
                if (Vector3.Distance(g, p) < 2.6f) return true;
            return false;
        }

        private static void AddCapsule(GameObject go, float radius, float height)
        {
            var cc = go.AddComponent<CapsuleCollider>();
            cc.center = new Vector3(0f, height * 0.5f, 0f);
            cc.radius = radius;
            cc.height = height;
        }

        private static void AddBox(GameObject go, Vector3 size)
        {
            var bc = go.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, size.y * 0.5f, 0f);
            bc.size = size;
        }

        // ------------------------------------------------------------------ свет

        /// <summary>Не ставим декор на тропу, в кратер, на скелета и перед выходом.</summary>
        private static bool Reserved(Vector3 p, float across)
        {
            if (Vector3.Distance(p, Floor(CraterZ)) < 7.5f) return true;
            if (Mathf.Abs(across) < 0.28f) return true;
            if (p.z > ExitZ - 6f && Mathf.Abs(across) < 0.6f) return true;
            if (Vector3.Distance(p, SkeletonPos) < 3.2f) return true;
            return false;
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

        // ------------------------------------------------------------------ живность

        /// <summary>Где светятся грибы/кристаллы/колокольчики — вокруг них кружат мотыльки, рядом не ставим подлесок.</summary>
        private static readonly System.Collections.Generic.List<Vector3> _glowSpots = new();

        /// <summary>Птички на опушке у тропы (вспархивают при приближении), мотыльки у света, жуки у якорей.</summary>
        private static void BuildCritters(Transform gameplay, Kit kit)
        {
            var root = new GameObject("Critters").transform;
            root.SetParent(gameplay);

            void Spawn(GameObject model, Vector3 p, Critter.Kind kind, float scale, float radius)
            {
                if (model == null) return;
                var go = ForestDetails.Place(model, root, p, Range(0f, 360f), scale);
                go.name = kind.ToString();
                var c = go.AddComponent<Critter>();
                c.Type = kind;
                c.Radius = radius;
            }

            for (int i = 0; i < 4; i++)
                Spawn(kit.Bird, Floor(EdgeStartZ + 3f + i * 6f + Range(-1f, 1f), (i % 2 == 0 ? 1f : -1f) * Range(0.25f, 0.45f)), Critter.Kind.Bird, 1.6f, 1.2f);
            foreach (var spot in _glowSpots)
                if (spot.z > FallenStartZ && _rng.NextDouble() < 0.8)
                    Spawn(kit.Moth, spot, Critter.Kind.Moth, 1.8f, Range(1.2f, 2f));
            for (int i = 0; i < 6; i++)
                Spawn(kit.Beetle, Floor(Range(GroveStartZ, ExitZ - 8f), (i % 2 == 0 ? 1f : -1f) * Range(0.5f, 0.8f)), Critter.Kind.Beetle, 2.2f, 1.2f);
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
