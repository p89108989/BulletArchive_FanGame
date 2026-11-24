using NUnit.Framework;
using UnityEngine;

// 由白天天空貼圖調出黃昏與黑夜：依像素種類 (天空/雲/海) 換色，保留原圖的雲與漸層細節。
// 取樣顏色來自 skybox_skydays_3.png 的實際像素
public class SkyColorGradingTests
{
    private static readonly Color Zenith = Rgb(49, 96, 201);
    private static readonly Color Horizon = Rgb(168, 222, 252);
    private static readonly Color Cloud = Rgb(125, 173, 227);
    private static readonly Color Sea = Rgb(39, 174, 225);

    private static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);

    private static float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

    [Test]
    public void BlackBackground_StaysBlack()
    {
        Assert.Less(Luma(SkyColorGrading.Night(Color.black, 0f)), 0.01f);
        Assert.Less(Luma(SkyColorGrading.Dusk(Color.black)), 0.01f);
    }

    [Test]
    public void Night_IsMuchDarkerThanDay()
    {
        Assert.Less(Luma(SkyColorGrading.Night(Zenith, 0f)), Luma(Zenith) * 0.3f);
        Assert.Less(Luma(SkyColorGrading.Night(Sea, 0f)), Luma(Sea) * 0.3f);
    }

    [Test]
    public void Night_CloudsStayVisibleAgainstTheSky()
    {
        Assert.Greater(Luma(SkyColorGrading.Night(Cloud, 0f)), Luma(SkyColorGrading.Night(Zenith, 0f)) * 1.5f);
    }

    [Test]
    public void Night_SeaIsDarkBlue()
    {
        Color sea = SkyColorGrading.Night(Sea, 0f);
        Assert.Greater(sea.b, sea.r);
    }

    [Test]
    public void Night_StarsOnlyAppearInOpenSky()
    {
        Assert.Greater(Luma(SkyColorGrading.Night(Zenith, 1f)), Luma(SkyColorGrading.Night(Zenith, 0f)) + 0.3f, "a star in the open sky");
        Assert.AreEqual(Luma(SkyColorGrading.Night(Sea, 0f)), Luma(SkyColorGrading.Night(Sea, 1f)), 0.001f, "no stars on the sea");
    }

    [Test]
    public void Dusk_HorizonGlowsWarmAndZenithStaysCool()
    {
        Color horizon = SkyColorGrading.Dusk(Horizon);
        Color zenith = SkyColorGrading.Dusk(Zenith);

        Assert.Greater(horizon.r, horizon.b, "the horizon turns orange");
        Assert.Greater(zenith.b, zenith.r * 0.9f, "the zenith stays indigo");
    }

    [Test]
    public void Dusk_SeaReflectsWarmLight()
    {
        Color sea = SkyColorGrading.Dusk(Sea);
        Assert.Greater(sea.r, Sea.r);
    }
}
