using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Boss 雷射 (追蹤)：蓄力期間紅圈緊貼玩家目前的位置；雷射出現後才打在玩家 1 秒前的位置
public class BossLaserTrackingTests
{
    private GameObject boss;
    private GameObject player;
    private GameObject beamPrefab;
    private GameObject warningPrefab;

    [SetUp]
    public void SetUp()
    {
        player = new GameObject("Player");
        player.AddComponent<PlayerHealth>();

        beamPrefab = new GameObject("BeamPrefab");
        beamPrefab.AddComponent<BossBeamVisual>();

        warningPrefab = new GameObject("WarningPrefab");
        warningPrefab.SetActive(false);
        Transform ring = new GameObject("Ring").transform;
        ring.SetParent(warningPrefab.transform, false);
        Transform fill = new GameObject("Fill").transform;
        fill.SetParent(warningPrefab.transform, false);
        GroundWarningCircle warning = warningPrefab.AddComponent<GroundWarningCircle>();
        TestUtil.SetField(warning, "ring", ring);
        TestUtil.SetField(warning, "fill", fill);

        boss = new GameObject("Boss");
        boss.SetActive(false);
        boss.AddComponent<EnemyHealth>();
        boss.AddComponent<BossAttackGate>();
        BossLaserAttack laser = boss.AddComponent<BossLaserAttack>();
        TestUtil.SetField(laser, "animator", boss.AddComponent<Animator>());
        TestUtil.SetField(laser, "beamPrefab", beamPrefab.GetComponent<BossBeamVisual>());
        TestUtil.SetField(laser, "warningPrefab", warning);
        TestUtil.SetField(laser, "beamOrigins", new[] { boss.transform });
        TestUtil.SetField(laser, "target", player.transform);
        TestUtil.SetField(laser, "trackingChance", 1f);
        TestUtil.SetField(laser, "firstLaserDelay", 0f);
        TestUtil.SetField(laser, "beamStartTime", 1.5f);
        TestUtil.SetField(laser, "groundHeight", 0f);
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(boss);
        Object.Destroy(player);
        Object.Destroy(beamPrefab);
        Object.Destroy(warningPrefab);
        foreach (GroundWarningCircle circle in Object.FindObjectsByType<GroundWarningCircle>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.Destroy(circle.gameObject);
        foreach (BossBeamVisual beam in Object.FindObjectsByType<BossBeamVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.Destroy(beam.gameObject);
    }

    // 掃射趟數在遊戲中 (Inspector 或外部數值表) 改動後，下一次雷射就要照新的趟數掃
    [UnityTest]
    public IEnumerator SweepPasses_ChangedAtRuntime_AppliesToNextLaser()
    {
        BossLaserAttack laser = boss.GetComponent<BossLaserAttack>();
        TestUtil.SetField(laser, "trackingChance", 0f);
        TestUtil.SetField(laser, "sweepPasses", 2);
        boss.SetActive(true);
        yield return null;

        laser.enabled = false;
        TestUtil.SetField(laser, "sweepPasses", 3);
        laser.enabled = true;
        // 下一次雷射在重新啟用後的下一幀開始 (首次延遲為 0)
        yield return null;
        yield return null;

        FieldInfo field = typeof(BossLaserAttack).GetField("sweepPoints", BindingFlags.Instance | BindingFlags.NonPublic);
        Vector2[] points = (Vector2[])field.GetValue(laser);
        Assert.AreEqual(7, points.Length, "3 passes = 7 zigzag points");
    }

    [UnityTest]
    public IEnumerator Warning_StaysOnPlayersCurrentPosition()
    {
        boss.SetActive(true);
        yield return new WaitForSeconds(0.3f);

        player.transform.position = new Vector3(20f, 0f, 10f);
        yield return null;
        yield return null;

        GroundWarningCircle warning = Object.FindFirstObjectByType<GroundWarningCircle>();
        Assert.IsNotNull(warning, "the warning is visible while charging");
        Vector3 offset = warning.transform.position - player.transform.position;
        offset.y = 0f;
        Assert.Less(offset.magnitude, 0.01f, "the warning sticks to the player, not to the delayed position");
    }
}
