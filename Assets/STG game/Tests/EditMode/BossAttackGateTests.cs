using NUnit.Framework;
using UnityEngine;

// 攻擊使用權：同一時間只有一個攻擊；特殊機制 (由血量觸發、不能等) 可以直接接管，被接管的攻擊之後釋放也不會放掉別人的使用權
public class BossAttackGateTests
{
    private GameObject host;
    private BossAttackGate gate;
    private GameObject first;
    private GameObject second;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("Boss");
        gate = host.AddComponent<BossAttackGate>();
        first = new GameObject("NormalAttack");
        second = new GameObject("Mechanic");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(host);
        Object.DestroyImmediate(first);
        Object.DestroyImmediate(second);
    }

    [Test]
    public void ForceAcquire_TakesOverFromTheCurrentOwner()
    {
        Assert.IsTrue(gate.TryAcquire(first));

        gate.ForceAcquire(second);

        gate.Release(first);
        Assert.IsTrue(gate.IsBusy, "the old owner can no longer release it");
        Assert.IsFalse(gate.TryAcquire(first));

        gate.Release(second);
        Assert.IsFalse(gate.IsBusy);
    }
}
