using UnityEngine;

// 蔚藍檔案 Boss 血條的多層血量換算：總血量每 perLayer 一層，remaining = 還剩幾層 (含目前這層)，
// fill = 目前這層剩下的比例。總血量不是 perLayer 的整數倍時，最上面那層比較短，比例以那層的實際容量計算
public struct BossHpLayers
{
    public int remaining;
    public float fill;

    public static BossHpLayers Compute(int current, int max, int perLayer)
    {
        if (current <= 0 || max <= 0) return new BossHpLayers();
        if (perLayer <= 0) return new BossHpLayers { remaining = 1, fill = Mathf.Clamp01((float)current / max) };

        int remaining = Mathf.CeilToInt((float)current / perLayer);
        int below = (remaining - 1) * perLayer;
        int capacity = Mathf.Min(perLayer, max - below);
        return new BossHpLayers
        {
            remaining = remaining,
            fill = capacity > 0 ? Mathf.Clamp01((float)(current - below) / capacity) : 0f,
        };
    }
}
