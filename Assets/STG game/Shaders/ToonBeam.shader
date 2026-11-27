// 卡通風格光束：依視角把表面分成三段純色 (中心亮芯 → 主色 → 邊緣深色)，再用背面外擴畫一圈描邊。
// 描邊只往「垂直於光束長度軸 (_OutlineAxis)」的方向外擴，管子沿長度拉長時描邊粗細才不會跟著被放大
// (內建 Cylinder 沿 Y 軸 = (0,1,0)；戰鬥雷射的 BossBeam_Tube 沿 Z 軸 = (0,0,1)；球設 (0,0,0))。
// 波動 (戰鬥用，_WaveAmplitude > 0 才有)：一波波鼓起沿長度軸往前推進，鼓起處亮芯變寬；
// 波長以世界公尺計算 (由物件在長度軸上的縮放換算)，不需要腳本另外傳光束長度。開始畫面幅度為 0，完全靜止
Shader "STG/ToonBeam"
{
    Properties
    {
        _CoreColor ("Core Color (中心亮芯)", Color) = (1, 1, 1, 1)
        _MainColor ("Main Color (主色)", Color) = (0.35, 0.85, 1, 1)
        _EdgeColor ("Edge Color (邊緣色)", Color) = (0.1, 0.35, 0.9, 1)
        _CoreSize ("Core Size (亮芯佔的比例，0~1)", Range(0, 1)) = 0.55
        _EdgeSize ("Edge Size (邊緣色佔的比例，0~1)", Range(0, 1)) = 0.2
        _OutlineColor ("Outline Color", Color) = (0.08, 0.1, 0.2, 1)
        _OutlineWidth ("Outline Width (網格單位)", Range(0, 0.3)) = 0.08
        _OutlineAxis ("Length Axis (網格的長度軸，描邊不往這個方向推)", Vector) = (0, 1, 0, 0)
        _WaveAmplitude ("Wave Amplitude (鼓起的比例，0 = 靜止)", Range(0, 1)) = 0
        _WaveLength ("Wave Length (公尺)", Float) = 20
        _WaveSpeed ("Wave Speed (公尺/秒，往長度軸正方向)", Float) = 60
        _WaveCoreBoost ("Wave Core Boost (鼓起處亮芯多寬)", Range(0, 1)) = 0.25
        [Toggle] _UseCustomTime ("Use Custom Time (用腳本給的時間，開始畫面慢動作用)", Float) = 0
        _CustomTime ("Custom Time", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _CoreColor;
            half4 _MainColor;
            half4 _EdgeColor;
            half _CoreSize;
            half _EdgeSize;
            half4 _OutlineColor;
            half _OutlineWidth;
            float4 _OutlineAxis;
            half _WaveAmplitude;
            float _WaveLength;
            float _WaveSpeed;
            half _WaveCoreBoost;
            float _UseCustomTime;
            float _CustomTime;
        CBUFFER_END

        // 長度軸 (單位向量)；設成 (0,0,0) = 球之類沒有長度方向的網格
        float3 LengthAxis()
        {
            float axisLength = length(_OutlineAxis.xyz);
            return axisLength > 0.001 ? _OutlineAxis.xyz / axisLength : float3(0, 0, 0);
        }

        // 動畫時間：一般用全域時間；開始畫面由 IntroShaderTime 給放慢的時間
        float BeamTime()
        {
            return _UseCustomTime > 0.5 ? _CustomTime : _Time.y;
        }

        // 波動：回傳鼓起後的頂點位置，wave = 0 (谷) ~ 1 (峰)。沒有長度軸或幅度為 0 時不變形
        float3 ApplyWave(float3 positionOS, out half wave)
        {
            wave = 0;
            float3 axis = LengthAxis();
            if (_WaveAmplitude <= 0 || dot(axis, axis) < 0.5) return positionOS;

            float along = dot(positionOS, axis);
            float metersPerUnit = length(mul((float3x3)UNITY_MATRIX_M, axis));
            float meters = along * metersPerUnit;
            wave = sin((meters - BeamTime() * _WaveSpeed) / max(_WaveLength, 0.01) * 6.2831853) * 0.5 + 0.5;
            float3 radial = positionOS - axis * along;
            return axis * along + radial * (1 + _WaveAmplitude * wave);
        }
        ENDHLSL

        // 描邊：只畫背面，頂點 (跟著波動鼓起) 沿法線 (去掉長度軸分量) 往外推
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                half wave;
                float3 position = ApplyWave(input.positionOS.xyz, wave);
                float3 axis = LengthAxis();
                float3 push = input.normalOS - axis * dot(input.normalOS, axis);
                output.positionCS = TransformObjectToHClip(position + push * _OutlineWidth);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        // 本體：越正對鏡頭 (光束中心) 越亮，三段色塊硬切換；波峰處亮芯變寬
        Pass
        {
            Name "ToonBeam"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half wave : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                half wave;
                output.positionWS = TransformObjectToWorld(ApplyWave(input.positionOS.xyz, wave));
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.wave = wave;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                // facing = 1 在光束中心 (正對鏡頭)，0 在輪廓邊緣
                half facing = saturate(abs(dot(normalize(input.normalWS), viewDir)));
                half coreSize = saturate(_CoreSize + _WaveCoreBoost * input.wave);
                half4 color = facing < _EdgeSize ? _EdgeColor : _MainColor;
                return facing > 1 - coreSize ? _CoreColor : color;
            }
            ENDHLSL
        }
    }
}
