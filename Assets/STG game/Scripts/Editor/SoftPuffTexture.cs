#if UNITY_EDITOR
using UnityEngine;

// 產生細緻煙霧/水霧用的柔邊雲團貼圖 (白色，粒子顏色決定最後的色調)。
// 專案內現成的煙霧都是卡通描邊風格 (或蔚藍檔案專用 shader 的 RGB 分層貼圖，一般 shader 無法正確顯示)，
// 所以自己產生：圓形漸層 × 分形雜訊 — 雜訊讓雲團外緣起伏、內部有濃淡，看起來像一團霧而不是一顆圓點
public static class SoftPuffTexture
{
    // 雲團外緣的平均位置與起伏幅度 (以貼圖半徑為 1)
    private const float EdgeRadius = 0.62f;
    private const float EdgeVariation = 0.4f;
    // 從外緣往內多遠開始變濃 (比例)，越小邊緣越銳利
    private const float SoftnessRatio = 0.65f;
    // 內部濃淡：最淡處仍保留的比例
    private const float MinDensity = 0.65f;
    private const int Octaves = 4;
    private const float BaseFrequency = 4f;

    public static Color32[] Generate(int size, int seed)
    {
        Color32[] pixels = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f - half) / half;
                float v = (y + 0.5f - half) / half;
                float radius = Mathf.Sqrt(u * u + v * v);

                // 外緣隨方向起伏：用單位圓上的位置取雜訊，同一方向的外緣一致，避免出現破碎的孔洞
                float angleNoise = Fbm(u / Mathf.Max(radius, 0.0001f) + 3f, v / Mathf.Max(radius, 0.0001f) + 3f, seed);
                float edge = EdgeRadius + (angleNoise - 0.5f) * EdgeVariation;
                float falloff = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge * SoftnessRatio, edge, radius));

                float density = Mathf.Lerp(MinDensity, 1f, Fbm((u + 1f) * 0.5f, (v + 1f) * 0.5f, seed + 101));
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(falloff * density) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        return pixels;
    }

    // 水滴用的柔邊圓點：沒有雜訊，拉長 (Stretched Billboard) 後成為乾淨的水線；雲團貼圖拉長會像羽毛
    public static Color32[] GenerateDot(int size)
    {
        Color32[] pixels = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f - half) / half;
                float v = (y + 0.5f - half) / half;
                // 半徑 0.95 處就淡到 0，最外圈的像素保證全透明 (不然拉長後兩端會有一條硬邊)
                float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v) / 0.95f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Pow(falloff, 1.5f) * 255f));
            }
        }

        return pixels;
    }

    // 分形值雜訊 (0~1)：多層不同頻率的平滑雜訊相加
    private static float Fbm(float x, float y, int seed)
    {
        float sum = 0f;
        float amplitude = 0.5f;
        float total = 0f;
        float frequency = BaseFrequency;
        for (int i = 0; i < Octaves; i++)
        {
            sum += ValueNoise(x * frequency, y * frequency, seed + i * 17) * amplitude;
            total += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return sum / total;
    }

    private static float ValueNoise(float x, float y, int seed)
    {
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        float tx = x - x0;
        float ty = y - y0;
        float sx = tx * tx * (3f - 2f * tx);
        float sy = ty * ty * (3f - 2f * ty);

        float a = Hash(x0, y0, seed);
        float b = Hash(x0 + 1, y0, seed);
        float c = Hash(x0, y0 + 1, seed);
        float d = Hash(x0 + 1, y0 + 1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sy);
    }

    // 整數雜湊 → 0~1，固定種子得到固定結果 (每次重建貼圖都一樣)
    private static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144665);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0xFFFFFF;
        }
    }
}
#endif
