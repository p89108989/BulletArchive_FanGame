using UnityEngine;

// 戰鬥中的角色語音與槍聲：依選到的角色 (GameSelection) 從 voiceSets 挑一組 CharacterVoiceSet。
// 衝刺、受傷 (倒下那一下不算，交給落敗語音)、一般射擊每一發、每隔 moveCheckInterval 秒檢查一次「這段時間有移動」就講移動語音；
// 戰鬥開始 (START)、落敗、勝利結算面板由 BattleFlow 的事件觸發
public class PlayerVoice : MonoBehaviour
{
    [Header("Voices")]
    [SerializeField, Tooltip("各角色的語音 (第一組是沒選角色時的預設)")] private CharacterVoiceSet[] voiceSets = System.Array.Empty<CharacterVoiceSet>();
    [SerializeField, Tooltip("選角場景寫入的選擇，可留空 (留空用第一組)")] private GameSelection gameSelection;

    [Header("References")]
    [SerializeField, Tooltip("戰鬥流程 (開始 / 落敗 / 勝利語音)，可留空")] private BattleFlow battleFlow;

    [Header("Move Line")]
    [SerializeField, Min(0.1f), Tooltip("每隔幾秒檢查一次有沒有移動，有就講移動語音")] private float moveCheckInterval = 15f;
    [SerializeField, Min(0f), Tooltip("這段時間內至少走了多遠 (m) 才算有移動")] private float minMoveDistance = 1f;

    private CharacterVoiceSet voices;
    private PlayerMovement movement;
    private PlayerHealth health;
    private PlayerLaser laser;
    private float moveTimer;
    private float movedDistance;
    private Vector3 lastPosition;

    public CharacterVoiceSet Voices => voices;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        health = GetComponent<PlayerHealth>();
        laser = GetComponent<PlayerLaser>();
        string id = gameSelection != null && gameSelection.HasSelection ? gameSelection.CharacterId : null;
        voices = CharacterVoiceSet.Find(voiceSets, id);
        lastPosition = transform.position;
    }

    private void OnEnable()
    {
        if (movement != null) movement.OnDashStarted += HandleDash;
        if (health != null) health.OnDamaged += HandleDamaged;
        if (laser != null) laser.OnShotFired += HandleShot;
        if (battleFlow == null) return;

        battleFlow.OnBattleStarted += HandleBattleStarted;
        battleFlow.OnDefeated += HandleDefeated;
        battleFlow.OnVictoryResultsShown += HandleVictory;
    }

    private void OnDisable()
    {
        if (movement != null) movement.OnDashStarted -= HandleDash;
        if (health != null) health.OnDamaged -= HandleDamaged;
        if (laser != null) laser.OnShotFired -= HandleShot;
        if (battleFlow == null) return;

        battleFlow.OnBattleStarted -= HandleBattleStarted;
        battleFlow.OnDefeated -= HandleDefeated;
        battleFlow.OnVictoryResultsShown -= HandleVictory;
    }

    // 只累積水平移動距離；戰鬥還沒開始 / 已結束 (遊戲暫停) 時 deltaTime 為 0，不會計時
    private void Update()
    {
        Vector3 position = transform.position;
        Vector3 delta = position - lastPosition;
        delta.y = 0f;
        movedDistance += delta.magnitude;
        lastPosition = position;
        if (battleFlow != null && !battleFlow.IsBattleRunning) return;

        moveTimer += Time.deltaTime;
        if (moveTimer < moveCheckInterval) return;

        if (movedDistance >= minMoveDistance) Say(voices != null ? voices.Move : null);
        moveTimer = 0f;
        movedDistance = 0f;
    }

    private void HandleDash() => Say(voices != null ? voices.Dash : null);

    private void HandleDamaged()
    {
        if (health != null && health.CurrentHealth <= 0) return;

        Say(voices != null ? voices.Damage : null);
    }

    private void HandleShot()
    {
        if (voices != null) voices.BasicAttack.Play();
    }

    private void HandleBattleStarted() => Say(voices != null ? voices.BattleStart : null);
    private void HandleDefeated() => Say(voices != null ? voices.Defeat : null);
    private void HandleVictory() => Say(voices != null ? voices.Victory : null);

    private static void Say(SoundEffect line)
    {
        if (line != null) line.PlayAsVoice();
    }
}
