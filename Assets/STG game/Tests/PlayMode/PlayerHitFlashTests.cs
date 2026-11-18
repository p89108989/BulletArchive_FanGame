using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// 致命一擊會觸發受傷閃爍；死亡後 Game Over 會暫停時間，角色必須維持可見而不是凍結在隱藏的那一格
public class PlayerHitFlashTests
{
    private GameObject playerObject;

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(playerObject);
    }

    [UnityTest]
    public IEnumerator HitFlash_OnDeath_LeavesRenderersVisible()
    {
        playerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        PlayerHealth health = playerObject.AddComponent<PlayerHealth>();
        playerObject.AddComponent<PlayerHitFlash>();
        Renderer body = playerObject.GetComponent<Renderer>();
        yield return null;

        health.TakeDamage(health.MaxHealth);

        Assert.IsTrue(body.enabled);
    }
}
