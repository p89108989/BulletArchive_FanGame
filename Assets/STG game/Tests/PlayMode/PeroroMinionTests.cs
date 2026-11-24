using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// 小 Peroro：受到的傷害同步扣在 Boss 身上，被擊倒時 Boss 再額外受到小 Peroro 最大血量的傷害；
// 倒下後在原地停留一段時間才消失；定時朝玩家發射指定顏色的子彈
public class PeroroMinionTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();
    private EnemyHealth boss;
    private GameObject target;

    [SetUp]
    public void SetUp()
    {
        // Boss 血量要遠大於小怪，扣血 (含擊倒額外傷害) 才不會被歸零截斷
        GameObject bossObject = Track(new GameObject("Boss"));
        bossObject.SetActive(false);
        boss = bossObject.AddComponent<EnemyHealth>();
        TestUtil.SetField(boss, "maxHealth", 1000);
        bossObject.SetActive(true);
        target = Track(new GameObject("Target"));
        target.transform.position = new Vector3(0f, 0f, 30f);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in spawned)
        {
            if (go != null) Object.Destroy(go);
        }
        spawned.Clear();
    }

    private GameObject Track(GameObject go)
    {
        spawned.Add(go);
        return go;
    }

    private PeroroMinion CreateMinion(GameObject boltPrefab, float attackInterval, float corpseDuration)
    {
        GameObject minionObject = Track(new GameObject("Minion"));
        minionObject.SetActive(false);
        minionObject.AddComponent<EnemyHealth>();
        PeroroMinion minion = minionObject.AddComponent<PeroroMinion>();
        TestUtil.SetField(minion, "boltPrefab", boltPrefab);
        TestUtil.SetField(minion, "target", target.transform);
        TestUtil.SetField(minion, "appearDuration", 0f);
        TestUtil.SetField(minion, "attackInterval", attackInterval);
        TestUtil.SetField(minion, "corpseDuration", corpseDuration);
        minion.Initialize(boss, BulletPolarity.Yellow, -10f, 10f);
        minionObject.SetActive(true);
        return minion;
    }

    private GameObject CreateBoltPrefab()
    {
        GameObject bolt = Track(new GameObject("Bolt"));
        bolt.SetActive(false);
        bolt.AddComponent<EnemyBullet>();
        return bolt;
    }

    [UnityTest]
    public IEnumerator Damage_IsForwardedToBoss()
    {
        PeroroMinion minion = CreateMinion(CreateBoltPrefab(), 99f, 3f);
        EnemyHealth minionHealth = minion.GetComponent<EnemyHealth>();
        yield return null;

        minionHealth.TakeDamage(5);
        Assert.AreEqual(boss.MaxHealth - 5, boss.CurrentHealth);

        // 擊倒時：剩餘血量照常同步，另外再扣一次小 Peroro 的最大血量 (溢出的傷害不算)
        minionHealth.TakeDamage(9999);
        Assert.AreEqual(boss.MaxHealth - minionHealth.MaxHealth * 2, boss.CurrentHealth);
    }

    [UnityTest]
    public IEnumerator Death_StaysForCorpseDurationThenDisappears()
    {
        PeroroMinion minion = CreateMinion(CreateBoltPrefab(), 99f, 0.4f);
        yield return null;

        minion.GetComponent<EnemyHealth>().TakeDamage(9999);
        yield return new WaitForSeconds(0.2f);
        Assert.IsTrue(minion != null && minion.gameObject.activeInHierarchy, "the corpse should stay for a while");

        yield return new WaitForSeconds(0.4f);
        Assert.IsTrue(minion == null, "the minion should be gone after the corpse duration");
    }

    // Boss 被擊倒時，場上的小怪跟著倒下
    [UnityTest]
    public IEnumerator BossDeath_KillsMinion()
    {
        PeroroMinion minion = CreateMinion(CreateBoltPrefab(), 99f, 5f);
        yield return null;

        boss.TakeDamage(boss.MaxHealth);

        Assert.IsTrue(minion.IsDead);
    }

    [UnityTest]
    public IEnumerator Attack_FiresThreeBoltsOfItsColor()
    {
        PeroroMinion minion = CreateMinion(CreateBoltPrefab(), 0.1f, 3f);

        yield return new WaitForSeconds(2.2f);

        int yellowBolts = 0;
        foreach (EnemyBullet bullet in Object.FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None))
        {
            if (bullet.gameObject.activeInHierarchy && bullet.Polarity == BulletPolarity.Yellow) yellowBolts++;
        }
        Assert.GreaterOrEqual(yellowBolts, 3);
        Assert.AreEqual(0, yellowBolts % 3, "bolts are fired in volleys of three");
    }
}
