using System.Collections;
using UnityEngine;

public class EnemyBulletSpawner : MonoBehaviour, ICombatPhaseListener
{
    [Header("Bullet")]
    [SerializeField, Tooltip("要生成的子彈 Prefab (需帶有 EnemyBullet 元件)")] private GameObject bulletPrefab;
    [SerializeField, Tooltip("子彈飛行速度 (m/s)")] private float bulletSpeed = 37.5f;
    [SerializeField, Tooltip("物件池預先建立的子彈數量")] private int poolPrewarmCount = 20;
    [SerializeField, Tooltip("每發子彈從這些屬性中隨機挑一種")] private BulletPolarity[] randomPolarities = { BulletPolarity.Blue, BulletPolarity.Yellow };

    [Header("Spawn Points")]
    [SerializeField, Tooltip("地圖外圍的生成點，依序輪流發射")] private Transform[] spawnPoints;

    [Header("Fire Timing")]
    [SerializeField, Tooltip("每次發射的間隔時間 (秒)")] private float fireInterval = 1.2f;
    [SerializeField, Tooltip("砲台發射的聲音")] private SoundEffect fireSound = new SoundEffect();

    [Header("Targeting")]
    [SerializeField, Tooltip("瞄準目標，留空時會自動抓場景中 Tag 為 Player 的角色")] private Transform target;
    [SerializeField, Tooltip("瞄準點在目標身上往上偏移的高度，對準角色軀幹而非腳底")] private float aimHeightOffset = 5.8f;

    private GameObjectPool bulletPool;
    private int nextSpawnIndex;
    private Coroutine fireRoutine;
    private bool isPaused;

    private void Awake()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) target = player.transform;
        }

        bulletPool = new GameObjectPool(bulletPrefab, transform, poolPrewarmCount);
    }

    private void OnEnable()
    {
        fireRoutine = StartCoroutine(FireLoop());
    }

    private void OnDisable()
    {
        if (fireRoutine != null)
        {
            StopCoroutine(fireRoutine);
            fireRoutine = null;
        }
    }

    // 固定間隔輪流從各個生成點朝玩家目前位置發射一發子彈
    private IEnumerator FireLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(fireInterval);
        while (true)
        {
            FireFromNextSpawnPoint();
            yield return wait;
        }
    }

    private void FireFromNextSpawnPoint()
    {
        if (isPaused || spawnPoints == null || spawnPoints.Length == 0 || target == null || bulletPrefab == null) return;

        Transform spawnPoint = spawnPoints[nextSpawnIndex];
        nextSpawnIndex = (nextSpawnIndex + 1) % spawnPoints.Length;
        // 砲台被摧毀 (停用) 後，它的發射點不再發射
        if (spawnPoint == null || !spawnPoint.gameObject.activeInHierarchy) return;

        Vector3 aimPoint = target.position + Vector3.up * aimHeightOffset;
        Vector3 direction = (aimPoint - spawnPoint.position).normalized;

        GameObject bulletObject = bulletPool.Get(spawnPoint.position, Quaternion.identity);
        EnemyBullet bullet = bulletObject.GetComponent<EnemyBullet>();
        bullet.Launch(direction, bulletSpeed, PickRandomPolarity(), ReturnBulletToPool);
        fireSound.Play();
    }

    // Boss 戰期間砲台暫停發射 (計時照跑，回到一般戰鬥時直接接續)
    public void OnCombatPhaseChanged(CombatPhase phase)
    {
        isPaused = phase == CombatPhase.Boss;
    }

    private BulletPolarity PickRandomPolarity()
    {
        if (randomPolarities == null || randomPolarities.Length == 0) return BulletPolarity.Blue;
        return randomPolarities[Random.Range(0, randomPolarities.Length)];
    }

    private void ReturnBulletToPool(EnemyBullet bullet)
    {
        bulletPool.Release(bullet.gameObject);
    }
}
