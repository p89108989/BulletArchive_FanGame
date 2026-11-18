using UnityEngine;
using UnityEngine.UI;

// 單一顏色的能量條 (暫用與血條相同的樣式)：藍、黃各放一個，各自指定要顯示的顏色
public class EnergyBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("要顯示能量的玩家，留空時會自動抓場景中 Tag 為 Player 的角色")] private PlayerEnergy playerEnergy;
    [SerializeField, Tooltip("這條能量條顯示的顏色")] private BulletPolarity polarity = BulletPolarity.Blue;
    [SerializeField, Tooltip("能量條")] private Slider energySlider;
    [SerializeField, Tooltip("能量百分比文字")] private Text energyText;

    private int displayedPercent = -1;
    private bool started;

    private void Awake()
    {
        if (RequiredReference.IsMissing(energySlider, nameof(energySlider), this)) return;
        if (RequiredReference.IsMissing(energyText, nameof(energyText), this)) return;
        if (playerEnergy == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) playerEnergy = player.GetComponent<PlayerEnergy>();
        }
    }

    private void OnEnable()
    {
        if (playerEnergy == null) return;

        playerEnergy.OnEnergyChanged += HandleEnergyChanged;
        if (started) RefreshFromPlayer();
    }

    // 初次顯示放在 Start：此時同場景所有物件的 Awake 都已執行，玩家能量必定已初始化，不受物件初始化順序影響
    private void Start()
    {
        started = true;
        RefreshFromPlayer();
    }

    private void RefreshFromPlayer()
    {
        if (playerEnergy != null) Refresh(playerEnergy.GetEnergy(polarity), playerEnergy.MaxEnergy);
    }

    private void OnDisable()
    {
        if (playerEnergy == null) return;

        playerEnergy.OnEnergyChanged -= HandleEnergyChanged;
    }

    // 能量事件包含兩種顏色，只處理自己負責的那一種
    private void HandleEnergyChanged(BulletPolarity changedPolarity, float current, float max)
    {
        if (changedPolarity != polarity) return;
        Refresh(current, max);
    }

    // 雷射發射時每幀都會觸發；文字只在整數百分比改變時才重建，避免每幀產生字串
    private void Refresh(float current, float max)
    {
        energySlider.maxValue = max;
        energySlider.value = current;

        int percent = Mathf.CeilToInt(current / max * 100f);
        if (percent == displayedPercent) return;

        displayedPercent = percent;
        energyText.text = percent + "%";
    }
}
