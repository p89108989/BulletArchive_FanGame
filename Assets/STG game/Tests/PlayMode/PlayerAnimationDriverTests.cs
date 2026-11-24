using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// 死亡動畫必須在 Game Over 暫停 (timeScale = 0) 下照常播放
public class PlayerAnimationDriverTests
{
    private GameObject playerObject;
    private PlayerHealth health;
    private Animator animator;

    [SetUp]
    public void SetUp()
    {
        playerObject = new GameObject("Player");
        playerObject.SetActive(false);
        health = playerObject.AddComponent<PlayerHealth>();
        playerObject.AddComponent<PlayerAnimationDriver>();
        animator = playerObject.GetComponent<Animator>();
        playerObject.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(playerObject);
    }

    [UnityTest]
    public IEnumerator Died_SwitchesAnimatorToUnscaledTime()
    {
        yield return null;
        Assert.AreEqual(AnimatorUpdateMode.Normal, animator.updateMode, "precondition");

        health.TakeDamage(health.MaxHealth);

        Assert.AreEqual(AnimatorUpdateMode.UnscaledTime, animator.updateMode);
    }
}
