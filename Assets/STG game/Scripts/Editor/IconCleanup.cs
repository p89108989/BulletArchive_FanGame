#if UNITY_EDITOR
using UnityEngine;

// 圖示去背的善後：去背工具常在背景留下 alpha 只有 1~4 的雜點，肉眼看不見，
// 但 UI 遮罩、點擊判定會把它們當成圖的一部分。把 alpha 不超過門檻的像素清成完全透明，其餘不動
public static class IconCleanup
{
    public static Color32[] ClearFaintAlpha(Color32[] pixels, byte threshold)
    {
        Color32[] result = (Color32[])pixels.Clone();
        for (int i = 0; i < result.Length; i++)
        {
            if (result[i].a <= threshold) result[i] = new Color32(0, 0, 0, 0);
        }

        return result;
    }
}
#endif
