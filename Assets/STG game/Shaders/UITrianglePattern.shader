// UI 面板背景：淺藍到白的漸層 (由上往下)，疊一層低透明度的三角形圖案，圖案緩慢往斜上方移動。
// 給開始畫面左側面板這類大面積 UI 用 (Image 的頂點顏色會乘上去，可整體淡入淡出)
Shader "STG/UITrianglePattern"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _TopColor ("Top Color", Color) = (0.86, 0.93, 1, 1)
        _BottomColor ("Bottom Color", Color) = (0.97, 0.985, 1, 1)
        _TriangleColor ("Triangle Color", Color) = (0.29, 0.62, 0.96, 1)
        _TriangleAlpha ("Triangle Alpha (最深的三角形)", Range(0, 1)) = 0.12
        _CellSize ("Cell Size (三角形邊長，UI 像素)", Float) = 180
        _Angle ("Angle", Float) = -14
        _ScrollSpeed ("Scroll Speed (每秒移動的格數)", Vector) = (0.04, 0.02, 0, 0)
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
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 local : TEXCOORD0;
                float2 uv : TEXCOORD1;
                fixed4 color : COLOR;
            };

            fixed4 _TopColor;
            fixed4 _BottomColor;
            fixed4 _TriangleColor;
            float _TriangleAlpha;
            float _CellSize;
            float _Angle;
            float4 _ScrollSpeed;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                // 用物件座標 (UI 像素) 算圖案，面板拉長時三角形不會跟著變形
                o.local = v.vertex.xy;
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color = lerp(_BottomColor, _TopColor, saturate(i.uv.y));

                float angle = radians(_Angle);
                float2x2 rotation = float2x2(cos(angle), -sin(angle), sin(angle), cos(angle));
                float2 q = mul(rotation, i.local) / _CellSize + _Time.y * _ScrollSpeed.xy;
                float2 skew = float2(q.x - q.y * 0.57735, q.y * 1.1547);
                float2 cell = floor(skew);
                float2 f = frac(skew);
                bool upper = f.x + f.y > 1;
                float random = Hash(cell * 2 + (upper ? 1 : 0));
                // 只有部分三角形上色，深淺不一
                float strength = random > 0.55 ? (random - 0.55) / 0.45 : 0;
                color.rgb = lerp(color.rgb, _TriangleColor.rgb, strength * _TriangleAlpha);
                return color * i.color;
            }
            ENDCG
        }
    }
}
