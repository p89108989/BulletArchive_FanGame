using UnityEngine;
using UnityEngine.UI;

// 戰鬥計時器 (HUD 右上)：從 00:00 往上數。用遊戲時間累加，暫停 (時間流速 0) 時自然停住；
// 只在顯示的秒數改變時才更新文字，避免每幀產生字串
public class BattleTimerUI : MonoBehaviour
{
    [SerializeField, Tooltip("顯示時間的文字")] private Text label;

    private int shownSeconds = -1;

    public float Elapsed { get; private set; }

    private void Awake()
    {
        RequiredReference.IsMissing(label, nameof(label), this);
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void Update()
    {
        Elapsed += Time.deltaTime;
        Refresh();
    }

    private void Refresh()
    {
        int seconds = Mathf.FloorToInt(Elapsed);
        if (seconds == shownSeconds || label == null) return;

        shownSeconds = seconds;
        label.text = BattleClock.Format(Elapsed);
    }
}
