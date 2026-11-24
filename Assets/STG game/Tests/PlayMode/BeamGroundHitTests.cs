using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Boss 雷射打在地面的判定：落點半徑內的玩家受傷 (紅色攻擊，護盾擋不住)，只有衝刺的無敵能躲
public class BeamGroundHitTests
{
    private GameObject player;
    private PlayerHealth health;

    [SetUp]
    public void SetUp()
    {
        player = new GameObject("Player");
        health = player.AddComponent<PlayerHealth>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(player);
    }

    [UnityTest]
    public IEnumerator PlayerInsideRadius_TakesDamage()
    {
        yield return null;
        player.transform.position = new Vector3(3f, 1f, 0f);

        bool hit = BeamGroundHit.TryDamage(player.transform, health, new Vector3(0f, 0.3f, 0f), 5f, 1);

        Assert.IsTrue(hit);
        Assert.AreEqual(health.MaxHealth - 1, health.CurrentHealth);
    }

    [UnityTest]
    public IEnumerator PlayerOutsideRadius_IsSafe()
    {
        yield return null;
        player.transform.position = new Vector3(6f, 0f, 0f);

        bool hit = BeamGroundHit.TryDamage(player.transform, health, Vector3.zero, 5f, 1);

        Assert.IsFalse(hit);
        Assert.AreEqual(health.MaxHealth, health.CurrentHealth);
    }

    [UnityTest]
    public IEnumerator DashingPlayer_DodgesTheBeam()
    {
        yield return null;
        player.transform.position = Vector3.zero;
        health.GrantInvincibility(0.2f);

        BeamGroundHit.TryDamage(player.transform, health, Vector3.zero, 5f, 1);

        Assert.AreEqual(health.MaxHealth, health.CurrentHealth);
    }
}
