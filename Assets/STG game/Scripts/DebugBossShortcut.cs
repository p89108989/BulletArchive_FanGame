using UnityEngine;
using UnityEngine.InputSystem;

// Debug 用快捷鍵：按 B 直接進入 Boss 戰，不用等 bossStartDelay。
// 只在 Editor 與 Development Build 生效，正式版本按了也沒反應
public class DebugBossShortcut : MonoBehaviour
{
    [SerializeField, Tooltip("要切換階段的控制器，留空時自動抓同物件上的")] private CombatPhaseController phaseController;
    [SerializeField, Tooltip("進入 Boss 戰的按鍵")] private Key bossKey = Key.B;

    private void Awake()
    {
        if (phaseController == null) phaseController = GetComponent<CombatPhaseController>();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || phaseController == null) return;
        if (!keyboard[bossKey].wasPressedThisFrame || phaseController.CurrentPhase == CombatPhase.Boss) return;

        phaseController.EnterPhase(CombatPhase.Boss);
    }
#endif
}
