using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 開始畫面的按鈕：start 進入選角場景、教學 (還沒做，按鈕先停用)、離開遊戲
public class TitleScreen : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField, Tooltip("開始：載入下一個場景")] private Button startButton;
    [SerializeField, Tooltip("教學：還沒有教學內容，先停用")] private Button tutorialButton;
    [SerializeField, Tooltip("離開遊戲")] private Button quitButton;

    [Header("Flow")]
    [SerializeField, Tooltip("按 start 後載入的場景 (要在 Build Settings 裡)；留空則只發出事件")] private string nextSceneName = "CharacterSelect";
    [SerializeField, Tooltip("按離開遊戲時真的關閉程式 (編輯器中為停止 Play)；測試時關掉")] private bool quitApplication = true;
    [SerializeField, Tooltip("換場景用的稜鏡轉場，可留空 (留空直接換場景)")] private SceneTransition sceneTransition;

    public event Action OnStartRequested;
    public event Action OnQuitRequested;

    private void OnEnable()
    {
        if (startButton != null) startButton.onClick.AddListener(StartGame);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
        if (tutorialButton != null) tutorialButton.interactable = false;
    }

    private void OnDisable()
    {
        if (startButton != null) startButton.onClick.RemoveListener(StartGame);
        if (quitButton != null) quitButton.onClick.RemoveListener(QuitGame);
    }

    public void StartGame()
    {
        OnStartRequested?.Invoke();
        if (string.IsNullOrEmpty(nextSceneName)) return;

        if (sceneTransition != null) sceneTransition.LoadScene(nextSceneName, false);
        else SceneManager.LoadScene(nextSceneName);
    }

    public void QuitGame()
    {
        OnQuitRequested?.Invoke();
        if (!quitApplication) return;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
