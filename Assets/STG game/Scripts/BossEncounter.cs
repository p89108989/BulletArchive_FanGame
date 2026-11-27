using UnityEngine;

// Boss 出場與退場：進入 Boss 戰時在指定位置生成 Boss (同一時間只有一隻)，接上畫面血條；
// Boss 被擊倒且死亡演出 (BossDeathPerformance) 結束後，依設定回到一般戰鬥或顯示結算面板。
// 從選角場景進來時 (GameSelection 有選擇) 改用選到的 Boss 清單：還有下一隻就換下一隻並回到一般戰鬥 (無盡模式)，
// 打完了就顯示結算。Boss 的攻擊與血量由 Boss 自己的元件處理，這裡只負責「何時出現、退場後接什麼」
public class BossEncounter : MonoBehaviour, ICombatPhaseListener
{
    public enum DefeatOutcome
    {
        // 回到一般戰鬥：相機、天色等由各監聽者自行切回
        ReturnToNormal,
        // VICTORY 字卡 → 結算面板
        ShowResults,
    }

    [Header("Boss")]
    [SerializeField, Tooltip("這一關要打哪一隻 Boss (Prefab、名稱、生成位置、相機位置都在裡面)；從選角場景進來時改用 GameSelection 的選擇")] private BossProfile bossProfile;
    [SerializeField, Tooltip("選角場景寫入的選擇，可留空 (留空或沒有選擇時用上面的 bossProfile)")] private GameSelection gameSelection;

    [Header("UI")]
    [SerializeField, Tooltip("畫面上方的 Boss 血條，Boss 出場時接上，可留空")] private EnemyHealthBarUI bossHealthBar;

    [Header("Player")]
    [SerializeField, Tooltip("玩家的活動範圍限制，Boss 出場時依 BossProfile 開關、回到一般戰鬥時解除，可留空")] private PlayerAreaLimiter playerAreaLimiter;

    [Header("Defeat")]
    [SerializeField, Tooltip("Boss 被擊倒 (死亡演出結束) 後要做什麼")] private DefeatOutcome defeatOutcome = DefeatOutcome.ReturnToNormal;
    [SerializeField, Tooltip("回到一般戰鬥時用來切換階段的控制器")] private CombatPhaseController phaseController;
    [SerializeField, Tooltip("戰鬥結束演出 (VICTORY 字卡 → 結算面板)")] private BattleFlow battleFlow;

    private GameObject boss;
    private BossDeathPerformance deathPerformance;
    private int bossIndex;

    public GameObject Boss => boss;
    // 目前 (或下一場) 的 Boss，相機、天空等依這個決定 Boss 戰的設定；無盡模式擊倒後就已換成下一隻
    public BossProfile Profile => UsesSelection ? gameSelection.GetBoss(bossIndex) : bossProfile;
    // 這是選到的清單中的第幾隻 (從 0 開始，循環時繼續累加)
    public int BossIndex => bossIndex;

    private bool UsesSelection => gameSelection != null && gameSelection.HasSelection;

    private void Awake()
    {
        if (UsesSelection) return;
        if (RequiredReference.IsMissing(bossProfile, nameof(bossProfile), this)) return;
        if (bossProfile.BossPrefab == null) Debug.LogError("[BossEncounter] " + bossProfile.name + " 沒有指定 Boss Prefab", bossProfile);
    }

    private void OnEnable()
    {
        if (deathPerformance != null) deathPerformance.OnFinished += HandleBossDefeated;
    }

    private void OnDisable()
    {
        if (deathPerformance != null) deathPerformance.OnFinished -= HandleBossDefeated;
    }

    public void OnCombatPhaseChanged(CombatPhase phase)
    {
        if (phase != CombatPhase.Boss)
        {
            if (playerAreaLimiter != null) playerAreaLimiter.Release();
            return;
        }

        if (boss != null || !isActiveAndEnabled) return;
        BossProfile profile = Profile;
        if (profile == null || profile.BossPrefab == null) return;

        ApplyPlayerArea(profile);

        GameObject prefab = profile.BossPrefab;
        boss = Instantiate(prefab, profile.SpawnPosition, profile.SpawnRotation);
        boss.name = prefab.name;
        if (bossHealthBar != null) bossHealthBar.Bind(boss.GetComponent<EnemyHealth>(), profile.DisplayName, profile.Portrait);

        deathPerformance = boss.GetComponent<BossDeathPerformance>();
        if (deathPerformance != null) deathPerformance.OnFinished += HandleBossDefeated;
        else Debug.LogWarning("[BossEncounter] Boss 沒有 BossDeathPerformance，被擊倒後不會退場", boss);
    }

    // 相機只看得到場地一部分的 Boss 才限制玩家範圍
    private void ApplyPlayerArea(BossProfile profile)
    {
        if (playerAreaLimiter == null) return;

        if (profile.LimitsPlayerArea) playerAreaLimiter.Restrict(profile.PlayerAreaMin, profile.PlayerAreaMax);
        else playerAreaLimiter.Release();
    }

    private void HandleBossDefeated()
    {
        deathPerformance.OnFinished -= HandleBossDefeated;
        deathPerformance = null;
        boss = null;

        if (UsesSelection)
        {
            HandleSelectedBossDefeated();
            return;
        }

        if (defeatOutcome == DefeatOutcome.ShowResults && battleFlow != null)
        {
            battleFlow.ShowVictory();
            return;
        }

        if (phaseController != null) phaseController.EnterPhase(CombatPhase.Normal);
    }

    // 選到的清單還有下一隻：先換好 Profile 再回到一般戰鬥 (相機、天空切回時已讀得到下一隻)，倒數後下一隻出場；
    // 打完了顯示結算 (沒有結算面板就回到一般戰鬥)
    private void HandleSelectedBossDefeated()
    {
        if (gameSelection.GetBoss(bossIndex + 1) != null)
        {
            bossIndex++;
            if (phaseController == null) return;
            phaseController.EnterPhase(CombatPhase.Normal);
            phaseController.RestartBossCountdown();
            return;
        }

        if (battleFlow != null)
        {
            battleFlow.ShowVictory();
            return;
        }

        if (phaseController != null) phaseController.EnterPhase(CombatPhase.Normal);
    }
}
