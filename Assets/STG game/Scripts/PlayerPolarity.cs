using System;
using UnityEngine;
using UnityEngine.InputSystem;

// 玩家極性：按鍵在藍/黃之間切換，切換時顯示對應顏色的護盾；
// 與自身極性相同的子彈會被護盾吸收，不造成傷害
public class PlayerPolarity : MonoBehaviour, IPolarityAbsorber
{
    [Header("Polarity")]
    [SerializeField, Tooltip("開場時的極性")] private BulletPolarity startPolarity = BulletPolarity.Blue;
    [SerializeField, Tooltip("切換極性的按鍵")] private Key switchKey = Key.Space;
    [SerializeField, Tooltip("同極性的子彈命中時是否吸收 (不扣血)")] private bool absorbSamePolarity = true;

    [Header("Shield Visuals")]
    [SerializeField, Tooltip("藍色極性時顯示的護盾")] private GameObject blueShield;
    [SerializeField, Tooltip("黃色極性時顯示的護盾")] private GameObject yellowShield;

    public BulletPolarity CurrentPolarity { get; private set; }

    // 極性改變時觸發，供 UI、音效等外部系統訂閱
    public event Action<BulletPolarity> OnPolarityChanged;
    // 護盾成功吸收一次同屬性攻擊時觸發 (參數為被吸收攻擊的屬性)，供能量系統等訂閱
    public event Action<BulletPolarity> OnAttackAbsorbed;

    private void Awake()
    {
        SetPolarity(startPolarity);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard[switchKey].wasPressedThisFrame)
        {
            SetPolarity(CurrentPolarity == BulletPolarity.Blue ? BulletPolarity.Yellow : BulletPolarity.Blue);
        }
    }

    public void SetPolarity(BulletPolarity polarity)
    {
        CurrentPolarity = polarity;
        RefreshShields();
        OnPolarityChanged?.Invoke(polarity);
    }

    // 護盾暫時失效 (例如 Binah 光炮機制期間)：兩色護盾都不顯示、也不吸收任何攻擊；極性本身保留，恢復時回到原本的顏色
    public void SetShieldSuppressed(bool suppressed)
    {
        IsShieldSuppressed = suppressed;
        RefreshShields();
        OnShieldSuppressionChanged?.Invoke(suppressed);
    }

    // 護盾暫時失效 / 恢復時觸發 (true = 失效)，供護盾卡等 UI 訂閱
    public event Action<bool> OnShieldSuppressionChanged;

    public bool IsShieldSuppressed { get; private set; }

    private void RefreshShields()
    {
        if (blueShield != null) blueShield.SetActive(!IsShieldSuppressed && CurrentPolarity == BulletPolarity.Blue);
        if (yellowShield != null) yellowShield.SetActive(!IsShieldSuppressed && CurrentPolarity == BulletPolarity.Yellow);
    }

    public bool TryAbsorb(BulletPolarity bulletPolarity)
    {
        bool absorbed = absorbSamePolarity && !IsShieldSuppressed && bulletPolarity == CurrentPolarity;
        if (absorbed) OnAttackAbsorbed?.Invoke(bulletPolarity);
        return absorbed;
    }
}
