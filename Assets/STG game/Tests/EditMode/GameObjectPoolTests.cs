using NUnit.Framework;
using UnityEngine;

// 物件池：歸還的物件會被停用並在下次取出時重複使用，而不是再生成新的
public class GameObjectPoolTests
{
    private GameObject prefab;
    private GameObject parent;

    [SetUp]
    public void SetUp()
    {
        prefab = new GameObject("PooledPrefab");
        parent = new GameObject("PoolParent");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(parent);
        Object.DestroyImmediate(prefab);
    }

    [Test]
    public void Release_DeactivatesInstance()
    {
        GameObjectPool pool = new GameObjectPool(prefab, parent.transform);
        GameObject instance = pool.Get(Vector3.zero, Quaternion.identity);

        pool.Release(instance);

        Assert.IsFalse(instance.activeSelf);
    }

    [Test]
    public void Get_AfterRelease_ReusesSameInstanceAtNewPosition()
    {
        GameObjectPool pool = new GameObjectPool(prefab, parent.transform);
        GameObject first = pool.Get(Vector3.zero, Quaternion.identity);
        pool.Release(first);

        GameObject second = pool.Get(new Vector3(1f, 2f, 3f), Quaternion.identity);

        Assert.AreSame(first, second);
        Assert.IsTrue(second.activeSelf);
        Assert.AreEqual(new Vector3(1f, 2f, 3f), second.transform.position);
    }

    [Test]
    public void Prewarm_CreatesInactiveInstancesUnderParent()
    {
        new GameObjectPool(prefab, parent.transform, 3);

        Assert.AreEqual(3, parent.transform.childCount);
        foreach (Transform child in parent.transform) Assert.IsFalse(child.gameObject.activeSelf);
    }
}
