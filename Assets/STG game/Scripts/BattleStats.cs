using UnityEngine;

// 結算面板用的戰鬥數據：被打倒的敵人數 (EnemyHealth.OnAnyKilled) 與戰鬥時間。
// 時間用遊戲時間 (Time.time)，開場演出與暫停期間不計
public class BattleStats : MonoBehaviour
{
    private float startTime;
    private float stopTime;
    private bool started;
    private bool stopped;

    public int Kills { get; private set; }

    public float ElapsedSeconds
    {
        get
        {
            if (!started) return 0f;
            return (stopped ? stopTime : Time.time) - startTime;
        }
    }

    private void OnEnable()
    {
        EnemyHealth.OnAnyKilled += HandleKilled;
    }

    private void OnDisable()
    {
        EnemyHealth.OnAnyKilled -= HandleKilled;
    }

    // 戰鬥正式開始 (START 字卡結束)
    public void StartTiming()
    {
        startTime = Time.time;
        started = true;
        stopped = false;
    }

    // 戰鬥結束 (勝利或玩家倒下)
    public void StopTiming()
    {
        if (!started || stopped) return;

        stopTime = Time.time;
        stopped = true;
    }

    private void HandleKilled(EnemyHealth enemy)
    {
        Kills++;
    }
}
