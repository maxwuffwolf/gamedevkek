// Земля зоны (URP, Unity 6) — процедурная, без текстур, в мировых координатах (тайлинг не виден):
//   * земля из двух оттенков с крупными пятнами (fbm-шум);
//   * мох пятнами, гуще вдали от тропы;
//   * извилистая утоптанная тропа (та же синусоида, что у ущелья) с рваными краями;
//   * камешки, тёмные трещинки и светлые крапинки (сухие листья);
//   * свет как у Game/Stylized Toon: ступени луны, цветная тень, мягкие пятна точечных источников, туман.
// Параметры тропы выставляет RavineSceneBuilder (по форме ущелья). _PathWidth = 0 — тропы нет.
Shader "Game/Ground Toon"
{
    Properties
    {
        [Header(Soil)]
        _DirtColor ("Soil A", Color) = (0.2, 0.24, 0.42, 1)
        _DirtColor2 ("Soil B", Color) = (0.27, 0.31, 0.5, 1)
        _NoiseScale ("Patch scale (1/m)", Float) = 0.16

        [Header(Moss)]
        _MossColor ("Moss A", Color) = (0.14, 0.34, 0.38, 1)
        _MossColor2 ("Moss B", Color) = (0.22, 0.44, 0.4, 1)
        _MossAmount ("Moss amount", Range(0, 1)) = 0.5

        [Header(Path)]
        _PathColor ("Path", Color) = (0.52, 0.44, 0.38, 1)
        _PathAmp ("Path sway amplitude (m)", Float) = 3.5
        _PathFreq ("Path sway frequency", Float) = 0.055
        _PathOffsetX ("Path X offset", Float) = 0
        _PathWidth ("Path half width (m, 0 = none)", Float) = 1.4
        _PathZMin ("Path starts at Z", Float) = -1000
        _PathZMax ("Path ends at Z", Float) = 1000

        [Header(Details)]
        _PebbleColor ("Pebbles", Color) = (0.46, 0.44, 0.48, 1)
        _PebbleDensity ("Pebble density", Range(0, 1)) = 0.12
        _LeafColor ("Leaf litter", Color) = (0.62, 0.36, 0.2, 1)
        _LeafDensity ("Leaf density", Range(0, 1)) = 0.15
        _CrackColor ("Cracks", Color) = (0.12, 0.1, 0.12, 1)

        [Header(Toon lighting)]
        _ShadowColor ("Shadow tint", Color) = (0.45, 0.5, 0.8, 1)
        [IntRange] _Steps ("Light steps", Range(1, 5)) = 2
        _StepSmooth ("Step softness", Range(0.001, 0.5)) = 0.06
        _PointSmooth ("Point lights: 0 toon steps, 1 soft pools", Range(0, 1)) = 1
        _AmbientStrength ("Ambient strength", Range(0, 1)) = 0.9
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _DirtColor;
            half4 _DirtColor2;
            float _NoiseScale;
            half4 _MossColor;
            half4 _MossColor2;
            half _MossAmount;
            half4 _PathColor;
            float _PathAmp;
            float _PathFreq;
            float _PathOffsetX;
            float _PathWidth;
            float _PathZMin;
            float _PathZMax;
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

            // Клеточный шум: x — расстояние до ближайшей точки, y — её случайное число, z — до второй ближайшей.
            float3 Cells(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float best = 8.0, second = 8.0, id = 0.0;
                for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    float2 g = float2(x, y);
                    float2 h = Hash22(i + g);
                    float d = length(g + h * 0.8 + 0.1 - f);
                    if (d < best) { second = best; best = d; id = Hash21(i + g + 3.1); }
                    else if (d < second) { second = d; }
                }
                return float3(best, id, second);
            }

            half Band(half x, half steps, half smoothness)
            {
                half s = x * steps;
                half f = floor(s);
                half t = smoothstep(1.0 - smoothness, 1.0, frac(s));
                return saturate((f + t) / steps);
            }

            // ---------------------------------------------------------------- цвет земли
            half3 GroundAlbedo(float2 p)
            {
                float big = Fbm(p * _NoiseScale);
                float mid = Fbm(p * _NoiseScale * 3.1 + 11.7);
                float fine = ValueNoise(p * 3.3);

                // земля: два оттенка крупными пятнами + мелкая зернистость
                half3 c = lerp(_DirtColor.rgb, _DirtColor2.rgb, smoothstep(0.35, 0.7, mid));
                c *= 0.9 + 0.2 * fine;

                // тропа: та же синусоида, что у ущелья, края рваные
                float pathMask = 0.0;
                if (_PathWidth > 0.0)
                {
                    float cx = _PathOffsetX + _PathAmp * sin(p.y * _PathFreq);
                    float d = abs(p.x - cx) + (mid - 0.5) * 1.6 + (fine - 0.5) * 0.35;
                    float zFade = smoothstep(_PathZMin, _PathZMin + 3.0, p.y) * (1.0 - smoothstep(_PathZMax - 3.0, _PathZMax, p.y));
                    pathMask = (1.0 - smoothstep(_PathWidth * 0.55, _PathWidth, d)) * zFade;
                    // колея: в середине тропа светлее и глаже
                    half3 path = _PathColor.rgb * (0.85 + 0.25 * fine) * lerp(0.92, 1.08, smoothstep(_PathWidth, 0.0, d));
                    c = lerp(c, path, pathMask);
                }

                // мох: пятнами, на тропе вытоптан
                float mossN = big + (mid - 0.5) * 0.35;
                float moss = smoothstep(1.0 - _MossAmount, 1.15 - _MossAmount, mossN) * (1.0 - pathMask);
                half3 mossCol = lerp(_MossColor.rgb, _MossColor2.rgb, fine) * (0.85 + 0.3 * ValueNoise(p * 9.0));
                c = lerp(c, mossCol, moss);

                // трещинки: тонкие тёмные линии по границам клеток
                float3 cr = Cells(p * 0.9);
                float crack = (1.0 - smoothstep(0.0, 0.05, cr.z - cr.x)) * smoothstep(0.5, 0.65, mid) * (1.0 - moss) * (1.0 - pathMask * 0.6) * 0.55;
                c = lerp(c, _CrackColor.rgb, crack);

                // камешки: неровные пятна (координаты «плывут» шумом, чтобы не было идеальных кругов), с тёмной каймой
                float2 warp = float2(ValueNoise(p * 3.1), ValueNoise(p * 3.1 + 7.7)) - 0.5;
                float3 pc = Cells(p * 2.6 + warp * 0.9);
                float pebbleOn = step(1.0 - _PebbleDensity, pc.y) * (1.0 - moss * 0.7);
                float r = 0.07 + 0.08 * frac(pc.y * 7.3);
                float pebble = (1.0 - smoothstep(r - 0.04, r, pc.x)) * pebbleOn;
                float rim = (1.0 - smoothstep(r, r + 0.06, pc.x)) * pebbleOn - pebble;
                c = lerp(c, c * 0.55, saturate(rim) * 0.7);
                c = lerp(c, _PebbleColor.rgb * (0.8 + 0.4 * frac(pc.y * 13.1)), pebble);

                // сухие листья: мелкие тёплые крапинки, больше по краям тропы
                float2 warp2 = float2(ValueNoise(p * 6.3 + 3.0), ValueNoise(p * 6.3 + 11.0)) - 0.5;
                float3 lc = Cells(float2(p.x * 5.0, p.y * 3.2) + warp2 * 1.1 + 2.3); // вытянутые и рваные, как листья
                float leafOn = step(1.0 - _LeafDensity * (0.6 + 0.8 * (1.0 - pathMask)), lc.y);
                float leaf = (1.0 - smoothstep(0.08, 0.13, lc.x)) * leafOn;
                c = lerp(c, _LeafColor.rgb * (0.7 + 0.5 * frac(lc.y * 5.7)), leaf * 0.85);

                return c;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 albedo = GroundAlbedo(input.positionWS.xz);

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
                half3 color = albedo * (light + ambient);
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
