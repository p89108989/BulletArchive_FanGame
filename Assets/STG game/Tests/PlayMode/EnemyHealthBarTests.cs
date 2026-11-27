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

    // 蔚藍檔案 Boss 血條：每層 25 血 (100 血 = 4 層)，條上只畫目前這一層，下面露出下一層的顏色
    private EnemyHealthBarUI CreateBossBar(out GameObject panel, out Image current, out Image next, out Text valueText, out Text nameText, out Text layerText)
    {
        bar = new GameObject("BossBar");
        bar.SetActive(false);
        panel = new GameObject("Panel");
        panel.transform.SetParent(bar.transform);
        current = CreateFill("Current", panel.transform);
        next = CreateFill("Next", panel.transform);
        valueText = new GameObject("Value").AddComponent<Text>();
        valueText.transform.SetParent(panel.transform);
        nameText = new GameObject("Name").AddComponent<Text>();
        nameText.transform.SetParent(panel.transform);
        layerText = new GameObject("Layers").AddComponent<Text>();
        layerText.transform.SetParent(panel.transform);

        EnemyHealthBarUI ui = bar.AddComponent<EnemyHealthBarUI>();
        TestUtil.SetField(ui, "panel", panel);
        TestUtil.SetField(ui, "currentFill", current);
        TestUtil.SetField(ui, "nextLayer", next);
        TestUtil.SetField(ui, "valueText", valueText);
        TestUtil.SetField(ui, "nameText", nameText);
        TestUtil.SetField(ui, "layerCountText", layerText);
        TestUtil.SetField(ui, "hpPerLayer", 25);
        bar.SetActive(true);
        return ui;
    }

    private static Image CreateFill(string name, Transform parent)
    {
        Image image = new GameObject(name).AddComponent<Image>();
        image.transform.SetParent(parent);
        image.type = Image.Type.Filled;
        return image;
    }

    [UnityTest]
    public IEnumerator BossBar_HiddenUntilBossAppears()
    {
        CreateBossBar(out GameObject panel, out _, out _, out _, out _, out _);
        yield return null;

        Assert.IsFalse(panel.activeSelf);
    }

    [UnityTest]
    public IEnumerator BossBar_Bind_ShowsNameHealthAndLayers()
    {
        EnemyHealthBarUI ui = CreateBossBar(out GameObject panel, out Image current, out Image next, out Text valueText, out Text nameText, out Text layerText);
        yield return null;

        ui.Bind(health, "Perorozilla");
        health.TakeDamage(30);

        Assert.IsTrue(panel.activeSelf);
        Assert.AreEqual("70 / 100", valueText.text);
        Assert.AreEqual("Perorozilla", nameText.text);
        Assert.AreEqual("x3", layerText.text, "70 HP = inside the 3rd layer");
        Assert.AreEqual(0.8f, current.fillAmount, 0.001f, "20 of the layer's 25");
        Assert.IsTrue(next.gameObject.activeSelf, "the next layer shows underneath");
        Assert.AreNotEqual(current.color, next.color, "each layer has its own colour");
    }

    // 最後一層下面沒有下一層了
    [UnityTest]
    public IEnumerator BossBar_LastLayerHidesNextLayer()
    {
        EnemyHealthBarUI ui = CreateBossBar(out _, out _, out Image next, out _, out _, out Text layerText);
        yield return null;
        ui.Bind(health, "Perorozilla");

        health.TakeDamage(80);

        Assert.AreEqual("x1", layerText.text);
        Assert.IsFalse(next.gameObject.activeSelf);
    }

    // Boss 出場時 BossEncounter 把畫面血條接到生成出來的 Boss 身上
    [UnityTest]
    public IEnumerator BossEncounter_BindsBarToSpawnedBoss()
    {
        CreateBossBar(out GameObject panel, out _, out _, out Text valueText, out Text nameText, out _);
        GameObject encounterObject = new GameObject("Encounter");
        encounterObject.SetActive(false);
        BossEncounter encounter = encounterObject.AddComponent<BossEncounter>();
        BossProfile profile = ScriptableObject.CreateInstance<BossProfile>();
        TestUtil.SetField(profile, "bossPrefab", enemy);
        TestUtil.SetField(profile, "displayName", "Perorozilla");
        TestUtil.SetField(encounter, "bossProfile", profile);
        TestUtil.SetField(encounter, "bossHealthBar", bar.GetComponent<EnemyHealthBarUI>());
        encounterObject.SetActive(true);
        yield return null;

        encounter.OnCombatPhaseChanged(CombatPhase.Boss);
        encounter.Boss.GetComponent<EnemyHealth>().TakeDamage(10);

        Assert.IsTrue(panel.activeSelf);
        Assert.AreEqual("90 / 100", valueText.text, "the bar follows the spawned boss, not the prefab");
        Assert.AreEqual("Perorozilla", nameText.text);
        Object.Destroy(encounter.Boss);
        Object.Destroy(encounterObject);
        Object.Destroy(profile);
    }

    // 血條左側的 Boss 頭像換成目前選定 Boss 的頭像 (BossProfile)
    [UnityTest]
    public IEnumerator BossEncounter_ShowsTheSelectedBossPortrait()
    {
        EnemyHealthBarUI ui = CreateBossBar(out _, out _, out _, out _, out _, out _);
        Image portraitImage = new GameObject("Portrait").AddComponent<Image>();
        portraitImage.transform.SetParent(bar.transform);
        TestUtil.SetField(ui, "portraitImage", portraitImage);
        Texture2D texture = new Texture2D(4, 4);
        Sprite portrait = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), Vector2.one * 0.5f);

        BossProfile profile = ScriptableObject.CreateInstance<BossProfile>();
        TestUtil.SetField(profile, "bossPrefab", enemy);
        TestUtil.SetField(profile, "portrait", portrait);
        GameObject encounterObject = new GameObject("Encounter");
        encounterObject.SetActive(false);
        BossEncounter encounter = encounterObject.AddComponent<BossEncounter>();
        TestUtil.SetField(encounter, "bossProfile", profile);
        TestUtil.SetField(encounter, "bossHealthBar", ui);
        encounterObject.SetActive(true);
        yield return null;

        encounter.OnCombatPhaseChanged(CombatPhase.Boss);

        Assert.AreEqual(portrait, portraitImage.sprite);
        Object.Destroy(encounter.Boss);
        Object.Destroy(encounterObject);
        Object.Destroy(profile);
        Object.Destroy(portrait);
        Object.Destroy(texture);
    }

    [UnityTest]
    public IEnumerator BossBar_HidesWhenBossDies()
    {
        EnemyHealthBarUI ui = CreateBossBar(out GameObject panel, out _, out _, out _, out _, out _);
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
