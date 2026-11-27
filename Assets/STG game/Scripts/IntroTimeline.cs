using UnityEngine;

// 開始畫面的慢動作時間曲線：ease-out，開頭動得快、越來越慢，到 duration 剛好停在 1 (之後一直是 1 = 定格)
public static class IntroTimeline
{
    /// <summary>經過 elapsed 秒後的進度 (0~1)；power 越大，後段放慢得越明顯</summary>
    public static float Progress(float elapsed, float duration, float power)
    {
        if (duration <= 0f) return 1f;

        float t = Mathf.Clamp01(elapsed / duration);
        return 1f - Mathf.Pow(1f - t, Mathf.Max(1f, power));
    }
}
