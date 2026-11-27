using NUnit.Framework;

// 開始畫面的慢動作時間曲線：從 0 開始、越來越慢，到時間長度時剛好停在 1，之後保持定格
public class IntroTimelineTests
{
    [Test]
    public void StartsAtZero()
    {
        Assert.AreEqual(0f, IntroTimeline.Progress(0f, 4f, 3f), 1e-5f);
    }

    [Test]
    public void ReachesOneAtTheEnd_AndStaysFrozen()
    {
        Assert.AreEqual(1f, IntroTimeline.Progress(4f, 4f, 3f), 1e-5f);
        Assert.AreEqual(1f, IntroTimeline.Progress(10f, 4f, 3f), 1e-5f);
    }

    [Test]
    public void SlowsDownOverTime()
    {
        float early = IntroTimeline.Progress(1f, 4f, 3f) - IntroTimeline.Progress(0f, 4f, 3f);
        float late = IntroTimeline.Progress(4f, 4f, 3f) - IntroTimeline.Progress(3f, 4f, 3f);

        Assert.Greater(early, late * 5f, "the first second moves much more than the last one");
    }

    [Test]
    public void ZeroDuration_IsAlreadyFinished()
    {
        Assert.AreEqual(1f, IntroTimeline.Progress(0f, 0f, 3f), 1e-5f);
    }
}
