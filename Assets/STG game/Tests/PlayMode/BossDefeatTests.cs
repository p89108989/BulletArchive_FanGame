using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// Boss 被擊倒：播死亡動畫 → 沉下去消失 → 依設定回到一般戰鬥 (視角、天色跟著換回) 或顯示結算面板
public class BossDefeatTests
{
    private GameObject bossTemplate;
    private GameObject controllerObject;
    private CombatPhaseController phaseController;
    private GameObject encounterObject;
    private BossEncounter encounter;
    private GameObject resultObject;
    private GameObject player;

    [SetUp]
    public void SetUp()
    {
        bossTemplate = CreateBoss("BossTemplate", out _);
        bossTemplate.transform.position = new Vector3(0f, -500f, 0f);

        controllerObject = new GameObject("Phase");
        controllerObject.SetActive(false);
        phaseController = controllerObject.AddComponent<CombatPhaseController>();
        TestUtil.SetField(phaseController, "bossStartDelay", 999f);
        controllerObject.SetActive(true);

        encounterObject = new GameObject("Encounter");
        encounterObject.SetActive(false);
        encounter = encounterObject.AddComponent<BossEncounter>();
        TestUtil.SetField(encounter, "bossPrefab", bossTemplate);
        TestUtil.SetField(encounter, "phaseController", phaseController);
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        if (encounter != null && encounter.Boss != null) Object.Destroy(encounter.Boss);
        Object.Destroy(bossTemplate);
        Object.Destroy(controllerObject);
        Object.Destroy(encounterObject);
        if (resultObject != null) Object.Destroy(resultObject);
        if (player != null) Object.Destroy(player);
    }

    private static GameObject CreateBoss(string name, out BoxCollider bodyCollider)
    {
        GameObject boss = new GameObject(name);
        boss.SetActive(false);
        boss.AddComponent<EnemyHealth>();
        bodyCollider = new GameObject("Body").AddComponent<BoxCollider>();
        bodyCollider.transform.SetParent(boss.transform, false);
        BossDeathPerformance performance = boss.AddComponent<BossDeathPerformance>();
        TestUtil.SetField(performance, "deathAnimationDuration", 0.2f);
        TestUtil.SetField(performance, "sinkDuration", 0.2f);
        TestUtil.SetField(performance, "sinkDistance", 10f);
        boss.SetActive(true);
        return boss;
    }

    [UnityTest]
    public IEnumerator DeathPerformance_StopsBeingHittableThenSinksAndDisappears()
    {
        GameObject boss = CreateBoss("Boss", out BoxCollider body);
        BossDeathPerformance performance = boss.GetComponent<BossDeathPerformance>();
        int finishedCount = 0;
        performance.OnFinished += () => finishedCount++;
        yield return null;

        boss.GetComponent<EnemyHealth>().TakeDamage(9999);
        Assert.IsFalse(body.enabled, "the dying boss can no longer be hit");

        yield return new WaitForSeconds(0.3f);
        Assert.IsTrue(boss != null, "still on screen while sinking");
        Assert.Less(boss.transform.position.y, -0.5f, "sinks after the death animation");

        yield return new WaitForSeconds(0.3f);
        Assert.AreEqual(1, finishedCount);
        Assert.IsTrue(boss == null, "the boss disappears");
    }

    // 下沉消失時冒出煙霧遮住 Boss；煙霧不掛在 Boss 底下，Boss 移除後仍留在原地散開
    [UnityTest]
    public IEnumerator DeathPerformance_PuffsSmokeWhileSinking()
    {
        GameObject smokeTemplate = new GameObject("VanishSmoke");
        smokeTemplate.AddComponent<ParticleSystem>();
        GameObject boss = CreateBoss("Boss", out _);
        BossDeathPerformance performance = boss.GetComponent<BossDeathPerformance>();
        TestUtil.SetField(performance, "vanishSmokePrefab", smokeTemplate);
        TestUtil.SetField(performance, "smokeCount", 3);
        yield return null;

        boss.GetComponent<EnemyHealth>().TakeDamage(9999);
        yield return new WaitForSeconds(0.1f);
        Assert.AreEqual(0, CountSmoke(), "no smoke during the death animation");

        // 演出共 0.4 秒；多留一些餘裕，編輯器失焦降幀時也不會誤判
        yield return new WaitForSeconds(0.8f);
        Assert.IsTrue(boss == null, "the boss is gone after sinking");
        Assert.AreEqual(3, CountSmoke(), "all smoke clouds were spawned and outlive the boss");

        foreach (ParticleSystem smoke in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) Object.Destroy(smoke.gameObject);
    }

    private static int CountSmoke()
    {
        int count = 0;
        foreach (ParticleSystem smoke in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
        {
            if (smoke.name == "VanishSmoke(Clone)") count++;
        }

        return count;
    }

    [UnityTest]
    public IEnumerator ReturnToNormal_SwitchesPhaseBackAfterPerformance()
    {
        TestUtil.SetField(encounter, "defeatOutcome", BossEncounter.DefeatOutcome.ReturnToNormal);
        encounterObject.SetActive(true);
        phaseController.EnterPhase(CombatPhase.Boss);
        encounter.OnCombatPhaseChanged(CombatPhase.Boss);
        yield return null;

        encounter.Boss.GetComponent<EnemyHealth>().TakeDamage(9999);
        yield return new WaitForSeconds(0.1f);
        Assert.AreEqual(CombatPhase.Boss, phaseController.CurrentPhase, "waits for the death performance");

        yield return new WaitForSeconds(0.5f);
        Assert.AreEqual(CombatPhase.Normal, phaseController.CurrentPhase);
    }

    [UnityTest]
    public IEnumerator ShowResults_OpensResultPanelAfterPerformance()
    {
        player = new GameObject("Player");
        PlayerHealth playerHealth = player.AddComponent<PlayerHealth>();

        resultObject = new GameObject("Results");
        resultObject.SetActive(false);
        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(resultObject.transform);
        Text detail = new GameObject("Detail").AddComponent<Text>();
        detail.transform.SetParent(panel.transform);
        BossResultScreen results = resultObject.AddComponent<BossResultScreen>();
        TestUtil.SetField(results, "panel", panel);
        TestUtil.SetField(results, "detailText", detail);
        TestUtil.SetField(results, "playerHealth", playerHealth);
        TestUtil.SetField(results, "reloadSceneOnRestart", false);
        resultObject.SetActive(true);

        TestUtil.SetField(encounter, "defeatOutcome", BossEncounter.DefeatOutcome.ShowResults);
        TestUtil.SetField(encounter, "resultScreen", results);
        encounterObject.SetActive(true);
        phaseController.EnterPhase(CombatPhase.Boss);
        encounter.OnCombatPhaseChanged(CombatPhase.Boss);
        yield return null;
        Assert.IsFalse(panel.activeSelf, "hidden during the fight");

        encounter.Boss.GetComponent<EnemyHealth>().TakeDamage(9999);
        // 面板出現後遊戲時間會暫停，要用真實時間等
        yield return new WaitForSecondsRealtime(0.6f);

        Assert.IsTrue(panel.activeSelf);
        StringAssert.Contains("剩餘血量  " + playerHealth.MaxHealth + " / " + playerHealth.MaxHealth, detail.text);
        StringAssert.Contains("00:00", detail.text, "clear time since the boss appeared");
        Assert.AreEqual(0f, Time.timeScale, "the game pauses on the result panel");
        Assert.AreEqual(CombatPhase.Boss, phaseController.CurrentPhase);
    }
}
