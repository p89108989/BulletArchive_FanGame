using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// 敵人死亡後停用本體並在一段時間後重生；等待重生期間元件被停用再啟用時，必須以「存活、可受傷」的狀態回來
public class DisableOnDeathTests
{
    private GameObject root;
    private GameObject body;
    private EnemyHealth health;
    private DisableOnDeath death;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Turret");
        root.SetActive(false);
        body = new GameObject("Pivot");
        body.transform.SetParent(root.transform);
        health = root.AddComponent<EnemyHealth>();
        death = root.AddComponent<DisableOnDeath>();
        TestUtil.SetField(death, "disableTarget", body);
        TestUtil.SetField(death, "respawnDelay", 30f);
        root.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(root);
    }

    [UnityTest]
    public IEnumerator Death_DisablesBodyAndKeepsRootActive()
    {
        yield return null;

        health.TakeDamage(health.MaxHealth);

        Assert.IsFalse(body.activeSelf);
        Assert.IsTrue(root.activeSelf);
    }

    // Boss 戰期間不重生；Boss 戰結束回到一般戰鬥時立刻重生 (不用再等剩下的倒數)
    [UnityTest]
    public IEnumerator BossPhase_NoRespawnThenRespawnsAsSoonAsBossEnds()
    {
        TestUtil.SetField(death, "respawnDelay", 0.2f);
        yield return null;
        health.TakeDamage(health.MaxHealth);
        death.OnCombatPhaseChanged(CombatPhase.Boss);

        yield return new WaitForSeconds(0.4f);
        Assert.IsFalse(body.activeSelf, "should not respawn during the boss phase");

        TestUtil.SetField(death, "respawnDelay", 30f);
        death.OnCombatPhaseChanged(CombatPhase.Normal);

        Assert.IsTrue(body.activeSelf, "respawns right when the boss fight ends");
        Assert.IsFalse(health.IsDead);
    }

    // 回到一般戰鬥時本來就活著的砲台不受影響 (不會重設血量)
    [UnityTest]
    public IEnumerator NormalPhase_LeavesAliveTurretAlone()
    {
        yield return null;
        health.TakeDamage(1);
        int damaged = health.CurrentHealth;

        death.OnCombatPhaseChanged(CombatPhase.Normal);

        Assert.AreEqual(damaged, health.CurrentHealth);
    }

    [UnityTest]
    public IEnumerator ReEnable_DuringRespawn_ReturnsAliveTurret()
    {
        yield return null;
        health.TakeDamage(health.MaxHealth);

        death.enabled = false;
        death.enabled = true;

        Assert.IsTrue(body.activeSelf);
        Assert.IsFalse(health.IsDead);
    }
}
