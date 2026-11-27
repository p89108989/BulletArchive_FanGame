using UnityEngine;

// 分格能量條的換算 (蔚藍檔案 COST 條)：每格代表 max / segmentCount，正在累積的那一格依比例部分填滿
public static class EnergyGauge
{
    // 第 segmentIndex 格 (0 起算) 的填滿比例 0~1
    public static float SegmentFill(float energy, float max, int segmentIndex, int segmentCount)
    {
        if (max <= 0f || segmentCount <= 0) return 0f;

        float segments = Mathf.Clamp01(energy / max) * segmentCount;
        return Mathf.Clamp01(segments - segmentIndex);
    }

    // 已經完全填滿的格數 (顯示在前面的數字)
    public static int FullSegments(float energy, float max, int segmentCount)
    {
        if (max <= 0f || segmentCount <= 0) return 0;

        // 加一點容差，避免 30% 這類值因浮點誤差算成 2.9999 格
        return Mathf.FloorToInt(Mathf.Clamp01(energy / max) * segmentCount + 0.0001f);
    }
}
