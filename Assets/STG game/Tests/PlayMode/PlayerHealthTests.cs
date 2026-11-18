using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// 受傷後的無敵時間不應被較短的無敵 (例如衝刺) 覆蓋縮短
public class PlayerHealthTests
{
    private GameObject playerObject;
    private PlayerHealth health;

    [SetUp]
    public void SetUp()
    {
        playerObject = new GameObject("Player");
        health = playerObject.AddComponent<PlayerHealth>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(playerObject);
    }

    [UnityTest]
    public IEnumerator ShortGrant_DoesNotShortenDamageInvincibility()
    {
        health.TakeDamage(1);
        health.GrantInvincibility(0.2f);

        yield return new WaitForSeconds(0.5f);
        health.TakeDamage(1);

        Assert.AreEqual(health.MaxHealth - 1, health.CurrentHealth);
    }

    [UnityTest]
    public IEnumerator Invincibility_ExpiresAfterDuration()
    {
        health.TakeDamage(1);

        yield return new WaitForSeconds(health.InvincibilityDuration + 0.1f);
        health.TakeDamage(1);

        Assert.AreEqual(health.MaxHealth - 2, health.CurrentHealth);
    }
}
