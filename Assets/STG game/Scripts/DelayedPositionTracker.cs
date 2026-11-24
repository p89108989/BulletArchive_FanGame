using UnityEngine;

// 記錄一段時間內的位置，查詢「某個時間點時在哪裡」(例如玩家 1 秒前的位置)。
// 固定容量的環狀緩衝，記錄滿了就覆寫最舊的，不會產生 GC
public class DelayedPositionTracker
{
    private readonly float[] times;
    private readonly Vector3[] positions;
    private int start;
    private int count;

    public DelayedPositionTracker(int capacity)
    {
        capacity = Mathf.Max(2, capacity);
        times = new float[capacity];
        positions = new Vector3[capacity];
    }

    public void Clear()
    {
        start = 0;
        count = 0;
    }

    // 時間必須遞增；同一時間重複記錄時覆寫最後一筆
    public void Record(float time, Vector3 position)
    {
        if (count > 0 && time <= times[IndexOf(count - 1)])
        {
            positions[IndexOf(count - 1)] = position;
            return;
        }

        if (count < times.Length)
        {
            count++;
        }
        else
        {
            start = (start + 1) % times.Length;
        }

        int last = IndexOf(count - 1);
        times[last] = time;
        positions[last] = position;
    }

    // 查詢指定時間的位置 (兩筆記錄之間線性內插)；超出記錄範圍時回傳最舊/最新的一筆
    public Vector3 Sample(float time)
    {
        if (count == 0) return Vector3.zero;
        if (time <= times[IndexOf(0)]) return positions[IndexOf(0)];

        for (int i = count - 1; i > 0; i--)
        {
            int previous = IndexOf(i - 1);
            if (time < times[previous]) continue;

            int next = IndexOf(i);
            float span = times[next] - times[previous];
            float t = span > 0f ? (time - times[previous]) / span : 1f;
            return Vector3.Lerp(positions[previous], positions[next], Mathf.Clamp01(t));
        }

        return positions[IndexOf(count - 1)];
    }

    private int IndexOf(int offset) => (start + offset) % times.Length;
}
