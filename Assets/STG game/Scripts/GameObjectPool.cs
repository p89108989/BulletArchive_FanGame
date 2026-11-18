using System.Collections.Generic;
using UnityEngine;

// 簡單的 GameObject 物件池，重用頻繁生成/銷毀的子彈等物件，避免 Instantiate/Destroy 造成的 GC 與效能開銷
public class GameObjectPool
{
    private readonly GameObject prefab;
    private readonly Transform parent;
    private readonly Queue<GameObject> inactiveInstances = new Queue<GameObject>();

    public GameObjectPool(GameObject prefab, Transform parent, int prewarmCount = 0)
    {
        this.prefab = prefab;
        this.parent = parent;

        for (int i = 0; i < prewarmCount; i++)
        {
            GameObject instance = CreateInstance();
            Release(instance);
        }
    }

    // 取得一個物件並設定位置/旋轉，優先重用池中閒置的實例
    public GameObject Get(Vector3 position, Quaternion rotation)
    {
        GameObject instance = inactiveInstances.Count > 0 ? inactiveInstances.Dequeue() : CreateInstance();
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);
        return instance;
    }

    // 歸還物件到池中，停用並保留供之後重用
    public void Release(GameObject instance)
    {
        instance.SetActive(false);
        inactiveInstances.Enqueue(instance);
    }

    private GameObject CreateInstance()
    {
        GameObject instance = Object.Instantiate(prefab, parent);
        instance.SetActive(false);
        return instance;
    }
}
