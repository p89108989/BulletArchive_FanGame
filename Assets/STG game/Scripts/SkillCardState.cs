// HUD 技能卡的狀態
public enum SkillCardState
{
    // 可以使用
    Ready,
    // 冷卻中 (衝刺)
    Cooldown,
    // 條件不足 (F 大招能量不夠)
    Unavailable,
    // 發動中 (F 大招的光束時間)
    Active,
    // 尚未開放 (特殊技能)
    Locked,
}
