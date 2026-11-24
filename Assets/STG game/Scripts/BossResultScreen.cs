using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Boss 結算面板 (美術之後再替換)：擊倒 Boss 後顯示通關時間與剩餘血量並暫停遊戲，按重新開始鍵重新載入場景。
// 本元件需放在常駐啟用的物件上，面板是它底下預設關閉的子物件
public class BossResultScreen : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("結算面板 (預設關閉)")] private GameObject panel;
    [SerializeField, Tooltip("顯示通關時間與剩餘血量的文字")] private Text detailText;
    [SerializeField, Tooltip("玩家血量，留空時會自動抓場景中 Tag 為 Player 的角色")] private PlayerHealth playerHealth;

    [Header("Restart")]
    [SerializeField, Tooltip("重新開始的按鍵")] private Key restartKey = Key.R;
    [SerializeField, Tooltip("重新開始時重新載入目前場景 (測試時可關閉)")] private bool reloadSceneOnRestart = true;

    private bool isShowing;

    private void Awake()
    {
        if (playerHealth == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) playerHealth = player.GetComponent<PlayerHealth>();
        }

        if (RequiredReference.IsMissing(panel, nameof(panel), this)) return;
        panel.SetActive(false);
    }

    // 停用或場景卸載時，若是本元件暫停了遊戲就把時間流速還原，避免下一個場景卡在暫停
    private void OnDisable()
    {
        if (isShowing) Time.timeScale = 1f;
    }

    // 暫停期間 Update 仍會執行，可以讀取重新開始鍵
    private void Update()
    {
        if (!isShowing) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[restartKey].wasPressedThisFrame) Restart();
    }

    // 顯示結算：clearSeconds 為 Boss 出場到被擊倒的秒數
    public void Show(float clearSeconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(clearSeconds));
        string time = (totalSeconds / 60).ToString("00") + ":" + (totalSeconds % 60).ToString("00");
        string hp = playerHealth != null ? playerHealth.CurrentHealth + " / " + playerHealth.MaxHealth : "-";
        if (detailText != null) detailText.text = "通關時間  " + time + "\n剩餘血量  " + hp + "\n\n<size=30>按 " + restartKey + " 重新開始</size>";

        isShowing = true;
        panel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void Restart()
    {
        isShowing = false;
        panel.SetActive(false);
        Time.timeScale = 1f;

        if (reloadSceneOnRestart) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
