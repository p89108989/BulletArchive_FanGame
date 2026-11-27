using UnityEngine;
using UnityEngine.UI;

// HUD 右下的能量條 (蔚藍檔案 COST 條風格)：一種顏色一條 (Pulse = 藍、Flame = 黃)，
// 10 格各代表 10%，前面的數字牌顯示已填滿的格數。只在能量改變時更新
public class EnergyGaugeUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("玩家能量，留空時會自動抓場景中 Tag 為 Player 的角色")] private PlayerEnergy playerEnergy;
    [SerializeField, Tooltip("這條顯示哪一種顏色的能量")] private BulletPolarity polarity = BulletPolarity.Blue;
    [SerializeField, Tooltip("每一格的填色 Image (Filled 類型)，由左到右")] private Image[] segmentFills = System.Array.Empty<Image>();
    [SerializeField, Tooltip("數字牌上的數字")] private Text numberLabel;

    // 數字只有 0~格數，預先做好字串，更新時不產生 GC
    private string[] numberTexts;
    private bool started;

    private void Awake()
    {
        if (playerEnergy == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) playerEnergy = player.GetComponentInParent<PlayerEnergy>();
        }

        numberTexts = new string[segmentFills.Length + 1];
        for (int i = 0; i < numberTexts.Length; i++) numberTexts[i] = i.ToString();
        RequiredReference.IsMissing(playerEnergy, nameof(playerEnergy), this);
    }

    private void OnEnable()
    {
        if (playerEnergy == null) return;

        playerEnergy.OnEnergyChanged += HandleEnergyChanged;
        if (started) Refresh();
    }

    // 初次顯示放在 Start：此時玩家能量的 Awake 必定已執行，不受物件初始化順序影響
    private void Start()
    {
        started = true;
        Refresh();
    }

    private void OnDisable()
    {
        if (playerEnergy != null) playerEnergy.OnEnergyChanged -= HandleEnergyChanged;
    }

    private void Refresh()
    {
        if (playerEnergy != null) Show(playerEnergy.GetEnergy(polarity), playerEnergy.MaxEnergy);
    }

    private void HandleEnergyChanged(BulletPolarity changed, float value, float max)
    {
        if (changed == polarity) Show(value, max);
    }

    private void Show(float value, float max)
    {
        for (int i = 0; i < segmentFills.Length; i++)
        {
            if (segmentFills[i] != null) segmentFills[i].fillAmount = EnergyGauge.SegmentFill(value, max, i, segmentFills.Length);
        }

        if (numberLabel != null) numberLabel.text = numberTexts[EnergyGauge.FullSegments(value, max, segmentFills.Length)];
    }
}
