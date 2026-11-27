using NUnit.Framework;

// 戰鬥計時器的顯示格式：分:秒 (蔚藍檔案風格，兩位數補零)，從 00:00 往上數
public class BattleClockTests
{
    [Test]
    public void Format_PadsMinutesAndSeconds()
    {
        Assert.AreEqual("00:00", BattleClock.Format(0f));
        Assert.AreEqual("01:05", BattleClock.Format(65.9f), "partial seconds are dropped");
        Assert.AreEqual("10:00", BattleClock.Format(600f));
    }

    [Test]
    public void Format_KeepsCountingPastAnHour()
    {
        Assert.AreEqual("61:01", BattleClock.Format(3661f));
    }

    [Test]
    public void Format_NegativeIsZero()
    {
        Assert.AreEqual("00:00", BattleClock.Format(-3f));
    }
}
