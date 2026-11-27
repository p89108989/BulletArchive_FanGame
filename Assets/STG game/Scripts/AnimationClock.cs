using UnityEngine;

// 演出動畫 (稜鏡轉場、字卡、UI 進場、主畫面慢動作) 每幀前進的時間。
// 換場景的那一幀會卡上一秒以上 (載入模型、貼圖、編譯 shader)，直接用真實經過時間的話動畫會一口氣跳到結尾，
// 看起來像沒播；所以每幀最多只算 MaxStep，卡頓的那幀動畫只前進一小步
public static class AnimationClock
{
    // 約 30fps 的一幀
    public const float MaxStep = 1f / 30f;

    // 不受暫停影響 (轉場、字卡在戰鬥暫停時也要播)
    public static float UnscaledDelta => Step(Time.unscaledDeltaTime);
    // 受暫停影響
    public static float ScaledDelta => Step(Time.deltaTime);

    public static float Step(float delta)
    {
        return Mathf.Min(delta, MaxStep);
    }
}
