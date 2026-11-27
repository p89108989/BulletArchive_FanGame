using NUnit.Framework;

// 蔚藍檔案 Boss 血條的多層血量：總血量分成好幾層 (每層 hpPerLayer)，條上只畫目前這一層，
// 尾端「x N」= 還剩幾層；hpPerLayer <= 0 時整條就是一層
public class BossHpLayersTests
{
    [Test]
    public void FullHealth_ShowsAllLayersFull()
    {
        BossHpLayers layers = BossHpLayers.Compute(1000, 1000, 250);

        Assert.AreEqual(4, layers.remaining);
        Assert.AreEqual(1f, layers.fill, 0.001f);
    }

    [Test]
    public void PartialLayer_FillsOnlyThatLayer()
    {
        BossHpLayers layers = BossHpLayers.Compute(760, 1000, 250);

        Assert.AreEqual(4, layers.remaining, "760 is still inside the 4th layer");
        Assert.AreEqual(0.04f, layers.fill, 0.001f, "10 of the 4th layer's 250");
    }

    [Test]
    public void ExactBoundary_IsAFullLowerLayer()
    {
        BossHpLayers layers = BossHpLayers.Compute(750, 1000, 250);

        Assert.AreEqual(3, layers.remaining);
        Assert.AreEqual(1f, layers.fill, 0.001f);
    }

    [Test]
    public void Dead_IsEmpty()
    {
        BossHpLayers layers = BossHpLayers.Compute(0, 1000, 250);

        Assert.AreEqual(0, layers.remaining);
        Assert.AreEqual(0f, layers.fill);
    }

    [Test]
    public void NoLayerSize_IsOneBar()
    {
        BossHpLayers layers = BossHpLayers.Compute(300, 1000, 0);

        Assert.AreEqual(1, layers.remaining);
        Assert.AreEqual(0.3f, layers.fill, 0.001f);
    }
}
