using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Binah 的長條光炮：從 Binah 朝玩家當下的方向拉一條長條警示 (方向在警示開始時鎖定)，警示結束後光炮掃過，
// 長條內的玩家扣血 (只扣一次)；光炮打過的地面留下火焰，持續期間站在上面每隔一段時間扣血
public class BinahLaneBeamTests
{
    private GameObject boss;
    private GameObject player;
    private GameObject fireZonePrefab;
    private PlayerHealth playerHealth;
    private BossAttackGate gate;
    private BinahLaneBeamAttack attack;

    [SetUp]
    public void SetUp()
    {
        player = new GameObject("Player");
        player.SetActive(false);
        player.transform.position = new Vector3(0f, 0f, 40f);
        playerHealth = player.AddComponent<PlayerHealth>();
        TestUtil.SetField(playerHealth, "maxHealth", 20);
        TestUtil.SetField(playerHealth, "invincibilityDuration", 0f);
        player.SetActive(true);

        fireZonePrefab = new GameObject("FireZone");
        fireZonePrefab.SetActive(false);
        GroundFireZone zonePrefab = fireZonePrefab.AddComponent<GroundFireZone>();
        TestUtil.SetField(zonePrefab, "duration", 0.6f);
        TestUtil.SetField(zonePrefab, "tickInterval", 0.2f);
        TestUtil.SetField(zonePrefab, "damage", 1);

        boss = new GameObject("Binah");
        boss.SetActive(false);
        boss.AddComponent<EnemyHealth>();
        gate = boss.AddComponent<BossAttackGate>();
        attack = boss.AddComponent<BinahLaneBeamAttack>();
        TestUtil.SetField(attack, "target", player.transform);
        TestUtil.SetField(attack, "playerHealth", playerHealth);
        TestUtil.SetField(attack, "firstDelay", 0f);
        TestUtil.SetField(attack, "interval", 30f);
        TestUtil.SetField(attack, "warningDuration", 0.3f);
        TestUtil.SetField(attack, "fireDuration", 0.1f);
        TestUtil.SetField(attack, "damage", 2);
        TestUtil.SetField(attack, "laneWidth", 12f);
        TestUtil.SetField(attack, "laneLength", 100f);
        TestUtil.SetField(attack, "fireZonePrefab", zonePrefab);
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(boss);
        Object.Destroy(player);
        Object.Destroy(fireZonePrefab);
        foreach (GroundFireZone zone in Object.FindObjectsByType<GroundFireZone>(FindObjectsSortMode.None)) Object.Destroy(zone.gameObject);
    }

    private IEnumerator WaitUntil(System.Func<bool> condition, float timeout = 3f)
    {
        float end = Time.realtimeSinceStartup + timeout;
        while (!condition() && Time.realtimeSinceStartup < end) yield return null;
    }

    [UnityTest]
    public IEnumerator PlayerInTheLane_IsHitOnceAfterTheWarning()
    {
        boss.SetActive(true);
        yield return WaitUntil(() => attack.IsWarning);
        Assert.AreEqual(20, playerHealth.CurrentHealth, "no damage during the warning");

        yield return WaitUntil(() => !attack.IsAttacking);
        Assert.AreEqual(18, playerHealth.CurrentHealth);
    }

    // 光炮從 Binah 腳下沿長條往遠處掃：站在遠端的玩家要等光炮掃到才扣血
    [UnityTest]
    public IEnumerator BeamSweepsOutward_FarPlayerIsHitWhenTheSweepArrives()
    {
        TestUtil.SetField(attack, "fireDuration", 0.8f);
        player.transform.position = new Vector3(0f, 0f, 90f);
        boss.SetActive(true);

        yield return WaitUntil(() => attack.IsFiring);
        yield return null;
        Assert.AreEqual(20, playerHealth.CurrentHealth, "the sweep starts near Binah, far from the player");
        Assert.Less(attack.SweepProgress, 0.5f);

        yield return WaitUntil(() => !attack.IsAttacking);
        Assert.AreEqual(18, playerHealth.CurrentHealth, "hit once the sweep passes");
    }

    [UnityTest]
    public IEnumerator LaneIsLockedWhenTheWarningStarts_SteppingOutDodgesIt()
    {
        boss.SetActive(true);
        yield return WaitUntil(() => attack.IsWarning);

        player.transform.position = new Vector3(20f, 0f, 40f);
        yield return WaitUntil(() => !attack.IsAttacking);

        Assert.AreEqual(20, playerHealth.CurrentHealth);
    }

    [UnityTest]
    public IEnumerator LeavesAFireZoneAlongTheLane()
    {
        player.transform.position = new Vector3(20f, 0f, 40f);
        boss.SetActive(true);
        yield return WaitUntil(() => attack.IsWarning);
        player.transform.position = new Vector3(50f, 0f, 0f);  // 光炮打完後再走進火焰

        yield return WaitUntil(() => Object.FindAnyObjectByType<GroundFireZone>() != null);
        GroundFireZone zone = Object.FindAnyObjectByType<GroundFireZone>();
        Assert.IsTrue(zone.Contains(new Vector3(10f, 0f, 20f)), "on the line from Binah toward (20, 40)");
        Assert.IsFalse(zone.Contains(new Vector3(-20f, 0f, 20f)));

        player.transform.position = new Vector3(10f, 0f, 20f);
        yield return new WaitForSeconds(0.5f);
        Assert.Less(playerHealth.CurrentHealth, 20, "burns while standing in the fire");

        yield return new WaitForSeconds(0.5f);
        Assert.IsTrue(zone == null, "the fire goes out after its duration");
    }

    // 火焰一出現 (0 秒) 就先判定一次，不用等第一個 tickInterval
    [UnityTest]
    public IEnumerator FireZone_DamagesImmediatelyWhenItAppears()
    {
        TestUtil.SetField(fireZonePrefab.GetComponent<GroundFireZone>(), "tickInterval", 1f);
        GroundFireZone zone = Object.Instantiate(fireZonePrefab).GetComponent<GroundFireZone>();
        zone.Begin(Vector3.zero, Vector3.forward, 100f, 12f, playerHealth);

        yield return null;
        yield return null;
        Assert.AreEqual(19, playerHealth.CurrentHealth, "first tick at 0 s");
    }

    [UnityTest]
    public IEnumerator WaitsWhileAnotherAttackHoldsTheGate()
    {
        GameObject other = new GameObject("OtherAttack");
        gate.TryAcquire(other);
        boss.SetActive(true);

        yield return new WaitForSeconds(0.3f);
        Assert.IsFalse(attack.IsAttacking);

        gate.Release(other);
        yield return WaitUntil(() => attack.IsAttacking);
        Assert.IsTrue(attack.IsAttacking);
        Object.Destroy(other);
    }
}
