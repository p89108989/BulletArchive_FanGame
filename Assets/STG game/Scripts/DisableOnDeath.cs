using System.Collections;
using UnityEngine;

// 敵人血量歸零時播放爆炸特效並停用指定物件；可設定一段時間後重生 (回滿血並重新啟用)。
// 要重生時，停用目標必須是子物件 (例如砲台的 Pivot)，自己保持啟用才能計時。Boss 戰期間暫停重生倒數
[RequireComponent(typeof(EnemyHealth))]
public class DisableOnDeath : MonoBehaviour, ICombatPhaseListener
{
    [Header("Disable")]
    [SerializeField, Tooltip("死亡時停用的物件，留空時停用自己 (此時無法重生)")] private GameObject disableTarget;
    [SerializeField, Min(0f), Tooltip("死亡後幾秒重生，0 = 不重生")] private float respawnDelay;

    [Header("Death Effect")]
    [SerializeField, Tooltip("死亡時生成的爆炸特效 Prefab，可留空")] private GameObject deathEffectPrefab;
    [SerializeField, Tooltip("特效生成位置相對於自身的偏移")] private Vector3 effectOffset = new Vector3(0f, 3.5f, 0f);
    [SerializeField, Tooltip("特效縮放 (爆炸特效以半徑 10 的範圍設計)")] private float effectScale = 0.6f;
    [SerializeField, Tooltip("特效存在多久後刪除 (秒)")] private float effectLifetime = 3f;

    private EnemyHealth health;
    private bool isRespawnPaused;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
    }

    // 若在等待重生期間被停用 (重生計時隨之中斷)，重新啟用時立刻以存活狀態重生，避免本體出現卻處於死亡、打不到的狀態
    private void OnEnable()
    {
        health.OnDied += HandleDied;
        if (respawnDelay > 0f && disableTarget != null && disableTarget != gameObject && !disableTarget.activeSelf) Respawn();
    }

    // 只取消訂閱與等待中的重生；不在這裡動子物件，因為場景卸載或父物件停用時再啟用子物件會造成錯誤
    private void OnDisable()
    {
        health.OnDied -= HandleDied;
        StopAllCoroutines();
    }

    private void HandleDied()
    {
        if (deathEffectPrefab != null)
        {
            GameObject effect = Instantiate(deathEffectPrefab, transform.position + effectOffset, Quaternion.identity);
            effect.transform.localScale = Vector3.one * effectScale;
            Destroy(effect, effectLifetime);
        }

        bool canRespawn = respawnDelay > 0f && disableTarget != null && disableTarget != gameObject;
        if (!canRespawn)
        {
            (disableTarget != null ? disableTarget : gameObject).SetActive(false);
            return;
        }

        disableTarget.SetActive(false);
        StartCoroutine(RespawnAfterDelay());
    }

    // Boss 戰期間倒數暫停，回到一般戰鬥後從剩餘秒數繼續
    private IEnumerator RespawnAfterDelay()
    {
        float remaining = respawnDelay;
        while (remaining > 0f)
        {
            if (!isRespawnPaused) remaining -= Time.deltaTime;
            yield return null;
        }
        Respawn();
    }

    public void OnCombatPhaseChanged(CombatPhase phase)
    {
        isRespawnPaused = phase == CombatPhase.Boss;
    }

    private void Respawn()
    {
        health.ResetHealth();
        disableTarget.SetActive(true);
    }
}
