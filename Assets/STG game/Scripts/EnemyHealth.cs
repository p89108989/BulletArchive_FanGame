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
    // 受到傷害的倍率 (例如 Boss 暈眩時 ×1.5)；小數部分累積到下一次，每發 1 點也能正確得到 1.5 倍
    public float DamageMultiplier { get; set; } = 1f;

    private float damageCarry;

    // 血量變化時觸發 (currentHealth, maxHealth)，供血條 UI 等外部系統訂閱
    public event Action<int, int> OnHealthChanged;
    // 實際受到傷害時觸發一次，供受擊特效等外部系統訂閱
    public event Action OnDamaged;
    // 血量歸零時觸發一次
    public event Action OnDied;
    // 任何敵人被打倒時觸發 (結算的擊殺數)；用 Demolish 直接清掉的不算
    public static event Action<EnemyHealth> OnAnyKilled;

    // 專案關掉了 Domain Reload (加快進 Play)，靜態事件不會自動清空；每次進 Play 時清掉上一次留下的訂閱
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetStatics()
    {
        OnAnyKilled = null;
    }

    private void Awake()
    {
        ResetHealth();
    }

    // 回復滿血並解除死亡狀態 (例如重新出場時)
    public void ResetHealth()
    {
        CurrentHealth = maxHealth;
        IsDead = false;
        damageCarry = 0f;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void TakeDamage(int amount)
    {
        if (IsDead || amount <= 0) return;

        float scaled = amount * DamageMultiplier + damageCarry;
        int applied = Mathf.FloorToInt(scaled);
        damageCarry = scaled - applied;
        if (applied <= 0) return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - applied);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        OnDamaged?.Invoke();

        if (CurrentHealth > 0) return;

        Die();
        OnAnyKilled?.Invoke(this);
    }

    // 系統直接清掉 (例如進入 Boss 戰時摧毀砲台)：照樣走死亡流程 (爆炸、重生)，但不算玩家的擊殺
    public void Demolish()
    {
        if (IsDead) return;

        CurrentHealth = 0;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        Die();
    }

    private void Die()
    {
        IsDead = true;
        OnDied?.Invoke();
    }
}
