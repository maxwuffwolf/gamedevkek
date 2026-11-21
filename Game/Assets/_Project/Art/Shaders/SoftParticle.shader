// Мягкая круглая частица без текстуры (URP): дымка, светлячки, искорки росы.
// Круг рисуется математикой из UV, цвет = цвет частицы × _Color × _Intensity.
// Смешивание задаётся в материале: Alpha (дымка) или Additive (светящиеся искры, их подхватывает Bloom).
// Soft Fade: частица растворяется там, где касается земли/моделей (нужен Depth Texture, в URP-ассете он включён).
Shader "Game/Soft Particle"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0, 8)) = 1
        _Softness ("Edge softness", Range(0.2, 4)) = 1.5
        _NoiseStrength ("Cloudy noise", Range(0, 1)) = 0
        _SoftFade ("Soft fade distance (m)", Range(0, 5)) = 1.5
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst blend", Float) = 10
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "Particle"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _Softness;
                half _NoiseStrength;
                half _SoftFade;
                half _SrcBlend;
                half _DstBlend;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color       : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color       : COLOR;
                float2 uv         : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half fogFactor    : TEXCOORD3;
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
                o.screenPos = ComputeScreenPos(pos.positionCS);
                o.color = input.color;
                o.uv = input.uv;
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            half Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 c = input.uv * 2.0 - 1.0;
                half circle = pow(saturate(1.0 - dot(c, c)), _Softness);
                if (_NoiseStrength > 0.001)
                {
                    float2 p = input.uv * 3.0 + input.positionWS.xz * 0.05 + _Time.y * 0.03;
                    half n = Noise(p) * 0.65 + Noise(p * 2.3) * 0.35;
                    circle *= lerp(1.0, saturate(n * 1.8 - 0.2), _NoiseStrength);
                }

                // мягкое касание с геометрией
                half fade = 1.0;
                if (_SoftFade > 0.001)
                {
                    float2 suv = input.screenPos.xy / input.screenPos.w;
                    float sceneEye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                    float selfEye = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                    fade = saturate((sceneEye - selfEye) / _SoftFade);
                }

                half a = circle * fade * input.color.a * _Color.a;
                half3 rgb = input.color.rgb * _Color.rgb * _Intensity;
                bool additive = _DstBlend < 1.5; // One = 1
                if (additive)
                {
                    rgb *= a;
                    rgb = MixFogColor(rgb, half3(0, 0, 0), input.fogFactor);
                    return half4(rgb, a);
                }
                rgb = MixFog(rgb, input.fogFactor);
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
