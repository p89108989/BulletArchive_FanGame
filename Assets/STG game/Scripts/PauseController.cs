using UnityEngine;

// 暫停 (HUD 右上的暫停鍵 / 暫停選單的「繼續」)：把時間流速設為 0 並打開暫停選單，恢復時還原成暫停前的值。
// 開場 START、結算面板等已經把時間停住時不接受暫停，否則恢復時會搶走它們的暫停狀態
public class PauseController : MonoBehaviour
{
    [SerializeField, Tooltip("暫停時打開的選單 (只控制顯示，按鈕由 PauseMenu 處理)，可留空")] private GameObject menu;

    private float timeScaleBeforePause = 1f;

    public bool IsPaused { get; private set; }

    private void Awake()
    {
        if (menu != null) menu.SetActive(false);
    }

    // 停用或場景卸載時若還在暫停，把時間還原，避免下一個場景卡在暫停
    private void OnDisable()
    {
        if (IsPaused) Resume();
    }

    // 給暫停鍵 (Button.onClick) 呼叫
    public void TogglePause()
    {
        if (IsPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (IsPaused || Time.timeScale <= 0f) return;

        timeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0f;
        IsPaused = true;
        if (menu != null) menu.SetActive(true);
    }

    public void Resume()
    {
        if (!IsPaused) return;

        Time.timeScale = timeScaleBeforePause;
        IsPaused = false;
        if (menu != null) menu.SetActive(false);
    }
}
