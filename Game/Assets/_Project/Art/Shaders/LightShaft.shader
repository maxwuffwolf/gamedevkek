// Луч света сквозь кроны (фейковый объёмный свет, URP).
// Меш — две скрещенные полоски (их делает AtmosphereBuilder): U поперёк луча, V вдоль (0 — земля, 1 — верх).
// Аддитивный, без записи глубины. Края и концы мягко гаснут, по лучу медленно «плывёт» пыль,
// а когда смотришь на полоску ребром, она исчезает — так не видно, что это плоскость.
Shader "Game/Light Shaft"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.72, 0.45, 1)
        _Intensity ("Intensity", Range(0, 3)) = 0.35
        _EdgeSoftness ("Edge softness", Range(0.5, 6)) = 2.2
        _BottomFade ("Bottom fade", Range(0.01, 1)) = 0.35
        _TopFade ("Top fade", Range(0.01, 1)) = 0.25
        _DustSpeed ("Dust speed", Range(0, 2)) = 0.15
        _DustScale ("Dust scale", Range(0.5, 20)) = 5
        _DustStrength ("Dust strength", Range(0, 1)) = 0.45
        _Pulse ("Pulse", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Shaft"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _EdgeSoftness;
                half _BottomFade;
                half _TopFade;
                half _DustSpeed;
                half _DustScale;
                half _DustStrength;
                half _Pulse;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half facing       : TEXCOORD1;
                half seed         : TEXCOORD2;
                half fogFactor    : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = input.uv;
                float3 n = TransformObjectToWorldNormal(input.normalOS);
                float3 v = normalize(GetWorldSpaceViewDir(positionWS));
                o.facing = abs(dot(n, v));
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                o.seed = frac(sin(dot(origin.xz, float2(12.9898, 78.233))) * 43758.5453);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = frac(sin(dot(i, float2(127.1, 311.7))) * 43758.5453);
                float b = frac(sin(dot(i + float2(1, 0), float2(127.1, 311.7))) * 43758.5453);
                float c = frac(sin(dot(i + float2(0, 1), float2(127.1, 311.7))) * 43758.5453);
                float d = frac(sin(dot(i + float2(1, 1), float2(127.1, 311.7))) * 43758.5453);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                half across = pow(saturate(sin(uv.x * PI)), _EdgeSoftness);
                half along = smoothstep(0.0, _BottomFade, uv.y) * smoothstep(1.0, 1.0 - _TopFade, uv.y);
                float t = _Time.y * _DustSpeed;
                half dust = Noise(float2(uv.x * _DustScale * 0.6 + input.seed * 7.0, uv.y * _DustScale - t))
                          * 0.6 + Noise(float2(uv.x * _DustScale * 1.7, uv.y * _DustScale * 2.3 - t * 1.7)) * 0.4;
                half dustMul = lerp(1.0, dust * 1.6, _DustStrength);
                half pulse = 1.0 - _Pulse * (0.5 + 0.5 * sin(_Time.y * 0.6 + input.seed * 6.2831));
                half edgeOn = smoothstep(0.05, 0.45, input.facing);
                half a = across * along * dustMul * pulse * edgeOn * _Intensity;
                half3 col = _Color.rgb * a;
                // туман гасит дальние лучи (аддитив: смешиваем с чёрным)
                col = MixFogColor(col, half3(0, 0, 0), input.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
