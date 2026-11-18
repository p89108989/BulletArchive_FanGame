using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// 極性吸收與能量規則：同色吸收 +5%、上限 100%、Drain 不會扣到負數、無屬性攻擊永遠不被吸收；以及敵人血量的死亡/重置
public class PolarityEnergyTests
{
    private GameObject subject;

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(subject);
    }

    private (PlayerPolarity polarity, PlayerEnergy energy) CreatePlayer()
    {
        subject = new GameObject("Player");
        PlayerEnergy energy = subject.AddComponent<PlayerEnergy>();
        return (subject.GetComponent<PlayerPolarity>(), energy);
    }

    [UnityTest]
    public IEnumerator TryAbsorb_OnlySameColorIsAbsorbed()
    {
        (PlayerPolarity polarity, _) = CreatePlayer();
        yield return null;

        polarity.SetPolarity(BulletPolarity.Blue);
        Assert.IsTrue(polarity.TryAbsorb(BulletPolarity.Blue));
        Assert.IsFalse(polarity.TryAbsorb(BulletPolarity.Yellow));
        Assert.IsFalse(polarity.TryAbsorb(BulletPolarity.None));

        polarity.SetPolarity(BulletPolarity.Yellow);
        Assert.IsTrue(polarity.TryAbsorb(BulletPolarity.Yellow));
        Assert.IsFalse(polarity.TryAbsorb(BulletPolarity.Blue));
        Assert.IsFalse(polarity.TryAbsorb(BulletPolarity.None));
    }

    [UnityTest]
    public IEnumerator Absorb_AddsFivePercent_ClampsAtCapacity()
    {
        (PlayerPolarity polarity, PlayerEnergy energy) = CreatePlayer();
        yield return null;
        polarity.SetPolarity(BulletPolarity.Blue);

        polarity.TryAbsorb(BulletPolarity.Blue);
        Assert.AreEqual(5f, energy.GetEnergy(BulletPolarity.Blue));
        Assert.AreEqual(0f, energy.GetEnergy(BulletPolarity.Yellow));

        for (int i = 0; i < 30; i++) polarity.TryAbsorb(BulletPolarity.Blue);
        Assert.AreEqual(100f, energy.GetEnergy(BulletPolarity.Blue));
    }

    [UnityTest]
    public IEnumerator Drain_ClampsAtZero_ReturnsDrainedAmount()
    {
        (PlayerPolarity polarity, PlayerEnergy energy) = CreatePlayer();
        yield return null;
        polarity.SetPolarity(BulletPolarity.Yellow);
        polarity.TryAbsorb(BulletPolarity.Yellow);

        float drained = energy.Drain(BulletPolarity.Yellow, 8f);

        Assert.AreEqual(5f, drained);
        Assert.AreEqual(0f, energy.GetEnergy(BulletPolarity.Yellow));
        Assert.IsFalse(energy.HasEnergy(BulletPolarity.Yellow));
        Assert.AreEqual(0f, energy.Drain(BulletPolarity.None, 1f));
    }

    [UnityTest]
    public IEnumerator EnemyHealth_DiesOnce_ResetClearsDeath()
    {
        subject = new GameObject("Enemy");
        EnemyHealth health = subject.AddComponent<EnemyHealth>();
        int deaths = 0;
        health.OnDied += () => deaths++;
        yield return null;

        health.TakeDamage(health.MaxHealth);
        health.TakeDamage(1);
        Assert.AreEqual(1, deaths);
        Assert.IsTrue(health.IsDead);

        health.ResetHealth();
        Assert.IsFalse(health.IsDead);
        Assert.AreEqual(health.MaxHealth, health.CurrentHealth);
    }
}
