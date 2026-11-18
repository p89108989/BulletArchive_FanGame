using System.Collections;
using UnityEngine;

// 在玩家附近落下一波地面範圍攻擊 (圈形倒數)，逼玩家移動閃避或切換對應極性的護盾。
// 可自己定時發動 (autoSpawnWaves)，或關掉自動、改由直升機等攻擊者呼叫 SpawnWave()
public class AreaStrikeSpawner : MonoBehaviour
{
    [Header("Strike")]
    [SerializeField, Tooltip("範圍攻擊 Prefab (需帶有 AreaStrike 元件)")] private GameObject strikePrefab;
    [SerializeField, Tooltip("範圍半徑 (m)")] private float strikeRadius = 10f;
    [SerializeField, Tooltip("紅圈倒數時間 (秒)，填滿後判定傷害")] private float warningDuration = 1.5f;
    [SerializeField, Tooltip("物件池預先建立的數量")] private int poolPrewarmCount = 6;
    [SerializeField, Tooltip("每個範圍攻擊從這些屬性中隨機挑一種；None = 紅色無屬性，護盾無法吸收")]
    private BulletPolarity[] randomPolarities = { BulletPolarity.None, BulletPolarity.Blue, BulletPolarity.Yellow };

    [Header("Wave Timing")]
    [SerializeField, Tooltip("自己定時發動攻擊；關閉時只在外部呼叫 SpawnWave() 時發動")] private bool autoSpawnWaves = true;
    [SerializeField, Tooltip("開場後第一波出現前的延遲 (秒)，僅自動模式")] private float firstWaveDelay = 3f;
    [SerializeField, Tooltip("每波之間的間隔 (秒)，僅自動模式")] private float waveInterval = 4f;
    [SerializeField, Range(1, 10), Tooltip("每波落下的範圍攻擊數量")] private int strikesPerWave = 3;
    [SerializeField, Tooltip("同一波內每個範圍攻擊依序出現的間隔 (秒)")] private float strikeStagger = 0.25f;

    [Header("Placement")]
    [SerializeField, Tooltip("瞄準目標，留空時會自動抓場景中 Tag 為 Player 的角色")] private Transform target;
    [SerializeField, Tooltip("每波第一個範圍攻擊直接落在目標當下位置")] private bool firstStrikeOnTarget = true;
    [SerializeField, Tooltip("其餘範圍攻擊在目標周圍隨機散佈的半徑 (m)")] private float scatterRadius = 15f;
    [SerializeField, Tooltip("紅圈貼地的高度 (地板表面 y 值再略高一點，避免與地板 Z-fighting)")] private float groundHeight = 0.35f;

    // 每生成一個範圍攻擊就觸發，供直升機等攻擊者對準爆炸時間發射飛彈
    public event System.Action<AreaStrike> OnStrikeSpawned;

    private GameObjectPool strikePool;

    private void Awake()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) target = player.transform;
        }

        strikePool = new GameObjectPool(strikePrefab, transform, poolPrewarmCount);
    }

    private void OnEnable()
    {
        if (autoSpawnWaves) StartCoroutine(AutoWaveLoop());
    }

    // 停用時一併停掉自動計時與進行中的波次
    private void OnDisable()
    {
        StopAllCoroutines();
    }

    // 立即發動一波攻擊 (同一波內依 strikeStagger 依序落下)
    public void SpawnWave()
    {
        StartCoroutine(WaveRoutine());
    }

    private IEnumerator AutoWaveLoop()
    {
        yield return new WaitForSeconds(firstWaveDelay);

        WaitForSeconds intervalWait = new WaitForSeconds(waveInterval);
        while (true)
        {
            yield return WaveRoutine();
            yield return intervalWait;
        }
    }

    private IEnumerator WaveRoutine()
    {
        WaitForSeconds staggerWait = new WaitForSeconds(strikeStagger);
        for (int i = 0; i < strikesPerWave; i++)
        {
            SpawnStrike(i == 0 && firstStrikeOnTarget);
            if (strikeStagger > 0f) yield return staggerWait;
        }
    }

    private void SpawnStrike(bool onTarget)
    {
        if (target == null || strikePrefab == null) return;

        Vector2 scatter = onTarget ? Vector2.zero : Random.insideUnitCircle * scatterRadius;
        Vector3 position = new Vector3(target.position.x + scatter.x, groundHeight, target.position.z + scatter.y);

        GameObject strikeObject = strikePool.Get(position, Quaternion.identity);
        AreaStrike strike = strikeObject.GetComponent<AreaStrike>();
        strike.Begin(strikeRadius, warningDuration, PickRandomPolarity(), ReturnStrikeToPool);
        OnStrikeSpawned?.Invoke(strike);
    }

    private BulletPolarity PickRandomPolarity()
    {
        if (randomPolarities == null || randomPolarities.Length == 0) return BulletPolarity.None;
        return randomPolarities[Random.Range(0, randomPolarities.Length)];
    }

    private void ReturnStrikeToPool(AreaStrike strike)
    {
        strikePool.Release(strike.gameObject);
    }
}
