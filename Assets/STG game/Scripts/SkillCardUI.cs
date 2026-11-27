using UnityEngine;
using UnityEngine.UI;

// HUD 右下的技能卡 (蔚藍檔案 EX 技能卡風格)：依技能目前的狀態切換外觀。
// 衝刺 — 冷卻中顯示圓形倒數 (暗色扇形像時鐘一樣退掉) 與剩餘秒數；
// F 大招 — 能量夠才亮，發動中顯示綠色外框與剩餘秒數；特殊技能 — 尚未開放，變暗加鎖頭。
// 狀態每幀換算，但外觀只在狀態、秒數或進度改變時才寫入
public class SkillCardUI : MonoBehaviour
{
    public enum SkillKind
    {
        Dash,
        Burst,
        Special,
    }

    [Header("Skill")]
    [SerializeField, Tooltip("這張卡顯示哪個技能")] private SkillKind kind;
    [SerializeField, Tooltip("衝刺來源，留空時會自動抓場景中 Tag 為 Player 的角色")] private PlayerMovement playerMovement;
    [SerializeField, Tooltip("F 大招來源，留空時會自動抓場景中 Tag 為 Player 的角色")] private PlayerLaser playerLaser;

    [Header("Visuals")]
    [SerializeField, Tooltip("不能使用時蓋在卡片上的暗色")] private GameObject dimOverlay;
    [SerializeField, Tooltip("冷卻/發動剩餘量 (Filled 類型)")] private Image cooldownFill;
    [SerializeField, Tooltip("剩餘秒數")] private Text secondsLabel;
    [SerializeField, Tooltip("尚未開放時顯示的鎖頭")] private GameObject lockIcon;
    [SerializeField, Tooltip("發動中顯示的外框，可留空")] private GameObject activeHighlight;

    // 秒數只顯示整數，預先做好字串避免每次產生
    private static readonly string[] SecondTexts = BuildSecondTexts(100);

    private SkillCardStatus shown;
    private bool hasShown;

    private void Awake()
    {
        if (playerMovement != null && playerLaser != null) return;

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return;
        if (playerMovement == null) playerMovement = player.GetComponentInParent<PlayerMovement>();
        if (playerLaser == null) playerLaser = player.GetComponentInParent<PlayerLaser>();
    }

    private void Update()
    {
        SkillCardStatus status = CurrentStatus();
        if (hasShown && status.state == shown.state && status.seconds == shown.seconds && Mathf.Approximately(status.progress, shown.progress)) return;

        Apply(status);
    }

    private SkillCardStatus CurrentStatus()
    {
        switch (kind)
        {
            case SkillKind.Dash:
                return playerMovement != null
                    ? SkillCardStatus.ForDash(playerMovement.DashCooldownRemaining, playerMovement.DashCooldown)
                    : SkillCardStatus.Locked();
            case SkillKind.Burst:
                return playerLaser != null
                    ? SkillCardStatus.ForBurst(playerLaser.IsBurstActive, playerLaser.BurstTimeRemaining, playerLaser.BurstDuration, playerLaser.CanStartBurst)
                    : SkillCardStatus.Locked();
            default:
                return SkillCardStatus.Locked();
        }
    }

    private void Apply(SkillCardStatus status)
    {
        shown = status;
        hasShown = true;

        bool timed = status.state == SkillCardState.Cooldown || status.state == SkillCardState.Active;
        // 冷卻中不整張變暗：只靠圓形倒數的暗色扇形，亮暗對比才看得出倒數進度
        SetActive(dimOverlay, status.state == SkillCardState.Unavailable || status.state == SkillCardState.Locked);
        SetActive(lockIcon, status.state == SkillCardState.Locked);
        SetActive(activeHighlight, status.state == SkillCardState.Active);

        if (cooldownFill != null)
        {
            SetActive(cooldownFill.gameObject, timed);
            cooldownFill.fillAmount = status.progress;
        }

        if (secondsLabel != null)
        {
            SetActive(secondsLabel.gameObject, timed);
            secondsLabel.text = SecondTexts[Mathf.Clamp(status.seconds, 0, SecondTexts.Length - 1)];
        }
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active) target.SetActive(active);
    }

    private static string[] BuildSecondTexts(int count)
    {
        string[] texts = new string[count];
        for (int i = 0; i < count; i++) texts[i] = i.ToString();
        return texts;
    }
}
