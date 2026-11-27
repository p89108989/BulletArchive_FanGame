using NUnit.Framework;

// HUD 右下技能卡的狀態：可用 / 冷卻中 (衝刺) / 能量不足 (F 大招) / 發動中 (F 大招) / 尚未開放 (特殊技能)。
// progress = 卡片上暗色遮罩的比例 (冷卻剩多少、發動剩多少)；seconds = 顯示的剩餘秒數 (無條件進位)
public class SkillCardStatusTests
{
    [Test]
    public void Dash_ReadyWhenCooldownIsOver()
    {
        SkillCardStatus status = SkillCardStatus.ForDash(0f, 2f);

        Assert.AreEqual(SkillCardState.Ready, status.state);
        Assert.AreEqual(0f, status.progress);
        Assert.AreEqual(0, status.seconds);
    }

    [Test]
    public void Dash_CoolingDownShowsRemainingPart()
    {
        SkillCardStatus status = SkillCardStatus.ForDash(1.2f, 2f);

        Assert.AreEqual(SkillCardState.Cooldown, status.state);
        Assert.AreEqual(0.6f, status.progress, 0.001f);
        Assert.AreEqual(2, status.seconds, "1.2 seconds left shows as 2");
    }

    [Test]
    public void Burst_ActiveShowsRemainingTime()
    {
        SkillCardStatus status = SkillCardStatus.ForBurst(true, 3.4f, 5f, false);

        Assert.AreEqual(SkillCardState.Active, status.state);
        Assert.AreEqual(0.68f, status.progress, 0.001f);
        Assert.AreEqual(4, status.seconds);
    }

    [Test]
    public void Burst_ReadyOnlyWithEnoughEnergy()
    {
        Assert.AreEqual(SkillCardState.Ready, SkillCardStatus.ForBurst(false, 0f, 5f, true).state);
        Assert.AreEqual(SkillCardState.Unavailable, SkillCardStatus.ForBurst(false, 0f, 5f, false).state);
    }

    [Test]
    public void Special_IsLocked()
    {
        Assert.AreEqual(SkillCardState.Locked, SkillCardStatus.Locked().state);
    }
}
