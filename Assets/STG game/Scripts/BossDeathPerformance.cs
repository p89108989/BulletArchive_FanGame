using System;
using System.Collections;
using UnityEngine;

// Boss 被擊倒的演出：立刻關掉碰撞 (不再被打到、不再閃白)，播死亡動畫，
// 動畫結束後往下沉入海中，下沉時從 Boss 腳下陸續冒出卡通煙霧，沉完通知外部 (回到一般戰鬥或顯示結算) 並移除 Boss。
// Boss 的各種攻擊在血量歸零時已自行停止，這裡只負責退場
[RequireComponent(typeof(EnemyHealth))]
public class BossDeathPerformance : MonoBehaviour
{
    [SerializeField, Tooltip("Boss 模型上的 Animator (需有 Death 觸發參數)，可留空")] private Animator animator;
    [SerializeField, Min(0f), Tooltip("死亡動畫長度 (秒)，播完才開始下沉")] private float deathAnimationDuration = 5.57f;
    [SerializeField, Min(0.01f), Tooltip("下沉消失花的時間 (秒)")] private float sinkDuration = 3f;
    [SerializeField, Min(0f), Tooltip("下沉的距離 (m)，要足以讓整隻 Boss 沉到看不見")] private float sinkDistance = 260f;

    [Header("Vanish Smoke")]
    [SerializeField, Tooltip("下沉時冒出的煙霧 Prefab (含 ParticleSystem，生成後播放)，可留空")] private GameObject vanishSmokePrefab;
    [SerializeField, Min(0), Tooltip("煙霧團數，在下沉前段陸續冒出")] private int smokeCount = 6;
    [SerializeField, Tooltip("煙霧分布範圍的中心 (相對 Boss 根物件)：從 Boss 腳下 (海面附近) 冒出再往上飄一些；Boss 根物件在海面下 100 m")] private Vector3 smokeCenterOffset = new Vector3(0f, 105f, 0f);
    [SerializeField, Tooltip("煙霧分布範圍的半徑 (X 左右、Y 上下、Z 前後)")] private Vector3 smokeAreaExtents = new Vector3(50f, 6f, 15f);
    [SerializeField, Min(0.01f), Tooltip("煙霧的放大倍數 (Boss 很大，需要放大才蓋得住)")] private float smokeScale = 6f;
    [SerializeField, Min(0.1f), Tooltip("煙霧存在多久後移除 (秒)")] private float smokeLifetime = 5f;
    [SerializeField, Range(0.05f, 1f), Tooltip("煙霧在下沉時間的前多少比例內冒完")] private float smokeSpawnPortion = 0.85f;

    private static readonly int DeathHash = Animator.StringToHash("Death");

    // 死亡演出 (含下沉) 全部結束、Boss 即將移除時觸發一次
    public event Action OnFinished;

    private EnemyHealth health;
    private Collider[] colliders;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        colliders = GetComponentsInChildren<Collider>(true);
    }

    private void OnEnable()
    {
        health.OnDied += HandleDied;
    }

    private void OnDisable()
    {
        health.OnDied -= HandleDied;
    }

    private void HandleDied()
    {
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null) colliders[i].enabled = false;
        }

        if (animator != null) animator.SetTrigger(DeathHash);
        StartCoroutine(Perform());
    }

    private IEnumerator Perform()
    {
        yield return new WaitForSeconds(deathAnimationDuration);

        Vector3 start = transform.position;
        int spawnedSmoke = 0;
        float smokeInterval = sinkDuration * smokeSpawnPortion / Mathf.Max(1, smokeCount);
        for (float elapsed = 0f; elapsed < sinkDuration; elapsed += Time.deltaTime)
        {
            while (spawnedSmoke < smokeCount && elapsed >= spawnedSmoke * smokeInterval)
            {
                SpawnSmoke(start);
                spawnedSmoke++;
            }

            // 先慢後快，像失去支撐後整隻沉下去
            float t = elapsed / sinkDuration;
            transform.position = start + Vector3.down * (sinkDistance * t * t);
            yield return null;
        }

        for (; spawnedSmoke < smokeCount; spawnedSmoke++) SpawnSmoke(start);
        transform.position = start + Vector3.down * sinkDistance;
        OnFinished?.Invoke();
        Destroy(gameObject);
    }

    // 煙霧以 Boss 原本的位置為準 (不掛在 Boss 底下)，Boss 沉下去後煙霧留在原地散開
    private void SpawnSmoke(Vector3 bossStart)
    {
        if (vanishSmokePrefab == null) return;

        Vector3 offset = new Vector3(
            UnityEngine.Random.Range(-smokeAreaExtents.x, smokeAreaExtents.x),
            UnityEngine.Random.Range(-smokeAreaExtents.y, smokeAreaExtents.y),
            UnityEngine.Random.Range(-smokeAreaExtents.z, smokeAreaExtents.z));
        GameObject smoke = Instantiate(vanishSmokePrefab, bossStart + smokeCenterOffset + offset, Quaternion.identity);
        smoke.transform.localScale = Vector3.one * smokeScale;
        ParticleSystem particles = smoke.GetComponentInChildren<ParticleSystem>();
        if (particles != null) particles.Play(true);
        Destroy(smoke, smokeLifetime);
    }
}
