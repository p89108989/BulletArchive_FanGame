#if UNITY_EDITOR
using UnityEngine;

// 流沙貼圖：沙色、半透明的橫向波紋 (沙往下流時一道道的沙紋)，加上細小的沙粒雜點。
// 波紋只用週期函數組成，上下左右都能無縫重複，捲動時看不到接縫
public static class SandFlowTexture
{
    private static readonly Color32 SandColor = new Color32(222, 192, 138, 255);
    private const float MinAlpha = 0.35f;
    private const float MaxAlpha = 0.8f;
    // 一張貼圖裡有幾道沙紋 (上下方向)
    private const int RippleBands = 6;
    // 沙粒雜點的強度 (±)
    private const float GrainStrength = 0.06f;

    public static Color32[] Generate(int size, int seed)
    {
        Color32[] pixels = new Color32[size * size];
        System.Random random = new System.Random(seed);
        float phaseA = (float)random.NextDouble() * Mathf.PI * 2f;
        float phaseB = (float)random.NextDouble() * Mathf.PI * 2f;
        const float Tau = Mathf.PI * 2f;

        for (int y = 0; y < size; y++)
        {
            float v = (float)y / size;
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size;
                // 沙紋左右彎曲 (彎曲量本身也是週期的，左右接得起來)
                float warp = 0.12f * Mathf.Sin(Tau * 2f * u + phaseA) + 0.05f * Mathf.Sin(Tau * 5f * u + phaseB);
                float ripple = 0.5f + 0.5f * Mathf.Sin(Tau * (RippleBands * v + warp));
                // 讓亮紋窄一點、暗的間隔寬一點，比較像一道道沙痕
                ripple = ripple * ripple;
                float grain = ((Hash(x, y, seed) & 0xFF) / 255f - 0.5f) * 2f * GrainStrength;
                float alpha = Mathf.Clamp(Mathf.Lerp(MinAlpha, MaxAlpha, ripple) + grain, MinAlpha - GrainStrength, MaxAlpha + GrainStrength);
                pixels[y * size + x] = new Color32(SandColor.r, SandColor.g, SandColor.b, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        return pixels;
    }

    private static uint Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144665);
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }
    }
}
#endif
