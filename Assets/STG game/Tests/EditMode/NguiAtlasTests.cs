using NUnit.Framework;
using UnityEngine;

// 把 NGUI 圖集定義 (蔚藍檔案匯出的 *.asset：每張 sprite 的名稱、像素範圍、九宮格邊界) 轉成 Unity sprite 切片。
// NGUI 的座標原點在左上，Unity sprite 在左下
public class NguiAtlasTests
{
    private const string SampleAtlas =
        "  m_Name: Combat\n" +
        "  mSprites:\n" +
        "  - name: Combat_Balloon_Sup\n" +
        "    x: 248\n" +
        "    y: 319\n" +
        "    width: 69\n" +
        "    height: 95\n" +
        "    borderLeft: 46\n" +
        "    borderRight: 20\n" +
        "    borderTop: 3\n" +
        "    borderBottom: 7\n" +
        "    paddingLeft: 0\n" +
        "    paddingRight: 0\n" +
        "    paddingTop: 0\n" +
        "    paddingBottom: 0\n" +
        "  - name: Combat_Time_Icon\n" +
        "    x: 0\n" +
        "    y: 0\n" +
        "    width: 40\n" +
        "    height: 44\n" +
        "    borderLeft: 0\n" +
        "    borderRight: 0\n" +
        "    borderTop: 0\n" +
        "    borderBottom: 0\n" +
        "    paddingLeft: 0\n" +
        "    paddingRight: 0\n" +
        "    paddingTop: 0\n" +
        "    paddingBottom: 0\n" +
        "  mPixelSize: 1\n";

    [Test]
    public void Parse_ReadsEverySprite()
    {
        NguiAtlas.Entry[] entries = NguiAtlas.Parse(SampleAtlas);

        Assert.AreEqual(2, entries.Length);
        Assert.AreEqual("Combat_Balloon_Sup", entries[0].name);
        Assert.AreEqual(new RectInt(248, 319, 69, 95), entries[0].rect);
        Assert.AreEqual("Combat_Time_Icon", entries[1].name);
    }

    [Test]
    public void ToUnityRect_FlipsYFromTopLeftOrigin()
    {
        // 在 1024 高的圖上，從上方 319 開始、高 95 → 從下方 1024 - 319 - 95 = 610 開始
        Rect rect = NguiAtlas.ToUnityRect(new RectInt(248, 319, 69, 95), 1024);

        Assert.AreEqual(new Rect(248, 610, 69, 95), rect);
    }

    [Test]
    public void ToUnityBorder_UsesLeftBottomRightTopOrder()
    {
        NguiAtlas.Entry entry = NguiAtlas.Parse(SampleAtlas)[0];

        Assert.AreEqual(new Vector4(46, 7, 20, 3), NguiAtlas.ToUnityBorder(entry));
    }
}
