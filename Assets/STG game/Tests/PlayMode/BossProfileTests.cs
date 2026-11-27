using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// 選 Boss：BossEncounter 依指定的 BossProfile 生成 Boss (Prefab、位置、名稱)，
// CameraPoseTransition 進入 Boss 戰時移到該 Boss 的相機位置
public class BossProfileTests
{
    private GameObject bossTemplate;
    private GameObject encounterObject;
    private GameObject cameraObject;
    private GameObject transitionObject;
    private BossProfile profile;

    [SetUp]
    public void SetUp()
    {
        bossTemplate = new GameObject("Boss_Test");
        bossTemplate.AddComponent<EnemyHealth>();
        bossTemplate.transform.position = new Vector3(0f, -500f, 0f);

        profile = ScriptableObject.CreateInstance<BossProfile>();
        TestUtil.SetField(profile, "bossPrefab", bossTemplate);
        TestUtil.SetField(profile, "displayName", "Binah");
        TestUtil.SetField(profile, "spawnPosition", new Vector3(10f, -100f, -40f));
        TestUtil.SetField(profile, "spawnRotation", new Vector3(0f, 90f, 0f));
        TestUtil.SetField(profile, "cameraPosition", new Vector3(-50f, 90f, 110f));
        TestUtil.SetField(profile, "cameraRotation", new Vector3(50f, 180f, 0f));
    }

    [TearDown]
    public void TearDown()
    {
        BossEncounter encounter = encounterObject != null ? encounterObject.GetComponent<BossEncounter>() : null;
        if (encounter != null && encounter.Boss != null) Object.Destroy(encounter.Boss);
        Object.Destroy(bossTemplate);
        Object.Destroy(profile);
        if (encounterObject != null) Object.Destroy(encounterObject);
        if (cameraObject != null) Object.Destroy(cameraObject);
        if (transitionObject != null) Object.Destroy(transitionObject);
    }

    private BossEncounter CreateEncounter()
    {
        encounterObject = new GameObject("Encounter");
        encounterObject.SetActive(false);
        BossEncounter encounter = encounterObject.AddComponent<BossEncounter>();
        TestUtil.SetField(encounter, "bossProfile", profile);
        encounterObject.SetActive(true);
        return encounter;
    }

    [UnityTest]
    public IEnumerator Encounter_SpawnsTheSelectedBossAtItsSpawnPoint()
    {
        BossEncounter encounter = CreateEncounter();
        yield return null;

        encounter.OnCombatPhaseChanged(CombatPhase.Boss);

        Assert.IsNotNull(encounter.Boss);
        Assert.AreEqual("Boss_Test", encounter.Boss.name);
        Assert.AreEqual(new Vector3(10f, -100f, -40f), encounter.Boss.transform.position);
        Assert.AreEqual(90f, encounter.Boss.transform.eulerAngles.y, 0.01f);
    }

    [UnityTest]
    public IEnumerator CameraTransition_UsesTheSelectedBossCameraPose()
    {
        BossEncounter encounter = CreateEncounter();
        cameraObject = new GameObject("Camera");
        cameraObject.transform.SetPositionAndRotation(new Vector3(-50f, 65f, 90f), Quaternion.Euler(60f, 180f, 0f));

        transitionObject = new GameObject("CameraTransition");
        transitionObject.SetActive(false);
        CameraPoseTransition transition = transitionObject.AddComponent<CameraPoseTransition>();
        TestUtil.SetField(transition, "cameraTransform", cameraObject.transform);
        TestUtil.SetField(transition, "bossEncounter", encounter);
        TestUtil.SetField(transition, "transitionDuration", 0f);
        transitionObject.SetActive(true);
        yield return null;

        transition.OnCombatPhaseChanged(CombatPhase.Boss);
        yield return null;

        Assert.AreEqual(new Vector3(-50f, 90f, 110f), cameraObject.transform.position);
        Assert.AreEqual(50f, cameraObject.transform.eulerAngles.x, 0.01f);
    }
}
