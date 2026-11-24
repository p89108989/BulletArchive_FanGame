using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// 死亡時指定的外觀物件 (光環、護盾) 要隱藏，倒地後不再顯示
public class PlayerDeathHandlerTests
{
    private GameObject playerObject;
    private GameObject halo;
    private PlayerHealth health;

    [SetUp]
    public void SetUp()
    {
        playerObject = new GameObject("Player");
        playerObject.SetActive(false);
        health = playerObject.AddComponent<PlayerHealth>();
        halo = new GameObject("Halo");
        halo.transform.SetParent(playerObject.transform);
        PlayerDeathHandler handler = playerObject.AddComponent<PlayerDeathHandler>();
        TestUtil.SetField(handler, "disableOnDeath", new Behaviour[0]);
        TestUtil.SetField(handler, "hideOnDeath", new[] { halo });
        playerObject.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(playerObject);
    }

    [UnityTest]
    public IEnumerator Died_HidesListedObjects()
    {
        yield return null;
        Assert.IsTrue(halo.activeSelf, "precondition");

        health.TakeDamage(health.MaxHealth);

        Assert.IsFalse(halo.activeSelf);
    }
}
