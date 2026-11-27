using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// HUD 左下的玩家血條 (蔚藍檔案角色血條風格)：綠色血量立刻反映傷害，
// 白色的「剛扣掉的血」先停留一下再追上目前血量，看得出這次扣了多少
public class PlayerHealthBarUITests
{
    private GameObject player;
    private GameObject bar;
    private PlayerHealth health;
    private Image fill;
    private Image trail;

    [SetUp]
    public void SetUp()
    {
        player = new GameObject("Player");
        player.SetActive(false);
        health = player.AddComponent<PlayerHealth>();
        // 與場景中 Rio 相同的 20 血，1/4、1/2 才是整數
        TestUtil.SetField(health, "maxHealth", 20);
        TestUtil.SetField(health, "invincibilityDuration", 0f);
        player.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(player);
        Object.Destroy(bar);
    }

    private void CreateBar(float trailDelay, float trailSpeed)
    {
        bar = new GameObject("Bar");
        bar.SetActive(false);
        fill = new GameObject("Fill").AddComponent<Image>();
        fill.transform.SetParent(bar.transform);
        trail = new GameObject("Trail").AddComponent<Image>();
        trail.transform.SetParent(bar.transform);
        PlayerHealthBarUI ui = bar.AddComponent<PlayerHealthBarUI>();
        TestUtil.SetField(ui, "playerHealth", health);
        TestUtil.SetField(ui, "fill", fill);
        TestUtil.SetField(ui, "damageTrail", trail);
        TestUtil.SetField(ui, "trailDelay", trailDelay);
        TestUtil.SetField(ui, "trailSpeed", trailSpeed);
        bar.SetActive(true);
    }

    [UnityTest]
    public IEnumerator StartsFull()
    {
        CreateBar(0.3f, 1f);
        yield return null;

        Assert.AreEqual(1f, fill.fillAmount, 0.001f);
        Assert.AreEqual(1f, trail.fillAmount, 0.001f);
    }

    [UnityTest]
    public IEnumerator Damage_DropsFillAtOnceAndTrailLingers()
    {
        CreateBar(0.3f, 1f);
        yield return null;

        health.TakeDamage(health.MaxHealth / 4);
        yield return null;

        Assert.AreEqual(0.75f, fill.fillAmount, 0.001f, "the green bar drops immediately");
        Assert.AreEqual(1f, trail.fillAmount, 0.001f, "the white trail still shows the lost part");
    }

    [UnityTest]
    public IEnumerator Trail_CatchesUpAfterDelay()
    {
        CreateBar(0.1f, 5f);
        yield return null;

        health.TakeDamage(health.MaxHealth / 2);
        yield return new WaitForSeconds(0.5f);

        Assert.AreEqual(0.5f, trail.fillAmount, 0.001f);
    }
}
