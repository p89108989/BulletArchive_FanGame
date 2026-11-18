using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// 簡易 Game Over 畫面 (美術之後再替換)：玩家死亡時顯示面板並暫停遊戲，按重新開始鍵重新載入場景。
// 本元件需放在常駐啟用的物件上，面板是它底下預設關閉的子物件
public class GameOverScreen : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("監聽死亡的玩家血量，留空時會自動抓場景中 Tag 為 Player 的角色")] private PlayerHealth playerHealth;
    [SerializeField, Tooltip("Game Over 面板 (預設關閉)")] private GameObject panel;

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

        if (panel == null)
        {
            Debug.LogError("[GameOverScreen] panel 未指定", this);
            enabled = false;
            return;
        }

        panel.SetActive(false);
    }

    private void OnEnable()
    {
        if (playerHealth != null) playerHealth.OnDied += HandleDied;
    }

    // 停用或場景卸載時，若是本元件暫停了遊戲就把時間流速還原，避免下一個場景卡在暫停
    private void OnDisable()
    {
        if (playerHealth != null) playerHealth.OnDied -= HandleDied;
        if (isShowing) Time.timeScale = 1f;
    }

    // 暫停期間 Update 仍會執行，可以讀取重新開始鍵
    private void Update()
    {
        if (!isShowing) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[restartKey].wasPressedThisFrame) Restart();
    }

    public void Restart()
    {
        isShowing = false;
        panel.SetActive(false);
        Time.timeScale = 1f;

        if (reloadSceneOnRestart) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void HandleDied()
    {
        isShowing = true;
        panel.SetActive(true);
        Time.timeScale = 0f;
    }
}
