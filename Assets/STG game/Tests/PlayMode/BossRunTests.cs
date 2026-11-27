using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// 從選角場景帶進來的 Boss 清單：無盡模式擊倒後換下一隻 (回到一般戰鬥、倒數後再出場)，打完最後一隻從頭再來；
// 單一 Boss 擊倒後顯示結算；沒有選擇時照場景原本設定的 Boss
public class BossRunTests
{
    private GameObject bossTemplate;
    private GameObject controllerObject;
    private CombatPhaseController phaseController;
    private GameObject encounterObject;
    private BossEncounter encounter;
    private GameSelection selection;
    private BossProfile sceneProfile;
    private BossProfile a;
    private BossProfile b;
    private GameObject resultObject;
    private GameObject panel;
    private GameObject player;

    [SetUp]
    public void SetUp()
    {
        bossTemplate = new GameObject("BossTemplate");
        bossTemplate.SetActive(false);
        bossTemplate.AddComponent<EnemyHealth>();
        BossDeathPerformance performance = bossTemplate.AddComponent<BossDeathPerformance>();
        TestUtil.SetField(performance, "deathAnimationDuration", 0.05f);
        TestUtil.SetField(performance, "sinkDuration", 0.05f);
        bossTemplate.transform.position = new Vector3(0f, -500f, 0f);
        bossTemplate.SetActive(true);

        controllerObject = new GameObject("Phase");
        controllerObject.SetActive(false);
        phaseController = controllerObject.AddComponent<CombatPhaseController>();
        TestUtil.SetField(phaseController, "bossStartDelay", 999f);

        encounterObject = new GameObject("Encounter");
        encounterObject.SetActive(false);
        encounter = encounterObject.AddComponent<BossEncounter>();
        TestUtil.SetField(phaseController, "phaseListeners", new MonoBehaviour[] { encounter });

        sceneProfile = CreateProfile();
        a = CreateProfile();
        b = CreateProfile();
        selection = ScriptableObject.CreateInstance<GameSelection>();
        TestUtil.SetField(encounter, "bossProfile", sceneProfile);
        TestUtil.SetField(encounter, "phaseController", phaseController);
        TestUtil.SetField(encounter, "gameSelection", selection);
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        if (encounter != null && encounter.Boss != null) Object.Destroy(encounter.Boss);
        Object.Destroy(bossTemplate);
        Object.Destroy(controllerObject);
        Object.Destroy(encounterObject);
        Object.Destroy(selection);
        Object.Destroy(sceneProfile);
        Object.Destroy(a);
        Object.Destroy(b);
        if (resultObject != null) Object.Destroy(resultObject);
        if (player != null) Object.Destroy(player);
    }

    private BossProfile CreateProfile()
    {
        BossProfile profile = ScriptableObject.CreateInstance<BossProfile>();
        TestUtil.SetField(profile, "bossPrefab", bossTemplate);
        return profile;
    }

    private static IEnumerator WaitUntil(System.Func<bool> condition)
    {
        float deadline = Time.realtimeSinceStartup + 5f;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
    }

    private void Begin()
    {
        controllerObject.SetActive(true);
        encounterObject.SetActive(true);
    }

    // 進 Boss 戰、把 Boss 打倒並等到死亡演出結束
    private IEnumerator FightAndDefeat()
    {
        phaseController.EnterPhase(CombatPhase.Boss);
        yield return null;
        Assert.IsNotNull(encounter.Boss, "the boss spawned");
        GameObject boss = encounter.Boss;
        boss.GetComponent<EnemyHealth>().TakeDamage(99999);
        yield return WaitUntil(() => boss == null);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Endless_AfterDefeat_NextBossProfileIsCurrent()
    {
        selection.Select("CH0331", new[] { a, b }, true);
        Begin();
        Assert.AreSame(a, encounter.Profile);

        yield return FightAndDefeat();

        Assert.AreSame(b, encounter.Profile, "camera and sky read the next boss before it appears");
        Assert.AreEqual(1, encounter.BossIndex);
        Assert.AreEqual(CombatPhase.Normal, phaseController.CurrentPhase);
    }

    [UnityTest]
    public IEnumerator Endless_LoopsBackToFirst()
    {
        selection.Select("CH0331", new[] { a, b }, true);
        Begin();

        yield return FightAndDefeat();
        yield return FightAndDefeat();

        Assert.AreSame(a, encounter.Profile);
        Assert.AreEqual(CombatPhase.Normal, phaseController.CurrentPhase);
    }

    [UnityTest]
    public IEnumerator SingleBoss_FromSelection_ShowsResults()
    {
        player = new GameObject("Player");
        PlayerHealth playerHealth = player.AddComponent<PlayerHealth>();
        resultObject = new GameObject("Results");
        BattleFlow flow = TestUtil.CreateBattleFlowWithResults(resultObject, playerHealth, out panel, out _);
        TestUtil.SetField(encounter, "battleFlow", flow);
        TestUtil.SetField(encounter, "defeatOutcome", BossEncounter.DefeatOutcome.ReturnToNormal);

        selection.Select("CH0331", new[] { a }, false);
        Begin();
        yield return FightAndDefeat();
        yield return WaitUntil(() => panel.activeSelf);

        Assert.IsTrue(panel.activeSelf, "a single boss picked in the select screen ends with the results");
    }

    [UnityTest]
    public IEnumerator NoSelection_UsesSceneProfile()
    {
        Begin();
        Assert.AreSame(sceneProfile, encounter.Profile);

        yield return FightAndDefeat();

        Assert.AreSame(sceneProfile, encounter.Profile, "without a selection nothing changes");
        Assert.AreEqual(CombatPhase.Normal, phaseController.CurrentPhase);
    }
}
