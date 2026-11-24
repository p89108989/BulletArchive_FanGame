using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Boss 召喚：膠囊沿拋物線飛到落點並回報落地一次；落點都在活動範圍內且彼此保持距離
public class BossSummonTests
{
    private GameObject capsuleObject;

    [TearDown]
    public void TearDown()
    {
        if (capsuleObject != null) Object.Destroy(capsuleObject);
    }

    [UnityTest]
    public IEnumerator Capsule_Launch_LandsOnTargetAndReportsOnce()
    {
        capsuleObject = new GameObject("Capsule");
        SummonCapsule capsule = capsuleObject.AddComponent<SummonCapsule>();
        TestUtil.SetField(capsule, "openDuration", 5f);
        int landedCount = 0;
        Vector3 target = new Vector3(10f, 0f, 5f);

        capsule.Launch(new Vector3(0f, 50f, -40f), target, 0.2f, 10f, c => landedCount++);
        yield return new WaitForSeconds(0.4f);

        Assert.AreEqual(1, landedCount);
        Assert.Less(Vector3.Distance(capsuleObject.transform.position, target), 0.01f);
    }

    // 膠囊的原點在球心：落地時要往上墊高一個半徑，整顆球才會坐在地面上而不是一半埋進地板
    [UnityTest]
    public IEnumerator Capsule_LandingHeight_SitsOnGround()
    {
        capsuleObject = new GameObject("Capsule");
        SummonCapsule capsule = capsuleObject.AddComponent<SummonCapsule>();
        TestUtil.SetField(capsule, "openDuration", 5f);
        TestUtil.SetField(capsule, "landingHeight", 4f);
        Vector3 ground = new Vector3(10f, 0.3f, 5f);

        capsule.Launch(new Vector3(0f, 50f, -40f), ground, 0.2f, 10f, null);
        yield return new WaitForSeconds(0.4f);

        Assert.Less(Vector3.Distance(capsuleObject.transform.position, ground + Vector3.up * 4f), 0.01f);
    }

    // Boss 普通攻擊吐出的碎塊：同樣沿拋物線落在目標點並回報落地一次
    [UnityTest]
    public IEnumerator Debris_Launch_LandsOnTargetAndReportsOnce()
    {
        capsuleObject = new GameObject("Debris");
        ArcProjectile debris = capsuleObject.AddComponent<ArcProjectile>();
        TestUtil.SetField(debris, "fadeDuration", 5f);
        int landedCount = 0;
        Vector3 target = new Vector3(-5f, 0.3f, 12f);

        debris.Launch(new Vector3(0f, 40f, -30f), target, 0.2f, 15f, d => landedCount++);
        yield return new WaitForSeconds(0.4f);

        Assert.AreEqual(1, landedCount);
        Assert.Less(Vector3.Distance(capsuleObject.transform.position, target), 0.01f);
    }

    [Test]
    public void PickPoints_AreInsideAreaAndSeparated()
    {
        Random.InitState(1234);
        Vector3[] points = new Vector3[3];
        Vector2 min = new Vector2(-100f, -4f);
        Vector2 max = new Vector2(0f, 47f);

        int count = SummonPlacement.PickPoints(min, max, 25f, new Vector3(-50f, 0f, 20f), 15f, 0.3f, points);

        Assert.AreEqual(3, count);
        for (int i = 0; i < count; i++)
        {
            Assert.That(points[i].x, Is.InRange(min.x, max.x));
            Assert.That(points[i].z, Is.InRange(min.y, max.y));
            Assert.AreEqual(0.3f, points[i].y, 0.001f);
            Assert.GreaterOrEqual(Vector3.Distance(points[i], new Vector3(-50f, 0.3f, 20f)), 15f);
            for (int j = i + 1; j < count; j++) Assert.GreaterOrEqual(Vector3.Distance(points[i], points[j]), 25f);
        }
    }
}
