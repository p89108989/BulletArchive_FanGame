using NUnit.Framework;
using UnityEngine;

// 能量條 (蔚藍檔案 COST 條風格)：10 格、每格 10%，正在累積的那一格依比例填一部分；
// 前面的數字 = 已經填滿的格數
public class EnergyGaugeTests
{
    [Test]
    public void SegmentFill_FullPartialAndEmpty()
    {
        // 73% → 前 7 格滿、第 8 格 30%、之後空
        Assert.AreEqual(1f, EnergyGauge.SegmentFill(73f, 100f, 6, 10), 0.001f);
        Assert.AreEqual(0.3f, EnergyGauge.SegmentFill(73f, 100f, 7, 10), 0.001f);
        Assert.AreEqual(0f, EnergyGauge.SegmentFill(73f, 100f, 8, 10), 0.001f);
    }

    [Test]
    public void FullSegments_CountsOnlyCompleteSegments()
    {
        Assert.AreEqual(7, EnergyGauge.FullSegments(73f, 100f, 10));
        Assert.AreEqual(10, EnergyGauge.FullSegments(100f, 100f, 10));
        Assert.AreEqual(0, EnergyGauge.FullSegments(9.9f, 100f, 10));
    }

    [Test]
    public void ZeroCapacity_IsEmpty()
    {
        Assert.AreEqual(0f, EnergyGauge.SegmentFill(50f, 0f, 0, 10));
        Assert.AreEqual(0, EnergyGauge.FullSegments(50f, 0f, 10));
    }

    // 蔚藍檔案 COST 數字牌 (Combat_CostBg，117×123) 去掉頂端的「COST」字樣，被字蓋住的圓補回來
    [Test]
    public void CostBadge_RemoveCaption_RestoresCircleAndClearsCorners()
    {
        int width = CostBadgeArt.Width;
        int height = CostBadgeArt.Height;
        Color32[] pixels = new Color32[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);

        Color32[] result = CostBadgeArt.RemoveCaption(pixels);

        // Texture2D 像素陣列由下往上：上方第 y 列 = height - 1 - y
        Color32 TopLeftOrigin(int x, int y) => result[(height - 1 - y) * width + x];
        Assert.AreEqual(0, TopLeftOrigin(5, 5).a, "the caption corner becomes transparent");
        Assert.AreEqual(CostBadgeArt.RingColor, TopLeftOrigin(63, 18), "the dark ring is redrawn under the old caption");
        Assert.AreEqual(CostBadgeArt.InnerColor, TopLeftOrigin(63, 30), "the blue circle is redrawn under the old caption");
        Assert.AreEqual(new Color32(255, 255, 255, 255), TopLeftOrigin(63, 90), "the rest of the badge is untouched");
    }

    [Test]
    public void CostBadge_ToFlame_TurnsBlueIntoWarmColor()
    {
        Color32 flame = CostBadgeArt.ToFlame(CostBadgeArt.InnerColor);

        Assert.Greater(flame.r, flame.b);
        Assert.Greater(flame.g, flame.b);
        Assert.AreEqual(CostBadgeArt.InnerColor.a, flame.a);
    }
}
