using NUnit.Framework;
using UnityEngine;

// Boss 雷射的兩種軌跡：追蹤玩家「1 秒前的位置」與覆蓋整個活動區的 M 型掃射
public class LaserTrajectoryTests
{
    [Test]
    public void DelayedTracker_ReturnsPositionFromThePast()
    {
        var tracker = new DelayedPositionTracker(64);
        tracker.Record(0f, new Vector3(0f, 0f, 0f));
        tracker.Record(1f, new Vector3(10f, 0f, 0f));
        tracker.Record(2f, new Vector3(20f, 0f, 0f));

        Assert.AreEqual(10f, tracker.Sample(1f).x, 0.001f);
        Assert.AreEqual(15f, tracker.Sample(1.5f).x, 0.001f, "between records it interpolates");
    }

    [Test]
    public void DelayedTracker_ClampsToRecordedRange()
    {
        var tracker = new DelayedPositionTracker(64);
        tracker.Record(5f, new Vector3(1f, 0f, 0f));
        tracker.Record(6f, new Vector3(2f, 0f, 0f));

        Assert.AreEqual(1f, tracker.Sample(0f).x, 0.001f, "older than history -> oldest record");
        Assert.AreEqual(2f, tracker.Sample(9f).x, 0.001f, "newer than history -> newest record");
    }

    [Test]
    public void DelayedTracker_KeepsMostRecentWhenFull()
    {
        var tracker = new DelayedPositionTracker(4);
        for (int i = 0; i < 10; i++) tracker.Record(i, new Vector3(i, 0f, 0f));

        Assert.AreEqual(6f, tracker.Sample(0f).x, 0.001f, "only the last 4 records are kept");
        Assert.AreEqual(8.5f, tracker.Sample(8.5f).x, 0.001f);
    }

    private static readonly Vector2[] MPoints =
    {
        new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(1f, 1f), new Vector2(1f, 0f),
    };

    // 來回掃 N 次：從近側 (玩家側) 出發，每一趟都掃到遠側 (Boss 側) 再回來，橫向均勻推進到另一邊
    [Test]
    public void Zigzag_ReachesFarSideOncePerPass()
    {
        Vector2[] points = SweepPath.Zigzag(5, new Vector2(0.05f, 0.1f));

        Assert.AreEqual(11, points.Length);
        Assert.AreEqual(0.95f, points[0].x, 0.001f, "starts on screen-left (+X)");
        Assert.AreEqual(0.05f, points[10].x, 0.001f, "ends on screen-right");
        int farPeaks = 0;
        for (int i = 0; i < points.Length; i++)
        {
            if (i > 0) Assert.Less(points[i].x, points[i - 1].x, "moves steadily across");
            if (i % 2 == 0) Assert.AreEqual(0.9f, points[i].y, 0.001f, "even points are on the player side");
            else
            {
                Assert.AreEqual(0.1f, points[i].y, 0.001f, "odd points reach the boss side");
                farPeaks++;
            }
        }

        Assert.AreEqual(5, farPeaks);
    }

    [Test]
    public void SweepPath_StartsAndEndsAtPathEnds()
    {
        Vector2 min = new Vector2(-100f, 0f);
        Vector2 max = new Vector2(0f, 50f);

        Vector3 start = SweepPath.Evaluate(MPoints, min, max, 0f, false, 0.3f);
        Vector3 end = SweepPath.Evaluate(MPoints, min, max, 1f, false, 0.3f);

        Assert.That(Vector3.Distance(start, new Vector3(-100f, 0.3f, 0f)), Is.LessThan(0.01f));
        Assert.That(Vector3.Distance(end, new Vector3(0f, 0.3f, 0f)), Is.LessThan(0.01f));
    }

    [Test]
    public void SweepPath_ReverseRunsTheOtherWay()
    {
        Vector2 min = new Vector2(-100f, 0f);
        Vector2 max = new Vector2(0f, 50f);

        Vector3 start = SweepPath.Evaluate(MPoints, min, max, 0f, true, 0f);

        Assert.That(Vector3.Distance(start, new Vector3(0f, 0f, 0f)), Is.LessThan(0.01f));
    }

    [Test]
    public void SweepPath_MovesAtConstantSpeedAlongPath()
    {
        // 第一段長 50 (0,0)->(0,50)，第二段長 sqrt(50^2+25^2)≈55.9：
        // 用路徑長度而非線段數分配進度，掃射速度才會均勻
        Vector2 min = new Vector2(-100f, 0f);
        Vector2 max = new Vector2(0f, 50f);
        float first = 50f;
        float diagonal = Mathf.Sqrt(50f * 50f + 25f * 25f);
        float total = first * 2f + diagonal * 2f;

        Vector3 corner = SweepPath.Evaluate(MPoints, min, max, first / total, false, 0f);

        Assert.That(Vector3.Distance(corner, new Vector3(-100f, 0f, 50f)), Is.LessThan(0.01f));
    }
}
