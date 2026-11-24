using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// 敵方血條：Boss 的畫面上方血條 (UI) 與小怪頭上的血條 (世界空間、始終面向鏡頭)
public class EnemyHealthBarTests
{
    private GameObject enemy;
    private EnemyHealth health;
    private GameObject bar;

    [SetUp]
    public void SetUp()
    {
        enemy = new GameObject("Enemy");
        enemy.SetActive(false);
        health = enemy.AddComponent<EnemyHealth>();
        TestUtil.SetField(health, "maxHealth", 100);
        enemy.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(enemy);
        Object.Destroy(bar);
    }

    private EnemyHealthBarUI CreateBossBar(out GameObject panel, out Slider slider, out Text valueText, out Text nameText)
    {
        bar = new GameObject("BossBar");
        bar.SetActive(false);
        panel = new GameObject("Panel");
        panel.transform.SetParent(bar.transform);
        slider = new GameObject("Slider").AddComponent<Slider>();
        slider.transform.SetParent(panel.transform);
        valueText = new GameObject("Value").AddComponent<Text>();
        valueText.transform.SetParent(panel.transform);
        nameText = new GameObject("Name").AddComponent<Text>();
        nameText.transform.SetParent(panel.transform);

        EnemyHealthBarUI ui = bar.AddComponent<EnemyHealthBarUI>();
        TestUtil.SetField(ui, "panel", panel);
        TestUtil.SetField(ui, "healthSlider", slider);
        TestUtil.SetField(ui, "valueText", valueText);
        TestUtil.SetField(ui, "nameText", nameText);
        bar.SetActive(true);
        return ui;
    }

    [UnityTest]
    public IEnumerator BossBar_HiddenUntilBossAppears()
    {
        CreateBossBar(out GameObject panel, out _, out _, out _);
        yield return null;

        Assert.IsFalse(panel.activeSelf);
    }

    [UnityTest]
    public IEnumerator BossBar_Bind_ShowsNameAndHealth()
    {
        EnemyHealthBarUI ui = CreateBossBar(out GameObject panel, out Slider slider, out Text valueText, out Text nameText);
        yield return null;

        ui.Bind(health, "Perorozilla");
        health.TakeDamage(30);

        Assert.IsTrue(panel.activeSelf);
        Assert.AreEqual(100f, slider.maxValue);
        Assert.AreEqual(70f, slider.value);
        Assert.AreEqual("70 / 100", valueText.text);
        Assert.AreEqual("Perorozilla", nameText.text);
    }

    // Boss 出場時 BossEncounter 把畫面血條接到生成出來的 Boss 身上
    [UnityTest]
    public IEnumerator BossEncounter_BindsBarToSpawnedBoss()
    {
        CreateBossBar(out GameObject panel, out Slider slider, out _, out Text nameText);
        GameObject encounterObject = new GameObject("Encounter");
        encounterObject.SetActive(false);
        BossEncounter encounter = encounterObject.AddComponent<BossEncounter>();
        TestUtil.SetField(encounter, "bossPrefab", enemy);
        TestUtil.SetField(encounter, "bossHealthBar", bar.GetComponent<EnemyHealthBarUI>());
        TestUtil.SetField(encounter, "bossDisplayName", "Perorozilla");
        encounterObject.SetActive(true);
        yield return null;

        encounter.OnCombatPhaseChanged(CombatPhase.Boss);
        encounter.Boss.GetComponent<EnemyHealth>().TakeDamage(10);

        Assert.IsTrue(panel.activeSelf);
        Assert.AreEqual(90f, slider.value, "the bar follows the spawned boss, not the prefab");
        Assert.AreEqual("Perorozilla", nameText.text);
        Object.Destroy(encounter.Boss);
        Object.Destroy(encounterObject);
    }

    [UnityTest]
    public IEnumerator BossBar_HidesWhenBossDies()
    {
        EnemyHealthBarUI ui = CreateBossBar(out GameObject panel, out _, out _, out _);
        yield return null;
        ui.Bind(health, "Perorozilla");

        health.TakeDamage(100);

        Assert.IsFalse(panel.activeSelf);
    }

    private WorldHealthBar CreateWorldBar(out Transform fill, out GameObject visual)
    {
        bar = new GameObject("WorldBar");
        bar.SetActive(false);
        bar.transform.SetParent(enemy.transform, false);
        visual = new GameObject("Visual");
        visual.transform.SetParent(bar.transform, false);
        fill = new GameObject("Fill").transform;
        fill.SetParent(visual.transform, false);

        WorldHealthBar worldBar = bar.AddComponent<WorldHealthBar>();
        TestUtil.SetField(worldBar, "health", health);
        TestUtil.SetField(worldBar, "visual", visual);
        TestUtil.SetField(worldBar, "fill", fill);
        TestUtil.SetField(worldBar, "width", 8f);
        bar.SetActive(true);
        return worldBar;
    }

    [UnityTest]
    public IEnumerator WorldBar_FillShrinksFromRightWithHealth()
    {
        CreateWorldBar(out Transform fill, out _);
        yield return null;

        health.TakeDamage(25);

        Assert.AreEqual(6f, fill.localScale.x, 0.01f, "75% of an 8 m bar");
        Assert.AreEqual(-1f, fill.localPosition.x, 0.01f, "the left edge stays put, so the fill shifts left by half the lost width");
    }

    [UnityTest]
    public IEnumerator WorldBar_FacesTheCamera()
    {
        WorldHealthBar worldBar = CreateWorldBar(out _, out _);
        Camera camera = new GameObject("Camera").AddComponent<Camera>();
        camera.transform.rotation = Quaternion.Euler(40f, 160f, 0f);
        yield return null;

        worldBar.FaceCamera(camera);

        Assert.Greater(Quaternion.Dot(bar.transform.rotation, camera.transform.rotation), 0.9999f);
        Object.Destroy(camera.gameObject);
    }

    [UnityTest]
    public IEnumerator WorldBar_HidesWhenEnemyDies()
    {
        CreateWorldBar(out _, out GameObject visual);
        yield return null;

        health.TakeDamage(100);

        Assert.IsFalse(visual.activeSelf);
    }
}
