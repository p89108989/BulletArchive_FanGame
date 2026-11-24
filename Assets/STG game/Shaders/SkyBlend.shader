// 天空球：在兩張天空貼圖間交叉淡化 (白天 ↔ 黃昏 ↔ 黑夜)，不受光照。
// _MainTex = 起始貼圖、_BlendTex = 目標貼圖、_Blend = 0~1，由 SkyEnvironment 以 MaterialPropertyBlock 設定
Shader "STG/SkyBlend"
{
    Properties
    {
        _MainTex ("From Texture", 2D) = "white" {}
        _BlendTex ("To Texture", 2D) = "white" {}
        _Blend ("Blend", Range(0, 1)) = 1
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Cull [_Cull]

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_BlendTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BlendTex_ST;
                float _Blend;
                float4 _Color;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 from = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 to = SAMPLE_TEXTURE2D(_BlendTex, sampler_MainTex, input.uv);
                return lerp(from, to, _Blend) * _Color;
            }
            ENDHLSL
        }
    }
}
