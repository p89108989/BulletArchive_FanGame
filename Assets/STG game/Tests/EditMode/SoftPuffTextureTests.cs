using NUnit.Framework;

// 細緻煙霧/水霧用的柔邊雲團貼圖：白色、中間濃、往外漸淡到全透明，雲團邊緣有雜訊起伏 (不是完美的圓)
public class SoftPuffTextureTests
{
    private const int Size = 64;

    private static byte AlphaAt(UnityEngine.Color32[] pixels, int x, int y)
    {
        return pixels[y * Size + x].a;
    }

    [Test]
    public void CenterIsDenseAndCornersAreClear()
    {
        UnityEngine.Color32[] pixels = SoftPuffTexture.Generate(Size, 1);

        Assert.AreEqual(Size * Size, pixels.Length);
        Assert.Greater(AlphaAt(pixels, Size / 2, Size / 2), 150, "dense in the middle");
        Assert.AreEqual(0, AlphaAt(pixels, 0, 0));
        Assert.AreEqual(0, AlphaAt(pixels, Size - 1, Size - 1));
    }

    [Test]
    public void IsWhiteSoParticleColorTintsIt()
    {
        UnityEngine.Color32[] pixels = SoftPuffTexture.Generate(Size, 1);

        UnityEngine.Color32 center = pixels[(Size / 2) * Size + Size / 2];
        Assert.AreEqual(255, center.r);
        Assert.AreEqual(255, center.g);
        Assert.AreEqual(255, center.b);
    }

    [Test]
    public void EdgeIsNoisyNotAPerfectCircle()
    {
        UnityEngine.Color32[] pixels = SoftPuffTexture.Generate(Size, 1);

        // 同一個半徑上取一圈：完美的圓每一點 alpha 都一樣，雲團邊緣應該有明顯起伏
        int min = 255, max = 0;
        for (int i = 0; i < 32; i++)
        {
            float angle = i * UnityEngine.Mathf.PI * 2f / 32f;
            int x = UnityEngine.Mathf.RoundToInt(Size / 2 + UnityEngine.Mathf.Cos(angle) * Size * 0.3f);
            int y = UnityEngine.Mathf.RoundToInt(Size / 2 + UnityEngine.Mathf.Sin(angle) * Size * 0.3f);
            int a = AlphaAt(pixels, x, y);
            if (a < min) min = a;
            if (a > max) max = a;
        }

        Assert.Greater(max - min, 30, "alpha should vary around the ring");
    }

    // 水滴用：圓形、中心最亮往外平滑淡出、沒有雜訊 (拉長後成為乾淨的水線)
    [Test]
    public void Dot_IsRoundAndFadesOutward()
    {
        UnityEngine.Color32[] pixels = SoftPuffTexture.GenerateDot(Size);

        int center = AlphaAt(pixels, Size / 2, Size / 2);
        int mid = AlphaAt(pixels, Size / 2 + Size / 4, Size / 2);
        Assert.Greater(center, 240);
        Assert.Greater(center, mid, "fades outward");
        Assert.Greater(mid, 0);
        Assert.AreEqual(0, AlphaAt(pixels, 0, Size / 2), "clear at the edge");
        Assert.AreEqual(mid, AlphaAt(pixels, Size / 2, Size / 2 + Size / 4), "same at the same radius (no noise)");
    }

    [Test]
    public void SameSeedGivesSameTexture()
    {
        CollectionAssert.AreEqual(SoftPuffTexture.Generate(Size, 7), SoftPuffTexture.Generate(Size, 7));
    }
}
