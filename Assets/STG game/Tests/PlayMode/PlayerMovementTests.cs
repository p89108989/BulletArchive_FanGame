using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// 衝刺需有冷卻時間 (預設 2 秒，從衝刺開始時計算)，避免連按衝刺維持無敵
public class PlayerMovementTests
{
    private GameObject playerObject;
    private PlayerMovement movement;

    [SetUp]
    public void SetUp()
    {
        playerObject = new GameObject("Player");
        playerObject.AddComponent<CapsuleCollider>();
        movement = playerObject.AddComponent<PlayerMovement>();
        playerObject.GetComponent<Rigidbody>().useGravity = false;
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(playerObject);
    }

    [UnityTest]
    public IEnumerator TryStartDash_SecondCallWithinCooldown_ReturnsFalse()
    {
        Assert.IsTrue(movement.TryStartDash());

        yield return new WaitForSeconds(0.5f);

        Assert.IsFalse(movement.TryStartDash());
    }

    [UnityTest]
    public IEnumerator TryStartDash_AfterCooldown_ReturnsTrue()
    {
        Assert.IsTrue(movement.TryStartDash());

        yield return new WaitForSeconds(2.1f);

        Assert.IsTrue(movement.TryStartDash());
    }
}
