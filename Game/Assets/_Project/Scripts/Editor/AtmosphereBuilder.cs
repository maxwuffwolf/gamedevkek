using System.Linq;
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

        /// <summary>
        /// Версия настроек атмосферы. Если поднять её, при следующей пересборке сцены профиль пост-обработки
        /// и материалы (эффекты, окружение) перезапишутся значениями из кода. Иначе ручные правки сохраняются.
        /// </summary>
        public const int SettingsVersion = 11;
        private const string VersionKey = "Game.Atmosphere.SettingsVersion";

        /// <summary>true, пока идёт пересборка, где ассеты нужно обновить до <see cref="SettingsVersion"/>.</summary>
        public static bool RefreshAssets { get; private set; }

        public static void BeginBuild() => RefreshAssets = EditorPrefs.GetInt(VersionKey, 0) < SettingsVersion;

        public static void EndBuild()
        {
            if (RefreshAssets) EditorPrefs.SetInt(VersionKey, SettingsVersion);
            RefreshAssets = false;
        }

        // --- поздние сумерки, ~20:00–22:00: солнце село, ключевой свет — холодное небо/луна,
        //     на западе догорает тёплая полоска заката, главные тёплые пятна — костёр, светлячки и свет героя
        //     v4 (визуал «как у Shape of Dreams»): общий свет темнее, чтобы локальные источники (грибы, кристаллы,
        //     светлячки, свет героя) стали яркими пятнами, за которые цепляется глаз. Контраст делает пост-обработка.
        public static readonly Color SunColor = new Color(0.55f, 0.66f, 1f);      // лунный ключевой свет
        public const float SunIntensity = 1f;
        public static readonly Color FillColor = new Color(1f, 0.5f, 0.3f);       // отсвет заката у горизонта
        public const float FillIntensity = 0.32f;
        public static readonly Color FogColor = new Color(0.07f, 0.08f, 0.16f);
        public static readonly Color SkyAmbient = new Color(0.2f, 0.23f, 0.42f);
        public static readonly Color EquatorAmbient = new Color(0.15f, 0.14f, 0.25f);
        public static readonly Color GroundAmbient = new Color(0.04f, 0.035f, 0.065f);
        public const float FogDensity = 0.012f;

        /// <summary>
        /// Луна высоко (50°): плоская земля в toon-шейдере остаётся в «светлой» ступени, всё читается.
        /// Ниже 35° земля целиком уходит в «тень».
        /// </summary>
        public static readonly Vector3 SunEuler = new Vector3(50f, 125f, 0f);

        /// <summary>Закатный отсвет: почти горизонтально с запада, только подкрашивает западные бока.</summary>
        public static readonly Vector3 AfterglowEuler = new Vector3(6f, 90f, 0f);

        /// <summary>Фейковые лучи сквозь кроны. С top-down камеры читаются как непонятные полосы, поэтому выключены.</summary>
        public static readonly bool LightShafts = false;

        // ------------------------------------------------------------------ свет и туман

        public static void BuildLighting(Transform parent)
        {
            var sunGo = new GameObject("Moon_Key");
            sunGo.transform.SetParent(parent);
            sunGo.transform.rotation = Quaternion.Euler(SunEuler);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = SunColor;
            sun.intensity = SunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;
            RenderSettings.sun = sun;

            // тёплый отсвет заката: даёт контраст тёплое/холодное, как в кино
            var fillGo = new GameObject("Afterglow_West");
            fillGo.transform.SetParent(parent);
            fillGo.transform.rotation = Quaternion.Euler(AfterglowEuler);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = FillColor;
            fill.intensity = FillIntensity;
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
        public static void BuildEffects(Transform parent, System.Random rng) =>
            BuildEffects(parent, rng, new Vector3(0f, 0f, 8f), new Vector2(80f, 80f));

        /// <param name="center">Центр игровой области (частицы рождаются вокруг него).</param>
        /// <param name="size">Размер области по X и Z, метры. Чем меньше область, тем гуще частицы.</param>
        public static void BuildEffects(Transform parent, System.Random rng, Vector3 center, Vector2 size)
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

            if (LightShafts) BuildLightShafts(root, rng);
            BuildHeroLight(root);
            BuildGroundMist(root, center, size);
            BuildFireflies(root, center, size);
            BuildMotes(root, center, size);
            BuildDew(root, center, size);
        }

        private static VolumeProfile GetOrCreateProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (profile != null && !RefreshAssets) return profile;

            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumePath);
            }
            else
            {
                foreach (var old in profile.components.ToArray()) Object.DestroyImmediate(old, true);
                profile.components.Clear();
            }

            // ACES: «киношная» кривая — тёмные тени, сочные цвета, мягко пережатые яркие пятна (на них и держится Bloom).
            // ACES темнит средние тона, поэтому сразу компенсируем экспозицией.
            var tone = Add<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.ACES);

            // Bloom: всё, что ярче порога (эмиссия грибов/кристаллов, светлячки, искры, глаза), получает ореол
            var bloom = Add<Bloom>(profile);
            bloom.threshold.Override(0.8f);
            bloom.intensity.Override(1.1f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.95f, 0.9f));
            bloom.highQualityFiltering.Override(true);

            var color = Add<ColorAdjustments>(profile);
            color.postExposure.Override(0.95f);
            color.contrast.Override(20f);
            color.saturation.Override(6f);

            // тени чуть опускаем и холодим (было: подняты — отсюда «серость»), света слегка тёплые
            var lgg = Add<LiftGammaGain>(profile);
            lgg.lift.Override(new Vector4(1f, 1f, 1.04f, 0f)); // без отрицательного сдвига: он даёт цветные «кольца» в тёмных местах
            lgg.gamma.Override(new Vector4(1f, 1f, 1f, 0.22f)); // гамма вверх: средние и тёмные тона светлее, детали читаются, ночь остаётся
            lgg.gain.Override(new Vector4(1.04f, 1f, 0.95f, 0.03f));

            var wb = Add<WhiteBalance>(profile);
            wb.temperature.Override(-6f);

            // кино-раскраска: тени в синеву, света в золото
            var split = Add<SplitToning>(profile);
            split.shadows.Override(new Color(0.28f, 0.34f, 0.66f));
            split.highlights.Override(new Color(1f, 0.74f, 0.5f));
            split.balance.Override(-15f);

            // виньетка: края кадра темнее, внимание в центр, к герою
            var vignette = Add<Vignette>(profile);
            vignette.color.Override(new Color(0.01f, 0.01f, 0.04f));
            vignette.intensity.Override(0.4f);
            vignette.smoothness.Override(0.45f);

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
            EnvironmentPrefabs.EnsureFolder(Dir);
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

        // --- свет вокруг героя (как «радиус света» в Diablo): ночью игрок всегда видит, что рядом

        private static void BuildHeroLight(Transform parent)
        {
            var go = new GameObject("HeroLight");
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(0f, 3f, -3f);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            // тёплое пятно вокруг героя на холодной земле: герой всегда стоит «в свете» и читается на фоне
            l.color = new Color(1f, 0.8f, 0.58f);
            l.range = 8f;
            l.intensity = 7f;
            l.shadows = LightShadows.None;
            go.AddComponent<Game.Gameplay.World.HeroLight>().Height = 2.2f; // ниже — ярче и уже пятно на земле
        }

        // --- частицы

        private static void BuildGroundMist(Transform parent, Vector3 center, Vector2 size)
        {
            var mat = GetOrCreateMaterial("M_Mist", "Game/Soft Particle", m =>
            {
                SetAlphaBlend(m);
                m.SetFloat("_Softness", 1.2f);
                m.SetFloat("_NoiseStrength", 0.6f);
                m.SetFloat("_SoftFade", 2f);
            });
            if (mat == null) return;

            var ps = NewParticles(parent, "GroundMist", center + new Vector3(0f, 0.5f, 0f), mat);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(14f, 22f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            // клочья дымки над землёй: в тёмной сцене светлый туман и даёт «глубину»
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.4f, 0.48f, 0.78f, 0.13f), new Color(0.52f, 0.5f, 0.8f, 0.09f));
            main.maxParticles = 260;

            var em = ps.emission;
            em.rateOverTime = Density(size, 11f, 18f);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(size.x + 16f, 0.5f, size.y + 16f);
            shape.randomDirectionAmount = 1f;

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

            FadeInOut(ps, 0.25f, 0.7f);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.maxParticleSize = 3f;
        }

        private static void BuildFireflies(Transform parent, Vector3 center, Vector2 size)
        {
            var mat = GetOrCreateMaterial("M_Firefly", "Game/Soft Particle", m =>
            {
                SetAdditive(m);
                m.SetColor("_Color", new Color(1f, 0.82f, 0.4f));
                m.SetFloat("_Intensity", 7f);
                m.SetFloat("_Softness", 2.2f);
                m.SetFloat("_SoftFade", 0f);
            });
            if (mat == null) return;

            var ps = NewParticles(parent, "Fireflies", center + new Vector3(0f, 1.4f, 0f), mat);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 11f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.13f, 0.24f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.55f), new Color(0.8f, 1f, 0.6f));
            main.maxParticles = 400;

            var em = ps.emission;
            em.rateOverTime = Density(size, 24f, 40f);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(size.x, 2.4f, size.y);
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

        private static void BuildDew(Transform parent, Vector3 center, Vector2 size)
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
            var ps = NewParticles(parent, "DewGlints", center + new Vector3(0f, 0.25f, 0f), mat);
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
            shape.scale = new Vector3(size.x, 0.35f, size.y);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Blink(0f, 0.5f, 1f, 1f, 0f));
        }

        /// <summary>Пылинки и пыльца в воздухе: еле заметные холодные точки медленно плывут — воздух «живой», есть объём.</summary>
        private static void BuildMotes(Transform parent, Vector3 center, Vector2 size)
        {
            var mat = GetOrCreateMaterial("M_Mote", "Game/Soft Particle", m =>
            {
                SetAdditive(m);
                m.SetColor("_Color", new Color(0.62f, 0.75f, 1f));
                m.SetFloat("_Intensity", 2.2f);
                m.SetFloat("_Softness", 2.4f);
                m.SetFloat("_SoftFade", 0f);
            });
            if (mat == null) return;

            var ps = NewParticles(parent, "AirMotes", center + new Vector3(0f, 2.5f, 0f), mat);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 12f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.07f);
            main.maxParticles = 600;

            var em = ps.emission;
            em.rateOverTime = Density(size, 35f, 55f);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(size.x, 5f, size.y);
            shape.randomDirectionAmount = 1f;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.2f;
            noise.scrollSpeed = 0.1f;

            FadeInOut(ps, 0.2f, 0.75f);
        }

        /// <summary>Сколько частиц в секунду: на площади 80×80 — base, на маленькой — пропорционально меньше, но не меньше min.</summary>
        private static float Density(Vector2 size, float basePer6400, float min) =>
            Mathf.Max(min, basePer6400 * size.x * size.y / 6400f * 1.6f);

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
            EnvironmentPrefabs.EnsureFolder(Dir);
            string path = $"{Dir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[Game] Нет шейдера {shaderName} — эффект {name} пропущен. Проверь Console на ошибки шейдеров.");
                return mat;
            }
            if (mat != null && mat.shader == shader && !RefreshAssets) return mat;
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
