using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// 物理查詢依賴 Layer 過濾 (否則固定大小的 NonAlloc 緩衝區可能被無關碰撞體擠滿)：確認 prefab 的 Layer 設定沒被改掉
public class LayerSetupTests
{
    private const string PrefabFolder = "Assets/STG game/resources/prefab/";

    private static GameObject Load(string name)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + name + ".prefab");
        Assert.IsNotNull(prefab, name);
        return prefab;
    }

    private static void AssertAllCollidersOnLayer(GameObject root, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
        {
            Assert.AreEqual(layer, collider.gameObject.layer, collider.name + " should be on " + layerName);
        }
    }

    [Test]
    public void AreaStrikePrefab_HitLayers_IsPlayerOnly()
    {
        SerializedObject strike = new SerializedObject(Load("AreaStrike").GetComponent<AreaStrike>());
        Assert.AreEqual(LayerMask.GetMask("Player"), strike.FindProperty("hitLayers").intValue);
    }

    [Test]
    public void TurretPrefab_CollidersOnEnemyLayer()
    {
        AssertAllCollidersOnLayer(Load("EnemyTurret"), "Enemy");
    }

    [Test]
    public void BulletPrefab_CollidersOnEnemyBulletLayer()
    {
        AssertAllCollidersOnLayer(Load("EnemyBullet_SM05"), "EnemyBullet");
    }
}
