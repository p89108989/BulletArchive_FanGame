using UnityEngine;

// 戰鬥字卡的時間軸 (時間單位：秒；位置單位：1920×1080 參考解析度的 UI 像素)。
// START 照原遊戲 UIAni_Start：0.18~0.32 秒從畫面左外快速飛到中間偏左 (減速)、0.32~1.43 秒慢慢往右、
// 1.43~1.48 秒一口氣從右邊飛出；背景壓暗 0 → 0.92 (0.5 秒) → 維持 → 0 (1.6 秒)；閃光 0.52 秒亮起、1.55 秒熄滅。
// WARNING / VICTORY / DEFEAT 是「蓋章」：一開始放大、很快縮回原尺寸並淡入
public static class BannerTimeline
{
    public const float StartDuration = 1.85f;
    public const float Darkness = 0.92f;

    private const float FlyInStart = 0.18f;
    private const float FlyInEnd = 0.32f;
    private const float DriftEnd = 1.43f;
    private const float FlyOutEnd = 1.48f;
    private const float OffLeft = -2376f;
    private const float ArriveX = -406f;
    private const float DriftX = 423f;
    private const float OffRight = 3518f;

    private const float StampSettle = 0.15f;
    private const float StampStartScale = 1.6f;

    public static float StartX(float t)
    {
        if (t <= FlyInStart) return OffLeft;
        // 飛入段減速 (ease-out)
        if (t <= FlyInEnd) return Mathf.Lerp(OffLeft, ArriveX, EaseOut(Mathf.InverseLerp(FlyInStart, FlyInEnd, t)));
        if (t <= DriftEnd) return Mathf.Lerp(ArriveX, DriftX, Mathf.InverseLerp(FlyInEnd, DriftEnd, t));
        if (t <= FlyOutEnd) return Mathf.Lerp(DriftX, OffRight, Mathf.InverseLerp(DriftEnd, FlyOutEnd, t));
        return OffRight;
    }

    public static bool StartFinished(float t)
    {
        return t >= StartDuration;
    }

    public static float StartDarkness(float t)
    {
        return Darkness * Envelope(t, 0f, 0.5f, 1.32f, 1.6f);
    }

    public static float StartLight(float t)
    {
        return Envelope(t, FlyInStart, 0.52f, 1.02f, 1.55f);
    }

    // 蓋章字卡的縮放：一開始較大，StampSettle 秒內縮回 1
    public static float StampScale(float t)
    {
        return Mathf.Lerp(StampStartScale, 1f, EaseOut(Mathf.Clamp01(t / StampSettle)));
    }

    public static float StampAlpha(float t)
    {
        return Mathf.Clamp01(t / (StampSettle * 0.6f));
    }

    // 0 → 1 (riseStart ~ riseEnd) → 1 → 0 (fallStart ~ fallEnd)
    private static float Envelope(float t, float riseStart, float riseEnd, float fallStart, float fallEnd)
    {
        if (t <= riseEnd) return Mathf.InverseLerp(riseStart, riseEnd, t);
        if (t <= fallStart) return 1f;
        return 1f - Mathf.InverseLerp(fallStart, fallEnd, t);
    }

    private static float EaseOut(float x)
    {
        return 1f - (1f - x) * (1f - x);
    }
}
