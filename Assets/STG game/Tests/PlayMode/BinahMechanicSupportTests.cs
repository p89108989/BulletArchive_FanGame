using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Binah 光炮機制需要的基礎功能：
// 敵人受傷倍率 (暈眩時 ×1.5)、玩家護盾暫時失效 (機制期間)、玩家被強制推到指定位置 (潑水把玩家沖到場地最下方)
public class BinahMechanicSupportTests
{
    private GameObject target;

    [TearDown]
    public void TearDown()
    {
        if (target != null) Object.Destroy(target);
    }

    // ── 受傷倍率 ──
    private EnemyHealth CreateEnemy()
    {
        target = new GameObject("Enemy");
        return target.AddComponent<EnemyHealth>();
    }

    [UnityTest]
    public IEnumerator DamageMultiplier_DefaultIsNormalDamage()
    {
        EnemyHealth health = CreateEnemy();
        yield return null;

        health.TakeDamage(1);

        Assert.AreEqual(health.MaxHealth - 1, health.CurrentHealth);
    }

    [UnityTest]
    public IEnumerator DamageMultiplier_OneAndAHalf_KeepsFractionsAcrossHits()
    {
        EnemyHealth health = CreateEnemy();
        yield return null;
        health.DamageMultiplier = 1.5f;

        // 每發 1 點 (一般射擊)：兩發 = 3 點，不能每發都四捨五入成 2 (會變成 ×2)
        health.TakeDamage(1);
        health.TakeDamage(1);

        Assert.AreEqual(health.MaxHealth - 3, health.CurrentHealth);
    }

    // ── 護盾暫時失效 ──
    private PlayerPolarity CreatePolarity(out GameObject blueShield, out GameObject yellowShield)
    {
        target = new GameObject("Player");
        target.SetActive(false);
        blueShield = new GameObject("Blue");
        blueShield.transform.SetParent(target.transform);
        yellowShield = new GameObject("Yellow");
        yellowShield.transform.SetParent(target.transform);
        PlayerPolarity polarity = target.AddComponent<PlayerPolarity>();
        TestUtil.SetField(polarity, "blueShield", blueShield);
        TestUtil.SetField(polarity, "yellowShield", yellowShield);
        target.SetActive(true);
        return polarity;
    }

    [UnityTest]
    public IEnumerator ShieldSuppressed_HidesShieldAndCannotAbsorb()
    {
        PlayerPolarity polarity = CreatePolarity(out GameObject blue, out GameObject yellow);
        yield return null;

        polarity.SetShieldSuppressed(true);

        Assert.IsFalse(blue.activeSelf);
        Assert.IsFalse(yellow.activeSelf);
        Assert.IsFalse(polarity.TryAbsorb(polarity.CurrentPolarity), "no shield, nothing is absorbed");
    }

    [UnityTest]
    public IEnumerator ShieldRestored_ComesBackWithSamePolarity()
    {
        PlayerPolarity polarity = CreatePolarity(out GameObject blue, out GameObject yellow);
        yield return null;
        polarity.SetPolarity(BulletPolarity.Yellow);
        polarity.SetShieldSuppressed(true);

        polarity.SetShieldSuppressed(false);

        Assert.IsFalse(blue.activeSelf);
        Assert.IsTrue(yellow.activeSelf);
        Assert.IsTrue(polarity.TryAbsorb(BulletPolarity.Yellow));
    }

    [UnityTest]
    public IEnumerator ShieldSuppressed_SwitchingPolarityDoesNotShowAShield()
    {
        PlayerPolarity polarity = CreatePolarity(out GameObject blue, out GameObject yellow);
        yield return null;
        polarity.SetShieldSuppressed(true);

        polarity.SetPolarity(BulletPolarity.Yellow);

        Assert.IsFalse(blue.activeSelf);
        Assert.IsFalse(yellow.activeSelf);
    }

    // ── 強制推送 ──
    [UnityTest]
    public IEnumerator PushTo_MovesPlayerToDestination()
    {
        target = new GameObject("Player");
        target.AddComponent<CapsuleCollider>();
        PlayerMovement movement = target.AddComponent<PlayerMovement>();
        Rigidbody body = target.GetComponent<Rigidbody>();
        body.useGravity = false;
        yield return null;

        movement.PushTo(new Vector3(0f, 0f, 10f), 0.3f);
        Assert.IsTrue(movement.IsBeingPushed);

        yield return new WaitForSeconds(0.6f);

        Assert.AreEqual(10f, body.position.z, 0.6f);
        Assert.IsFalse(movement.IsBeingPushed);
    }

    // 推送途中被拖慢 (地面摩擦、碰撞) 也要準時推到目的地：每一步依剩餘距離 / 剩餘時間重新算速度
    [UnityTest]
    public IEnumerator PushTo_CatchesUpWhenSlowedDown()
    {
        target = new GameObject("Player");
        target.AddComponent<CapsuleCollider>();
        PlayerMovement movement = target.AddComponent<PlayerMovement>();
        Rigidbody body = target.GetComponent<Rigidbody>();
        body.useGravity = false;
        yield return null;

        movement.PushTo(new Vector3(0f, 0f, 10f), 0.5f);
        yield return new WaitForSeconds(0.2f);
        body.position -= new Vector3(0f, 0f, 3f);

        yield return new WaitForSeconds(0.5f);

        Assert.AreEqual(10f, body.position.z, 0.6f);
    }
}
