using System;
using UnityEngine;

// 玩家能量 (百分比制)：藍/黃各自獨立。護盾每吸收一次同屬性攻擊回復對應顏色的能量，
// 雷射等攻擊則持續消耗目前極性顏色的能量
[RequireComponent(typeof(PlayerPolarity))]
public class PlayerEnergy : MonoBehaviour
{
    [Header("Energy")]
    [SerializeField, Min(1f), Tooltip("每種顏色能量的上限 (%)")] private float energyCapacity = 100f;
    [SerializeField, Min(0f), Tooltip("護盾每吸收一次同屬性攻擊回復的能量 (%)")] private float absorbGain = 5f;
    [SerializeField, Min(0f), Tooltip("開場時每種顏色的能量 (%)")] private float startingEnergy;

    private const int BlueIndex = 0;
    private const int YellowIndex = 1;

    private readonly float[] energy = new float[2];
    private PlayerPolarity playerPolarity;

    public float MaxEnergy => energyCapacity;

    // 能量改變時觸發 (顏色, 目前能量, 上限)，供能量 UI 等外部系統訂閱
    public event Action<BulletPolarity, float, float> OnEnergyChanged;

    private void Awake()
    {
        playerPolarity = GetComponent<PlayerPolarity>();

        float initial = Mathf.Min(startingEnergy, energyCapacity);
        energy[BlueIndex] = initial;
        energy[YellowIndex] = initial;
    }

    private void OnEnable()
    {
        playerPolarity.OnAttackAbsorbed += HandleAttackAbsorbed;
    }

    private void OnDisable()
    {
        playerPolarity.OnAttackAbsorbed -= HandleAttackAbsorbed;
    }

    public float GetEnergy(BulletPolarity polarity)
    {
        int index = IndexOf(polarity);
        return index < 0 ? 0f : energy[index];
    }

    public bool HasEnergy(BulletPolarity polarity)
    {
        return GetEnergy(polarity) > 0f;
    }

    // 持續消耗 (例如雷射每幀扣一點)：能量不足時扣到 0 為止，回傳實際扣掉的量
    public float Drain(BulletPolarity polarity, float amount)
    {
        int index = IndexOf(polarity);
        if (index < 0 || amount <= 0f || energy[index] <= 0f) return 0f;

        float drained = Mathf.Min(amount, energy[index]);
        SetEnergy(index, polarity, energy[index] - drained);
        return drained;
    }

    // 一次性消耗 (例如技能)：能量不足時不扣並回傳 false
    public bool TrySpend(BulletPolarity polarity, float amount)
    {
        int index = IndexOf(polarity);
        if (index < 0 || amount <= 0f || energy[index] < amount) return false;

        SetEnergy(index, polarity, energy[index] - amount);
        return true;
    }

    private void HandleAttackAbsorbed(BulletPolarity polarity)
    {
        int index = IndexOf(polarity);
        if (index < 0 || energy[index] >= energyCapacity) return;

        SetEnergy(index, polarity, Mathf.Min(energyCapacity, energy[index] + absorbGain));
    }

    private void SetEnergy(int index, BulletPolarity polarity, float value)
    {
        energy[index] = value;
        OnEnergyChanged?.Invoke(polarity, value, energyCapacity);
    }

    // 只有藍/黃兩種能量；無屬性 (None) 回傳 -1
    private static int IndexOf(BulletPolarity polarity)
    {
        switch (polarity)
        {
            case BulletPolarity.Blue: return BlueIndex;
            case BulletPolarity.Yellow: return YellowIndex;
            default: return -1;
        }
    }
}
