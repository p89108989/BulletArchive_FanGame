using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Boss 戰的場地規則：進入 Boss 戰時摧毀場上所有砲台 (Boss 戰結束後由 DisableOnDeath 立刻重生)
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
}
