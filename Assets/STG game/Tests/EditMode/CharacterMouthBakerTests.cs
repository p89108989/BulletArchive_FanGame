using NUnit.Framework;
using UnityEngine;

// 角色嘴巴貼圖的合成：EyeMouth 貼圖放大後，左下角嘴巴區換成「膚色 + 嘴型圖集的那一格」，其他區域維持原貼圖
public class CharacterMouthBakerTests
{
    private static readonly Color32 Skin = new Color32(250, 220, 210, 255);
    private static readonly Color32 Eye = new Color32(10, 20, 200, 255);
    private static readonly Color32 Lip = new Color32(200, 40, 40, 255);

    private static Color32[] Fill(int size, Color32 color)
    {
        Color32[] pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        return pixels;
    }

    // 8×8 格、每格 2px 的圖集：401 那格 (第 4 欄、由上往下第 1 列) 左下角 1px 是嘴唇，其餘透明
    private static Color32[] Atlas(out int size)
    {
        size = 16;
        Color32[] pixels = Fill(size, new Color32(0, 0, 0, 0));
        RectInt tile = MouthTile.ToAtlasPixels(401, size);
        int bottom = size - tile.y - tile.height;
        pixels[bottom * size + tile.x] = Lip;
        return pixels;
    }

    [Test]
    public void OutsideTheMouth_KeepsTheEyeMouthTexture()
    {
        Color32[] atlas = Atlas(out int atlasSize);
        Color32[] result = CharacterMouthBaker.Compose(Fill(4, Eye), 4, atlas, atlasSize, 401, Skin, 8, 0.25f);

        Assert.AreEqual(Eye, result[7 * 8 + 7], "top-right is untouched");
        Assert.AreEqual(Eye, result[0 * 8 + 4], "right of the mouth area is untouched");
    }

    [Test]
    public void MouthArea_IsSkinWithTheMouthTileOnTop()
    {
        Color32[] atlas = Atlas(out int atlasSize);
        Color32[] result = CharacterMouthBaker.Compose(Fill(4, Eye), 4, atlas, atlasSize, 401, Skin, 8, 0.25f);

        // 嘴巴區 = 左下 2×2 (8 × 0.25)，對應圖集那格的 2×2
        Assert.AreEqual(Lip, result[0 * 8 + 0], "opaque mouth pixel");
        Assert.AreEqual(Skin, result[1 * 8 + 1], "transparent part of the tile shows skin");
    }
}
