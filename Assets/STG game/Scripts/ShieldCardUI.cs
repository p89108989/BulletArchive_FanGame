using UnityEngine;
using UnityEngine.UI;

// HUD 右下的護盾卡：卡底顏色顯示玩家目前的護盾極性 (藍 / 黃)；護盾暫時失效 (Binah 光炮機制) 時變灰。
// 極性與失效狀態只在切換時改變，所以訂閱 PlayerPolarity 的事件更新，不需要每幀檢查
public class ShieldCardUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("護盾極性來源，留空時會自動抓場景中 Tag 為 Player 的角色")] private PlayerPolarity playerPolarity;
    [SerializeField, Tooltip("跟著極性變色的卡底")] private Image background;

    [Header("Colors")]
    [SerializeField, Tooltip("藍色護盾時的卡底顏色")] private Color blueColor = new Color(0f, 0.45f, 0.85f, 1f);
    [SerializeField, Tooltip("黃色護盾時的卡底顏色")] private Color yellowColor = new Color(0.9f, 0.6f, 0.05f, 1f);
    [SerializeField, Tooltip("護盾暫時失效時的卡底顏色")] private Color suppressedColor = new Color(0.35f, 0.35f, 0.38f, 1f);

    private void Awake()
    {
        if (playerPolarity == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) playerPolarity = player.GetComponentInParent<PlayerPolarity>();
        }

        RequiredReference.IsMissing(background, nameof(background), this);
    }

    private void OnEnable()
    {
        if (playerPolarity == null) return;

        playerPolarity.OnPolarityChanged += HandlePolarityChanged;
        playerPolarity.OnShieldSuppressionChanged += HandleSuppressionChanged;
        // 重新啟用時可能錯過了切換，先同步一次
        Refresh();
    }

    private void OnDisable()
    {
        if (playerPolarity == null) return;

        playerPolarity.OnPolarityChanged -= HandlePolarityChanged;
        playerPolarity.OnShieldSuppressionChanged -= HandleSuppressionChanged;
    }

    private void HandlePolarityChanged(BulletPolarity polarity)
    {
        Refresh();
    }

    private void HandleSuppressionChanged(bool suppressed)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (background == null || playerPolarity == null) return;

        if (playerPolarity.IsShieldSuppressed) background.color = suppressedColor;
        else background.color = playerPolarity.CurrentPolarity == BulletPolarity.Yellow ? yellowColor : blueColor;
    }
}
