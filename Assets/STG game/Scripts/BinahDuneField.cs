using System.Collections.Generic;
using UnityEngine;

// Binah 在場上堆出的沙堆 (例行潑沙與光炮機制共用)：場上最多 maxDunes 個，滿了就不再生成；
// 新沙堆與既有沙堆至少相隔 spacing，找不到空位就不放 (避免疊在一起)；被打掉的沙堆不算數；Boss 死亡時全部清除
[RequireComponent(typeof(EnemyHealth))]
public class BinahDuneField : MonoBehaviour
{
    [Header("Dunes")]
    [SerializeField, Tooltip("沙堆 Prefab (需有 Collider 與 BinahDune)")] private GameObject dunePrefab;
    [SerializeField, Min(0), Tooltip("場上最多幾個沙堆")] private int maxDunes = 3;
    [SerializeField, Min(0f), Tooltip("沙堆之間至少相隔多遠 (m)；沙堆約 22 m 寬")] private float spacing = 28f;

    [Header("Area (X/Z)")]
    [SerializeField, Tooltip("沙堆可生成範圍的最小 X / Z")] private Vector2 areaMin = new Vector2(-95f, 25f);
    [SerializeField, Tooltip("沙堆可生成範圍的最大 X / Z")] private Vector2 areaMax = new Vector2(-5f, 60f);

    private const int AttemptsPerDune = 12;
    private const float MaxRandomYaw = 20f;

    private readonly List<BinahDune> dunes = new List<BinahDune>();
    private EnemyHealth health;

    // 場上還在 (沒被打掉) 的沙堆數量
    public int ActiveCount
    {
        get
        {
            Prune();
            return dunes.Count;
        }
    }

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
    }

    private void OnEnable()
    {
        health.OnDied += Clear;
    }

    private void OnDisable()
    {
        health.OnDied -= Clear;
    }

    // 在整個可生成範圍內隨機放一個
    public bool TrySpawnRandom()
    {
        if (!HasRoom()) return false;

        for (int attempt = 0; attempt < AttemptsPerDune; attempt++)
        {
            Vector3 position = new Vector3(Random.Range(areaMin.x, areaMax.x), 0f, Random.Range(areaMin.y, areaMax.y));
            if (TryPlace(position)) return true;
        }

        return false;
    }

    // 在 point 附近 radius 內放一個 (例如潑沙落下的地方)，位置限制在可生成範圍內
    public bool TrySpawnNear(Vector3 point, float radius)
    {
        if (!HasRoom()) return false;

        for (int attempt = 0; attempt < AttemptsPerDune; attempt++)
        {
            Vector2 offset = attempt == 0 ? Vector2.zero : Random.insideUnitCircle * radius;
            Vector3 position = new Vector3(
                Mathf.Clamp(point.x + offset.x, areaMin.x, areaMax.x), 0f,
                Mathf.Clamp(point.z + offset.y, areaMin.y, areaMax.y));
            if (TryPlace(position)) return true;
        }

        return false;
    }

    // 站在任何一個還在場上的沙堆正後方 (倒梯形掩護區) 就算躲過
    public bool IsCovered(Vector3 point, Vector3 fireDirection)
    {
        for (int i = 0; i < dunes.Count; i++)
        {
            if (dunes[i] != null && dunes[i].Covers(point, fireDirection)) return true;
        }

        return false;
    }

    // 把場上的沙堆全部打破 (各自播放破壞特效)，例如光炮機制結束時
    public void BreakAll()
    {
        for (int i = 0; i < dunes.Count; i++)
        {
            if (dunes[i] != null && !dunes[i].IsDestroyed) dunes[i].TakeDamage(dunes[i].MaxHealth);
        }

        dunes.Clear();
    }

    public void Clear()
    {
        for (int i = 0; i < dunes.Count; i++)
        {
            if (dunes[i] != null) Destroy(dunes[i].gameObject);
        }

        dunes.Clear();
    }

    private bool HasRoom()
    {
        return dunePrefab != null && ActiveCount < maxDunes;
    }

    private bool TryPlace(Vector3 position)
    {
        if (!IsFarFromDunes(position)) return false;

        GameObject dune = Instantiate(dunePrefab, position, Quaternion.Euler(0f, Random.Range(-MaxRandomYaw, MaxRandomYaw), 0f));
        dune.SetActive(true);
        BinahDune cover = dune.GetComponent<BinahDune>();
        if (cover == null) cover = dune.AddComponent<BinahDune>();
        dunes.Add(cover);
        return true;
    }

    private bool IsFarFromDunes(Vector3 position)
    {
        for (int i = 0; i < dunes.Count; i++)
        {
            Vector3 offset = dunes[i].transform.position - position;
            offset.y = 0f;
            if (offset.sqrMagnitude < spacing * spacing) return false;
        }

        return true;
    }

    // 移除已被打掉的沙堆
    private void Prune()
    {
        dunes.RemoveAll(dune => dune == null || dune.IsDestroyed);
    }
}
