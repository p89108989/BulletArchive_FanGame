using System;
using UnityEngine;

// 敵方單位 (直升機等) 的血量；之後玩家的攻擊命中時透過 IDamageable 扣血
public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField, Min(1), Tooltip("最大血量")] private int maxHealth = 100;

    public int CurrentHealth { get; private set; }
    public int MaxHealth => maxHealth;
    public bool IsDead { get; private set; }

    // 血量變化時觸發 (currentHealth, maxHealth)，供血條 UI 等外部系統訂閱
    public event Action<int, int> OnHealthChanged;
    // 實際受到傷害時觸發一次，供受擊特效等外部系統訂閱
    public event Action OnDamaged;
    // 血量歸零時觸發一次
    public event Action OnDied;

    private void Awake()
    {
        ResetHealth();
    }

    // 回復滿血並解除死亡狀態 (例如重新出場時)
    public void ResetHealth()
    {
        CurrentHealth = maxHealth;
        IsDead = false;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void TakeDamage(int amount)
    {
        if (IsDead || amount <= 0) return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        OnDamaged?.Invoke();

        if (CurrentHealth > 0) return;

        IsDead = true;
        OnDied?.Invoke();
    }
}
