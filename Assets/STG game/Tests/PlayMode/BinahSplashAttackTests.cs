using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Binah 的例行攻擊「換位潑沙」：每 interval 秒隨機換到另一個位置 (潛入 → 竄出)，竄出時潑沙並在前方堆一個沙堆
// (場上最多 3 個，滿了不堆)；光炮特殊機制進行中暫停；Boss 死亡後停止
public class BinahSplashAttackTests
{
    private static readonly Vector3 SpotA = Vector3.zero;
    private static readonly Vector3 SpotB = new Vector3(50f, 0f, 0f);

    private GameObject boss;
    private GameObject dunePrefab;
    private EnemyHealth health;
    private BinahDuneField duneField;
    private BinahCannonMechanic mechanic;
    private BinahSplashAttack attack;

    [SetUp]
    public void SetUp()
    {
        dunePrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dunePrefab.name = "Dune";
        dunePrefab.SetActive(false);
        dunePrefab.AddComponent<BinahDune>();

        boss = new GameObject("Binah");
        boss.SetActive(false);
        health = boss.AddComponent<EnemyHealth>();
        TestUtil.SetField(health, "maxHealth", 100);

        BinahReposition reposition = boss.AddComponent<BinahReposition>();
        TestUtil.SetField(reposition, "diveDuration", 0.1f);
        TestUtil.SetField(reposition, "emergeDelay", 0.05f);
        TestUtil.SetField(reposition, "settleDuration", 0.05f);

        duneField = boss.AddComponent<BinahDuneField>();
        TestUtil.SetField(duneField, "dunePrefab", dunePrefab);
        TestUtil.SetField(duneField, "areaMin", new Vector2(-200f, -200f));
        TestUtil.SetField(duneField, "areaMax", new Vector2(200f, 200f));

        mechanic = boss.AddComponent<BinahCannonMechanic>();
        TestUtil.SetField(mechanic, "reposition", reposition);
        TestUtil.SetField(mechanic, "duneField", duneField);
        TestUtil.SetField(mechanic, "triggerFractions", new[] { 0.9f });
        TestUtil.SetField(mechanic, "chargeDurations", new[] { 1.2f });
        TestUtil.SetField(mechanic, "centerPosition", SpotA);
        TestUtil.SetField(mechanic, "splashDelay", 0.02f);
        TestUtil.SetField(mechanic, "pushDuration", 0.05f);
        TestUtil.SetField(mechanic, "duneCount", new Vector2Int(0, 0));
        TestUtil.SetField(mechanic, "fireDuration", 0.05f);

        attack = boss.AddComponent<BinahSplashAttack>();
        TestUtil.SetField(attack, "reposition", reposition);
        TestUtil.SetField(attack, "mechanic", mechanic);
        TestUtil.SetField(attack, "duneField", duneField);
        TestUtil.SetField(attack, "interval", 0.3f);
        TestUtil.SetField(attack, "spots", new[]
        {
            new BinahSplashAttack.Spot { position = SpotA, yaw = 0f },
            new BinahSplashAttack.Spot { position = SpotB, yaw = -90f },
        });
        TestUtil.SetField(attack, "duneDistance", 20f);
        TestUtil.SetField(attack, "duneRadius", 3f);
        boss.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        duneField.Clear();
        Object.Destroy(boss);
        Object.Destroy(dunePrefab);
    }

    private IEnumerator WaitUntil(System.Func<bool> condition, float timeout = 3f)
    {
        float end = Time.realtimeSinceStartup + timeout;
        while (!condition() && Time.realtimeSinceStartup < end) yield return null;
    }

    [UnityTest]
    public IEnumerator AfterTheInterval_MovesToAnotherSpotAndPilesADuneInFront()
    {
        yield return null;
        Assert.AreEqual(SpotA, boss.transform.position);

        yield return WaitUntil(() => duneField.ActiveCount > 0);

        Assert.AreEqual(SpotB, boss.transform.position, "the only other spot");
        Assert.AreEqual(-90f, Mathf.DeltaAngle(0f, boss.transform.eulerAngles.y), 0.01f);
        Vector3 dune = Object.FindAnyObjectByType<BinahDune>().transform.position;
        Vector3 expected = SpotB + Quaternion.Euler(0f, -90f, 0f) * Vector3.forward * 20f;
        Assert.LessOrEqual(Vector2.Distance(new Vector2(dune.x, dune.z), new Vector2(expected.x, expected.z)), 3.01f, "the sand lands in front of Binah");
    }

    [UnityTest]
    public IEnumerator WhileTheCannonMechanicRuns_DoesNotMove()
    {
        yield return null;
        health.TakeDamage(15);  // 100 → 85，低於 90% 觸發光炮機制 (中心 = SpotA)
        Assert.IsTrue(mechanic.IsRunning);

        float end = Time.time + 1f;
        while (Time.time < end && mechanic.IsRunning)
        {
            Assert.Less(boss.transform.position.x, 1f, "never repositions to spot B during the mechanic");
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator AfterTheMechanic_Resumes()
    {
        yield return null;
        health.TakeDamage(15);
        yield return WaitUntil(() => !mechanic.IsRunning);

        yield return WaitUntil(() => boss.transform.position == SpotB);
        Assert.AreEqual(SpotB, boss.transform.position);
    }

    // 要拿到攻擊使用權才換位 (其他攻擊進行中就等)；換位期間佔住使用權，換完才釋放
    [UnityTest]
    public IEnumerator WaitsForTheGateAndHoldsItWhileMoving()
    {
        BossAttackGate gate = boss.AddComponent<BossAttackGate>();
        TestUtil.SetField(attack, "attackGate", gate);
        GameObject other = new GameObject("OtherAttack");
        gate.TryAcquire(other);
        yield return null;

        yield return new WaitForSeconds(0.6f);
        Assert.AreEqual(SpotA, boss.transform.position, "waits while another attack is running");

        gate.Release(other);
        yield return WaitUntil(() => gate.IsBusy);
        Assert.IsTrue(gate.IsBusy, "holds the gate while relocating");

        yield return WaitUntil(() => boss.transform.position == SpotB && !gate.IsBusy);
        Assert.AreEqual(SpotB, boss.transform.position);
        Assert.IsFalse(gate.IsBusy, "released after the move");
        Object.Destroy(other);
    }

    [UnityTest]
    public IEnumerator AfterDeath_Stops()
    {
        yield return null;
        health.TakeDamage(health.MaxHealth);

        yield return new WaitForSeconds(0.6f);

        Assert.AreEqual(SpotA, boss.transform.position);
    }
}
