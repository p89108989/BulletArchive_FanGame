using UnityEngine;
using UnityEngine.UI;

// 放技能時跳出的對話框 (蔚藍檔案左上的頭像 + 對話泡泡)：平常隱藏，Show 後顯示台詞，showDuration 秒後自動收起。
// 目前只有 F 大招會觸發 (訂閱 PlayerLaser.OnBurstStarted)；之後的特殊技能直接呼叫 Show 即可
public class SkillDialogUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("整個對話框 (頭像 + 泡泡)，顯示/隱藏用")] private GameObject panel;
    [SerializeField, Tooltip("台詞文字")] private Text lineLabel;
    [SerializeField, Tooltip("F 大招來源，留空時會自動抓場景中 Tag 為 Player 的角色")] private PlayerLaser playerLaser;

    [Header("Lines")]
    [SerializeField, TextArea, Tooltip("開 F 大招時的台詞")] private string burstLine = "全部掃清吧！";
    [SerializeField, Min(0.1f), Tooltip("對話框顯示多久 (秒)")] private float showDuration = 2.5f;

    private float hideAt;

    private void Awake()
    {
        if (playerLaser == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) playerLaser = player.GetComponentInParent<PlayerLaser>();
        }

        if (RequiredReference.IsMissing(panel, nameof(panel), this)) return;
        panel.SetActive(false);
    }

    private void OnEnable()
    {
        if (playerLaser != null) playerLaser.OnBurstStarted += HandleBurstStarted;
    }

    private void OnDisable()
    {
        if (playerLaser != null) playerLaser.OnBurstStarted -= HandleBurstStarted;
    }

    // 只有顯示中才需要檢查收起時間
    private void Update()
    {
        if (panel != null && panel.activeSelf && Time.time >= hideAt) panel.SetActive(false);
    }

    public void Show(string line)
    {
        if (panel == null) return;

        if (lineLabel != null) lineLabel.text = line;
        hideAt = Time.time + showDuration;
        panel.SetActive(true);
    }

    private void HandleBurstStarted()
    {
        Show(burstLine);
    }
}
