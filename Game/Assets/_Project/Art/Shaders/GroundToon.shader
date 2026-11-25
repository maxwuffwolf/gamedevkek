// Земля зоны (URP, Unity 6) — процедурная, без текстур, в мировых координатах (тайлинг не виден).
// Рисованный «стилизованный» вид в духе Darksiders Genesis / Shape of Dreams: крупные читаемые формы,
// у каждой формы светлая верхняя кромка и тёмная нижняя (как мазок кистью), мелкого шума мало.
//   * земля: комья с фаской, два оттенка крупными пятнами;
//   * зоны поперёк ущелья: у стен сырая тёмная земля, мох и листва, к середине — утоптанная тропа;
//   * тропа (та же синусоида, что у ущелья) с камнями по краям и сухими трещинками;
//   * кратер падения: раскалённое ядро, плиты оплавленного базальта со светящимися швами,
//     радиальные разломы, пепельное кольцо и лучи выброса, тлеющие искры (свечение через Bloom);
//   * свет как у Game/Stylized Toon: ступени луны, цветная тень, мягкие пятна точечных источников, туман.
// Форму ущелья, тропы и кратера выставляет RavineSceneBuilder. _PathWidth = 0 — тропы нет, _CraterRadius = 0 — кратера нет.
Shader "Game/Ground Toon"
{
    Properties
    {
        [Header(Soil)]
        _DirtColor ("Soil A", Color) = (0.25, 0.21, 0.22, 1)
        _DirtColor2 ("Soil B", Color) = (0.33, 0.27, 0.24, 1)
        _DampColor ("Damp soil near walls", Color) = (0.17, 0.15, 0.19, 1)
        _NoiseScale ("Patch scale (1/m)", Float) = 0.16
        _ClodScale ("Clod size (1/m)", Float) = 1.6
        _Bevel ("Painted bevel strength", Range(0, 1)) = 0.55

        [Header(Moss)]
        _MossColor ("Moss A", Color) = (0.15, 0.25, 0.21, 1)
        _MossColor2 ("Moss B", Color) = (0.24, 0.34, 0.24, 1)
        _MossAmount ("Moss amount", Range(0, 1)) = 0.5

        [Header(Ravine shape)]
        _RavineWidth ("Half width base, sway, freq, phase", Vector) = (6.5, 1.8, 0.13, 1.3)
        _WallBand ("Damp band near walls (0..1 across)", Range(0, 1)) = 0.62

        [Header(Path)]
        _PathColor ("Path", Color) = (0.45, 0.36, 0.27, 1)
        _PathAmp ("Path sway amplitude (m)", Float) = 3.5
        _PathFreq ("Path sway frequency", Float) = 0.055
        _PathOffsetX ("Path X offset", Float) = 0
        _PathWidth ("Path half width (m, 0 = none)", Float) = 1.4
        _PathZMin ("Path starts at Z", Float) = -1000
        _PathZMax ("Path ends at Z", Float) = 1000

        [Header(Crater)]
        _CraterCenter ("Crater center (x, z)", Vector) = (0, -34, 0, 0)
        _CraterRadius ("Crater radius (m, 0 = none)", Float) = 0
        _CharColor ("Charred plates", Color) = (0.13, 0.11, 0.14, 1)
        _CharColor2 ("Charred plates B", Color) = (0.2, 0.16, 0.18, 1)
        _AshColor ("Ash", Color) = (0.36, 0.33, 0.38, 1)
        [HDR] _MagmaColor ("Magma (HDR)", Color) = (2.6, 0.75, 0.18, 1)
        [HDR] _CoreColor ("Hot core (HDR)", Color) = (3.2, 1.6, 0.6, 1)
        _Heat ("Heat (glow amount)", Range(0, 2)) = 1
        _PulseSpeed ("Glow pulse speed", Float) = 1.3

        [Header(Details)]
        _PebbleColor ("Pebbles", Color) = (0.46, 0.44, 0.48, 1)
        _PebbleDensity ("Pebble density", Range(0, 1)) = 0.12
        _LeafColor ("Leaf litter", Color) = (0.62, 0.36, 0.2, 1)
        _LeafDensity ("Leaf density", Range(0, 1)) = 0.15
        _CrackColor ("Cracks", Color) = (0.12, 0.1, 0.12, 1)

        [Header(Toon lighting)]
        _ShadowColor ("Shadow tint", Color) = (0.38, 0.4, 0.66, 1)
        [IntRange] _Steps ("Light steps", Range(1, 5)) = 2
        _StepSmooth ("Step softness", Range(0.001, 0.5)) = 0.06
        _PointSmooth ("Point lights: 0 toon steps, 1 soft pools", Range(0, 1)) = 1
        _AmbientStrength ("Ambient strength", Range(0, 1)) = 0.75
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _DirtColor;
            half4 _DirtColor2;
            half4 _DampColor;
            float _NoiseScale;
            float _ClodScale;
            half _Bevel;
            half4 _MossColor;
            half4 _MossColor2;
            half _MossAmount;
            float4 _RavineWidth;
            half _WallBand;
            half4 _PathColor;
            float _PathAmp;
            float _PathFreq;
            float _PathOffsetX;
            float _PathWidth;
            float _PathZMin;
            float _PathZMax;
            float4 _CraterCenter;
            float _CraterRadius;
            half4 _CharColor;
            half4 _CharColor2;
            half4 _AshColor;
            half4 _MagmaColor;
            half4 _CoreColor;
            half _Heat;
            float _PulseSpeed;
            half4 _PebbleColor;
            half _PebbleDensity;
            half4 _LeafColor;
            half _LeafDensity;
            half4 _CrackColor;
            half4 _ShadowColor;
            half _Steps;
            half _StepSmooth;
            half _PointSmooth;
            half _AmbientStrength;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                half fogFactor    : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            // ---------------------------------------------------------------- шум
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 Hash22(float2 p)
            {
                float n = Hash21(p);
                return float2(n, Hash21(p + n + 17.17));
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i), b = Hash21(i + float2(1, 0)), c = Hash21(i + float2(0, 1)), d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0, a = 0.5;
                for (int k = 0; k < 4; k++)
                {
                    v += ValueNoise(p) * a;
                    p = p * 2.03 + float2(5.2, 1.3);
                    a *= 0.5;
                }
                return v / 0.9375;
            }

            // Клеточный шум (Вороной).
            struct CellInfo
            {
                float d1;     // расстояние до ближайшей точки
                float d2;     // до второй ближайшей
                float id;     // случайное число ближайшей клетки 0..1
                float2 toC;   // вектор от пикселя к центру ближайшей клетки
            };

            CellInfo Cells(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                CellInfo c;
                c.d1 = 8.0; c.d2 = 8.0; c.id = 0.0; c.toC = float2(0, 0);
                for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    float2 g = float2(x, y);
                    float2 h = Hash22(i + g);
                    float2 v = g + h * 0.8 + 0.1 - f;
                    float d = length(v);
                    if (d < c.d1) { c.d2 = c.d1; c.d1 = d; c.id = Hash21(i + g + 3.1); c.toC = v; }
                    else if (d < c.d2) { c.d2 = d; }
                }
                return c;
            }

            half Band(half x, half steps, half smoothness)
            {
                half s = x * steps;
                half f = floor(s);
                half t = smoothstep(1.0 - smoothness, 1.0, frac(s));
                return saturate((f + t) / steps);
            }

            // «Мазок кистью» для плиты/кома: светлая кромка с северо-запада (откуда луна), тёмная с юго-востока.
            // edge — расстояние до границы клетки (d2 - d1), toC — к центру клетки.
            half PaintedBevel(CellInfo c, float width)
            {
                half rim = 1.0 - smoothstep(0.0, width, c.d2 - c.d1);
                float2 n = -normalize(c.toC + 1e-4);              // наружу от центра — «нормаль» склона
                half facing = dot(n, normalize(float2(-0.55, 0.85))); // >0 — кромка смотрит на свет
                return rim * facing;
            }

            // ---------------------------------------------------------------- цвет земли
            struct Surface
            {
                half3 albedo;
                half3 emission;
            };

            void ApplyCrater(float2 p, float big, float mid, float fine, inout half3 c, inout half3 emission, out float craterMask)
            {
                craterMask = 0.0;
                if (_CraterRadius <= 0.0) return;
                float2 d = p - _CraterCenter.xy;
                float dist = length(d);
                float wob = (Fbm(d * 0.35 + 4.1) - 0.5) * 0.35;  // рваный край
                float r = dist / _CraterRadius + wob;               // 0 — центр, 1 — край чаши
                if (r > 2.2) return;
                float ang = atan2(d.y, d.x);
                float t = _Time.y * _PulseSpeed;

                // лучи выброса: вытянутые по радиусу полосы пепла и сажи до ~2 радиусов
                float rays = Fbm(float2(ang * 5.2, dist * 0.18) + 9.3);
                float rayMask = smoothstep(0.52, 0.68, rays) * (1.0 - smoothstep(1.1, 2.1, r)) * smoothstep(0.75, 1.05, r);
                float soot = (1.0 - smoothstep(0.9, 1.6, r)) * (0.55 + 0.45 * mid);
                c = lerp(c, c * 0.45, soot * 0.8);                          // земля вокруг закопчена
                c = lerp(c, _AshColor.rgb * (0.85 + 0.3 * fine), rayMask * 0.75);

                // пепельное кольцо у края чаши
                float ashRing = smoothstep(0.55, 0.85, r) * (1.0 - smoothstep(0.95, 1.25, r));
                half3 ash = _AshColor.rgb * (0.75 + 0.35 * fine) * lerp(0.8, 1.05, Fbm(p * 1.3));
                // плиты оплавленного базальта: крупные к центру, мельче к краю
                float2 warp = float2(ValueNoise(p * 1.7), ValueNoise(p * 1.7 + 5.5)) - 0.5;
                CellInfo pl = Cells(p * 1.15 + warp * 0.6);                   // одинаковый масштаб: иначе Вороной «закручивается» в кольца
                half tone = 0.75 + 0.5 * pl.id;
                half3 plate = lerp(_CharColor.rgb, _CharColor2.rgb, pl.id) * tone;
                half bev = PaintedBevel(pl, 0.22);
                plate *= 1.0 + bev * _Bevel * 0.9;                           // светлая/тёмная кромка плиты
                plate = lerp(plate, ash, smoothstep(0.62, 1.0, r) * 0.85);   // к краю плиты засыпаны пеплом
                float bowl = 1.0 - smoothstep(0.92, 1.15, r);
                craterMask = bowl;
                c = lerp(c, plate, bowl);
                c = lerp(c, ash, ashRing * (1.0 - bowl) * 0.8);

                // светящиеся швы между плитами: широкие и яркие в центре, тонкие и тусклые к краю
                float heat = (1.0 - smoothstep(0.05, 0.6, r)) * _Heat;
                float seamW = lerp(0.1, 0.03, saturate(r * 1.4));
                float seam = 1.0 - smoothstep(0.0, seamW, pl.d2 - pl.d1);
                float pulse = 0.78 + 0.22 * sin(t * 2.0 + pl.id * 12.0 + dist * 0.8);
                c = lerp(c, c * 0.35, seam * bowl);                           // шов — тёмная щель
                emission += _MagmaColor.rgb * seam * heat * pulse * 0.8;

                // радиальные разломы от центра, извилистые, разной длины
                float sectors = 7.0;
                float a01 = ang / 6.28318 * sectors + (Fbm(float2(dist * 0.5, ang * 2.0)) - 0.5) * 0.9;
                float sid = round(a01);                                  // разлом идёт по границе секторов
                float len = lerp(0.4, 0.95, Hash21(float2(sid, 7.0)));
                float ad = abs(a01 - sid) / sectors * 6.28318 * dist;      // расстояние до линии разлома, м
                float fissure = (1.0 - smoothstep(0.0, lerp(0.09, 0.02, saturate(r / len)), ad))
                              * (1.0 - smoothstep(len * 0.7, len, r)) * smoothstep(0.1, 0.22, r);
                c = lerp(c, _CrackColor.rgb * 0.6, fissure * 0.9);
                emission += _MagmaColor.rgb * fissure * (1.0 - smoothstep(0.0, len, r)) * 1.1 * _Heat * pulse;

                // раскалённое ядро — место удара
                float core = 1.0 - smoothstep(0.0, 0.14, r);
                c = lerp(c, c * 0.5, core);
                emission += _CoreColor.rgb * core * core * (0.85 + 0.15 * sin(t * 3.1)) * _Heat;

                // тлеющие искры в пепле
                CellInfo em = Cells(p * 4.5 + 13.0);
                float ember = (1.0 - smoothstep(0.03, 0.07, em.d1)) * step(0.86, em.id) * (1.0 - smoothstep(0.7, 1.4, r));
                float flick = saturate(sin(t * (2.0 + em.id * 5.0) + em.id * 40.0) * 0.5 + 0.5);
                emission += _MagmaColor.rgb * ember * flick * 1.4 * _Heat;
            }

            Surface GroundSurface(float2 p)
            {
                Surface s;
                s.emission = half3(0, 0, 0);
                float big = Fbm(p * _NoiseScale);
                float mid = Fbm(p * _NoiseScale * 3.1 + 11.7);
                float fine = ValueNoise(p * 3.3);

                // положение поперёк ущелья: 0 — середина, 1 — у стены (та же форма, что в RavineSceneBuilder)
                float cx = _PathOffsetX + _PathAmp * sin(p.y * _PathFreq);
                float halfW = _RavineWidth.x > 0.0 ? _RavineWidth.x + _RavineWidth.y * sin(p.y * _RavineWidth.z + _RavineWidth.w) : 1000.0;
                if (_CraterRadius > 0.0) halfW = max(halfW, _CraterRadius * 1.91 - abs(p.y - _CraterCenter.y) * 0.45); // чаша у кратера (= HalfWidth)
                float across = saturate(abs(p.x - cx) / max(halfW, 1.0) + (mid - 0.5) * 0.25);
                float wall = smoothstep(_WallBand, 1.0, across);

                // земля: комья с нарисованной фаской, два оттенка крупными пятнами
                float2 cw = float2(ValueNoise(p * 2.1), ValueNoise(p * 2.1 + 3.3)) - 0.5;
                CellInfo clod = Cells(p * max(_ClodScale, 0.5) + cw * 0.7);
                half3 c = lerp(_DirtColor.rgb, _DirtColor2.rgb, smoothstep(0.35, 0.7, mid));
                c *= 0.88 + 0.22 * clod.id;
                c *= 1.0 + PaintedBevel(clod, 0.18) * _Bevel * 0.45;
                c = lerp(c, _DampColor.rgb * (0.9 + 0.2 * clod.id), wall * 0.8); // у стен — сырая тёмная земля

                // тропа: синусоида, края рваные, по краям — камни, на ней — сухие трещинки
                float pathMask = 0.0;
                float pathEdge = 0.0;
                if (_PathWidth > 0.0)
                {
                    float dd = abs(p.x - cx) + (mid - 0.5) * 1.6 + (fine - 0.5) * 0.35;
                    float zFade = smoothstep(_PathZMin, _PathZMin + 3.0, p.y) * (1.0 - smoothstep(_PathZMax - 3.0, _PathZMax, p.y));
                    pathMask = (1.0 - smoothstep(_PathWidth * 0.55, _PathWidth, dd)) * zFade;
                    pathEdge = (1.0 - smoothstep(0.0, 0.45, abs(dd - _PathWidth * 0.85))) * zFade;
                    half3 path = _PathColor.rgb * (0.88 + 0.2 * fine) * lerp(0.92, 1.1, smoothstep(_PathWidth, 0.0, dd));
                    // сухая земля тропы: крупные «черепки» с тонкими трещинами
                    CellInfo dry = Cells(p * 1.25 + cw * 0.4);
                    path *= 1.0 + PaintedBevel(dry, 0.12) * _Bevel * 0.35;
                    path = lerp(path, path * 0.62, (1.0 - smoothstep(0.0, 0.035, dry.d2 - dry.d1)) * smoothstep(0.4, 0.6, big));
                    c = lerp(c, path, pathMask);
                }

                // мох: пятнами, гуще у стен, на тропе вытоптан; светлая кромка сверху и тёмный «подрез» по краю
                float mossN = big + (mid - 0.5) * 0.35 + wall * 0.35 - (1.0 - wall) * 0.08;
                float mossT = 1.0 - _MossAmount;
                float moss = smoothstep(mossT, mossT + 0.12, mossN) * (1.0 - pathMask);
                float mossLip = smoothstep(mossT - 0.03, mossT + 0.02, mossN) - smoothstep(mossT + 0.02, mossT + 0.07, mossN);
                CellInfo tuft = Cells(p * 2.4 + 21.0);
                half3 mossCol = lerp(_MossColor.rgb, _MossColor2.rgb, tuft.id * 0.7 + fine * 0.3);
                mossCol *= 1.0 + PaintedBevel(tuft, 0.25) * _Bevel * 0.6;     // мох кочками
                c = lerp(c, c * 0.6, saturate(mossLip) * (1.0 - pathMask));   // тень под краем моха
                c = lerp(c, mossCol, moss);

                // трещинки в земле (не на мху)
                CellInfo cr = Cells(p * 0.9);
                float crack = (1.0 - smoothstep(0.0, 0.05, cr.d2 - cr.d1)) * smoothstep(0.5, 0.65, mid) * (1.0 - moss) * (1.0 - pathMask * 0.6) * 0.5;
                c = lerp(c, _CrackColor.rgb, crack);

                // кратер падения
                float craterMask;
                ApplyCrater(p, big, mid, fine, c, s.emission, craterMask);

                // камешки: неровные, с тёмной каймой и светлой верхушкой; гуще по краям тропы
                float2 warp = float2(ValueNoise(p * 3.1), ValueNoise(p * 3.1 + 7.7)) - 0.5;
                CellInfo pc = Cells(p * 2.6 + warp * 0.9);
                float pebbleOn = step(1.0 - saturate(_PebbleDensity * (1.0 + pathEdge * 2.5)), pc.id) * (1.0 - moss * 0.7) * (1.0 - craterMask);
                float rr = 0.07 + 0.08 * frac(pc.id * 7.3);
                float pebble = (1.0 - smoothstep(rr - 0.04, rr, pc.d1)) * pebbleOn;
                float rim = (1.0 - smoothstep(rr, rr + 0.06, pc.d1)) * pebbleOn - pebble;
                c = lerp(c, c * 0.5, saturate(rim) * 0.75);
                half pebHi = saturate(dot(-normalize(pc.toC + 1e-4), normalize(float2(-0.55, 0.85)))) * smoothstep(rr * 0.3, rr, pc.d1);
                c = lerp(c, _PebbleColor.rgb * (0.8 + 0.4 * frac(pc.id * 13.1)) * (1.0 + pebHi * 0.45), pebble);

                // опавшие листья: тёплые «лодочки», кучками у стен и по краям тропы
                float2 warp2 = float2(ValueNoise(p * 6.3 + 3.0), ValueNoise(p * 6.3 + 11.0)) - 0.5;
                CellInfo lc = Cells(float2(p.x * 5.0, p.y * 3.2) + warp2 * 1.1 + 2.3);
                float leafZone = saturate(0.35 + wall * 0.9 + pathEdge * 0.4 + (big - 0.5)) * (1.0 - craterMask);
                float leafOn = step(1.0 - _LeafDensity * leafZone * 1.6, lc.id);
                float leaf = (1.0 - smoothstep(0.08, 0.13, lc.d1)) * leafOn;
                c = lerp(c, _LeafColor.rgb * (0.7 + 0.5 * frac(lc.id * 5.7)), leaf * 0.85);

                s.albedo = c;
                return s;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Surface surf = GroundSurface(input.positionWS.xz);
                half3 albedo = surf.albedo;

                float3 n = normalize(input.normalWS);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(screenUV);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half ndl = saturate(dot(n, mainLight.direction));
                half band = Band(ndl * mainLight.shadowAttenuation, _Steps, _StepSmooth);
                half3 light = lerp(_ShadowColor.rgb, half3(1, 1, 1), band) * mainLight.color * ao.directAmbientOcclusion;

                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalizedScreenSpaceUV = screenUV;
                    uint lightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(lightCount)
                        Light l = GetAdditionalLight(lightIndex, input.positionWS);
                        half a = saturate(dot(n, l.direction)) * l.distanceAttenuation;
                        half att = l.distanceAttenuation;
                        half aSoft = saturate((dot(n, l.direction) + 0.25) / 1.25) * att / (1.0 + att);
                        light += l.color * lerp(Band(saturate(a), _Steps, _StepSmooth), aSoft, _PointSmooth);
                    LIGHT_LOOP_END
                #endif

                half3 ambient = SampleSH(n) * _AmbientStrength * ao.indirectAmbientOcclusion;
                half3 color = albedo * (light + ambient) + surf.emission;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings DepthVert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half DepthFrag(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            Varyings NormalsVert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 NormalsFrag(Varyings input) : SV_Target
            {
                #if defined(_GBUFFER_NORMALS_OCT)
                    float3 n = normalize(input.normalWS);
                    float2 oct = saturate(PackNormalOctQuadEncode(n) * 0.5 + 0.5);
                    return half4(PackFloat2To888(oct), 0.0);
                #else
                    return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
                #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
