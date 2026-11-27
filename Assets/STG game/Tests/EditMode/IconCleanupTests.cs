using NUnit.Framework;
using UnityEngine;

// 去背不完全的圖示：背景殘留幾乎看不見的低透明度雜點 (alpha 1~4)，肉眼看不出來，
// 但遮罩、點擊判定會把它們當成圖的一部分。把低於門檻的像素清成完全透明，其餘不動
public class IconCleanupTests
{
    [Test]
    public void ClearFaintAlpha_RemovesNearlyInvisiblePixels()
    {
        Color32[] pixels =
        {
            new Color32(200, 200, 200, 1),
            new Color32(200, 200, 200, 4),
            new Color32(255, 255, 255, 5),
            new Color32(255, 255, 255, 255),
        };

        Color32[] result = IconCleanup.ClearFaintAlpha(pixels, 4);

        Assert.AreEqual(new Color32(0, 0, 0, 0), result[0]);
        Assert.AreEqual(new Color32(0, 0, 0, 0), result[1]);
        Assert.AreEqual(pixels[2], result[2], "above the threshold stays (anti-aliased edge)");
        Assert.AreEqual(pixels[3], result[3]);
    }
}
