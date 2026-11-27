using System.Collections;
using UnityEngine;

// 戰鬥的開場與結束演出：
// 開場暫停遊戲，等進場轉場播完放 START，START 飛出後才開始戰鬥 (開始計時)；
// 進入 Boss 戰放 WARNING (不暫停)；打完最後一隻 Boss (BossEncounter 呼叫 ShowVictory) 或玩家倒下 →
// 暫停、VICTORY / DEFEAT 字卡 → 結算面板 (關卡、擊殺數、使用時間)
public class BattleFlow : MonoBehaviour, ICombatPhaseListener
{
    [Header("References")]
    [SerializeField, Tooltip("字卡")] private BattleBanner banner;
    [SerializeField, Tooltip("擊殺數與戰鬥時間")] private BattleStats stats;
    [SerializeField, Tooltip("結算面板")] private ResultPanel resultPanel;
    [SerializeField, Tooltip("玩家血量 (倒下時顯示 DEFEAT)，留空時會自動抓場景中 Tag 為 Player 的角色")] private PlayerHealth playerHealth;
    [SerializeField, Tooltip("取得關卡名稱 (目前的 Boss)，可留空")] private BossEncounter bossEncounter;
    [SerializeField, Tooltip("選角場景寫入的選擇 (無盡模式時關卡名稱顯示「無盡模式」)，可留空")] private GameSelection gameSelection;
    [SerializeField, Tooltip("HUD 左上角的關卡名稱，開場時寫入選到的 Boss 名稱，可留空")] private UnityEngine.UI.Text stageLabel;

    [Header("Words")]
    [SerializeField, Tooltip("WARNING 字圖")] private Sprite warningWord;
    [SerializeField, Tooltip("VICTORY 字圖")] private Sprite victoryWord;
    [SerializeField, Tooltip("DEFEAT 字圖")] private Sprite defeatWord;
    [SerializeField, Tooltip("無盡模式的關卡名稱")] private string endlessStageName = "無盡模式";

    [Header("Audio")]
    [SerializeField, Tooltip("沒有經過選角 (直接開戰鬥場景) 時的戰鬥 BGM；有選關時用選角寫入的 BGM")] private AudioClip defaultBattleMusic;
    [SerializeField, Tooltip("勝利 BGM (VICTORY 字卡開始，一直播到結算畫面)")] private AudioClip victoryMusic;
    [SerializeField, Tooltip("START 字卡的音效")] private AudioClip startSound;
    [SerializeField, Tooltip("WARNING 字卡的警報音效")] private AudioClip warningSound;

    private bool pausedByIntro;
    private bool ended;

    public bool IsBattleRunning { get; private set; }

    // START 字卡出現時 (戰鬥開始語音)
    public event System.Action OnBattleStarted;
    // 玩家倒下、DEFEAT 字卡出現時
    public event System.Action OnDefeated;
    // 勝利後結算面板出現時
    public event System.Action OnVictoryResultsShown;

    private bool victory;

    private void Awake()
    {
        if (playerHealth != null) return;

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null) playerHealth = player.GetComponent<PlayerHealth>();
    }

    private void OnEnable()
    {
        if (playerHealth != null) playerHealth.OnDied += HandlePlayerDied;
    }

    private void OnDisable()
    {
        if (playerHealth != null) playerHealth.OnDied -= HandlePlayerDied;
        if (pausedByIntro) Time.timeScale = 1f;
    }

    private void Start()
    {
        if (stageLabel != null) stageLabel.text = StageName();
        // 從選角進來時這首已經在轉場開始時播了，這裡會接著播不重來
        AudioClip selected = gameSelection != null && gameSelection.HasSelection ? gameSelection.BattleMusic : null;
        GameAudio.PlayMusic(selected != null ? selected : defaultBattleMusic);
        Time.timeScale = 0f;
        pausedByIntro = true;
        StartCoroutine(Intro());
    }

    private IEnumerator Intro()
    {
        // 進場轉場 (稜鏡散開) 還在播就先等
        while (SceneTransition.IsBusy) yield return null;

        GameAudio.PlaySfx(startSound);
        OnBattleStarted?.Invoke();
        if (banner != null) banner.PlayStart(BeginBattle);
        else BeginBattle();
    }

    private void BeginBattle()
    {
        pausedByIntro = false;
        if (ended) return;

        Time.timeScale = 1f;
        IsBattleRunning = true;
        if (stats != null) stats.StartTiming();
    }

    public void OnCombatPhaseChanged(CombatPhase phase)
    {
        if (phase != CombatPhase.Boss || ended) return;

        GameAudio.PlaySfx(warningSound);
        if (banner != null) banner.PlayStamp(warningWord, true, null);
    }

    // 打完最後一隻 Boss
    public void ShowVictory()
    {
        if (ended) return;

        victory = true;
        GameAudio.PlayMusic(victoryMusic);
        End(victoryWord);
    }

    // 落敗沒有專屬 BGM：戰鬥 BGM 淡出
    private void HandlePlayerDied()
    {
        if (ended) return;

        GameAudio.StopMusic();
        End(defeatWord);
        OnDefeated?.Invoke();
    }

    private void End(Sprite word)
    {
        if (ended) return;

        ended = true;
        IsBattleRunning = false;
        if (stats != null) stats.StopTiming();
        Time.timeScale = 0f;
        if (banner != null) banner.PlayStamp(word, false, ShowResults);
        else ShowResults();
    }

    private void ShowResults()
    {
        if (victory) OnVictoryResultsShown?.Invoke();
        if (resultPanel == null) return;

        int kills = stats != null ? stats.Kills : 0;
        float seconds = stats != null ? stats.ElapsedSeconds : 0f;
        resultPanel.Show(StageName(), kills, seconds);
    }

    private string StageName()
    {
        if (gameSelection != null && gameSelection.HasSelection && gameSelection.Loop) return endlessStageName;

        BossProfile profile = bossEncounter != null ? bossEncounter.Profile : null;
        return profile != null ? profile.DisplayName : string.Empty;
    }
}
