// Boss 雷射光束：圓管網格 (半徑 0.5、沿本地 Z 從 0 到 1) 的頂點沿半徑方向依正弦波鼓起，
// 波形沿光束往前推進，看起來像一波波往外湧的能量；波峰同時更亮，雜訊貼圖沿光束高速流動。
// 加算混合、雙面、不寫深度。_BeamLength / _Intensity 由 BossBeamVisual 以 MaterialPropertyBlock 每幀設定
Shader "STG/BossBeamWave"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (2, 0.6, 1.6, 1)
        [HDR] _CrestColor ("Crest Color (波峰)", Color) = (3, 2.4, 3, 1)
        _NoiseTex ("Noise", 2D) = "white" {}
        _NoiseTiling ("Noise Tiling (每公尺)", Float) = 0.04
        _ScrollSpeed ("Noise Scroll Speed", Float) = 3
        _WaveAmplitude ("Wave Amplitude (半徑比例)", Range(0, 1)) = 0.35
        _WaveLength ("Wave Length (公尺)", Float) = 14
        _WaveSpeed ("Wave Speed (公尺/秒)", Float) = 60
        _EdgePower ("Edge Softness", Range(0.1, 8)) = 1.5
        _FlareScale ("Start Flare Scale (起點光團寬度，管寬的倍數)", Float) = 1
        _FlareLength ("Start Flare Length (起點光團長度，公尺)", Float) = 22
        _FlareViewPush ("Start Flare View Push (光團往鏡頭推幾公尺，蓋在眼球表面前)", Float) = 12
        [HDR] _FlareColor ("Start Flare Color (起點光團中央)", Color) = (1.6, 2.2, 3.4, 1)
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend (One = 加算發光；OneMinusSrcAlpha = 不透明感，蓋住眼球)", Float) = 1
        _BeamLength ("Beam Length", Float) = 50
        _Intensity ("Intensity", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+20" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha [_DstBlend]
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
                float4 _Color;
                float4 _CrestColor;
                float4 _NoiseTex_ST;
                float _NoiseTiling;
                float _ScrollSpeed;
                float _WaveAmplitude;
                float _WaveLength;
                float _WaveSpeed;
                float _EdgePower;
                float _FlareScale;
                float _FlareLength;
                float _FlareViewPush;
                float4 _FlareColor;
                float _BeamLength;
                float _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float wave : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 viewWS : TEXCOORD3;
                float flare : TEXCOORD4;
            };

            // 沿光束的距離 (公尺) 上的波形，0 = 波谷、1 = 波峰；起點與終點附近收斂，避免兩端也在鼓動
            // 起點的球形光團：前 _FlareLength 公尺照球的剖面鼓起，光束看起來是從整顆眼球裡噴出來 (0~1)
            float FlareAt(float along01)
            {
                float x = along01 * _BeamLength / max(_FlareLength, 0.01) * 2.0 - 1.0;
                return sqrt(saturate(1.0 - x * x));
            }

            float WaveAt(float along01)
            {
                float meters = along01 * _BeamLength;
                float phase = (meters - _Time.y * _WaveSpeed) / max(_WaveLength, 0.01) * 6.2831853;
                float ends = smoothstep(0.0, 0.04, along01) * smoothstep(1.0, 0.92, along01);
                return (sin(phase) * 0.5 + 0.5) * ends;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float wave = WaveAt(input.uv.y);

                // 圓管頂點的 XY 就是半徑方向：依波形往外推
                float3 position = input.positionOS.xyz;
                float flare = FlareAt(input.uv.y);
                position.xy *= max(1.0 + _WaveAmplitude * wave, _FlareScale * flare);

                // 光團的中心在眼球裡，會被眼球擋住：依光團剖面往鏡頭推，讓光團蓋在整顆眼睛前面
                float3 positionWS = TransformObjectToWorld(position);
                // 光團用球面法線 (圓管法線朝側面，從光束正前方看會整團被當成邊緣而變透明)
                float3 flareCenterWS = TransformObjectToWorld(float3(0.0, 0.0, 0.5 * _FlareLength / max(_BeamLength, 0.01)));
                float3 sphereNormalWS = normalize(positionWS - flareCenterWS + 1e-5);
                output.normalWS = normalize(lerp(TransformObjectToWorldNormal(input.normalOS), sphereNormalWS, saturate(flare * 4.0)));
                positionWS += normalize(GetCameraPositionWS() - positionWS) * (_FlareViewPush * flare);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.viewWS = GetWorldSpaceViewDir(positionWS);
                output.uv = input.uv;
                output.wave = wave;
                output.flare = flare;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 正對鏡頭的中央最亮、邊緣漸淡，圓管看起來像一束有體積的光
                float facing = abs(dot(normalize(input.normalWS), normalize(input.viewWS)));
                float body = pow(saturate(facing), _EdgePower);

                float2 noiseUV = float2(input.uv.x, input.uv.y * _BeamLength * _NoiseTiling - _Time.y * _ScrollSpeed);
                float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r;

                float3 color = lerp(_Color.rgb, _CrestColor.rgb, input.wave) * (0.65 + 0.7 * noise);
                float alpha = saturate(body * (0.55 + 0.45 * input.wave)) * _Color.a;
                // 光團跟光束同色系：中央偏白、往邊緣轉藍淡出 (圓管法線對光團不準，以光團剖面補亮)
                float3 flareColor = lerp(_Color.rgb, _FlareColor.rgb, body) * (0.75 + 0.5 * noise);
                color = lerp(color, flareColor, input.flare);
                alpha = max(alpha, input.flare * _FlareColor.a * lerp(0.35, 1.0, body)) * _Intensity;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
