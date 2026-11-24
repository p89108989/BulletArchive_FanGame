using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// 跟隨角色的視角 (P 鍵) 下：W 沿角色面對的方向前進，A/D 原地轉向
public class FacingRelativeMovementTests : InputTestFixture
{
    private GameObject playerObject;
    private PlayerMovement movement;
    private Rigidbody body;

    public override void Setup()
    {
        base.Setup();
        playerObject = new GameObject("Player");
        playerObject.AddComponent<CapsuleCollider>();
        movement = playerObject.AddComponent<PlayerMovement>();
        body = playerObject.GetComponent<Rigidbody>();
        body.useGravity = false;
        playerObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        movement.SetFacingRelativeControls(true);
    }

    public override void TearDown()
    {
        Object.Destroy(playerObject);
        base.TearDown();
    }

    [UnityTest]
    public IEnumerator W_MovesAlongFacing()
    {
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Press(keyboard.wKey);

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Vector3 velocity = body.linearVelocity;
        velocity.y = 0f;
        Assert.Greater(Vector3.Dot(velocity.normalized, Vector3.right), 0.99f, "W should move toward the character's facing (+X)");
    }

    [UnityTest]
    public IEnumerator A_TurnsLeftWithoutMoving()
    {
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Press(keyboard.aKey);

        yield return new WaitForSeconds(0.2f);

        Assert.Less(body.rotation.eulerAngles.y, 89f, "A should turn the character to the left");
        Vector3 velocity = body.linearVelocity;
        velocity.y = 0f;
        Assert.Less(velocity.magnitude, 0.01f);
    }
}
