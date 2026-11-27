using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// 場上的沙堆管理：例行潑沙與光炮機制共用；場上最多 maxDunes 個，滿了就不再生成；
// 與既有沙堆至少相隔 spacing；被打掉的不算數；Boss 死亡時清空
public class BinahDuneFieldTests
{
    private GameObject boss;
    private GameObject dunePrefab;
    private EnemyHealth bossHealth;
    private BinahDuneField field;

    [SetUp]
    public void SetUp()
    {
        dunePrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dunePrefab.name = "Dune";
        dunePrefab.transform.localScale = new Vector3(8f, 6f, 2f);
        dunePrefab.SetActive(false);
        BinahDune dune = dunePrefab.AddComponent<BinahDune>();
        TestUtil.SetField(dune, "maxHealth", 10);

        boss = new GameObject("Binah");
        boss.SetActive(false);
        bossHealth = boss.AddComponent<EnemyHealth>();
        field = boss.AddComponent<BinahDuneField>();
        TestUtil.SetField(field, "dunePrefab", dunePrefab);
        TestUtil.SetField(field, "maxDunes", 3);
        TestUtil.SetField(field, "spacing", 10f);
        TestUtil.SetField(field, "areaMin", new Vector2(-100f, -100f));
        TestUtil.SetField(field, "areaMax", new Vector2(100f, 100f));
        boss.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        field.Clear();
        Object.Destroy(boss);
        Object.Destroy(dunePrefab);
    }

    [UnityTest]
    public IEnumerator SpawnRandom_StopsAtTheCap()
    {
        yield return null;

        for (int i = 0; i < 5; i++) field.TrySpawnRandom();

        Assert.AreEqual(3, field.ActiveCount);
        Assert.IsFalse(field.TrySpawnRandom(), "the field is full");
    }

    [UnityTest]
    public IEnumerator DestroyedDune_FreesASlot()
    {
        yield return null;
        for (int i = 0; i < 3; i++) field.TrySpawnRandom();

        Object.FindAnyObjectByType<BinahDune>().TakeDamage(100);

        Assert.AreEqual(2, field.ActiveCount);
        Assert.IsTrue(field.TrySpawnRandom());
    }

    [UnityTest]
    public IEnumerator SpawnNear_PlacesWithinRadiusOfThePoint()
    {
        yield return null;

        Assert.IsTrue(field.TrySpawnNear(new Vector3(20f, 0f, 30f), 5f));

        BinahDune dune = Object.FindAnyObjectByType<BinahDune>();
        Vector3 offset = dune.transform.position - new Vector3(20f, 0f, 30f);
        offset.y = 0f;
        Assert.LessOrEqual(offset.magnitude, 5.01f);
    }

    [UnityTest]
    public IEnumerator SpawnNear_KeepsInsideTheArea()
    {
        TestUtil.SetField(field, "areaMax", new Vector2(10f, 10f));
        yield return null;

        Assert.IsTrue(field.TrySpawnNear(new Vector3(50f, 0f, 50f), 2f));

        Vector3 position = Object.FindAnyObjectByType<BinahDune>().transform.position;
        Assert.LessOrEqual(position.x, 10.01f);
        Assert.LessOrEqual(position.z, 10.01f);
    }

    [UnityTest]
    public IEnumerator SpawnNear_NoRoom_Skips()
    {
        yield return null;
        Assert.IsTrue(field.TrySpawnNear(Vector3.zero, 0f));

        Assert.IsFalse(field.TrySpawnNear(new Vector3(2f, 0f, 0f), 1f), "too close to the first dune");
        Assert.AreEqual(1, field.ActiveCount);
    }

    [UnityTest]
    public IEnumerator Covers_UsesAnyStandingDune()
    {
        yield return null;
        field.TrySpawnNear(new Vector3(0f, 0f, 10f), 0f);

        Assert.IsTrue(field.IsCovered(new Vector3(0f, 0f, 14f), Vector3.forward), "right behind the dune");
        Assert.IsFalse(field.IsCovered(new Vector3(30f, 0f, 14f), Vector3.forward));
    }

    // 全部打破：每個沙堆走自己的破壞流程 (有破壞特效時在原地播放)
    [UnityTest]
    public IEnumerator BreakAll_DestroysEveryDuneWithItsBreakEffect()
    {
        GameObject effect = new GameObject("DuneBreak");
        effect.SetActive(false);
        TestUtil.SetField(dunePrefab.GetComponent<BinahDune>(), "breakEffect", effect);
        yield return null;
        field.TrySpawnNear(new Vector3(-40f, 0f, 0f), 0f);
        field.TrySpawnNear(new Vector3(40f, 0f, 0f), 0f);

        field.BreakAll();

        Assert.AreEqual(0, field.ActiveCount);
        int effects = 0;
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go.name == "DuneBreak(Clone)")
            {
                effects++;
                Object.Destroy(go);
            }
        }

        Assert.AreEqual(2, effects);
        Object.Destroy(effect);
    }

    [UnityTest]
    public IEnumerator BossDies_ClearsTheField()
    {
        yield return null;
        field.TrySpawnRandom();
        field.TrySpawnRandom();

        bossHealth.TakeDamage(bossHealth.MaxHealth);

        Assert.AreEqual(0, field.ActiveCount);
    }
}
