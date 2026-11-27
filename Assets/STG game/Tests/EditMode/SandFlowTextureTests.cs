using NUnit.Framework;
using UnityEngine;

// 流沙貼圖：沙色的橫向波紋，上下左右可無縫重複 (捲動時看不到接縫)，半透明 (看得到底下的地板)
public class SandFlowTextureTests
{
    private const int Size = 64;

    [Test]
    public void TilesSeamlessly()
    {
        Color32[] pixels = SandFlowTexture.Generate(Size, 3);

        // 最後一列接回第一列、最後一欄接回第一欄時，差異不應比相鄰兩列大太多
        int maxNeighbourStep = 0, wrapStepV = 0, wrapStepU = 0;
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size - 1; y++) maxNeighbourStep = Mathf.Max(maxNeighbourStep, Mathf.Abs(pixels[y * Size + x].a - pixels[(y + 1) * Size + x].a));
            wrapStepV = Mathf.Max(wrapStepV, Mathf.Abs(pixels[(Size - 1) * Size + x].a - pixels[x].a));
        }

        for (int y = 0; y < Size; y++) wrapStepU = Mathf.Max(wrapStepU, Mathf.Abs(pixels[y * Size + Size - 1].a - pixels[y * Size].a));

        Assert.LessOrEqual(wrapStepV, maxNeighbourStep + 2, "top/bottom seam");
        Assert.LessOrEqual(wrapStepU, maxNeighbourStep + 2, "left/right seam");
    }

    [Test]
    public void IsTranslucentWithVisibleRipples()
    {
        Color32[] pixels = SandFlowTexture.Generate(Size, 3);

        int min = 255, max = 0;
        foreach (Color32 pixel in pixels)
        {
            min = Mathf.Min(min, pixel.a);
            max = Mathf.Max(max, pixel.a);
        }

        Assert.Less(max, 255, "never fully opaque");
        Assert.Greater(min, 0, "never fully clear");
        Assert.Greater(max - min, 60, "ripples are visible");
    }
}
