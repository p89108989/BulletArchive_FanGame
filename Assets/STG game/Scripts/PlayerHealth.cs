using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField, Range(1, 20), Tooltip("最大血量")] private int maxHealth = 5;

    [Header("Invincibility")]
    [SerializeField, Tooltip("受傷後的無敵時間 (秒)，期間內不會再受到傷害")] private float invincibilityDuration = 1f;

    public int CurrentHealth { get; private set; }
    public int MaxHealth => maxHealth;
    public float InvincibilityDuration => invincibilityDuration;
    public bool IsInvincible => Time.time < invincibleUntil;

    // 血量變化時觸發 (currentHealth, maxHealth)，供 UI 等外部系統訂閱
    public event Action<int, int> OnHealthChanged;
    // 實際受到傷害時觸發一次，供受擊特效等外部系統訂閱
    public event Action OnDamaged;
    // 血量歸零時觸發一次
    public event Action OnDied;

    private bool isDead;
    // 無敵結束的時間點；多個來源 (受傷、衝刺) 重疊時取較晚者，短的無敵不會縮短長的
    private float invincibleUntil;

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    // 承受傷害；無敵時間內或死亡後會直接忽略，不再重複扣血
    public void TakeDamage(int amount)
    {
        if (isDead || IsInvincible || amount <= 0) return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        OnDamaged?.Invoke();

        if (CurrentHealth <= 0)
        {
            isDead = true;
            OnDied?.Invoke();
            return;
        }

        GrantInvincibility(invincibilityDuration);
    }

    // 由外部系統 (例如衝刺) 觸發一段無敵時間；與尚未結束的無敵重疊時，以較晚結束的為準
    public void GrantInvincibility(float duration)
    {
        if (isDead) return;

        invincibleUntil = Mathf.Max(invincibleUntil, Time.time + duration);
    }
}
