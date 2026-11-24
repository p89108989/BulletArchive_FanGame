using System;
using System.Collections;
using UnityEngine;

// 遊戲戰鬥階段的狀態機：開場為一般戰鬥，經過 bossStartDelay 秒後進入 Boss 戰。
// 切換階段時通知所有監聽者 (砲台、直升機、戰車暫停攻擊；Boss 出場；相機換到 Boss 視角)，
// 各元件自己決定怎麼反應，這裡只負責「現在是哪個階段」與「什麼時候切換」
public class CombatPhaseController : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField, Min(0f), Tooltip("遊戲開始後幾秒進入 Boss 戰")] private float bossStartDelay = 60f;

    [Header("Listeners")]
    [SerializeField, Tooltip("切換階段時要通知的元件 (需實作 ICombatPhaseListener)")]
    private MonoBehaviour[] phaseListeners = Array.Empty<MonoBehaviour>();

    public CombatPhase CurrentPhase { get; private set; } = CombatPhase.Normal;

    // 階段切換時觸發，供 UI 等不在清單內的系統訂閱
    public event Action<CombatPhase> OnPhaseChanged;

    private void Awake()
    {
        for (int i = 0; i < phaseListeners.Length; i++)
        {
            if (phaseListeners[i] != null && !(phaseListeners[i] is ICombatPhaseListener))
            {
                Debug.LogError("[CombatPhaseController] " + phaseListeners[i].name + " 的 " + phaseListeners[i].GetType().Name + " 沒有實作 ICombatPhaseListener", phaseListeners[i]);
            }
        }
    }

    private void OnEnable()
    {
        if (CurrentPhase == CombatPhase.Normal) StartCoroutine(BossCountdown());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    // 切換到指定階段並通知所有監聽者；已在該階段時不重複通知
    public void EnterPhase(CombatPhase phase)
    {
        if (phase == CurrentPhase) return;

        CurrentPhase = phase;
        for (int i = 0; i < phaseListeners.Length; i++)
        {
            if (phaseListeners[i] is ICombatPhaseListener listener) listener.OnCombatPhaseChanged(phase);
        }
        OnPhaseChanged?.Invoke(phase);
    }

    private IEnumerator BossCountdown()
    {
        yield return new WaitForSeconds(bossStartDelay);
        EnterPhase(CombatPhase.Boss);
    }
}
