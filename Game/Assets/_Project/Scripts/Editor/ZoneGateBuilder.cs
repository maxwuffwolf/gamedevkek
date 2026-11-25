using Game.Core;
using Game.Gameplay.World;
using Game.Gameplay.Zones;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Собирает на сцене «врата» между зонами: каменную арку со светящейся пеленой + ZonePortal + ZoneEntrance.
    /// Портал смотрит «лицом» (ось Z) туда, где стоит точка прибытия: игрок, пришедший из другой зоны,
    /// появляется в 3.5 м перед аркой и смотрит от неё.
    /// Позже арку можно заменить своей моделью из Blender: ZonePortal и ZoneEntrance просто остаются на объекте.
    /// </summary>
    public static class ZoneGateBuilder
    {
        public enum Style
        {
            /// <summary>Холодная пелена: выход из оврага наверх, в лес.</summary>
            Ascent,
            /// <summary>Тёплая, фиолетово-золотая: спуск в овраг, где упала «звезда».</summary>
            Descent,
            /// <summary>
            /// Как в PoE: проход в зарослях — тёмный просвет между деревьями под упавшим стволом, часть пейзажа.
            /// Светится и подписывается только при наведении курсора.
            /// </summary>
            Thicket,
        }

        public static GameObject Build(Transform parent, Vector3 position, float yaw, string targetZone, string targetEntrance,
            string localEntranceId, Style style, float wallHalfWidth = 7f)
        {
            if (style == Style.Thicket)
                return BuildThicket(parent, position, yaw, targetZone, targetEntrance, localEntranceId, wallHalfWidth);

            Color glow = style == Style.Ascent ? new Color(0.45f, 0.75f, 1.6f) : new Color(1.4f, 0.75f, 1.9f);
            Color core = style == Style.Ascent ? new Color(1.2f, 1.6f, 2.2f) : new Color(2.2f, 1.6f, 0.9f);
            Color stone = style == Style.Ascent ? new Color(0.42f, 0.42f, 0.47f) : new Color(0.36f, 0.33f, 0.38f);
            string suffix = style.ToString();

            var root = new GameObject($"Portal_to_{targetZone}");
            root.transform.SetParent(parent);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            var stoneMat = ShowcaseSceneBuilder.Mat("GateStone_" + suffix, stone);
            Part(PrimitiveType.Cube, root.transform, "Pillar_L", new Vector3(-1.95f, 2.1f, 0f), new Vector3(0.9f, 4.2f, 0.9f), stoneMat, true, new Vector3(0f, 8f, -3f));
            Part(PrimitiveType.Cube, root.transform, "Pillar_R", new Vector3(1.95f, 2.1f, 0f), new Vector3(0.9f, 4.2f, 0.9f), stoneMat, true, new Vector3(0f, -6f, 3f));
            Part(PrimitiveType.Cube, root.transform, "Lintel", new Vector3(0f, 4.45f, 0f), new Vector3(5.2f, 0.8f, 1.1f), stoneMat, true, new Vector3(0f, 0f, 2f));
            Part(PrimitiveType.Cube, root.transform, "Keystone", new Vector3(0f, 5.0f, 0f), new Vector3(0.7f, 0.7f, 0.7f),
                ShowcaseSceneBuilder.UnlitMat("GateRune_" + suffix, core * 0.9f), false, new Vector3(0f, 0f, 45f));
            Part(PrimitiveType.Cylinder, root.transform, "Base", new Vector3(0f, 0.02f, 0.4f), new Vector3(6.4f, 0.02f, 4f),
                ShowcaseSceneBuilder.Mat("GateBase_" + suffix, stone * 0.7f), false, Vector3.zero);

            // пелена: толстая «плёнка» видна с обеих сторон (камера смотрит с юга, арка может стоять к ней спиной)
            Part(PrimitiveType.Cube, root.transform, "Veil", new Vector3(0f, 1.95f, 0f), new Vector3(3.0f, 3.7f, 0.06f),
                ShowcaseSceneBuilder.UnlitMat("GateVeil_" + suffix, glow), false, Vector3.zero);
            Part(PrimitiveType.Cube, root.transform, "VeilCore", new Vector3(0f, 1.8f, 0f), new Vector3(1.7f, 2.6f, 0.1f),
                ShowcaseSceneBuilder.UnlitMat("GateCore_" + suffix, core), false, Vector3.zero);

            var lightGo = new GameObject("GateLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 2.2f, 1.2f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(glow.r, glow.g, glow.b) / Mathf.Max(glow.r, glow.g, glow.b);
            light.range = 10f;
            light.intensity = 3.5f;
            light.shadows = LightShadows.None;
            var flicker = lightGo.AddComponent<LightFlicker>();
            flicker.BaseIntensity = 3.5f;
            flicker.Amount = 0.5f;
            flicker.Speed = 2f;

            // клик-зона: триггер (физику не блокирует, но луч мыши его ловит)
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 2.1f, 0f);
            box.size = new Vector3(3.6f, 4.2f, 1.6f);

            var portal = root.AddComponent<ZonePortal>();
            portal.TargetZone = targetZone;
            portal.TargetEntrance = targetEntrance;
            portal.ActionLabel = "Перейти";
            portal.Range = 3.2f;
            portal.LabelHeight = 5.9f;

            if (!string.IsNullOrEmpty(localEntranceId))
            {
                var entrance = new GameObject($"Entrance_{localEntranceId}", typeof(ZoneEntrance));
                entrance.transform.SetParent(root.transform, false);
                entrance.transform.localPosition = new Vector3(0f, 0f, 3.5f);
                entrance.transform.localRotation = Quaternion.identity;
                entrance.GetComponent<ZoneEntrance>().Id = localEntranceId;
            }
            return root;
        }

        // ------------------------------------------------------------------ проход в зарослях (PoE)

        private const float GapHalf = 1.6f;   // полуширина прохода
        private const float GapHeight = 3.3f;

        /// <summary>
        /// Локальные оси: +Z — к игроку (сторона прибытия), проход уходит в −Z. Заросли перекрывают всё
        /// ущелье/опушку шириной 2×<paramref name="halfWidth"/>, в середине — просвет под упавшим стволом.
        /// </summary>
        private static GameObject BuildThicket(Transform parent, Vector3 position, float yaw, string targetZone, string targetEntrance,
            string localEntranceId, float halfWidth)
        {
            var rng = new System.Random(targetZone.GetHashCode() & 0x7fffffff);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            var root = new GameObject($"Passage_to_{targetZone}");
            root.transform.SetParent(parent);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var t = root.transform;
            Vector3 W(float x, float y, float z) => t.TransformPoint(new Vector3(x, y, z));

            var thicket = new GameObject("Thicket").transform;
            thicket.SetParent(t, false);

            // --- деревья: стена в два ряда, у прохода — два крупных, наклонены друг к другу
            var trees = EnvironmentPrefabs.Load("Tree");
            void Tree(float x, float z, float scale, float lean)
            {
                if (trees.Length == 0) return;
                var prefab = trees[rng.Next(trees.Length)];
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, thicket);
                go.transform.position = W(x, 0f, z);
                go.transform.rotation = t.rotation * Quaternion.Euler(0f, 0f, lean) * Quaternion.Euler(0f, R(0f, 360f), 0f);
                go.transform.localScale = Vector3.one * scale;
            }
            // деревья у самого прохода пониже и наклонены наружу: камера смотрит сверху, и крупные кроны
            // закрывали бы просвет
            Tree(-GapHalf - 0.9f, -1.7f, 1.35f, -12f);
            Tree(GapHalf + 0.9f, -1.5f, 1.3f, 12f);
            for (float x = -halfWidth; x <= halfWidth; x += R(1.3f, 1.8f))
            {
                if (Mathf.Abs(x) > GapHalf + 2.2f) Tree(x + R(-0.3f, 0.3f), R(-2.6f, -1.9f), R(1.1f, 1.6f), R(-5f, 5f));
                if (Mathf.Abs(x) > GapHalf + 0.6f) Tree(x + R(-0.4f, 0.4f), R(-4.6f, -3.6f), R(1.2f, 1.7f), R(-4f, 4f));
            }

            // --- упавший мшистый ствол поперёк прохода — естественная «арка»
            var log = ForestDetails.Load("Det_Log_Mossy");
            if (log != null)
            {
                var go = ForestDetails.Place(log, thicket, W(0f, GapHeight - 0.15f, -1.45f), 0f, 1.55f, castShadows: true);
                go.transform.rotation = t.rotation * Quaternion.Euler(R(-4f, 4f), 0f, 7f);
            }

            // --- корни у деревьев прохода, кусты/папоротники/трава: заросли сливаются с землёй
            var roots = ForestDetails.Load("Det_Roots_Big");
            if (roots != null)
            {
                ForestDetails.Place(roots, thicket, W(-GapHalf - 0.7f, 0f, -1.7f), yaw + 120f, 1.3f, castShadows: true);
                ForestDetails.Place(roots, thicket, W(GapHalf + 0.7f, 0f, -1.5f), yaw - 60f, 1.2f, castShadows: true);
            }
            var bushes = ForestDetails.LoadAll("Det_Bush_Round", "Det_Bush_Blue", "Det_Bush_Flower", "Det_Berry_Blue");
            var ferns = ForestDetails.LoadAll("Det_Fern_A", "Det_Fern_B");
            var grass = ForestDetails.LoadAll("Det_Grass_Tall", "Det_Grass_Blue");
            void Scatter(GameObject[] set, int n, float zMin, float zMax, float minGap, float sMin, float sMax)
            {
                for (int i = 0; set.Length > 0 && i < n; i++)
                {
                    float x = R(-halfWidth, halfWidth);
                    if (Mathf.Abs(x) < minGap) x = Mathf.Sign(x == 0f ? 1f : x) * (minGap + R(0f, 0.6f));
                    ForestDetails.Place(set[rng.Next(set.Length)], thicket, W(x, 0f, R(zMin, zMax)), yaw + R(0f, 360f), R(sMin, sMax));
                }
            }
            Scatter(bushes, 16, -1.4f, 0.4f, GapHalf + 0.2f, 1.6f, 2.4f);
            Scatter(ferns, 12, -1.0f, 0.9f, GapHalf - 0.3f, 1.5f, 2.2f);
            Scatter(grass, 26, -0.8f, 1.6f, 0.9f, 1.0f, 1.6f);

            // --- «глубина» прохода: тёмная пустота, при наведении заливается светом (PassageHighlight)
            var mask = new GameObject("PassageDepth");
            mask.transform.SetParent(t, false);
            mask.transform.localPosition = new Vector3(0f, 0f, -1.5f);
            mask.AddComponent<MeshFilter>().sharedMesh = OpeningMesh();
            var mr = mask.AddComponent<MeshRenderer>();
            mr.sharedMaterial = PassageMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            // «тропа»: плоское пятно на земле — тёмная протоптанная дорожка уходит в чащу; при наведении
            // устье прохода светится и на земле (сверху это видно лучше, чем вертикальный просвет)
            var trail = new GameObject("PassageTrail");
            trail.transform.SetParent(t, false);
            trail.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            trail.AddComponent<MeshFilter>().sharedMesh = TrailMesh();
            var tr = trail.AddComponent<MeshRenderer>();
            tr.sharedMaterial = TrailMaterial();
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;

            // камешки и листва по краям тропы
            var edging = ForestDetails.LoadAll("Det_Pebbles", "Det_Leaves", "Det_Stick_A");
            for (int i = 0; edging.Length > 0 && i < 8; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                ForestDetails.Place(edging[rng.Next(edging.Length)], thicket,
                    W(side * (GapHalf + R(-0.1f, 0.35f)), 0f, R(-0.6f, 2.4f)), yaw + R(0f, 360f), R(0.9f, 1.3f));
            }

            var lightGo = new GameObject("PassageLight");
            lightGo.transform.SetParent(t, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.3f, 0.3f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.72f, 0.82f, 1f);
            light.range = 8f;
            light.intensity = 0.5f;
            light.shadows = LightShadows.None;

            // Сам портал — отдельный дочерний объект: иначе любой коллайдер зарослей (стена, деревья) считался бы
            // частью портала (PlayerController ищет IInteractable в родителях) и проход светился бы от наведения
            // на любое дерево.
            var zone = new GameObject("PassageZone");
            zone.transform.SetParent(t, false);

            var hl = zone.AddComponent<PassageHighlight>();
            hl.GlowRenderers = new Renderer[] { mr, tr };
            hl.LightHover = 6f;
            hl.GlowLight = light;

            // --- стена: дальше прохода не пройти (клик по проходу — переход)
            var blocker = new GameObject("Blocker");
            blocker.transform.SetParent(t, false);
            var bc = blocker.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 2f, -2.7f);
            bc.size = new Vector3(halfWidth * 2f + 2f, 4f, 1.4f);

            // клик-зона прохода (триггер ловит луч мыши, физику не блокирует)
            var box = zone.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 1.7f, -1.1f);
            box.size = new Vector3(GapHalf * 2f + 0.4f, 3.5f, 1.8f);

            var portal = zone.AddComponent<ZonePortal>();
            portal.TargetZone = targetZone;
            portal.TargetEntrance = targetEntrance;
            portal.ActionLabel = "Перейти";
            portal.Range = 3.6f;
            portal.LabelHeight = GapHeight + 1.3f;
            portal.LabelOnHoverOnly = true;
            portal.ShowLabel = false; // название и «ЛКМ — Перейти» и так показывает подсказка при наведении

            if (!string.IsNullOrEmpty(localEntranceId))
            {
                var entrance = new GameObject($"Entrance_{localEntranceId}", typeof(ZoneEntrance));
                entrance.transform.SetParent(t, false);
                entrance.transform.localPosition = new Vector3(0f, 0f, 3.2f);
                entrance.transform.localRotation = Quaternion.identity;
                entrance.GetComponent<ZoneEntrance>().Id = localEntranceId;
            }
            return root;
        }

        /// <summary>Плоская «тропа» от прихожей стороны (+Z) в глубь зарослей (−Z). UV: u поперёк, v от глубины (0) к игроку (1).</summary>
        private static Mesh TrailMesh()
        {
            const string path = "Assets/_Project/Art/Generated/PassageTrail.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = "PassageTrail" };
                AssetDatabase.CreateAsset(mesh, path);
            }
            float w = GapHalf + 0.6f;
            const float zNear = 2.8f, zFar = -4.6f;
            mesh.Clear();
            mesh.SetVertices(new[] { new Vector3(-w, 0f, zFar), new Vector3(w, 0f, zFar), new Vector3(-w, 0f, zNear), new Vector3(w, 0f, zNear) });
            mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
            mesh.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
            mesh.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Material TrailMaterial()
        {
            const string path = "Assets/_Project/Art/Generated/Materials/PassageTrail.mat";
            var shader = Shader.Find("Game/Passage Glow");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader != null ? shader : MaterialFactory.UnlitShader) { name = "PassageTrail" };
                AssetDatabase.CreateAsset(mat, path);
            }
            if (shader != null) mat.shader = shader;
            // устье прохода (z ≈ −1.5) — v ≈ 0.42; пятно вытянуто вдоль тропы
            mat.SetVector("_MaskCenter", new Vector4(0.5f, 0.45f, 0f, 0f));
            mat.SetVector("_MaskScale", new Vector4(2.0f, 2.1f, 0f, 0f));
            mat.SetColor("_DarkColor", new Color(0.02f, 0.025f, 0.06f));
            mat.SetColor("_GlowColor", new Color(0.9f, 1.05f, 1.45f));
            mat.SetFloat("_IdleGlow", 0.05f);
            mat.SetFloat("_EdgeSoftness", 0.6f);
            mat.SetFloat("_Alpha", 0.6f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Неровный «просвет» прохода: низ по земле, бока и верх рваные (листва). UV: u поперёк, v снизу вверх.</summary>
        private static Mesh OpeningMesh()
        {
            const string path = "Assets/_Project/Art/Generated/PassageOpening.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = "PassageOpening" };
                AssetDatabase.CreateAsset(mesh, path);
            }
            const int n = 28;
            float w = GapHalf + 0.15f, h = GapHeight + 0.1f;
            var verts = new System.Collections.Generic.List<Vector3> { new(0f, h * 0.42f, 0f) };
            var rnd = new System.Random(5);
            // низ: прямой по земле
            for (int i = 0; i <= 6; i++) verts.Add(new Vector3(Mathf.Lerp(-w, w, i / 6f), -0.05f, 0f));
            // арка: справа налево, с «рваным» краем листвы
            for (int i = 1; i < n; i++)
            {
                float a = Mathf.PI * i / n;
                float j = 1f + ((float)rnd.NextDouble() - 0.5f) * 0.16f;
                float x = Mathf.Cos(a) * w * j;
                float y = Mathf.Lerp(0.5f, h, Mathf.Pow(Mathf.Sin(a), 0.55f)) * (i == 1 || i == n - 1 ? 0.7f : 1f) * j;
                verts.Add(new Vector3(x, y, 0f));
            }
            var uvs = new System.Collections.Generic.List<Vector2>();
            foreach (var v in verts) uvs.Add(new Vector2(v.x / (2f * w) + 0.5f, Mathf.Clamp01(v.y / h)));
            var tris = new System.Collections.Generic.List<int>();
            int ring = verts.Count - 1;
            for (int i = 0; i < ring; i++)
            {
                int a = 1 + i, b = 1 + (i + 1) % ring;
                tris.Add(0); tris.Add(b); tris.Add(a);
            }
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Material PassageMaterial()
        {
            const string path = "Assets/_Project/Art/Generated/Materials/PassageGlow.mat";
            var shader = Shader.Find("Game/Passage Glow");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader != null ? shader : MaterialFactory.UnlitShader) { name = "PassageGlow" };
                AssetDatabase.CreateAsset(m, path);
            }
            if (shader != null && m.shader != shader) m.shader = shader;
            return m;
        }

        private static GameObject Part(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat,
            bool keepCollider, Vector3 euler)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
    }
}
