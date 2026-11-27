using UnityEngine;

// 技能卡要顯示的內容 (純資料，由技能的狀態換算)：
// progress = 卡片上暗色遮罩/進度的比例 (冷卻剩多少、發動剩多少，0~1)；seconds = 顯示的剩餘秒數 (無條件進位)
public struct SkillCardStatus
{
    public SkillCardState state;
    public float progress;
    public int seconds;

    public static SkillCardStatus ForDash(float cooldownRemaining, float cooldown)
    {
        if (cooldownRemaining <= 0f || cooldown <= 0f) return new SkillCardStatus { state = SkillCardState.Ready };

        return new SkillCardStatus
        {
            state = SkillCardState.Cooldown,
            progress = Mathf.Clamp01(cooldownRemaining / cooldown),
            seconds = Mathf.CeilToInt(cooldownRemaining),
        };
    }

    public static SkillCardStatus ForBurst(bool active, float remaining, float duration, bool canStart)
    {
        if (active)
        {
            return new SkillCardStatus
            {
                state = SkillCardState.Active,
                progress = duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f,
                seconds = Mathf.CeilToInt(remaining),
            };
        }

        return new SkillCardStatus { state = canStart ? SkillCardState.Ready : SkillCardState.Unavailable };
    }

    public static SkillCardStatus Locked()
    {
        return new SkillCardStatus { state = SkillCardState.Locked };
    }
}
