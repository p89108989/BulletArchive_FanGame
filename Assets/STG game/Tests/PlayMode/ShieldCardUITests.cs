using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// HUD 護盾卡：卡底顏色跟著玩家目前的護盾極性 (藍 / 黃) 變化
public class ShieldCardUITests
{
    private static readonly Color Blue = new Color(0f, 0.4f, 0.9f, 1f);
    private static readonly Color Yellow = new Color(0.95f, 0.65f, 0f, 1f);

    private GameObject player;
    private GameObject card;
    private PlayerPolarity polarity;

    [SetUp]
    public void SetUp()
    {
        player = new GameObject("Player");
        polarity = player.AddComponent<PlayerPolarity>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(player);
        Object.Destroy(card);
    }

    private Image CreateCard()
    {
        card = new GameObject("ShieldCard");
        card.SetActive(false);
        Image background = card.AddComponent<Image>();

        ShieldCardUI ui = card.AddComponent<ShieldCardUI>();
        TestUtil.SetField(ui, "playerPolarity", polarity);
        TestUtil.SetField(ui, "background", background);
        TestUtil.SetField(ui, "blueColor", Blue);
        TestUtil.SetField(ui, "yellowColor", Yellow);
        card.SetActive(true);
        return background;
    }

    [UnityTest]
    public IEnumerator ShowsCurrentPolarityOnEnable()
    {
        polarity.SetPolarity(BulletPolarity.Yellow);
        Image background = CreateCard();
        yield return null;

        Assert.AreEqual(Yellow, background.color);
    }

    [UnityTest]
    public IEnumerator FollowsPolaritySwitch()
    {
        polarity.SetPolarity(BulletPolarity.Blue);
        Image background = CreateCard();
        yield return null;

        polarity.SetPolarity(BulletPolarity.Yellow);
        Assert.AreEqual(Yellow, background.color, "switching to yellow recolors the card");

        polarity.SetPolarity(BulletPolarity.Blue);
        Assert.AreEqual(Blue, background.color, "and back to blue");
    }

    // 護盾暫時失效 (Binah 光炮機制) 時卡片變灰，恢復時回到目前極性的顏色
    [UnityTest]
    public IEnumerator SuppressedShield_TurnsGrey_ThenRestores()
    {
        Color grey = new Color(0.35f, 0.35f, 0.38f, 1f);
        polarity.SetPolarity(BulletPolarity.Yellow);
        Image background = CreateCard();
        TestUtil.SetField(card.GetComponent<ShieldCardUI>(), "suppressedColor", grey);
        yield return null;

        polarity.SetShieldSuppressed(true);
        Assert.AreEqual(grey, background.color);

        polarity.SetPolarity(BulletPolarity.Blue);
        Assert.AreEqual(grey, background.color, "switching while suppressed stays grey");

        polarity.SetShieldSuppressed(false);
        Assert.AreEqual(Blue, background.color);
    }

    [UnityTest]
    public IEnumerator EnabledWhileSuppressed_StartsGrey()
    {
        Color grey = new Color(0.35f, 0.35f, 0.38f, 1f);
        polarity.SetShieldSuppressed(true);
        card = new GameObject("ShieldCard");
        card.SetActive(false);
        Image background = card.AddComponent<Image>();
        ShieldCardUI ui = card.AddComponent<ShieldCardUI>();
        TestUtil.SetField(ui, "playerPolarity", polarity);
        TestUtil.SetField(ui, "background", background);
        TestUtil.SetField(ui, "suppressedColor", grey);
        card.SetActive(true);
        yield return null;

        Assert.AreEqual(grey, background.color);
    }

    [UnityTest]
    public IEnumerator StopsListeningWhenDisabled()
    {
        polarity.SetPolarity(BulletPolarity.Blue);
        Image background = CreateCard();
        yield return null;

        card.SetActive(false);
        polarity.SetPolarity(BulletPolarity.Yellow);

        Assert.AreEqual(Blue, background.color);
    }
}
