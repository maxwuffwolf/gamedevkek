// Стилизованный toon-шейдер для окружения (URP, Unity 6).
// Что даёт «вайб» в духе Darksiders Genesis / Shape of Dreams:
//   * ступенчатый свет (1–5 ступеней) с мягкой гранью;
//   * цветная тень (фиолетово-синяя, а не чёрная);
//   * градиент по высоте объекта: низ темнее, верх светлее;
//   * лёгкая случайная вариация яркости/оттенка на каждый экземпляр (по позиции объекта);
//   * rim-подсветка по краю силуэта;
//   * ветер для листвы (качается только верх модели).
// Цвет = текстура (палитра-атлас) × Tint × цвет вершин (vertex colors из Blender).
// Перенесён из наработок PR #2. Настройки материала окружения: EnvironmentPrefabs.cs.
Shader "Game/Stylized Toon"
{
    Properties
    {
        [Header(Color)]
        [MainTexture] _BaseMap ("Palette / Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [Toggle(_VERTEX_COLORS)] _UseVertexColors ("Use vertex colors", Float) = 1
        _Variation ("Per-instance variation", Range(0, 0.5)) = 0.08

        [Header(Height gradient)]
        _GradientBottom ("Bottom tint", Color) = (0.72, 0.72, 0.8, 1)
        _GradientTop ("Top tint", Color) = (1, 1, 1, 1)
        _GradientHeight ("Gradient height (m)", Float) = 2

        [Header(Toon lighting)]
        _ShadowColor ("Shadow tint", Color) = (0.42, 0.4, 0.62, 1)
        [IntRange] _Steps ("Light steps", Range(1, 5)) = 2
        _StepSmooth ("Step softness", Range(0.001, 0.5)) = 0.06
        _PointSmooth ("Point lights: 0 toon steps, 1 soft pools", Range(0, 1)) = 1
        _AmbientStrength ("Ambient strength", Range(0, 1)) = 0.35

        [Header(Rim)]
        _RimColor ("Rim color", Color) = (1, 0.93, 0.78, 1)
        _RimPower ("Rim power", Range(0.5, 8)) = 3.5
        _RimStrength ("Rim strength", Range(0, 1)) = 0.2

        [Header(Emission)]
        [HDR] _EmissionColor ("Emission (HDR, >1 glows through Bloom)", Color) = (0, 0, 0, 1)
        _EmissionFromAlbedo ("Emission x albedo (0 = flat color, 1 = tinted by vertex colors)", Range(0, 1)) = 0

        [Header(Wind foliage)]
        _WindStrength ("Wind strength", Range(0, 0.3)) = 0
        _WindSpeed ("Wind speed", Range(0, 5)) = 1.3
        _WindScale ("Wind scale", Range(0.01, 2)) = 0.35

        [Header(Render)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        // Все свойства материала в одном CBUFFER и одинаковые во всех проходах: так работает SRP Batcher.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Variation;
            half4 _GradientBottom;
            half4 _GradientTop;
            float _GradientHeight;
            half4 _ShadowColor;
            half _Steps;
            half _StepSmooth;
            half _PointSmooth;
            half _AmbientStrength;
            half4 _RimColor;
            half _RimPower;
            half _RimStrength;
            half4 _EmissionColor;
            half _EmissionFromAlbedo;
            float _WindStrength;
            float _WindSpeed;
            float _WindScale;
            half _Cull;
        CBUFFER_END

        // Ветер: смещаем вершину по XZ, сила растёт квадратично от земли к верху модели.
        float3 ApplyWind(float3 positionOS)
        {
            float3 originWS = TransformObjectToWorld(float3(0, 0, 0));
            float h = saturate(positionOS.y / max(_GradientHeight, 0.01));
            float phase = _Time.y * _WindSpeed + (originWS.x + originWS.z) * _WindScale + positionOS.y * 0.7;
            float2 sway = float2(sin(phase), cos(phase * 0.83)) * _WindStrength * h * h;
            positionOS.xz += sway;
            return positionOS;
        }

        // Хэш позиции объекта: у каждого экземпляра свой стабильный «рандом» 0..1.
        float InstanceHash()
        {
            float3 o = TransformObjectToWorld(float3(0, 0, 0));
            return frac(sin(dot(floor(o * 3.0), float3(12.9898, 78.233, 37.719))) * 43758.5453);
        }
        ENDHLSL

        // ------------------------------------------------------------------ основной проход
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma shader_feature_local _VERTEX_COLORS

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
                float2 uv         : TEXCOORD0;
                half4 color       : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                half4 color       : TEXCOORD3;
                half3 tint        : TEXCOORD4; // градиент по высоте × вариация экземпляра
                half fogFactor    : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);

                float3 posOS = ApplyWind(input.positionOS.xyz);
                VertexPositionInputs pos = GetVertexPositionInputs(posOS);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                half4 vc = input.color;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    // Blender пишет цвета вершин в sRGB, проект в Linear: переводим, иначе цвета «выцветают».
                    vc.rgb = SRGBToLinear(vc.rgb);
                #endif
                o.color = vc;

                float h = saturate(input.positionOS.y / max(_GradientHeight, 0.01));
                half3 grad = lerp(_GradientBottom.rgb, _GradientTop.rgb, h);
                float r = InstanceHash() - 0.5;
                // вариация: яркость ± и лёгкий сдвиг в тёплое/холодное
                half3 variation = 1.0 + _Variation * half3(r * 1.2, r, -r * 0.6);
                o.tint = grad * variation;

                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            // Ступенчатое освещение: x в 0..1 → steps уровней с мягкими переходами.
            half Band(half x, half steps, half smoothness)
            {
                half s = x * steps;
                half f = floor(s);
                half t = smoothstep(1.0 - smoothness, 1.0, frac(s));
                return saturate((f + t) / steps);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 albedo = tex.rgb * _BaseColor.rgb * input.tint;
                #if defined(_VERTEX_COLORS)
                    albedo *= input.color.rgb;
                #endif

                float3 n = normalize(input.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                if (_Cull < 0.5 && dot(n, v) < 0) n = -n; // двусторонние листья: изнанка освещается как лицо
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(screenUV);

                // --- главный (солнечный) свет
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half ndl = saturate(dot(n, mainLight.direction));
                half lit = ndl * mainLight.shadowAttenuation;
                half band = Band(lit, _Steps, _StepSmooth);
                half3 light = lerp(_ShadowColor.rgb, half3(1, 1, 1), band) * mainLight.color * ao.directAmbientOcclusion;

                // --- точечные источники (костёр, факелы): тоже ступенчато
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalizedScreenSpaceUV = screenUV;
                    uint lightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(lightCount)
                        Light l = GetAdditionalLight(lightIndex, input.positionWS);
                        // мягкий «бассейн» света вместо ступенек: ступенчатый точечный свет рисует на земле жёсткие круги
                        half a = saturate(dot(n, l.direction)) * l.distanceAttenuation;
                        half att = l.distanceAttenuation;
                        half aSoft = saturate((dot(n, l.direction) + 0.25) / 1.25) * att / (1.0 + att); // мягкое насыщение: вблизи источника не «выжигает»
                        light += l.color * lerp(Band(saturate(a), _Steps, _StepSmooth), aSoft, _PointSmooth);
                    LIGHT_LOOP_END
                #endif

                // --- окружение (небо/ambient) — подсвечивает тени
                half3 ambient = SampleSH(n) * _AmbientStrength * ao.indirectAmbientOcclusion;

                half3 color = albedo * (light + ambient);

                // --- rim: светлый ободок по силуэту, сильнее на освещённой стороне
                half rim = pow(1.0 - saturate(dot(n, v)), _RimPower);
                color += _RimColor.rgb * rim * _RimStrength * (0.35 + 0.65 * band);
                color += _EmissionColor.rgb * lerp(half3(1, 1, 1), albedo, _EmissionFromAlbedo);

                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ тени
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(ApplyWind(input.positionOS.xyz));
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                o.positionCS = ApplyShadowClamping(positionCS);
                return o;
            }

            half4 ShadowFrag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }

        // ------------------------------------------------------------------ глубина (нужна для SSAO, мягких частиц)
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = TransformObjectToHClip(ApplyWind(input.positionOS.xyz));
                return o;
            }

            half DepthFrag(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }

        // ------------------------------------------------------------------ нормали (SSAO в шаблоне URP включён)
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
            };

            Varyings NormalsVert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = TransformObjectToHClip(ApplyWind(input.positionOS.xyz));
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
