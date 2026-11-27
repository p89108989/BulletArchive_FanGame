using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 暫停選單的三個按鈕：繼續 (關閉選單)、重新開始 (重新載入戰鬥場景)、返回大廳 (回標題)。
// 換場景前先恢復時間，否則下一個場景會從 timeScale = 0 開始
public class PauseMenu : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("負責暫停 / 恢復時間與選單顯示")] private PauseController pauseController;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button lobbyButton;

    [Header("Scenes")]
    [SerializeField, Tooltip("換場景的轉場，可留空 (留空直接換場景)")] private SceneTransition sceneTransition;
    [SerializeField, Tooltip("重新開始載入的場景 (戰鬥場景)；留空則只發出事件")] private string restartSceneName = "mian game";
    [SerializeField, Tooltip("返回大廳載入的場景；留空則只發出事件")] private string lobbySceneName = "Title";

    public event Action OnRestartRequested;
    public event Action OnLobbyRequested;

    private void OnEnable()
    {
        if (continueButton != null) continueButton.onClick.AddListener(Continue);
        if (restartButton != null) restartButton.onClick.AddListener(Restart);
        if (lobbyButton != null) lobbyButton.onClick.AddListener(Lobby);
    }

    private void OnDisable()
    {
        if (continueButton != null) continueButton.onClick.RemoveListener(Continue);
        if (restartButton != null) restartButton.onClick.RemoveListener(Restart);
        if (lobbyButton != null) lobbyButton.onClick.RemoveListener(Lobby);
    }

    public void Continue()
    {
        if (pauseController != null) pauseController.Resume();
    }

    // 重新開始走「黑幕 → 碎裂」轉場，跟從選角進戰鬥一樣
    public void Restart()
    {
        Continue();
        OnRestartRequested?.Invoke();
        Load(restartSceneName, true);
    }

    public void Lobby()
    {
        Continue();
        OnLobbyRequested?.Invoke();
        Load(lobbySceneName, false);
    }

    private void Load(string sceneName, bool fromBlack)
    {
        if (string.IsNullOrEmpty(sceneName)) return;

        if (sceneTransition != null) sceneTransition.LoadScene(sceneName, fromBlack);
        else SceneManager.LoadScene(sceneName);
    }
}
