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
    private BossProfile profile;

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
        profile = ScriptableObject.CreateInstance<BossProfile>();
        TestUtil.SetField(profile, "bossPrefab", bossTemplate);
        TestUtil.SetField(encounter, "bossProfile", profile);
        TestUtil.SetField(encounter, "phaseController", phaseController);
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        if (encounter != null && encounter.Boss != null) Object.Destroy(encounter.Boss);
        Object.Destroy(bossTemplate);
        Object.Destroy(profile);
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

        yield return WaitUntil(() => boss == null || boss.transform.position.y < -0.5f);
        Assert.IsTrue(boss != null, "still on screen while sinking");
        Assert.Less(boss.transform.position.y, -0.5f, "sinks after the death animation");

        yield return WaitUntil(() => boss == null);
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

        yield return WaitUntil(() => boss == null);
        Assert.IsTrue(boss == null, "the boss is gone after sinking");
        Assert.AreEqual(3, CountSmoke(), "all smoke clouds were spawned and outlive the boss");

        foreach (ParticleSystem smoke in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) Object.Destroy(smoke.gameObject);
    }

    // 等到條件成立 (最多等 RealtimeTimeout 秒的真實時間)。編輯器失焦時 Unity 會降低幀率、遊戲時間走得比真實時間慢，
    // 固定等幾秒會誤判；結算面板出現後遊戲時間也會暫停，所以用真實時間計算逾時
    private const float RealtimeTimeout = 5f;

    private static IEnumerator WaitUntil(System.Func<bool> condition)
    {
        float deadline = Time.realtimeSinceStartup + RealtimeTimeout;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
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

        yield return WaitUntil(() => phaseController.CurrentPhase == CombatPhase.Normal);
        Assert.AreEqual(CombatPhase.Normal, phaseController.CurrentPhase);
    }

    [UnityTest]
    public IEnumerator ShowResults_OpensResultPanelAfterPerformance()
    {
        player = new GameObject("Player");
        PlayerHealth playerHealth = player.AddComponent<PlayerHealth>();

        resultObject = new GameObject("Results");
        BattleFlow flow = TestUtil.CreateBattleFlowWithResults(resultObject, playerHealth, out GameObject panel, out Text timeText);
        yield return null;

        TestUtil.SetField(encounter, "defeatOutcome", BossEncounter.DefeatOutcome.ShowResults);
        TestUtil.SetField(encounter, "battleFlow", flow);
        encounterObject.SetActive(true);
        phaseController.EnterPhase(CombatPhase.Boss);
        encounter.OnCombatPhaseChanged(CombatPhase.Boss);
        yield return null;
        Assert.IsFalse(panel.activeSelf, "hidden during the fight");

        encounter.Boss.GetComponent<EnemyHealth>().TakeDamage(9999);
        yield return WaitUntil(() => panel.activeSelf);

        Assert.IsTrue(panel.activeSelf);
        Assert.AreEqual("00:00", timeText.text, "battle time on the results");
        Assert.AreEqual(0f, Time.timeScale, "the game pauses on the result panel");
        Assert.AreEqual(CombatPhase.Boss, phaseController.CurrentPhase);
    }
}
