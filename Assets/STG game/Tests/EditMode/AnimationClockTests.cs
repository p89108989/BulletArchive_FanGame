using NUnit.Framework;

// 演出動畫 (轉場、字卡、進場) 每幀前進的時間：載入場景時某一幀可能卡一秒以上，
// 不能讓動畫一口氣跳到結尾，所以每幀最多只算 MaxStep
public class AnimationClockTests
{
    [Test]
    public void NormalFrame_IsUsedAsIs()
    {
        Assert.AreEqual(1f / 60f, AnimationClock.Step(1f / 60f), 1e-6f);
    }

    [Test]
    public void LoadingHitch_IsCappedToOneShortStep()
    {
        Assert.AreEqual(AnimationClock.MaxStep, AnimationClock.Step(1.06f), 1e-6f);
        Assert.LessOrEqual(AnimationClock.MaxStep, 1f / 20f);
    }
}
