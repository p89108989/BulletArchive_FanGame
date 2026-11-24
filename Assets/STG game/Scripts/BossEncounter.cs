using UnityEngine;

// Boss 出場與退場：進入 Boss 戰時在指定位置生成 Boss (同一時間只有一隻)，接上畫面血條；
// Boss 被擊倒且死亡演出 (BossDeathPerformance) 結束後，依設定回到一般戰鬥或顯示結算面板。
// Boss 的攻擊與血量由 Boss 自己的元件處理，這裡只負責「何時出現、退場後接什麼」
public class BossEncounter : MonoBehaviour, ICombatPhaseListener
{
    public enum DefeatOutcome
    {
        // 回到一般戰鬥：相機、天色等由各監聽者自行切回
        ReturnToNormal,
        // 顯示結算面板並暫停
        ShowResults,
    }

    [Header("Boss")]
    [SerializeField, Tooltip("Boss Prefab")] private GameObject bossPrefab;
    [SerializeField, Tooltip("Boss 生成位置")] private Vector3 spawnPosition = new Vector3(-50f, -100f, -65f);
    [SerializeField, Tooltip("Boss 生成時的旋轉 (尤拉角)")] private Vector3 spawnRotation;

    [Header("UI")]
    [SerializeField, Tooltip("畫面上方的 Boss 血條，Boss 出場時接上，可留空")] private EnemyHealthBarUI bossHealthBar;
    [SerializeField, Tooltip("血條上顯示的 Boss 名稱")] private string bossDisplayName = "Perorozilla";

    [Header("Defeat")]
    [SerializeField, Tooltip("Boss 被擊倒 (死亡演出結束) 後要做什麼")] private DefeatOutcome defeatOutcome = DefeatOutcome.ReturnToNormal;
    [SerializeField, Tooltip("回到一般戰鬥時用來切換階段的控制器")] private CombatPhaseController phaseController;
    [SerializeField, Tooltip("顯示結算時使用的面板")] private BossResultScreen resultScreen;

    private GameObject boss;
    private BossDeathPerformance deathPerformance;
    private float spawnTime;

    public GameObject Boss => boss;

    private void Awake()
    {
        RequiredReference.IsMissing(bossPrefab, nameof(bossPrefab), this);
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
        if (phase != CombatPhase.Boss || boss != null || !isActiveAndEnabled) return;

        boss = Instantiate(bossPrefab, spawnPosition, Quaternion.Euler(spawnRotation));
        boss.name = bossPrefab.name;
        spawnTime = Time.time;
        if (bossHealthBar != null) bossHealthBar.Bind(boss.GetComponent<EnemyHealth>(), bossDisplayName);

        deathPerformance = boss.GetComponent<BossDeathPerformance>();
        if (deathPerformance != null) deathPerformance.OnFinished += HandleBossDefeated;
        else Debug.LogWarning("[BossEncounter] Boss 沒有 BossDeathPerformance，被擊倒後不會退場", boss);
    }

    private void HandleBossDefeated()
    {
        deathPerformance.OnFinished -= HandleBossDefeated;
        deathPerformance = null;
        boss = null;

        if (defeatOutcome == DefeatOutcome.ShowResults && resultScreen != null)
        {
            resultScreen.Show(Time.time - spawnTime);
            return;
        }

        if (phaseController != null) phaseController.EnterPhase(CombatPhase.Normal);
    }
}
