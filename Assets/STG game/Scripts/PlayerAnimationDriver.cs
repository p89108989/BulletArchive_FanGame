using UnityEngine;

// 把玩家狀態轉成 Animator 參數：雷射發射中 → IsAttacking (上半身射擊層)，死亡 → IsDead (死亡動畫)。
// 移動/衝刺參數由 PlayerMovement 自己設定；這裡只負責射擊與死亡，讓雷射與血量元件不需要知道動畫
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerHealth))]
public class PlayerAnimationDriver : MonoBehaviour
{
    private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");

    private Animator animator;
    private PlayerHealth health;
    private PlayerLaser laser;
    private bool hasController;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        health = GetComponent<PlayerHealth>();
        TryGetComponent(out laser);
        hasController = animator.runtimeAnimatorController != null;
    }

    private void OnEnable()
    {
        health.OnDied += HandleDied;
    }

    private void OnDisable()
    {
        health.OnDied -= HandleDied;
    }

    private void Update()
    {
        if (!hasController) return;
        animator.SetBool(IsAttackingHash, laser != null && laser.IsFiring);
    }

    // Game Over 會把 timeScale 設為 0，改用不受 timeScale 影響的更新模式，死亡動畫才播得完
    private void HandleDied()
    {
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        if (!hasController) return;
        animator.SetBool(IsAttackingHash, false);
        animator.SetBool(IsDeadHash, true);
    }
}
