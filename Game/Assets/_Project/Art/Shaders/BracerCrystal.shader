// Кристалл наруча: две половины, золотая (Свет) и фиолетовая (Тьма).
// Какая грань к какой половине относится — R цвета вершин (1 = Свет), G — случайное число грани для мерцания.
//   _Balance 0..1: 0 — вся сила у Тьмы, 1 — у Света, 0.5 — поровну. Сильная сторона горит ярче, слабая гаснет.
//   _Pulse: вспышка при поглощении энергии (HeroAppearance поднимает и сам гасит).
// Цвет HDR → ловится Bloom'ом из Forest_Dusk_Volume.
Shader "Game/Bracer Crystal"
{
    Properties
    {
        [HDR] _LightColor ("Light (gold)", Color) = (1.0, 0.72, 0.22, 1)
        [HDR] _DarkColor ("Dark (violet)", Color) = (0.55, 0.22, 1.0, 1)
        _Balance ("Light / Dark balance", Range(0, 1)) = 0.5
        _Glow ("Glow", Range(0, 6)) = 1.6
        _Pulse ("Pulse", Range(0, 3)) = 0
        _Sparkle ("Facet sparkle", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _LightColor;
                half4 _DarkColor;
                half _Balance;
                half _Glow;
                half _Pulse;
                half _Sparkle;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                half fog : TEXCOORD3;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color; // R = 0/1, G = случайное: перевод в linear не нужен
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half isLight = step(0.5, i.color.r);
                half wl = smoothstep(0.0, 1.0, _Balance);
                half weight = lerp(1.0 - wl, wl, isLight);           // сила своей стороны 0..1
                half3 baseCol = lerp(_DarkColor.rgb, _LightColor.rgb, isLight);
                half intensity = lerp(0.25, 1.6, weight) * _Glow;

                float3 n = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half facet = 0.65 + 0.35 * saturate(dot(n, v));
                half sparkle = pow(saturate(sin(_Time.y * 2.3 + i.color.g * 40.0) * 0.5 + 0.5), 8.0) * _Sparkle;
                half rim = pow(1.0 - saturate(dot(n, v)), 3.0);

                // вспышка окрашивается в цвет сильной стороны
                half3 pulseCol = lerp(_DarkColor.rgb, _LightColor.rgb, wl);
                half3 c = baseCol * intensity * (facet + sparkle) + baseCol * rim * 0.6 + pulseCol * _Pulse * 2.5;
                c = MixFog(c, i.fog);
                return half4(c, 1);
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
            #pragma vertex V
            #pragma fragment F
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 V(float4 p : POSITION) : SV_POSITION { return TransformObjectToHClip(p.xyz); }
            half F(float4 p : SV_POSITION) : SV_Target { return p.z; }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
