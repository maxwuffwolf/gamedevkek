using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Editor
{
    /// <summary>
    /// «Вечные сумерки» для локации: низкое тёплое солнце, холодная подсветка, лавандовый туман,
    /// пост-обработка (Bloom, цветокоррекция, виньетка, лёгкое размытие дали),
    /// лучи света сквозь кроны, стелющаяся дымка, светлячки, искорки росы и искры костра.
    ///
    /// Всё, что стоит подкручивать руками, лежит ассетами в <see cref="Dir"/>:
    ///   Forest_Dusk_Volume.asset — пост-обработка; M_LightShaft / M_Mist / M_Firefly / M_Dew / M_Ember — материалы эффектов.
    /// Они создаются один раз и при пересборке сцены не перезаписываются. Удали ассет — он создастся заново с настройками по умолчанию.
    /// Свет и туман задаются константами ниже (они живут в сцене).
    /// </summary>
    public static class AtmosphereBuilder
    {
        public const string Dir = "Assets/_Project/Art/Atmosphere";
        public const string VolumePath = Dir + "/Forest_Dusk_Volume.asset";

        // --- палитра сумерек
        public static readonly Color SunColor = new Color(1f, 0.6f, 0.36f);
        public static readonly Color FillColor = new Color(0.46f, 0.55f, 0.95f);
        public static readonly Color FogColor = new Color(0.44f, 0.4f, 0.56f);
        public static readonly Color SkyAmbient = new Color(0.4f, 0.38f, 0.62f);
        public static readonly Color EquatorAmbient = new Color(0.44f, 0.34f, 0.42f);
        public static readonly Color GroundAmbient = new Color(0.13f, 0.11f, 0.17f);
        public const float FogDensity = 0.018f;

        /// <summary>Солнце висит низко на северо-востоке: камера смотрит на север, свет идёт почти навстречу (контровой).</summary>
        public static readonly Vector3 SunEuler = new Vector3(20f, 215f, 0f);

        // ------------------------------------------------------------------ свет и туман

        public static void BuildLighting(Transform parent)
        {
            var sunGo = new GameObject("Sun_Dusk");
            sunGo.transform.SetParent(parent);
            sunGo.transform.rotation = Quaternion.Euler(SunEuler);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = SunColor;
            sun.intensity = 1.7f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            RenderSettings.sun = sun;

            // холодная «луна» с противоположной стороны: теневые стороны не проваливаются в чёрное
            var fillGo = new GameObject("Fill_Moon");
            fillGo.transform.SetParent(parent);
            fillGo.transform.rotation = Quaternion.Euler(50f, SunEuler.y + 160f, 0f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = FillColor;
            fill.intensity = 0.22f;
            fill.shadows = LightShadows.None;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = SkyAmbient;
            RenderSettings.ambientEquatorColor = EquatorAmbient;
            RenderSettings.ambientGroundColor = GroundAmbient;
            RenderSettings.ambientIntensity = 1f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = FogDensity;
            DynamicGI.UpdateEnvironment();
        }

        /// <summary>Пост-обработка на камере + фон в цвет тумана.</summary>
        public static void SetupCamera(Camera cam)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = FogColor;
            cam.allowHDR = true;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.requiresDepthOption = CameraOverrideOption.On;
        }

        // ------------------------------------------------------------------ эффекты

        /// <summary>Volume, лучи, дымка, светлячки, роса. Вызывать после того, как лес расставлен.</summary>
        public static void BuildEffects(Transform parent, System.Random rng)
        {
            EnvironmentPrefabs.EnsureFolder(Dir);
            var root = new GameObject("Atmosphere").transform;
            root.SetParent(parent);

            var volumeGo = new GameObject("PostProcess_Volume");
            volumeGo.transform.SetParent(root);
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = GetOrCreateProfile();

            BuildLightShafts(root, rng);
            BuildGroundMist(root);
            BuildFireflies(root);
            BuildDew(root);
        }

        private static VolumeProfile GetOrCreateProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (profile != null) return profile;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumePath);

            var tone = Add<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.ACES);

            var bloom = Add<Bloom>(profile);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.75f);
            bloom.tint.Override(new Color(1f, 0.86f, 0.72f));
            bloom.highQualityFiltering.Override(true);

            var color = Add<ColorAdjustments>(profile);
            color.postExposure.Override(0.35f);
            color.contrast.Override(12f);
            color.saturation.Override(10f);
            color.colorFilter.Override(new Color(1f, 0.96f, 0.98f));

            var wb = Add<WhiteBalance>(profile);
            wb.temperature.Override(6f);
            wb.tint.Override(4f);

            // кино-раскраска: тени в сине-фиолетовый, света в персиковый
            var split = Add<SplitToning>(profile);
            split.shadows.Override(new Color(0.36f, 0.32f, 0.62f));
            split.highlights.Override(new Color(1f, 0.74f, 0.5f));
            split.balance.Override(-15f);

            var vignette = Add<Vignette>(profile);
            vignette.color.Override(new Color(0.07f, 0.03f, 0.11f));
            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.45f);

            // всё дальше ~24 м от камеры чуть плывёт — глубина кадра, герой при этом резкий
            var dof = Add<DepthOfField>(profile);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(24f);
            dof.gaussianEnd.Override(55f);
            dof.gaussianMaxRadius.Override(1.2f);
            dof.highQualitySampling.Override(true);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var c = profile.Add<T>(true);
            c.name = typeof(T).Name;
            c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(c, profile);
            return c;
        }

        // --- лучи света сквозь кроны

        private static void BuildLightShafts(Transform parent, System.Random rng)
        {
            var mat = GetOrCreateMaterial("M_LightShaft", "Game/Light Shaft", m =>
            {
                m.SetColor("_Color", new Color(1f, 0.7f, 0.44f));
                m.SetFloat("_Intensity", 0.3f);
            });
            if (mat == null) return;
            var mesh = ShaftMesh();

            var group = new GameObject("LightShafts").transform;
            group.SetParent(parent);

            // лучи наклонены туда, откуда светит солнце (круче настоящего, иначе легли бы почти горизонтально)
            var fwd = Quaternion.Euler(SunEuler) * Vector3.forward;
            var toSunFlat = new Vector3(-fwd.x, 0f, -fwd.z).normalized;
            const float elevation = 58f;
            var toSun = (toSunFlat * Mathf.Cos(elevation * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(elevation * Mathf.Deg2Rad)).normalized;

            for (int i = 0; i < 18; i++)
            {
                var p = new Vector3(R(rng, -32f, 32f), 0f, R(rng, -14f, 42f));
                var go = new GameObject("Shaft");
                go.transform.SetParent(group);
                go.transform.position = p + Vector3.down * 0.5f;
                float yawJitter = R(rng, -8f, 8f);
                go.transform.rotation = Quaternion.AngleAxis(yawJitter, Vector3.up) * Quaternion.FromToRotation(Vector3.up, toSun)
                                        * Quaternion.Euler(0f, R(rng, 0f, 90f), 0f);
                float w = R(rng, 1.4f, 3.2f);
                go.transform.localScale = new Vector3(w, R(rng, 11f, 16f), w);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        /// <summary>Две скрещенные вертикальные полоски 1×1, низ в нуле. Сохраняется ассетом, чтобы сцена на него ссылалась.</summary>
        private static Mesh ShaftMesh()
        {
            string path = Dir + "/SM_LightShaft.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = mesh == null;
            if (created) mesh = new Mesh { name = "SM_LightShaft" };
            mesh.Clear();

            var v = new Vector3[8];
            var n = new Vector3[8];
            var uv = new Vector2[8];
            for (int q = 0; q < 2; q++)
            {
                var side = q == 0 ? Vector3.right : Vector3.forward;
                var normal = q == 0 ? Vector3.forward : Vector3.left;
                int o = q * 4;
                v[o + 0] = -side * 0.5f; uv[o + 0] = new Vector2(0, 0);
                v[o + 1] = side * 0.5f; uv[o + 1] = new Vector2(1, 0);
                v[o + 2] = side * 0.5f + Vector3.up; uv[o + 2] = new Vector2(1, 1);
                v[o + 3] = -side * 0.5f + Vector3.up; uv[o + 3] = new Vector2(0, 1);
                for (int k = 0; k < 4; k++) n[o + k] = normal;
            }
            mesh.vertices = v;
            mesh.normals = n;
            mesh.uv = uv;
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2, 4, 6, 5, 4, 7, 6 };
            mesh.RecalculateBounds();

            if (created) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // --- частицы

        private static void BuildGroundMist(Transform parent)
        {
            var mat = GetOrCreateMaterial("M_Mist", "Game/Soft Particle", m =>
            {
                SetAlphaBlend(m);
                m.SetFloat("_Softness", 1.2f);
                m.SetFloat("_NoiseStrength", 0.6f);
                m.SetFloat("_SoftFade", 2f);
            });
            if (mat == null) return;

            var ps = NewParticles(parent, "GroundMist", new Vector3(0f, 0.7f, 8f), mat);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(14f, 22f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(6f, 12f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.78f, 0.72f, 0.92f, 0.13f), new Color(0.92f, 0.78f, 0.86f, 0.09f));
            main.maxParticles = 320;

            var em = ps.emission;
            em.rateOverTime = 18f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(96f, 0.8f, 96f);
            shape.randomDirectionAmount = 1f;

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

            FadeInOut(ps, 0.25f, 0.7f);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.maxParticleSize = 3f;
        }

        private static void BuildFireflies(Transform parent)
        {
            var mat = GetOrCreateMaterial("M_Firefly", "Game/Soft Particle", m =>
            {
                SetAdditive(m);
                m.SetColor("_Color", new Color(1f, 0.82f, 0.4f));
                m.SetFloat("_Intensity", 6f);
                m.SetFloat("_Softness", 2.2f);
                m.SetFloat("_SoftFade", 0f);
            });
            if (mat == null) return;

            var ps = NewParticles(parent, "Fireflies", new Vector3(0f, 1.4f, 8f), mat);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 11f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.55f), new Color(0.8f, 1f, 0.6f));
            main.maxParticles = 160;

            var em = ps.emission;
            em.rateOverTime = 18f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(80f, 2.2f, 80f);
            shape.randomDirectionAmount = 1f;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.2f;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            // мерцают: несколько вспышек за жизнь
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Blink(0f, 0.1f, 1f, 0.25f, 0.25f, 0.45f, 1f, 0.6f, 0.15f, 0.8f, 0.9f, 1f, 0f));
        }

        private static void BuildDew(Transform parent)
        {
            var mat = GetOrCreateMaterial("M_Dew", "Game/Soft Particle", m =>
            {
                SetAdditive(m);
                m.SetColor("_Color", new Color(0.82f, 0.9f, 1f));
                m.SetFloat("_Intensity", 4f);
                m.SetFloat("_Softness", 3f);
                m.SetFloat("_SoftFade", 0f);
            });
            if (mat == null) return;

            // крошечные блики росы на траве: вспыхивают и гаснут
            var ps = NewParticles(parent, "DewGlints", new Vector3(0f, 0.25f, 8f), mat);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.4f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            main.maxParticles = 200;

            var em = ps.emission;
            em.rateOverTime = 110f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(80f, 0.35f, 80f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Blink(0f, 0.5f, 1f, 1f, 0f));
        }

        /// <summary>Искры, улетающие вверх от костра.</summary>
        public static void BuildEmbers(Transform parent, Vector3 position)
        {
            var mat = GetOrCreateMaterial("M_Ember", "Game/Soft Particle", m =>
            {
                SetAdditive(m);
                m.SetColor("_Color", new Color(1f, 0.55f, 0.18f));
                m.SetFloat("_Intensity", 8f);
                m.SetFloat("_Softness", 2f);
                m.SetFloat("_SoftFade", 0f);
            });
            if (mat == null) return;

            var ps = NewParticles(parent, "Embers", position, mat);
            ps.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // конус смотрит вверх
            var main = ps.main;
            main.prewarm = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            main.maxParticles = 60;

            var em = ps.emission;
            em.rateOverTime = 14f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.3f;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.8f;
            noise.frequency = 0.8f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Blink(0f, 0.6f, 1f, 1f, 0f));
        }

        private static ParticleSystem NewParticles(Transform parent, string name, Vector3 position, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.duration = 10f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sortMode = ParticleSystemSortMode.Distance;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        private static void FadeInOut(ParticleSystem ps, float inAt, float outAt)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Blink(0f, inAt, 1f, outAt, 1f, 1f, 0f));
        }

        /// <summary>Белый градиент с ключами прозрачности: первая альфа в момент 0, дальше пары (время, альфа). Не больше 8 ключей.</summary>
        private static Gradient Blink(float firstAlpha, params float[] timeAlpha)
        {
            var alphas = new System.Collections.Generic.List<GradientAlphaKey> { new GradientAlphaKey(firstAlpha, 0f) };
            for (int i = 0; i + 1 < timeAlpha.Length; i += 2)
                alphas.Add(new GradientAlphaKey(timeAlpha[i + 1], timeAlpha[i]));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alphas.ToArray());
            return g;
        }

        // --- материалы эффектов

        private static Material GetOrCreateMaterial(string name, string shaderName, System.Action<Material> setup)
        {
            string path = $"{Dir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[Game] Нет шейдера {shaderName} — эффект {name} пропущен. Проверь Console на ошибки шейдеров.");
                return mat;
            }
            if (mat != null && mat.shader == shader) return mat;
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                setup(mat);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
                setup(mat);
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        private static void SetAlphaBlend(Material m)
        {
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void SetAdditive(Material m)
        {
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.renderQueue = (int)RenderQueue.Transparent + 5;
        }

        private static float R(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);
    }
}
