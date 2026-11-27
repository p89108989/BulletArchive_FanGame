using NUnit.Framework;

// Boss 特殊機制的血量門檻：血量第一次掉到 (或低於) 門檻時觸發一次，之後不再觸發；
// 一次扣很多血跨過好幾個門檻時，依序每次回報一個 (先回報較高的門檻)
public class HealthThresholdsTests
{
    [Test]
    public void AboveAllThresholds_NothingTriggers()
    {
        HealthThresholds thresholds = new HealthThresholds(new[] { 2f / 3f, 1f / 3f });

        Assert.AreEqual(-1, thresholds.Next(800, 1000));
    }

    [Test]
    public void CrossingAThreshold_TriggersItOnce()
    {
        HealthThresholds thresholds = new HealthThresholds(new[] { 2f / 3f, 1f / 3f });

        Assert.AreEqual(0, thresholds.Next(660, 1000));
        Assert.AreEqual(-1, thresholds.Next(650, 1000), "already triggered");
        Assert.AreEqual(1, thresholds.Next(330, 1000));
        Assert.AreEqual(-1, thresholds.Next(100, 1000));
    }

    [Test]
    public void SkippingPastSeveral_ReportsThemOneAtATimeInOrder()
    {
        HealthThresholds thresholds = new HealthThresholds(new[] { 2f / 3f, 1f / 3f });

        Assert.AreEqual(0, thresholds.Next(200, 1000));
        Assert.AreEqual(1, thresholds.Next(200, 1000));
        Assert.AreEqual(-1, thresholds.Next(200, 1000));
    }

    [Test]
    public void Dead_TriggersNothing()
    {
        HealthThresholds thresholds = new HealthThresholds(new[] { 2f / 3f });

        Assert.AreEqual(-1, thresholds.Next(0, 1000));
    }

    [Test]
    public void ExactlyAtThreshold_Triggers()
    {
        HealthThresholds thresholds = new HealthThresholds(new[] { 0.5f });

        Assert.AreEqual(0, thresholds.Next(500, 1000));
    }
}
