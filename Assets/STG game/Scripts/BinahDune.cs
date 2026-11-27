using UnityEngine;

// Binah 潑水 (沙) 後在場上堆出的沙丘：有自己的血量，會被 Boss 的其他攻擊打掉 (玩家的射擊不打沙丘：不放在 Enemy 層)。
// 光炮特殊機制時，只要沙丘還在場上 (不論剩多少血)，沙丘「正後方」的倒梯形區域就是安全的：
// 緊貼沙丘後緣時跟沙丘一樣寬，往後線性收窄，coverLength 公尺處剩 tipWidthRatio 的寬度 (梯形比三角形好判斷)
public class BinahDune : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField, Min(1), Tooltip("沙丘的血量，歸零時消失")] private int maxHealth = 30;

    [SerializeField, Tooltip("沙丘被打破時在原地播放的特效 (例如揚沙)，可留空")] private GameObject breakEffect;
    [SerializeField, Min(0.1f), Tooltip("破壞特效存在多久後移除 (秒)")] private float breakEffectLifetime = 3f;
    [SerializeField, Min(0.01f), Tooltip("破壞特效的縮放 (沿用 Binah 潑沙特效時要縮小)")] private float breakEffectScale = 0.35f;

    [Header("Cover")]
    [SerializeField, Min(0.1f), Tooltip("掩護區從沙丘後緣往後延伸多遠 (m)")] private float coverLength = 30f;
    [SerializeField, Range(0f, 1f), Tooltip("掩護區末端的寬度 (沙丘寬度的比例)；0 = 收成一點的三角形")] private float tipWidthRatio = 0.4f;

    public int MaxHealth => maxHealth;
    public int CurrentHealth { get; private set; }
    // 被打掉的那一刻起就不再擋光炮 (Destroy 要到這一幀結束才生效)
    public bool IsDestroyed { get; private set; }

    private Collider[] colliders;

    private void Awake()
    {
        CurrentHealth = maxHealth;
        colliders = GetComponentsInChildren<Collider>();
    }

    public void TakeDamage(int amount)
    {
        if (IsDestroyed || amount <= 0) return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        if (CurrentHealth > 0) return;

        IsDestroyed = true;
        if (breakEffect != null)
        {
            GameObject effect = Instantiate(breakEffect, transform.position, Quaternion.identity);
            effect.transform.localScale = Vector3.one * breakEffectScale;
            effect.SetActive(true);
            Destroy(effect, breakEffectLifetime);
        }

        Destroy(gameObject);
    }

    // point 是否在沙丘正後方的掩護區內；fireDirection = 光炮的水平方向 (從 Binah 往場地)
    public bool Covers(Vector3 point, Vector3 fireDirection)
    {
        if (IsDestroyed) return false;

        Vector3 forward = new Vector3(fireDirection.x, 0f, fireDirection.z);
        if (forward.sqrMagnitude < 0.0001f) return false;
        forward.Normalize();
        Vector3 lateral = new Vector3(forward.z, 0f, -forward.x);

        Bounds bounds = FootprintBounds();
        Vector3 extents = bounds.extents;
        float halfWidth = Mathf.Abs(extents.x * lateral.x) + Mathf.Abs(extents.z * lateral.z);
        float halfDepth = Mathf.Abs(extents.x * forward.x) + Mathf.Abs(extents.z * forward.z);

        Vector3 offset = point - bounds.center;
        float depthBehind = Vector3.Dot(offset, forward) - halfDepth;
        if (depthBehind < 0f || depthBehind > coverLength) return false;

        float allowedHalfWidth = halfWidth * Mathf.Lerp(1f, tipWidthRatio, depthBehind / coverLength);
        return Mathf.Abs(Vector3.Dot(offset, lateral)) <= allowedHalfWidth;
    }

    // 沙丘的水平範圍 (所有 Collider 的聯集)；沒有 Collider 時用自己的位置
    private Bounds FootprintBounds()
    {
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        bool hasAny = false;
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null) continue;
            if (!hasAny)
            {
                bounds = colliders[i].bounds;
                hasAny = true;
            }
            else
            {
                bounds.Encapsulate(colliders[i].bounds);
            }
        }

        return bounds;
    }
}
