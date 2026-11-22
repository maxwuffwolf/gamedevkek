// Toon-шейдер для персонажей (URP, Unity 6). Тот же свет, что у окружения (Game/Stylized Toon):
// ступени света, цветная тень, rim по силуэту. Плюс то, что нужно герою:
//   * карта нормалей (рельеф мышц из базы Quaternius читается и в toon-свете);
//   * слой грязи/пепла/царапин (_DirtAmount 0..1): процедурный по UV, плюс необязательная маска-текстура;
//     после падения 1, умылся или надел броню — плавно уводим в 0 (HeroAppearance);
//   * режим глаз (_EYES): альфа текстуры = маска радужки, цвет вершин R = какой глаз (1 правый, 0 левый),
//     у каждого глаза свой цвет и лёгкое свечение — гетерохромия;
//   * контур (_OutlineWidth > 0, в пикселях при 1080p): тёмная обводка «вывернутой оболочкой» — герой не сливается с землёй.
// Цвет = текстура × Tint × (цвет вершин, если включено).
Shader "Game/Character Toon"
{
    Properties
    {
        [Header(Color)]
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [Toggle(_VERTEX_COLORS)] _UseVertexColors ("Use vertex colors", Float) = 0
        [Toggle(_NORMALMAP)] _UseNormalMap ("Use normal map", Float) = 0
        [NoScaleOffset][Normal] _BumpMap ("Normal map", 2D) = "bump" {}
        _BumpScale ("Normal strength", Range(0, 2)) = 1

        [Header(Toon lighting)]
        _ShadowColor ("Shadow tint", Color) = (0.45, 0.42, 0.62, 1)
        [IntRange] _Steps ("Light steps", Range(1, 5)) = 2
        _StepSmooth ("Step softness", Range(0.001, 0.5)) = 0.12
        _PointSmooth ("Point lights: 0 toon steps, 1 soft pools", Range(0, 1)) = 1
        _AmbientStrength ("Ambient strength", Range(0, 1)) = 0.4

        [Header(Rim)]
        _RimColor ("Rim color", Color) = (1, 0.92, 0.8, 1)
        _RimPower ("Rim power", Range(0.5, 8)) = 3
        _RimStrength ("Rim strength", Range(0, 1)) = 0.25

        [Header(Outline)]
        _OutlineWidth ("Outline width (px at 1080p, 0 = off)", Range(0, 6)) = 0
        _OutlineColor ("Outline color", Color) = (0.03, 0.02, 0.05, 1)

        [Header(Dirt ash scratches)]
        _DirtAmount ("Dirt amount", Range(0, 1)) = 0
        [NoScaleOffset] _DirtMask ("Dirt mask (R, by UV)", 2D) = "white" {}
        _DirtColor ("Dirt / ash color", Color) = (0.36, 0.34, 0.32, 1)
        _DirtScale ("Dirt noise scale", Float) = 38
        _ScratchColor ("Scratch color", Color) = (0.55, 0.2, 0.16, 1)

        [Header(Eyes)]
        [Toggle(_EYES)] _Eyes ("Eye mode (heterochromia)", Float) = 0
        _EyeRight ("Right eye", Color) = (1, 0.7, 0.12, 1)
        _EyeLeft ("Left eye", Color) = (0.36, 0.16, 0.62, 1)
        _EyeGlow ("Eye glow", Range(0, 3)) = 0.6

        [Header(Emission)]
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)

        [Header(Render)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);  SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);  SAMPLER(sampler_BumpMap);
        TEXTURE2D(_DirtMask); SAMPLER(sampler_DirtMask);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _BumpScale;
            half4 _ShadowColor;
            half _Steps;
            half _StepSmooth;
            half _PointSmooth;
            half _AmbientStrength;
            half4 _RimColor;
            half _RimPower;
            half _RimStrength;
            half _OutlineWidth;
            half4 _OutlineColor;
            half _DirtAmount;
            half4 _DirtColor;
            float _DirtScale;
            half4 _ScratchColor;
            half4 _EyeRight;
            half4 _EyeLeft;
            half _EyeGlow;
            half4 _EmissionColor;
            half _Cull;
        CBUFFER_END
        ENDHLSL

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
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _EYES

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
                float4 tangentOS  : TANGENT;
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
                float4 tangentWS  : TEXCOORD3;
                half4 color       : TEXCOORD4;
                half fogFactor    : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = nrm.normalWS;
                o.tangentWS = float4(nrm.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                half4 vc = input.color;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    vc.rgb = SRGBToLinear(vc.rgb);
                #endif
                o.color = vc;
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half Band(half x, half steps, half smoothness)
            {
                half s = x * steps;
                half f = floor(s);
                half t = smoothstep(1.0 - smoothness, 1.0, frac(s));
                return saturate((f + t) / steps);
            }

            // дешёвый value noise по UV (стабилен при анимации, в отличие от позиции)
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
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
                return ValueNoise(p) * 0.55 + ValueNoise(p * 2.13 + 7.1) * 0.3 + ValueNoise(p * 4.7 + 3.3) * 0.15;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 albedo = tex.rgb * _BaseColor.rgb;
                #if defined(_VERTEX_COLORS)
                    albedo *= input.color.rgb;
                #endif

                half3 emission = _EmissionColor.rgb;

                #if defined(_EYES)
                    // радужка: яркость из текстуры × цвет глаза; R цвета вершин выбирает глаз
                    half iris = tex.a;
                    half3 eyeColor = lerp(_EyeLeft.rgb, _EyeRight.rgb, step(0.5, input.color.r));
                    half irisLum = saturate(dot(tex.rgb, half3(0.3, 0.59, 0.11)) * 2.2);
                    albedo = lerp(albedo, eyeColor * (0.35 + irisLum), iris);
                    emission += eyeColor * iris * _EyeGlow * (0.4 + irisLum);
                #endif

                // --- грязь, пепел и царапины
                half dirt = _DirtAmount * SAMPLE_TEXTURE2D(_DirtMask, sampler_DirtMask, input.uv).r;
                if (dirt > 0.001)
                {
                    float2 duv = input.uv * _DirtScale;
                    float n = Fbm(duv);
                    half blotch = smoothstep(1.0 - dirt * 0.85, 1.15 - dirt * 0.85, n + 0.15);
                    half ash = step(0.86, ValueNoise(duv * 6.0)) * dirt;          // мелкие светлые крупинки пепла
                    float sc = ValueNoise(input.uv * float2(_DirtScale * 7.0, _DirtScale * 0.6));
                    half scratch = smoothstep(0.035, 0.0, abs(sc - 0.5)) * step(0.62, ValueNoise(duv * 0.7)) * dirt;
                    albedo = lerp(albedo, albedo * 0.55 + _DirtColor.rgb * 0.45, blotch);
                    albedo = lerp(albedo, _DirtColor.rgb * 1.5, ash * 0.6);
                    albedo = lerp(albedo, _ScratchColor.rgb, scratch * 0.8);
                    albedo *= 1.0 - 0.12 * dirt;                                  // общий слой пыли
                }

                // --- нормаль
                float3 n = normalize(input.normalWS);
                #if defined(_NORMALMAP)
                    half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                    float sgn = input.tangentWS.w;
                    float3 bitangent = sgn * cross(n, input.tangentWS.xyz);
                    n = normalize(TransformTangentToWorld(nTS, half3x3(input.tangentWS.xyz, bitangent, n)));
                #endif

                float3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
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
                        // мягкий «бассейн» света вместо ступенек: ступенчатый точечный свет рисует на земле жёсткие круги
                        half a = saturate(dot(n, l.direction)) * l.distanceAttenuation;
                        half att = l.distanceAttenuation;
                        half aSoft = saturate((dot(n, l.direction) + 0.25) / 1.25) * att / (1.0 + att); // мягкое насыщение: вблизи источника не «выжигает»
                        light += l.color * lerp(Band(saturate(a), _Steps, _StepSmooth), aSoft, _PointSmooth);
                    LIGHT_LOOP_END
                #endif

                half3 ambient = SampleSH(n) * _AmbientStrength * ao.indirectAmbientOcclusion;
                half3 color = albedo * (light + ambient);
                half rim = pow(1.0 - saturate(dot(n, v)), _RimPower);
                // rim почти не зависит от луны: в темноте силуэт персонажа должен оставаться обведённым светом
                color += _RimColor.rgb * rim * _RimStrength * (0.65 + 0.35 * band);
                color += emission;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // контур: оболочка, раздутая по нормалям в экранном пространстве (толщина в пикселях не зависит от зума камеры),
        // рисуется задними гранями. SRPDefaultUnlit — URP рисует этот проход вместе с основным.
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half fogFactor : TEXCOORD0; };

            Varyings OutlineVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                float4 posCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(input.normalOS);
                float2 dir = mul((float3x3)UNITY_MATRIX_VP, nWS).xy;
                float len = length(dir);
                dir = len > 1e-5 ? dir / len : float2(0, 0);
                // ширина в пикселях при 1080p → в единицах NDC (экран = 2 единицы); * w — чтобы не уменьшалась с расстоянием
                float2 ndcPerPixel = 2.0 / _ScreenParams.xy;
                posCS.xy += dir * _OutlineWidth * (_ScreenParams.y / 1080.0) * ndcPerPixel * posCS.w;
                o.positionCS = _OutlineWidth > 0.001 ? posCS : float4(0, 0, -2, 1); // выключен — выносим за ближнюю плоскость
                o.fogFactor = ComputeFogFactor(posCS.z);
                return o;
            }

            half4 OutlineFrag(Varyings input) : SV_Target
            {
                return half4(MixFog(_OutlineColor.rgb, input.fogFactor), 1);
            }
            ENDHLSL
        }

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

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings ShadowVert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
                return o;
            }

            half4 ShadowFrag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }

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
            Cull [_Cull]

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
