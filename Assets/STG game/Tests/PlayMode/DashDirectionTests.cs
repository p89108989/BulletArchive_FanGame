using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// 衝刺方向：按下衝刺時有方向鍵 → 往方向鍵的方向衝 (即使角色因為射擊面向別處)；沒有方向鍵 → 往角色面向衝
public class DashDirectionTests : InputTestFixture
{
    private GameObject playerObject;
    private PlayerMovement movement;
    private Rigidbody body;
    private Keyboard keyboard;

    public override void Setup()
    {
        base.Setup();
        playerObject = new GameObject("Player");
        playerObject.AddComponent<CapsuleCollider>();
        movement = playerObject.AddComponent<PlayerMovement>();
        body = playerObject.GetComponent<Rigidbody>();
        body.useGravity = false;
        keyboard = InputSystem.AddDevice<Keyboard>();
    }

    public override void TearDown()
    {
        Object.Destroy(playerObject);
        base.TearDown();
    }

    private Vector3 HorizontalVelocity()
    {
        Vector3 velocity = body.linearVelocity;
        velocity.y = 0f;
        return velocity;
    }

    [UnityTest]
    public IEnumerator DashWithDirectionKey_GoesThatWay_EvenWhileFacingElsewhere()
    {
        // 射擊時角色被鎖定面向 +Z
        movement.SetFacingOverride(Vector3.forward);
        // 先按住方向鍵一幀再按衝刺 (實際操作的順序)
        Press(keyboard.dKey);
        yield return null;
        Press(keyboard.leftShiftKey);

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Vector3 velocity = HorizontalVelocity();
        Assert.Greater(Vector3.Dot(velocity.normalized, Vector3.right), 0.99f, "D + Shift should dash toward +X, not the facing (+Z)");
        Assert.Greater(velocity.magnitude, 10f, "dash speed (5 x 2.5), not walking speed");
    }

    [UnityTest]
    public IEnumerator Dash_TurnsCharacterToDashDirection()
    {
        // 射擊時角色被鎖定面向 +Z，衝刺時仍要轉向衝刺方向
        movement.SetFacingOverride(Vector3.forward);
        Press(keyboard.dKey);
        yield return null;
        Press(keyboard.leftShiftKey);

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Vector3 facing = body.rotation * Vector3.forward;
        Assert.Greater(Vector3.Dot(facing, Vector3.right), 0.99f, "the character faces +X while dashing toward +X");
    }

    [UnityTest]
    public IEnumerator DashWithoutDirectionKey_GoesAlongFacing()
    {
        playerObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        Press(keyboard.leftShiftKey);

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Vector3 velocity = HorizontalVelocity();
        Assert.Greater(Vector3.Dot(velocity.normalized, Vector3.right), 0.99f, "no direction key: dash where the character faces (+X)");
    }
}
