using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Boss 戰的場地規則：進入 Boss 戰時摧毀指定的砲台；玩家只能在限制範圍內移動 (一般戰鬥不限制)
public class BossPhaseRulesTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in spawned) Object.Destroy(go);
        spawned.Clear();
    }

    private GameObject Track(GameObject go)
    {
        spawned.Add(go);
        return go;
    }

    [UnityTest]
    public IEnumerator Demolisher_BossPhase_DestroysAllTargets()
    {
        EnemyHealth first = Track(new GameObject("TurretA")).AddComponent<EnemyHealth>();
        EnemyHealth second = Track(new GameObject("TurretB")).AddComponent<EnemyHealth>();
        BossPhaseDemolisher demolisher = Track(new GameObject("Demolisher")).AddComponent<BossPhaseDemolisher>();
        TestUtil.SetField(demolisher, "targets", new[] { first, second });
        yield return null;

        demolisher.OnCombatPhaseChanged(CombatPhase.Boss);

        Assert.IsTrue(first.IsDead);
        Assert.IsTrue(second.IsDead);
    }

    private PlayerAreaLimiter CreateLimitedBody(out Rigidbody body)
    {
        GameObject player = Track(new GameObject("Player"));
        player.SetActive(false);
        body = player.AddComponent<Rigidbody>();
        body.useGravity = false;
        PlayerAreaLimiter limiter = player.AddComponent<PlayerAreaLimiter>();
        TestUtil.SetField(limiter, "bossAreaMin", new Vector2(-10f, -10f));
        TestUtil.SetField(limiter, "bossAreaMax", new Vector2(10f, 10f));
        player.SetActive(true);
        return limiter;
    }

    [UnityTest]
    public IEnumerator Limiter_BossPhase_KeepsBodyInsideArea()
    {
        PlayerAreaLimiter limiter = CreateLimitedBody(out Rigidbody body);
        limiter.OnCombatPhaseChanged(CombatPhase.Boss);
        body.position = new Vector3(8f, 0f, 0f);
        body.linearVelocity = new Vector3(20f, 0f, 0f);

        yield return new WaitForSeconds(0.3f);

        Assert.LessOrEqual(body.position.x, 10.01f);
    }

    [UnityTest]
    public IEnumerator Limiter_NormalPhase_DoesNotRestrict()
    {
        CreateLimitedBody(out Rigidbody body);
        body.position = new Vector3(8f, 0f, 0f);
        body.linearVelocity = new Vector3(20f, 0f, 0f);

        yield return new WaitForSeconds(0.3f);

        Assert.Greater(body.position.x, 10.5f);
    }
}
