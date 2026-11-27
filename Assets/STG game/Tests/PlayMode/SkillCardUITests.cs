using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// HUD 技能卡：依技能狀態顯示暗色遮罩、剩餘秒數、鎖頭
public class SkillCardUITests
{
    private GameObject player;
    private GameObject card;
    private PlayerMovement movement;

    [SetUp]
    public void SetUp()
    {
        player = new GameObject("Player");
        player.AddComponent<CapsuleCollider>();
        movement = player.AddComponent<PlayerMovement>();
        player.GetComponent<Rigidbody>().useGravity = false;
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(player);
        Object.Destroy(card);
    }

    private SkillCardUI CreateCard(SkillCardUI.SkillKind kind, out Image dim, out Image cooldown, out Text seconds, out GameObject lockIcon)
    {
        card = new GameObject("Card");
        card.SetActive(false);
        dim = new GameObject("Dim").AddComponent<Image>();
        dim.transform.SetParent(card.transform);
        cooldown = new GameObject("Cooldown").AddComponent<Image>();
        cooldown.transform.SetParent(card.transform);
        cooldown.type = Image.Type.Filled;
        seconds = new GameObject("Seconds").AddComponent<Text>();
        seconds.transform.SetParent(card.transform);
        lockIcon = new GameObject("Lock");
        lockIcon.transform.SetParent(card.transform);

        SkillCardUI ui = card.AddComponent<SkillCardUI>();
        TestUtil.SetField(ui, "kind", kind);
        TestUtil.SetField(ui, "playerMovement", movement);
        TestUtil.SetField(ui, "dimOverlay", dim.gameObject);
        TestUtil.SetField(ui, "cooldownFill", cooldown);
        TestUtil.SetField(ui, "secondsLabel", seconds);
        TestUtil.SetField(ui, "lockIcon", lockIcon);
        card.SetActive(true);
        return ui;
    }

    [UnityTest]
    public IEnumerator DashCard_ReadyIsBright()
    {
        CreateCard(SkillCardUI.SkillKind.Dash, out Image dim, out Image cooldown, out Text seconds, out _);
        yield return null;

        Assert.IsFalse(dim.gameObject.activeSelf);
        Assert.IsFalse(cooldown.gameObject.activeSelf);
        Assert.IsFalse(seconds.gameObject.activeSelf);
    }

    [UnityTest]
    public IEnumerator DashCard_AfterDashShowsCooldown()
    {
        CreateCard(SkillCardUI.SkillKind.Dash, out Image dim, out Image cooldown, out Text seconds, out _);
        yield return null;

        movement.TryStartDash();
        yield return null;

        // 冷卻中只顯示圓形倒數的暗色扇形；整張卡再加一層暗色會讓扇形的亮暗對比看不出來
        Assert.IsFalse(dim.gameObject.activeSelf, "only the radial wedge darkens the card while cooling down");
        Assert.IsTrue(cooldown.gameObject.activeSelf);
        Assert.Greater(cooldown.fillAmount, 0.9f, "just used: almost the whole cooldown is left");
        Assert.AreEqual(Mathf.CeilToInt(movement.DashCooldown).ToString(), seconds.text);
    }

    [UnityTest]
    public IEnumerator SpecialCard_IsLocked()
    {
        CreateCard(SkillCardUI.SkillKind.Special, out Image dim, out _, out Text seconds, out GameObject lockIcon);
        yield return null;

        Assert.IsTrue(lockIcon.activeSelf);
        Assert.IsTrue(dim.gameObject.activeSelf);
        Assert.IsFalse(seconds.gameObject.activeSelf);
    }
}
