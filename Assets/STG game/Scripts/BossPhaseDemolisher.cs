using UnityEngine;

// 進入 Boss 戰時直接摧毀指定的敵人 (例如 Boss 那一側的砲台)，由它們自己的死亡流程播放爆炸；
// 重生倒數由 DisableOnDeath 在 Boss 戰期間暫停
public class BossPhaseDemolisher : MonoBehaviour, ICombatPhaseListener
{
    [Header("Targets")]
    [SerializeField, Tooltip("進入 Boss 戰時要摧毀的敵人")] private EnemyHealth[] targets = System.Array.Empty<EnemyHealth>();

    public void OnCombatPhaseChanged(CombatPhase phase)
    {
        if (phase != CombatPhase.Boss) return;

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null && !targets[i].IsDead) targets[i].TakeDamage(targets[i].MaxHealth);
        }
    }
}
