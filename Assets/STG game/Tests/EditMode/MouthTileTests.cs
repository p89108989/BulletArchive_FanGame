using NUnit.Framework;
using UnityEngine;

// BA 的嘴型編號 (動畫事件 SetMouthTile)：百位數 = 第幾欄 (嘴型種類)、個位數 = 由上往下第幾列 (張開程度)，都從 0 起算，
// 對應 8×8 的嘴型圖集 (Character_Mouth_*)。說話時 401 ⇄ 402 = 同一種嘴型小開 ⇄ 大一點。
// 轉成圖集上的像素範圍 (左上為原點，給圖片裁切用)
public class MouthTileTests
{
    [Test]
    public void Code401_IsFifthColumnSecondRow()
    {
        RectInt rect = MouthTile.ToAtlasPixels(401, 2048);

        Assert.AreEqual(new RectInt(1024, 256, 256, 256), rect);
    }

    [Test]
    public void Code300_IsTopRow()
    {
        RectInt rect = MouthTile.ToAtlasPixels(300, 1024);

        Assert.AreEqual(new RectInt(384, 0, 128, 128), rect);
    }

    [Test]
    public void InvalidCodes_AreRejected()
    {
        Assert.IsFalse(MouthTile.IsValid(408), "only 8 rows");
        Assert.IsFalse(MouthTile.IsValid(801), "only 8 columns");
        Assert.IsTrue(MouthTile.IsValid(602));
    }
}
