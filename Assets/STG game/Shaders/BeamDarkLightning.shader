// 光束外圍的黑色雷電 (Binah 光炮)：套在實心光柱外面、稍粗的透明圓管上，畫幾條鋸齒狀的黑色電弧，
// 沿光束螺旋前進；每隔一小段時間 (_FlickerRate 次/秒) 每條電弧重新決定形狀與是否出現，看起來在閃動。
// 網格要有 uv：uv.x 繞一圈 (0~1)、uv.y 沿長度 (0~1)；用物件在長度軸 (Y) 的縮放換算公尺，電弧的鋸齒大小不會因光束長短而變形
Shader "STG/BeamDarkLightning"
{
    Properties
    {
        _BoltColor ("Bolt Color (雷電主色)", Color) = (0.04, 0.01, 0.02, 0.95)
        _RimColor ("Rim Color (雷電邊緣的暗紅光)", Color) = (0.55, 0.08, 0.04, 0.6)
        _BoltCount ("Bolt Count (電弧條數)", Range(1, 6)) = 6
        _Thickness ("Thickness (線寬，繞一圈的比例)", Range(0.002, 0.1)) = 0.045
        _RimWidth ("Rim Width (邊緣光的寬度倍數)", Range(1, 4)) = 2.2
        _SegmentLength ("Segment Length (鋸齒每段的長度，公尺)", Float) = 9
        _Jitter ("Jitter (鋸齒的左右擺動，繞一圈的比例)", Range(0, 0.3)) = 0.1
        _Twist ("Twist (每公尺繞多少圈)", Float) = 0.002
        _Speed ("Speed (沿光束流動，公尺/秒)", Float) = 45
        _FlickerRate ("Flicker Rate (每秒換幾次形狀)", Float) = 14
        _Visible ("Visible (每次出現的機率)", Range(0, 1)) = 0.85
        [Toggle] _UseCustomTime ("Use Custom Time (用腳本給的時間，開始畫面慢動作用)", Float) = 0
        _CustomTime ("Custom Time", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float meters : TEXCOORD1;
            };

            fixed4 _BoltColor;
            fixed4 _RimColor;
            float _BoltCount;
            float _Thickness;
            float _RimWidth;
            float _SegmentLength;
            float _Jitter;
            float _Twist;
            float _Speed;
            float _FlickerRate;
            float _Visible;
            float _UseCustomTime;
            float _CustomTime;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                // 物件 Y 軸方向 1 單位 = 幾公尺 (光束長度靠拉長 Y 縮放)
                float metersPerUnit = length(mul((float3x3)UNITY_MATRIX_M, float3(0, 1, 0)));
                // 內建規格的圓柱高 2 (uv.y 0~1 對應 -1~1)
                o.meters = v.uv.y * 2 * metersPerUnit;
                return o;
            }

            float Hash(float n)
            {
                return frac(sin(n * 127.1) * 43758.5453);
            }

            // 兩個角度 (0~1，繞一圈) 之間的最短距離
            float AngleDistance(float a, float b)
            {
                float d = abs(frac(a - b + 0.5) - 0.5);
                return d;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 一般用全域時間；開始畫面由 IntroShaderTime 給放慢的時間
                float time = _UseCustomTime > 0.5 ? _CustomTime : _Time.y;
                float flicker = floor(time * _FlickerRate);
                float along = i.meters - time * _Speed;
                float segment = along / max(_SegmentLength, 0.01);
                float segmentIndex = floor(segment);
                float segmentT = frac(segment);

                float core = 0;
                float rim = 0;
                for (int k = 0; k < 6; k++)
                {
                    if (k >= _BoltCount) break;

                    float seed = k * 17.13 + flicker * 3.7;
                    if (Hash(seed) > _Visible) continue;

                    // 鋸齒：每段頭尾各一個隨機偏移，中間直線連起來
                    float a0 = (Hash(seed + segmentIndex * 1.31) - 0.5) * 2 * _Jitter;
                    float a1 = (Hash(seed + (segmentIndex + 1) * 1.31) - 0.5) * 2 * _Jitter;
                    float baseAngle = k / _BoltCount + Hash(seed + 0.5) * 0.2 + i.meters * _Twist;
                    float boltAngle = baseAngle + lerp(a0, a1, segmentT);
                    float d = AngleDistance(i.uv.x, boltAngle);

                    // 每條電弧粗細略有不同
                    float thickness = _Thickness * lerp(0.6, 1.3, Hash(seed + 9.1));
                    core = max(core, 1 - smoothstep(thickness * 0.6, thickness, d));
                    rim = max(rim, 1 - smoothstep(thickness, thickness * _RimWidth, d));
                }

                // 光束兩端淡出，不會在發射點、落點突然切斷
                float ends = smoothstep(0, 0.05, i.uv.y) * smoothstep(1, 0.95, i.uv.y);
                fixed4 color = lerp(fixed4(_RimColor.rgb, _RimColor.a * rim), _BoltColor, core);
                color.a *= ends;
                return color;
            }
            ENDCG
        }
    }
}
