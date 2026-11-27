using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// 結算用的戰鬥數據：被打倒的敵人數 (系統直接清掉的不算)、從 START 結束到戰鬥結束的時間
public class BattleStatsTests
{
    private readonly List<Object> spawned = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object o in spawned) Object.Destroy(o);
        spawned.Clear();
    }

    private T Track<T>(T o) where T : Object
    {
        spawned.Add(o);
        return o;
    }

    private EnemyHealth CreateEnemy()
    {
        return Track(new GameObject("Enemy")).AddComponent<EnemyHealth>();
    }

    private BattleStats CreateStats()
    {
        return Track(new GameObject("Stats")).AddComponent<BattleStats>();
    }

    [UnityTest]
    public IEnumerator KilledEnemies_AreCounted()
    {
        BattleStats stats = CreateStats();
        EnemyHealth first = CreateEnemy();
        EnemyHealth second = CreateEnemy();
        yield return null;

        first.TakeDamage(first.MaxHealth);
        second.TakeDamage(second.MaxHealth);
        second.TakeDamage(10);

        Assert.AreEqual(2, stats.Kills);
    }

    [UnityTest]
    public IEnumerator DemolishedEnemies_DoNotCount()
    {
        BattleStats stats = CreateStats();
        EnemyHealth turret = CreateEnemy();
        yield return null;

        turret.Demolish();

        Assert.IsTrue(turret.IsDead, "still dies (plays its death flow)");
        Assert.AreEqual(0, stats.Kills);
    }

    // 關掉 Domain Reload 後靜態事件不會自動清空：每次進 Play 要把上一次留下的訂閱清掉
    [UnityTest]
    public IEnumerator ResetStatics_ClearsLeftoverKillSubscribers()
    {
        int leftover = 0;
        EnemyHealth.OnAnyKilled += _ => leftover++;
        EnemyHealth.ResetStatics();
        EnemyHealth enemy = CreateEnemy();
        yield return null;

        enemy.TakeDamage(enemy.MaxHealth);

        Assert.AreEqual(0, leftover);
    }

    [UnityTest]
    public IEnumerator Timing_OnlyCountsBetweenStartAndStop()
    {
        BattleStats stats = CreateStats();
        yield return new WaitForSeconds(0.2f);
        Assert.AreEqual(0f, stats.ElapsedSeconds, 0.001f, "not started yet");

        stats.StartTiming();
        yield return new WaitForSeconds(0.3f);
        stats.StopTiming();
        float stopped = stats.ElapsedSeconds;
        yield return new WaitForSeconds(0.2f);

        Assert.AreEqual(0.3f, stopped, 0.1f);
        Assert.AreEqual(stopped, stats.ElapsedSeconds, 0.001f, "frozen after stopping");
    }
}
