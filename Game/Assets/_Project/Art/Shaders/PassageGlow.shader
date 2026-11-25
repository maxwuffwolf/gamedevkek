// «Глубина» прохода между зонами (как в PoE): в покое — тёмная пустота в зарослях, на краях растворяется
// в листве; при наведении курсора (_Hover 0..1, его крутит скрипт PassageHighlight) проход заливается
// бледным светом из глубины — ярко в центре, мягко к краям. HDR → подхватывает Bloom.
// UV меша: u 0..1 поперёк прохода, v 0..1 снизу вверх (меш строит ZoneGateBuilder).
Shader "Game/Passage Glow"
{
    Properties
    {
        _DarkColor ("Idle (darkness)", Color) = (0.015, 0.02, 0.05, 1)
        [HDR] _GlowColor ("Hover glow (HDR)", Color) = (1.6, 1.8, 2.4, 1)
        _IdleGlow ("Idle faint glow", Range(0, 1)) = 0.06
        _Hover ("Hover 0..1", Range(0, 1)) = 0
        _EdgeSoftness ("Edge softness", Range(0.05, 1)) = 0.35
        _Pulse ("Pulse speed", Float) = 1.2
        _MaskCenter ("Mask center (UV)", Vector) = (0.5, 0.42, 0, 0)
        _MaskScale ("Mask scale (UV)", Vector) = (2.0, 1.6, 0, 0)
        _Alpha ("Alpha", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Passage"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DarkColor;
                half4 _GlowColor;
                half _IdleGlow;
                half _Hover;
                half _EdgeSoftness;
                half _Pulse;
                float4 _MaskCenter;
                float4 _MaskScale;
                half _Alpha;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                // «капля» света: центр чуть ниже середины, к краям и к низу (земля) гаснет
                // (для плоской «тропы» на земле центр и масштаб маски свои — см. ZoneGateBuilder)
                float2 c = (i.uv - _MaskCenter.xy) * _MaskScale.xy;
                half d = length(c);
                half core = saturate(1.0 - d);
                half edge = smoothstep(0.0, _EdgeSoftness, core);       // мягкий край — сливается с листвой
                half pulse = 0.85 + 0.15 * sin(_Time.y * _Pulse * 6.2831);
                half glowAmt = saturate(_IdleGlow * pulse + _Hover) * (0.35 + 0.65 * core * core);
                half3 col = lerp(_DarkColor.rgb, _GlowColor.rgb, glowAmt);
                half alpha = edge * lerp(0.92, 1.0, _Hover) * _Alpha;
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
