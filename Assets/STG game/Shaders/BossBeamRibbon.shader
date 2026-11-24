// Boss 雷射的扭轉光帶：平面帶狀網格 (寬 1、X 從 -0.5 到 0.5，沿本地 Z 從 0 到 1) 一邊往前延伸一邊繞光束軸扭轉，
// 像拓樸學的紙帶 (莫比烏斯帶)；扭轉角度隨時間推進，整條帶子看起來在往前旋轉流動，帶寬也依正弦波一鼓一縮。
// 帶子中央最亮、往邊緣轉為藍色並淡出。加算混合、雙面、不寫深度。
// _BeamLength / _Intensity 由 BossBeamVisual 以 MaterialPropertyBlock 每幀設定；_PhaseOffset 讓多條帶子錯開角度
Shader "STG/BossBeamRibbon"
{
    Properties
    {
        [HDR] _CenterColor ("Center Color (帶子中央)", Color) = (3, 3.2, 3.5, 1)
        [HDR] _EdgeColor ("Edge Color (帶子邊緣)", Color) = (0.3, 1.2, 4, 1)
        _NoiseTex ("Noise", 2D) = "white" {}
        _NoiseTiling ("Noise Tiling (每公尺)", Float) = 0.05
        _ScrollSpeed ("Noise Scroll Speed", Float) = 4
        _TwistLength ("Twist Length (轉半圈的長度，公尺)", Float) = 18
        _TwistSpeed ("Twist Speed (每秒轉幾度)", Float) = 540
        _PhaseOffset ("Phase Offset (度)", Float) = 0
        _WaveAmplitude ("Width Wave Amplitude", Range(0, 1)) = 0.3
        _WaveLength ("Width Wave Length (公尺)", Float) = 14
        _WaveSpeed ("Width Wave Speed (公尺/秒)", Float) = 50
        _EdgeSoftness ("Edge Softness", Range(0.5, 6)) = 2
        _FlareScale ("Start Flare Scale (起點光團寬度，帶寬的倍數)", Float) = 1
        _FlareLength ("Start Flare Length (起點光團長度，公尺)", Float) = 22
        _FlareViewPush ("Start Flare View Push (光團往鏡頭推幾公尺，蓋在眼球表面前)", Float) = 12
        _BeamLength ("Beam Length", Float) = 50
        _Intensity ("Intensity", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+21" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _CenterColor;
                float4 _EdgeColor;
                float4 _NoiseTex_ST;
                float _NoiseTiling;
                float _ScrollSpeed;
                float _TwistLength;
                float _TwistSpeed;
                float _PhaseOffset;
                float _WaveAmplitude;
                float _WaveLength;
                float _WaveSpeed;
                float _EdgeSoftness;
                float _FlareScale;
                float _FlareLength;
                float _FlareViewPush;
                float _BeamLength;
                float _Intensity;
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
                float wave : TEXCOORD1;
                float flare : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float meters = input.uv.y * _BeamLength;

                // 帶寬依正弦波鼓縮 (往前推進)；起點與終點附近收斂
                float ends = smoothstep(0.0, 0.03, input.uv.y) * smoothstep(1.0, 0.94, input.uv.y);
                float wave = (sin((meters - _Time.y * _WaveSpeed) / max(_WaveLength, 0.01) * 6.2831853) * 0.5 + 0.5) * ends;
                // 起點的球形光團 (跟 BossBeamWave 同剖面)：帶子在眼球處張開包住整顆眼睛
                float x = meters / max(_FlareLength, 0.01) * 2.0 - 1.0;
                float flare = sqrt(saturate(1.0 - x * x));
                float halfWidth = input.positionOS.x * max(1.0 + _WaveAmplitude * wave, _FlareScale * flare);

                // 沿光束繞軸扭轉：每 _TwistLength 公尺轉半圈，並隨時間持續轉動
                float angle = radians(meters / max(_TwistLength, 0.01) * 180.0 - _Time.y * _TwistSpeed + _PhaseOffset);
                float3 position = float3(halfWidth * cos(angle), halfWidth * sin(angle), input.positionOS.z);

                // 同 BossBeamWave：光團往鏡頭推，蓋在眼球表面前
                float3 positionWS = TransformObjectToWorld(position);
                positionWS += normalize(GetCameraPositionWS() - positionWS) * (_FlareViewPush * flare);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.wave = wave;
                output.flare = flare;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 0 = 帶子中央、1 = 邊緣
                float across = abs(input.uv.x - 0.5) * 2.0;
                float body = pow(saturate(1.0 - across), _EdgeSoftness);

                float2 noiseUV = float2(input.uv.x, input.uv.y * _BeamLength * _NoiseTiling - _Time.y * _ScrollSpeed);
                float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r;

                float3 color = lerp(_EdgeColor.rgb, _CenterColor.rgb, body) * (0.7 + 0.6 * noise) * (0.8 + 0.4 * input.wave);
                // 光團內調暗：加算光疊太多會把藍白色的眼球光團洗成純白
                color *= 1.0 - 0.6 * input.flare;
                float alpha = saturate(body * 1.4) * _CenterColor.a * _Intensity;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
