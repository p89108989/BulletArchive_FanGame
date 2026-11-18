using System.Collections;
using UnityEngine;

public class PlayerHitFlash : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("受傷判定來源，留空時會自動抓自身的 PlayerHealth")] private PlayerHealth playerHealth;

    [Header("Flash")]
    [SerializeField, Tooltip("閃爍間隔 (秒)，數值越小閃越快")] private float blinkInterval = 0.1f;

    private Renderer[] renderers;
    private Coroutine flashRoutine;

    private void Awake()
    {
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void OnEnable()
    {
        if (playerHealth == null) return;

        playerHealth.OnDamaged += HandleDamaged;
        playerHealth.OnDied += HandleDied;
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnDamaged -= HandleDamaged;
            playerHealth.OnDied -= HandleDied;
        }

        StopFlash();
    }

    private void HandleDamaged()
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine(playerHealth.InvincibilityDuration));
    }

    // 致命一擊同樣會觸發閃爍；死亡後 Game Over 會暫停時間，必須停止閃爍並恢復顯示，否則角色可能凍結在隱藏狀態
    private void HandleDied()
    {
        StopFlash();
    }

    private void StopFlash()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }
        SetRenderersVisible(true);
    }

    // 在無敵時間內讓角色所有 Renderer 交替顯示/隱藏，做出閃爍效果
    private IEnumerator FlashRoutine(float duration)
    {
        float elapsed = 0f;
        bool visible = true;

        while (elapsed < duration)
        {
            visible = !visible;
            SetRenderersVisible(visible);
            yield return new WaitForSeconds(blinkInterval);
            elapsed += blinkInterval;
        }

        SetRenderersVisible(true);
        flashRoutine = null;
    }

    private void SetRenderersVisible(bool visible)
    {
        foreach (Renderer r in renderers)
        {
            r.enabled = visible;
        }
    }
}
