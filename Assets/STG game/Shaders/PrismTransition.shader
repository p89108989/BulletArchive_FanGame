// 稜鏡轉場 (UI 全螢幕 Image 用)，參考蔚藍檔案的換場影片，兩種模式：
// _Mode 0「碎開」(先壓黑再用)：_Progress 0.5 時瞬間出現藍白頁面 (淺青底 + 白色菱形)，
//   接著頁面裂成三角形、變成半透明玻璃 (看得到底下的新場景)，碎片由中心往外散開並放大 (往鏡頭飛)。
//   _Progress 0 ~ 0.5 不畫 (那段是黑幕)。
// _Mode 1「波浪」：0 ~ 0.5 三角形從左上往右下掃過舊畫面、越疊越多並轉白，0.5 時整面蓋滿；
//   0.5 ~ 1 三角形變回半透明玻璃，再依同一方向掃開露出新場景。
// 原遊戲的稜鏡轉場素材匯出時遺失了貼圖與 shader，所以完全用程式畫
Shader "STG/PrismTransition"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Progress ("Progress", Range(0, 1)) = 0
        _Mode ("Mode (0 = 碎開, 1 = 波浪)", Float) = 0
        _CellSize ("Cell Size (三角形邊長，畫面高度的比例)", Float) = 0.246
        _Angle ("Angle (格子傾斜角度)", Float) = -14
        _PageColor ("Page Color (藍白頁面的底色)", Color) = (0.66, 0.95, 1, 1)
        _PageLight ("Page Light (頁面上的白色菱形)", Color) = (1, 1, 1, 1)
        _ColorA ("Triangle Cyan", Color) = (0.5, 0.88, 1, 1)
        _ColorB ("Triangle Light", Color) = (0.86, 0.98, 1, 1)
        _GlowColor ("Glow (亮白塊)", Color) = (1, 1, 1, 1)
        _EdgeColor ("Edge", Color) = (0.27, 0.56, 0.93, 1)
        _EdgeWidth ("Edge Width (三角形內的比例)", Range(0, 0.3)) = 0.06
        _GlassAlpha ("Glass Alpha (玻璃碎片的不透明度)", Range(0, 1)) = 0.55
        _Spread ("Spread (碎開：由中心往外的延遲)", Range(0, 1)) = 0.45
        _Zoom ("Zoom (碎開：碎片放大的倍數)", Float) = 1.2
        _SweepDir ("Sweep Direction (波浪的方向)", Vector) = (1, -0.5, 0, 0)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
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
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 screen : TEXCOORD0;
                fixed4 color : COLOR;
            };

            float _Progress;
            float _Mode;
            float _CellSize;
            float _Angle;
            fixed4 _PageColor;
            fixed4 _PageLight;
            fixed4 _ColorA;
            fixed4 _ColorB;
            fixed4 _GlowColor;
            fixed4 _EdgeColor;
            float _EdgeWidth;
            float _GlassAlpha;
            float _Spread;
            float _Zoom;
            float4 _SweepDir;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screen = ComputeScreenPos(o.vertex);
                o.color = v.color;
                return o;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // 菱形 (中心 c、半寬 w、半高 h) 的柔和遮罩：1 = 裡面、0 = 外面
            float Diamond(float2 p, float2 c, float w, float h, float softness)
            {
                float d = abs(p.x - c.x) / w + abs(p.y - c.y) / h;
                return 1 - smoothstep(1 - softness, 1 + softness, d);
            }

            // 碎開前的藍白頁面：淺青底 + 中間兩塊白色大菱形 (錯開成 Z 字) + 外圍淡淡的菱形
            fixed3 Page(float2 p)
            {
                float2x2 tilt = float2x2(cos(0.45), -sin(0.45), sin(0.45), cos(0.45));
                float2 r = mul(tilt, p);
                float light = max(Diamond(r, float2(-0.12, 0.02), 0.2, 0.42, 0.02), Diamond(r, float2(0.16, -0.06), 0.2, 0.42, 0.02));
                float haze = max(Diamond(r, float2(-0.55, 0.15), 0.35, 0.7, 0.15), Diamond(r, float2(0.6, -0.2), 0.35, 0.7, 0.15)) * 0.35;
                return lerp(_PageColor.rgb, _PageLight.rgb, saturate(max(light, haze)));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                bool wave = _Mode > 0.5;
                if (_Progress <= 0 || (!wave && _Progress < 0.5)) return fixed4(0, 0, 0, 0);

                float2 uv = i.screen.xy / i.screen.w;
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 p = (uv - 0.5) * float2(aspect, 1);

                // 碎開的時間 (0 ~ 1)
                float s = saturate(_Progress * 2 - 1);
                float crack = smoothstep(0, 0.12, s);
                float glass = smoothstep(0.08, 0.3, s);
                float scatter = saturate((s - 0.12) / 0.88);
                float zoom = wave ? 1 : 1 + _Zoom * pow(scatter, 1.5);

                float angle = radians(_Angle);
                float2x2 rotation = float2x2(cos(angle), -sin(angle), sin(angle), cos(angle));
                float2 q = mul(rotation, p) / (_CellSize * zoom);

                // 正三角形格子：斜座標 (x - y/√3, 2y/√3) 的每個單位菱形切成上下兩個三角形
                float2 skew = float2(q.x - q.y * 0.57735, q.y * 1.1547);
                float2 cell = floor(skew);
                float2 f = frac(skew);
                bool upper = f.x + f.y > 1;
                float3 bary = upper ? float3(1 - f.x, 1 - f.y, f.x + f.y - 1) : float3(f.x, f.y, 1 - f.x - f.y);
                float inner = min(bary.x, min(bary.y, bary.z)) * 3;

                float random = Hash(cell * 2 + (upper ? 1 : 0));
                float2 centerSkew = cell + (upper ? float2(0.6667, 0.6667) : float2(0.3333, 0.3333));
                float2 center = float2(centerSkew.x + centerSkew.y * 0.5, centerSkew.y * 0.866) * _CellSize * zoom;
                float reach = 0.5 * aspect + 0.5;
                fixed3 tint = random > 0.85 ? _GlowColor.rgb : lerp(_ColorA.rgb, _ColorB.rgb, random);

                float size;
                fixed3 fill;
                float alpha;
                float edgeStrength;
                if (wave)
                {
                    // 沿掃動方向的位置 0 (先到) ~ 1 (後到)
                    float a = saturate(dot(center, normalize(_SweepDir.xy)) / (reach * 2) + 0.5);
                    if (_Progress < 0.5)
                    {
                        float c = _Progress * 2;
                        size = saturate((c * 1.6 - a - random * 0.15) / 0.3);
                        float whiten = smoothstep(0.5, 1, c);
                        fill = lerp(tint, lerp(_GlowColor.rgb, tint, step(0.55, random)), whiten);
                        alpha = lerp(_GlassAlpha, 1, whiten);
                    }
                    else
                    {
                        float r = _Progress * 2 - 1;
                        size = 1 - saturate((r * 1.6 - a - random * 0.15) / 0.3);
                        float clear = smoothstep(0, 0.3, r);
                        fill = lerp(lerp(_GlowColor.rgb, tint, step(0.55, random)), tint, clear);
                        alpha = lerp(1, _GlassAlpha, clear);
                    }

                    edgeStrength = 1;
                }
                else
                {
                    float delay = saturate(length(center) / reach) * _Spread + random * 0.1;
                    float span = max(0.05, 1 - _Spread - 0.1);
                    size = 1 - saturate((scatter - delay) / span);
                    fill = lerp(Page(p), tint, crack);
                    alpha = lerp(1, _GlassAlpha, glass);
                    edgeStrength = crack;
                }

                // 每塊三角形從自己的中心縮放
                float threshold = 1 - size;
                if (size <= 0.001 || inner < threshold) return fixed4(0, 0, 0, 0);

                if (inner < threshold + _EdgeWidth)
                {
                    fill = lerp(fill, _EdgeColor.rgb, edgeStrength);
                    alpha = lerp(alpha, 0.95, edgeStrength);
                }

                return fixed4(fill, alpha * i.color.a);
            }
            ENDCG
        }
    }
}
