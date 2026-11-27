using NUnit.Framework;

// 戰鬥字卡的時間軸 (START 照原遊戲 UIAni_Start)：從畫面左外飛入 → 減速慢慢往右 → 一口氣從右邊飛出；
// 背景壓暗 0 → 0.92 → 0。WARNING / VICTORY / DEFEAT 是「放大蓋章」：一開始較大，很快縮回原尺寸
public class BannerTimelineTests
{
    [Test]
    public void Start_BeginsOffScreenLeft()
    {
        Assert.Less(BannerTimeline.StartX(0f), -1500f);
    }

    [Test]
    public void Start_ArrivesQuickly_ThenDriftsSlowly()
    {
        Assert.AreEqual(-406f, BannerTimeline.StartX(0.32f), 1f);
        float drift = BannerTimeline.StartX(1.0f);
        Assert.Greater(drift, -406f);
        Assert.Less(drift, 423f);
    }

    [Test]
    public void Start_LeavesOffScreenRight()
    {
        Assert.Greater(BannerTimeline.StartX(1.6f), 1500f);
        Assert.IsTrue(BannerTimeline.StartFinished(BannerTimeline.StartDuration));
        Assert.IsFalse(BannerTimeline.StartFinished(1f));
    }

    [Test]
    public void Darkness_FadesInHoldsAndClears()
    {
        Assert.AreEqual(0f, BannerTimeline.StartDarkness(0f), 0.05f);
        Assert.AreEqual(0.92f, BannerTimeline.StartDarkness(0.9f), 0.01f);
        Assert.AreEqual(0f, BannerTimeline.StartDarkness(BannerTimeline.StartDuration), 0.05f);
    }

    [Test]
    public void Stamp_StartsBig_ThenSettles()
    {
        Assert.Greater(BannerTimeline.StampScale(0f), 1.2f);
        Assert.AreEqual(1f, BannerTimeline.StampScale(0.5f), 0.01f);
        Assert.AreEqual(1f, BannerTimeline.StampAlpha(0.5f), 0.01f);
    }
}
