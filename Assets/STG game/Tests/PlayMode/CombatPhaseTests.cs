using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// 戰鬥階段：開場倒數結束後切換到 Boss 戰並通知所有監聽者；Boss 戰期間砲台不再發射子彈
public class CombatPhaseTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();

    private class RecordingListener : MonoBehaviour, ICombatPhaseListener
    {
        public readonly List<CombatPhase> Received = new List<CombatPhase>();

        public void OnCombatPhaseChanged(CombatPhase phase)
        {
            Received.Add(phase);
        }
    }

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
    public IEnumerator BossDelayElapsed_EntersBossAndNotifiesListeners()
    {
        RecordingListener listener = Track(new GameObject("Listener")).AddComponent<RecordingListener>();
        GameObject controllerObject = Track(new GameObject("Controller"));
        controllerObject.SetActive(false);
        CombatPhaseController controller = controllerObject.AddComponent<CombatPhaseController>();
        TestUtil.SetField(controller, "bossStartDelay", 0.1f);
        TestUtil.SetField(controller, "phaseListeners", new MonoBehaviour[] { listener });
        controllerObject.SetActive(true);

        Assert.AreEqual(CombatPhase.Normal, controller.CurrentPhase);
        yield return new WaitForSeconds(0.3f);

        Assert.AreEqual(CombatPhase.Boss, controller.CurrentPhase);
        CollectionAssert.AreEqual(new[] { CombatPhase.Boss }, listener.Received);
    }

    [UnityTest]
    public IEnumerator BulletSpawner_InBossPhase_DoesNotFire()
    {
        GameObject bulletPrefab = Track(new GameObject("Bullet"));
        bulletPrefab.SetActive(false);
        bulletPrefab.AddComponent<EnemyBullet>();
        GameObject spawnPoint = Track(new GameObject("SpawnPoint"));
        GameObject target = Track(new GameObject("Target"));
        target.transform.position = new Vector3(0f, 0f, 20f);

        GameObject spawnerObject = Track(new GameObject("Spawner"));
        spawnerObject.SetActive(false);
        EnemyBulletSpawner spawner = spawnerObject.AddComponent<EnemyBulletSpawner>();
        TestUtil.SetField(spawner, "bulletPrefab", bulletPrefab);
        TestUtil.SetField(spawner, "spawnPoints", new[] { spawnPoint.transform });
        TestUtil.SetField(spawner, "target", target.transform);
        TestUtil.SetField(spawner, "fireInterval", 0.05f);
        TestUtil.SetField(spawner, "poolPrewarmCount", 0);
        spawner.OnCombatPhaseChanged(CombatPhase.Boss);
        spawnerObject.SetActive(true);

        yield return new WaitForSeconds(0.3f);

        Assert.AreEqual(0, spawnerObject.transform.childCount, "no bullet should be taken from the pool during the boss phase");
    }
}
