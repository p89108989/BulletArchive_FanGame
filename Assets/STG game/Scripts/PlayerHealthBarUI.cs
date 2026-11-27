using UnityEngine;
using UnityEngine.UI;

// HUD 左下的玩家血條 (蔚藍檔案角色血條風格，以 3 條分隔線分成 4 格)：
// 綠色血量 (fill) 受傷時立刻下降；白色的「剛扣掉的血」(damageTrail) 先停留 trailDelay 秒，再以 trailSpeed 追上目前血量
public class PlayerHealthBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("玩家血量，留空時會自動抓場景中 Tag 為 Player 的角色")] private PlayerHealth playerHealth;
    [SerializeField, Tooltip("目前血量 (Filled 類型)")] private Image fill;
    [SerializeField, Tooltip("剛扣掉的血 (Filled 類型，畫在目前血量後面)")] private Image damageTrail;

    [Header("Damage Trail")]
    [SerializeField, Min(0f), Tooltip("受傷後白色殘影停留多久才開始縮短 (秒)")] private float trailDelay = 0.4f;
    [SerializeField, Min(0.01f), Tooltip("白色殘影縮短的速度 (每秒整條血量的比例)")] private float trailSpeed = 1.5f;

    private float target = 1f;
    private float trailHoldUntil;
    private bool started;

    private void Awake()
    {
        if (playerHealth == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) playerHealth = player.GetComponentInParent<PlayerHealth>();
        }

        if (RequiredReference.IsMissing(playerHealth, nameof(playerHealth), this)) return;
        RequiredReference.IsMissing(fill, nameof(fill), this);
    }

    private void OnEnable()
    {
        if (playerHealth == null) return;

        playerHealth.OnHealthChanged += HandleHealthChanged;
        if (started) SnapToCurrent();
    }

    // 初次顯示放在 Start：此時玩家血量的 Awake 必定已執行
    private void Start()
    {
        started = true;
        SnapToCurrent();
    }

    private void OnDisable()
    {
        if (playerHealth != null) playerHealth.OnHealthChanged -= HandleHealthChanged;
    }

    // 只有白色殘影還沒追上時才需要每幀更新
    private void Update()
    {
        if (damageTrail == null || damageTrail.fillAmount <= target || Time.time < trailHoldUntil) return;

        damageTrail.fillAmount = Mathf.MoveTowards(damageTrail.fillAmount, target, trailSpeed * Time.deltaTime);
    }

    private void SnapToCurrent()
    {
        if (playerHealth == null) return;

        target = Ratio(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        if (fill != null) fill.fillAmount = target;
        if (damageTrail != null) damageTrail.fillAmount = target;
    }

    private void HandleHealthChanged(int current, int max)
    {
        target = Ratio(current, max);
        if (fill != null) fill.fillAmount = target;
        if (damageTrail == null) return;

        // 受傷：殘影留在原處一下；回血：殘影直接跟上
        if (damageTrail.fillAmount > target) trailHoldUntil = Time.time + trailDelay;
        else damageTrail.fillAmount = target;
    }

    private static float Ratio(int current, int max) => max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
}
