using UnityEngine;

// 戰鬥計時器的顯示格式：分:秒，兩位數補零 (超過 99 分鐘就照實際分鐘數顯示)
public static class BattleClock
{
    public static string Format(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
    }
}
