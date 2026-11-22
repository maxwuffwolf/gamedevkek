using System.IO;
using Game.Core;
using Game.Gameplay.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    /// <summary>
    /// Генерирует сцену-шоукейс из примитивов: лагерь с костром, квестовик, волчьи логова, лес, камни вожака,
    /// на юге — спуск обратно в овраг (зона "forest" в zones.json, сцена Showcase).
    /// Меню: Game → Пересобрать сцену Showcase. Сцену потом можно спокойно править руками.
    /// </summary>
    public static class ShowcaseSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Showcase.unity";
        private const string GeneratedDir = "Assets/_Project/Art/Generated";

        private static System.Random _rng;

        [MenuItem("Game/Пересобрать сцену Showcase", priority = 1)]
        private static void RebuildMenu()
        {
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Пересобрать сцену?",
                    "Сцена Showcase будет создана заново, ручные правки в ней пропадут.", "Пересобрать", "Отмена"))
                return;
            Build();
        }

        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            _rng = new System.Random(12345);
            AtmosphereBuilder.BeginBuild();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Directory.CreateDirectory(GeneratedDir + "/Materials");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var env = new GameObject("Environment").transform;
            var gameplay = new GameObject("Gameplay").transform;

            HeroBuilder.ApplyReadability();
            BuildLighting(env);
            BuildCamera();
            BuildGround(env);
            BuildCamp(env);
            BuildForest(env);
            BuildAlphaStones(env, new Vector3(0f, 0f, 31f));
            BuildBounds(env);
            AtmosphereBuilder.BuildEffects(env, _rng);

            // --- геймплейные маркеры (их и стоит двигать в инспекторе)
            var spawn = new GameObject("PlayerSpawn", typeof(PlayerSpawnPoint)).transform;
            spawn.SetParent(gameplay);
            spawn.position = new Vector3(0f, 0f, -3f);

            var npc = new GameObject("NPC_Elder", typeof(NpcSpawnPoint));
            npc.transform.SetParent(gameplay);
            npc.transform.position = new Vector3(3.2f, 0f, 1.5f);
            npc.transform.rotation = Quaternion.LookRotation(new Vector3(-3.2f, 0f, -4.5f));
            npc.GetComponent<NpcSpawnPoint>().NpcId = "npc_elder";

            AddSpawner(gameplay, "Spawner_Wolves_West", "wolf", new Vector3(-15f, 0f, 14f), 3, 5f, new Vector2Int(1, 2), 15f);
            AddSpawner(gameplay, "Spawner_Wolves_East", "wolf", new Vector3(14f, 0f, 16f), 3, 5f, new Vector2Int(1, 2), 15f);
            AddSpawner(gameplay, "Spawner_Wolf_Alpha", "wolf_alpha", new Vector3(0f, 0f, 31f), 1, 1.5f, new Vector2Int(3, 3), 60f);

            // --- переход обратно в овраг (юг). Из оврага сюда ведёт портал с TargetEntrance = "from_ravine"
            ZoneGateBuilder.Build(gameplay, RavineGate, 0f, targetZone: "ravine", targetEntrance: "from_forest",
                localEntranceId: "from_ravine", style: ZoneGateBuilder.Style.Descent);

            AtmosphereBuilder.EndBuild();
            EditorSceneManager.SaveScene(scene, ScenePath);
            ZoneScenes.SyncBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Game] Сцена собрана: {ScenePath}. Жми Play!");
        }

        /// <summary>Где стоит арка спуска в овраг (южный край леса).</summary>
        private static readonly Vector3 RavineGate = new(0f, 0f, -37f);

        // ------------------------------------------------------------------ окружение

        private static void BuildLighting(Transform parent) => AtmosphereBuilder.BuildLighting(parent);

        internal static void BuildCamera()
        {
            var cam = new GameObject("Main Camera") { tag = "MainCamera" };
            cam.transform.position = new Vector3(0f, 14f, -13f);
            cam.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            var c = cam.AddComponent<Camera>();
            c.fieldOfView = 40f;
            c.farClipPlane = 200f;
            cam.AddComponent<AudioListener>();
            AtmosphereBuilder.SetupCamera(c);
        }

        private static void BuildGround(Transform parent)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(parent);
            ground.transform.localScale = new Vector3(10f, 1f, 10f);
            ground.GetComponent<Renderer>().sharedMaterial = Mat("Grass", new Color(0.37f, 0.55f, 0.27f));

            // тропа на север, к волкам и камням
            for (int i = 0; i < 9; i++)
            {
                var p = Prop(PrimitiveType.Cylinder, parent, "Path", new Vector3(Mathf.Sin(i * 0.7f) * 1.5f, 0.005f, 6f + i * 3f),
                    new Vector3(2.6f, 0.01f, 2.6f), Mat("Dirt", new Color(0.55f, 0.43f, 0.3f)), keepCollider: false);
                p.transform.rotation = Quaternion.Euler(0f, i * 23f, 0f);
            }
        }

        private static void BuildCamp(Transform parent)
        {
            var camp = new GameObject("Camp").transform;
            camp.SetParent(parent);
            Prop(PrimitiveType.Cylinder, camp, "CampGround", new Vector3(0f, 0.008f, 0f), new Vector3(13f, 0.01f, 13f),
                Mat("Dirt", new Color(0.55f, 0.43f, 0.3f)), keepCollider: false);

            // костёр
            var fire = new GameObject("Campfire").transform;
            fire.SetParent(camp);
            fire.position = new Vector3(0f, 0f, 1.5f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                var s = Prop(PrimitiveType.Cube, fire, "Stone", fire.position + new Vector3(Mathf.Cos(a), 0.12f, Mathf.Sin(a)) * 0.75f,
                    new Vector3(0.3f, 0.25f, 0.3f), Mat("Stone", new Color(0.5f, 0.5f, 0.52f)), keepCollider: false);
                s.transform.rotation = Quaternion.Euler(0f, i * 37f, 0f);
            }
            Prop(PrimitiveType.Cube, fire, "Log", fire.position + new Vector3(0f, 0.15f, 0f), new Vector3(0.9f, 0.15f, 0.15f), Mat("Wood", new Color(0.4f, 0.27f, 0.15f)), false)
                .transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            Prop(PrimitiveType.Cube, fire, "Log", fire.position + new Vector3(0f, 0.15f, 0f), new Vector3(0.9f, 0.15f, 0.15f), Mat("Wood", new Color(0.4f, 0.27f, 0.15f)), false)
                .transform.rotation = Quaternion.Euler(0f, -40f, 0f);
            var flame = Prop(PrimitiveType.Cube, fire, "Flame", fire.position + new Vector3(0f, 0.45f, 0f), new Vector3(0.35f, 0.5f, 0.35f), UnlitMat("Fire", new Color(2.6f, 1.2f, 0.25f)), false);
            flame.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            var lightGo = new GameObject("FireLight");
            lightGo.transform.SetParent(fire);
            lightGo.transform.position = fire.position + Vector3.up * 1.2f;
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.6f, 0.25f);
            l.range = 14f;
            l.intensity = 6f;
            l.shadows = LightShadows.Soft; // в сумерках костёр — главный тёплый свет, пусть отбрасывает тени
            l.shadowStrength = 0.7f;
            l.shadowResolution = LightShadowResolution.Low; // точечный свет = 6 теневых карт, на средних они не влезают в атлас
            var flicker = lightGo.AddComponent<LightFlicker>();
            flicker.BaseIntensity = 6f;
            flicker.Amount = 0.8f;
            AtmosphereBuilder.BuildEmbers(fire, fire.position + Vector3.up * 0.5f);

            // палатки (куб, повёрнутый на 45° и наполовину в земле = треугольная крыша)
            Tent(camp, new Vector3(-4.5f, 0f, 2.5f), 20f, new Color(0.7f, 0.6f, 0.42f));
            Tent(camp, new Vector3(-3.5f, 0f, -2.5f), -15f, new Color(0.55f, 0.35f, 0.3f));
            Tent(camp, new Vector3(5f, 0f, -2f), 70f, new Color(0.45f, 0.5f, 0.6f));

            // ящики и бочки
            Prop(PrimitiveType.Cube, camp, "Crate", new Vector3(4.2f, 0.35f, 3.3f), Vector3.one * 0.7f, Mat("Wood", new Color(0.4f, 0.27f, 0.15f)), true)
                .transform.rotation = Quaternion.Euler(0f, 15f, 0f);
            Prop(PrimitiveType.Cube, camp, "Crate", new Vector3(4.9f, 0.3f, 2.7f), Vector3.one * 0.6f, Mat("Wood", new Color(0.4f, 0.27f, 0.15f)), true)
                .transform.rotation = Quaternion.Euler(0f, -20f, 0f);
            Prop(PrimitiveType.Cylinder, camp, "Barrel", new Vector3(2f, 0.45f, 4.5f), new Vector3(0.6f, 0.45f, 0.6f), Mat("Barrel", new Color(0.5f, 0.33f, 0.18f)), true);

            // частокол по кругу, с проходом на север
            for (int i = 0; i < 28; i++)
            {
                float a = i * Mathf.PI * 2f / 28f;
                var pos = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 9f;
                if (Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 0f)) < 25f) continue; // ворота на север
                Prop(PrimitiveType.Cylinder, camp, "Post", pos + Vector3.up * 0.6f, new Vector3(0.22f, 0.6f, 0.22f), Mat("Wood", new Color(0.4f, 0.27f, 0.15f)), true);
            }
        }

        private static void Tent(Transform parent, Vector3 pos, float yaw, Color color)
        {
            var root = new GameObject("Tent").transform;
            root.SetParent(parent);
            root.position = pos;
            root.rotation = Quaternion.Euler(0f, yaw, 0f);
            var roof = Prop(PrimitiveType.Cube, root, "Roof", pos, new Vector3(1.9f, 1.9f, 2.8f), Mat("Tent_" + ColorUtility.ToHtmlStringRGB(color), color), true);
            roof.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        private static void BuildForest(Transform parent)
        {
            var forest = new GameObject("Forest").transform;
            forest.SetParent(parent);

            // свои модели (Game → Окружение → Собрать префабы из моделей); если их нет — плейсхолдеры
            EnvironmentPrefabs.BuildAll(); // освежить префабы и материалы (toon-шейдер) перед расстановкой
            var treePrefabs = EnvironmentPrefabs.Load("Tree");
            var rockPrefabs = EnvironmentPrefabs.Load("Rock");
            var bushPrefabs = EnvironmentPrefabs.Load("Bush");
            var grassPrefabs = EnvironmentPrefabs.Load("Grass");

            var coneMesh = ConeMesh();
            var trunkMat = Mat("Trunk", new Color(0.36f, 0.25f, 0.16f));
            var leafMats = new[]
            {
                Mat("Leaves_A", new Color(0.2f, 0.42f, 0.22f)),
                Mat("Leaves_B", new Color(0.26f, 0.5f, 0.24f)),
                Mat("Leaves_C", new Color(0.18f, 0.36f, 0.26f)),
            };
            var rockMat = Mat("Rock", new Color(0.48f, 0.48f, 0.5f));

            int placed = 0;
            for (int attempt = 0; attempt < 2000 && placed < 230; attempt++)
            {
                var p = new Vector3(Range(-46f, 46f), 0f, Range(-46f, 46f));
                bool edge = Mathf.Abs(p.x) > 36f || Mathf.Abs(p.z) > 36f;
                if (!edge)
                {
                    // реже внутри, и не на лагере/тропе/логовах
                    if (_rng.NextDouble() > 0.25) continue;
                    if (IsReserved(p, 12f, 7f)) continue;
                }
                if (treePrefabs.Length > 0) PlacePrefab(forest, treePrefabs, p, 0.85f, 1.25f);
                else Tree(forest, p, coneMesh, trunkMat, leafMats[_rng.Next(leafMats.Length)]);
                placed++;
            }

            for (int i = 0; i < 40; i++)
            {
                var p = new Vector3(Range(-40f, 40f), 0f, Range(-40f, 40f));
                if (p.magnitude < 11f || (Mathf.Abs(p.x) < 4f && p.z > 0f) || IsReserved(p, 0f, 0f)) continue;
                if (rockPrefabs.Length > 0)
                {
                    PlacePrefab(forest, rockPrefabs, p, 0.7f, 1.3f);
                    continue;
                }
                float s = Range(0.5f, 1.6f);
                var rock = Prop(PrimitiveType.Cube, forest, "Rock", p + Vector3.up * s * 0.25f, new Vector3(s, s * 0.7f, s * 1.2f), rockMat, true);
                rock.transform.rotation = Quaternion.Euler(Range(-15f, 15f), Range(0f, 360f), Range(-15f, 15f));
            }

            // кусты и трава — только если есть свои модели (у плейсхолдеров их нет)
            for (int i = 0; bushPrefabs.Length > 0 && i < 90; i++)
            {
                var p = new Vector3(Range(-40f, 40f), 0f, Range(-40f, 40f));
                if (IsReserved(p, 9.5f, 4f)) continue;
                PlacePrefab(forest, bushPrefabs, p, 0.8f, 1.3f);
            }
            for (int i = 0; grassPrefabs.Length > 0 && i < 260; i++)
            {
                var p = new Vector3(Range(-40f, 40f), 0f, Range(-40f, 40f));
                if (p.magnitude < 7f) continue;
                PlacePrefab(forest, grassPrefabs, p, 0.8f, 1.4f);
            }
        }

        /// <summary>Лагерь, тропа на север, логова волков и камни вожака — там не сажаем лес.</summary>
        private static bool IsReserved(Vector3 p, float campRadius, float denRadius)
        {
            if (new Vector2(p.x, p.z).magnitude < campRadius) return true;
            if (Mathf.Abs(p.x) < 4f && p.z > 0f && p.z < 36f) return true;
            if (Vector3.Distance(p, new Vector3(-15f, 0f, 14f)) < denRadius) return true;
            if (Vector3.Distance(p, new Vector3(14f, 0f, 16f)) < denRadius) return true;
            if (Vector3.Distance(p, new Vector3(0f, 0f, 31f)) < denRadius + 1f) return true;
            if (Mathf.Abs(p.x - RavineGate.x) < 6f && p.z < RavineGate.z + 9f) return true; // проход к оврагу
            return false;
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

        private static void Tree(Transform parent, Vector3 pos, Mesh cone, Material trunkMat, Material leafMat)
        {
            float s = Range(0.8f, 1.4f);
            var root = new GameObject("Tree").transform;
            root.SetParent(parent);
            root.position = pos;
            root.rotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
            root.localScale = Vector3.one * s;
            var trunk = Prop(PrimitiveType.Cylinder, root, "Trunk", pos + Vector3.up * 0.9f * s, new Vector3(0.35f, 0.9f, 0.35f), trunkMat, true);
            trunk.transform.localScale = new Vector3(0.35f, 0.9f, 0.35f);
            for (int i = 0; i < 3; i++)
            {
                var leaf = new GameObject("Leaves");
                leaf.transform.SetParent(root, false);
                leaf.transform.localPosition = new Vector3(0f, 1.4f + i * 0.85f, 0f);
                leaf.transform.localScale = new Vector3(2.2f - i * 0.55f, 1.6f, 2.2f - i * 0.55f);
                leaf.transform.localRotation = Quaternion.Euler(0f, i * 20f, 0f);
                leaf.AddComponent<MeshFilter>().sharedMesh = cone;
                leaf.AddComponent<MeshRenderer>().sharedMaterial = leafMat;
            }
        }

        private static void BuildAlphaStones(Transform parent, Vector3 center)
        {
            var root = new GameObject("AlphaStones").transform;
            root.SetParent(parent);
            var mat = Mat("OldStone", new Color(0.42f, 0.43f, 0.47f));
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2f / 7f + 0.3f;
                var p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 6f;
                if (Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 270f)) < 30f) continue; // проход с юга
                float h = Range(1.8f, 3f);
                var st = Prop(PrimitiveType.Cube, root, "Monolith", p + Vector3.up * h * 0.5f, new Vector3(0.9f, h, 0.6f), mat, true);
                st.transform.rotation = Quaternion.Euler(Range(-6f, 6f), a * Mathf.Rad2Deg + 90f, Range(-6f, 6f));
            }
            Prop(PrimitiveType.Cylinder, root, "Ground", center + Vector3.up * 0.006f, new Vector3(10f, 0.01f, 10f), Mat("DarkDirt", new Color(0.35f, 0.3f, 0.25f)), false);
        }

        private static void BuildBounds(Transform parent)
        {
            var bounds = new GameObject("Bounds").transform;
            bounds.SetParent(parent);
            void Wall(Vector3 pos, Vector3 size)
            {
                var w = new GameObject("Wall");
                w.transform.SetParent(bounds);
                w.transform.position = pos;
                w.AddComponent<BoxCollider>().size = size;
            }
            Wall(new Vector3(0f, 2f, 42f), new Vector3(90f, 4f, 1f));
            Wall(new Vector3(0f, 2f, -42f), new Vector3(90f, 4f, 1f));
            Wall(new Vector3(42f, 2f, 0f), new Vector3(1f, 4f, 90f));
            Wall(new Vector3(-42f, 2f, 0f), new Vector3(1f, 4f, 90f));
        }

        private static void AddSpawner(Transform parent, string name, string monsterId, Vector3 pos, int count, float radius, Vector2Int levels, float respawn)
        {
            var go = new GameObject(name, typeof(MonsterSpawner));
            go.transform.SetParent(parent);
            go.transform.position = pos;
            var s = go.GetComponent<MonsterSpawner>();
            s.MonsterId = monsterId;
            s.Count = count;
            s.Radius = radius;
            s.LevelRange = levels;
            s.RespawnSeconds = respawn;
        }

        // ------------------------------------------------------------------ утилиты

        private static float Range(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        internal static GameObject Prop(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>Материал плейсхолдера: toon-шейдер (как у моделей), если он есть, иначе URP Lit.</summary>
        internal static Material Mat(string name, Color color)
        {
            string path = $"{GeneratedDir}/Materials/{name}.mat";
            var toon = Shader.Find(EnvironmentPrefabs.ToonShaderName);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = MaterialFactory.CreateLit(color);
                AssetDatabase.CreateAsset(m, path);
            }
            if (toon != null && (m.shader != toon || AtmosphereBuilder.RefreshAssets))
            {
                m.shader = toon;
                m.SetFloat("_UseVertexColors", 0f);
                m.DisableKeyword("_VERTEX_COLORS");
                m.SetColor("_ShadowColor", new Color(0.45f, 0.5f, 0.8f));
                m.SetFloat("_AmbientStrength", 0.85f);
                m.SetColor("_RimColor", ForestDetails.MoonRim);
                m.SetFloat("_RimStrength", 0.3f);
                m.SetFloat("_GradientHeight", 1.5f);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            else m.color = color;
            EditorUtility.SetDirty(m);
            return m;
        }

        internal static Material UnlitMat(string name, Color color)
        {
            string path = $"{GeneratedDir}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = MaterialFactory.CreateUnlit(color);
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = color; // HDR-цвет (>1) светится через Bloom
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Восьмигранный конус с плоским шейдингом (лоуполи-ёлка).</summary>
        internal static Mesh ConeMesh()
        {
            string path = $"{GeneratedDir}/Cone8.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            const int sides = 8;
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            var top = new Vector3(0f, 1f, 0f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                var p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f);
                var p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f);
                int b = verts.Count;
                verts.Add(p0); verts.Add(top); verts.Add(p1); // бок
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                b = verts.Count;
                verts.Add(p0); verts.Add(p1); verts.Add(Vector3.zero); // дно
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
            }
            var mesh = new Mesh { name = "Cone8" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
