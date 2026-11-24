// 需要依戰鬥階段改變行為的元件 (例如 Boss 戰時暫停攻擊的敵人)；由 CombatPhaseController 在切換階段時呼叫
public interface ICombatPhaseListener
{
    void OnCombatPhaseChanged(CombatPhase phase);
}
