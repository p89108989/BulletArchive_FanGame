// Boss 特殊機制的血量門檻 (例如 2/3、1/3)：血量第一次掉到門檻 (含) 以下時回報那個門檻的索引，每個門檻只回報一次。
// 一次扣很多血跨過好幾個門檻時，每次呼叫只回報一個 (依陣列順序，所以門檻要由高到低排)，呼叫端執行完一個機制再處理下一個
public class HealthThresholds
{
    private readonly float[] fractions;
    private readonly bool[] triggered;

    public HealthThresholds(float[] fractions)
    {
        this.fractions = fractions ?? System.Array.Empty<float>();
        triggered = new bool[this.fractions.Length];
    }

    // 回報下一個新跨過的門檻索引；沒有則回傳 -1。已死亡 (current <= 0) 不觸發
    public int Next(int current, int max)
    {
        if (current <= 0 || max <= 0) return -1;

        for (int i = 0; i < fractions.Length; i++)
        {
            if (triggered[i] || current > fractions[i] * max) continue;

            triggered[i] = true;
            return i;
        }

        return -1;
    }
}
