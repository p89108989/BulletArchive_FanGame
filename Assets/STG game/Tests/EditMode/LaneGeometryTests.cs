using NUnit.Framework;
using UnityEngine;

// 長條範圍 (光炮與地面火焰)：從起點沿方向延伸 length、寬 width (左右各一半)；只看水平位置
public class LaneGeometryTests
{
    private static readonly Vector3 Start = new Vector3(0f, 0f, 0f);
    private static readonly Vector3 Direction = Vector3.forward;

    [Test]
    public void PointOnTheCenterLine_IsInside()
    {
        Assert.IsTrue(LaneGeometry.Contains(Start, Direction, 100f, 12f, new Vector3(0f, 5f, 50f)));
    }

    [Test]
    public void WithinHalfWidth_IsInside_BeyondIsOutside()
    {
        Assert.IsTrue(LaneGeometry.Contains(Start, Direction, 100f, 12f, new Vector3(5.9f, 0f, 50f)));
        Assert.IsFalse(LaneGeometry.Contains(Start, Direction, 100f, 12f, new Vector3(6.1f, 0f, 50f)));
    }

    [Test]
    public void BehindTheStartOrPastTheEnd_IsOutside()
    {
        Assert.IsFalse(LaneGeometry.Contains(Start, Direction, 100f, 12f, new Vector3(0f, 0f, -1f)));
        Assert.IsFalse(LaneGeometry.Contains(Start, Direction, 100f, 12f, new Vector3(0f, 0f, 101f)));
    }

    [Test]
    public void DiagonalDirection_UsesDistanceToTheLine()
    {
        Vector3 diagonal = new Vector3(1f, 0f, 1f).normalized;

        Assert.IsTrue(LaneGeometry.Contains(Start, diagonal, 100f, 12f, new Vector3(30f, 0f, 30f)));
        Assert.IsFalse(LaneGeometry.Contains(Start, diagonal, 100f, 12f, new Vector3(30f, 0f, 0f)), "about 21 m off the line");
    }

    [Test]
    public void DirectionWithHeight_IsFlattened()
    {
        Assert.IsTrue(LaneGeometry.Contains(Start, new Vector3(0f, -5f, 1f), 100f, 12f, new Vector3(0f, 0f, 50f)));
    }
}
