#if UNITY_EDITOR
using UnityEngine;

// 蔚藍檔案 COST 數字牌 (Combat 圖集的 Combat_CostBg，117×123) 的改圖：
// 頂端烙著「COST」字樣，要換成我們自己的小字 (Pulse / Flame)，所以把字所在的上方區域清掉，
// 再依原圖量出的幾何把被字蓋住的外圈 (深色) 與內圓 (藍色) 補畫回來；另外做一張暖色 (Flame) 版本。
// 幾何數值是從原圖可見的外緣擬合出來的 (平均誤差 0.23px)
public static class CostBadgeArt
{
    public const int Width = 117;
    public const int Height = 123;

    // 以下座標原點在左上 (圖片座標)，單位為像素
    private static readonly Vector2 Center = new Vector2(63f, 68.5f);
    private const float OuterRadius = 53.6f;
    private const float InnerRadius = 45.5f;
    // 「COST」字樣 (含外框) 佔用的列，從頂端算到這一列為止
    private const int CaptionBottomRow = 41;

    public static readonly Color32 RingColor = new Color32(6, 26, 52, 229);
    public static readonly Color32 InnerColor = new Color32(3, 66, 139, 243);

    // pixels 為 Texture2D.GetPixels32 的順序 (由下往上)；回傳新的陣列
    public static Color32[] RemoveCaption(Color32[] pixels)
    {
        Color32[] result = (Color32[])pixels.Clone();
        for (int y = 0; y < CaptionBottomRow; y++)
        {
            int row = Height - 1 - y;
            for (int x = 0; x < Width; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Center);
                result[row * Width + x] = CircleColor(distance);
            }
        }

        return result;
    }

    // 色相轉成橘黃、提高亮度 (藍色原圖偏暗，直接換色相會變成土色)
    public static Color32 ToFlame(Color32 color)
    {
        Color.RGBToHSV(color, out _, out float saturation, out float value);
        Color flame = Color.HSVToRGB(FlameHue, saturation, Mathf.Min(1f, value * FlameBrightness));
        flame.a = color.a / 255f;
        return flame;
    }

    private const float FlameHue = 0.1f;
    private const float FlameBrightness = 1.7f;

    // 外圈與內圓的邊緣各留 1 像素做柔邊，避免鋸齒
    private static Color32 CircleColor(float distance)
    {
        if (distance <= InnerRadius - 0.5f) return InnerColor;
        if (distance <= InnerRadius + 0.5f) return Color32.Lerp(InnerColor, RingColor, distance - (InnerRadius - 0.5f));
        if (distance <= OuterRadius - 0.5f) return RingColor;
        if (distance <= OuterRadius + 0.5f)
        {
            Color32 edge = RingColor;
            edge.a = (byte)Mathf.RoundToInt(RingColor.a * (OuterRadius + 0.5f - distance));
            return edge;
        }

        return new Color32(0, 0, 0, 0);
    }
}
#endif
