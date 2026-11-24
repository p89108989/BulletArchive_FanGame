using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// 雷射偵測：子彈再多也不能擠掉敵人；只消除與光束異色的子彈
public class LaserBeamTracerTests
{
    private readonly List<GameObject> created = new List<GameObject>();
    private int enemyLayer;
    private int bulletLayer;

    [SetUp]
    public void SetUp()
    {
        enemyLayer = LayerMask.NameToLayer("Enemy");
        bulletLayer = LayerMask.NameToLayer("EnemyBullet");
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in created) Object.Destroy(go);
        created.Clear();
    }

    private LaserBeamTracer CreateTracer()
    {
        return new LaserBeamTracer(1.5f, 150f, LayerMask.GetMask("Enemy"), LayerMask.GetMask("EnemyBullet"));
    }

    private EnemyHealth CreateEnemy(Vector3 position)
    {
        GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Cube);
        enemy.layer = enemyLayer;
        enemy.transform.position = position;
        created.Add(enemy);
        return enemy.AddComponent<EnemyHealth>();
    }

    private void CreateBullet(Vector3 position, BulletPolarity polarity, System.Action<EnemyBullet> onDespawn)
    {
        GameObject bullet = new GameObject("Bullet");
        bullet.layer = bulletLayer;
        bullet.transform.position = position;
        bullet.AddComponent<SphereCollider>().isTrigger = true;
        bullet.AddComponent<EnemyBullet>().Launch(Vector3.forward, 0f, polarity, onDespawn);
        created.Add(bullet);
    }

    [UnityTest]
    public IEnumerator Trace_FindsEnemyBehindManyBullets()
    {
        for (int i = 0; i < 40; i++) CreateBullet(new Vector3(0f, 0f, 5f + i), BulletPolarity.Blue, null);
        EnemyHealth enemy = CreateEnemy(new Vector3(0f, 0f, 50f));
        yield return new WaitForFixedUpdate();

        float length = CreateTracer().Trace(Vector3.zero, Vector3.forward, BulletPolarity.Blue, out EnemyHealth target);

        Assert.AreSame(enemy, target);
        Assert.Less(length, 50f);
    }

    [UnityTest]
    public IEnumerator Trace_InterceptsOnlyOppositePolarityBullets()
    {
        bool yellowRemoved = false;
        bool blueRemoved = false;
        CreateBullet(new Vector3(0f, 0f, 10f), BulletPolarity.Yellow, b => yellowRemoved = true);
        CreateBullet(new Vector3(0f, 0f, 20f), BulletPolarity.Blue, b => blueRemoved = true);
        yield return new WaitForFixedUpdate();

        CreateTracer().Trace(Vector3.zero, Vector3.forward, BulletPolarity.Blue, out _);

        Assert.IsTrue(yellowRemoved);
        Assert.IsFalse(blueRemoved);
    }

    // 綠色大招光束 (藍黃合併) 以 None 當「放行的顏色」：藍、黃子彈都消除，紅色 (None) 攻擊仍只能閃避
    [UnityTest]
    public IEnumerator Trace_PassThroughNone_InterceptsBlueAndYellowButNotRed()
    {
        bool yellowRemoved = false;
        bool blueRemoved = false;
        bool redRemoved = false;
        CreateBullet(new Vector3(0f, 0f, 10f), BulletPolarity.Yellow, b => yellowRemoved = true);
        CreateBullet(new Vector3(0f, 0f, 20f), BulletPolarity.Blue, b => blueRemoved = true);
        CreateBullet(new Vector3(0f, 0f, 30f), BulletPolarity.None, b => redRemoved = true);
        yield return new WaitForFixedUpdate();

        CreateTracer().Trace(Vector3.zero, Vector3.forward, BulletPolarity.None, out _);

        Assert.IsTrue(yellowRemoved);
        Assert.IsTrue(blueRemoved);
        Assert.IsFalse(redRemoved);
    }

    [UnityTest]
    public IEnumerator Trace_ZeroEnemies_ReturnsMaxRange()
    {
        yield return new WaitForFixedUpdate();

        float length = CreateTracer().Trace(Vector3.zero, Vector3.forward, BulletPolarity.Blue, out EnemyHealth target);

        Assert.IsNull(target);
        Assert.AreEqual(150f, length);
    }
}
