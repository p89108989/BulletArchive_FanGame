using UnityEngine;
using UnityEngine.UI;

// 敵方 (Boss) 的畫面血條：平常隱藏，Bind 到某個 EnemyHealth 後顯示名稱與血量，目標死亡時隱藏。
// 本元件掛在常駐的物件上，只開關 panel，事件才能照 OnEnable/OnDisable 註冊與解除
public class EnemyHealthBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("整條血條的外框 (顯示/隱藏用)")] private GameObject panel;
    [SerializeField, Tooltip("血量條")] private Slider healthSlider;
    [SerializeField, Tooltip("血量數字文字，可留空")] private Text valueText;
    [SerializeField, Tooltip("名稱文字，可留空")] private Text nameText;

    private EnemyHealth health;

    private void Awake()
    {
        if (RequiredReference.IsMissing(panel, nameof(panel), this)) return;
        if (RequiredReference.IsMissing(healthSlider, nameof(healthSlider), this)) return;
        if (health == null) panel.SetActive(false);
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    // 開始顯示某個敵人的血量 (例如 Boss 出場時)；再次呼叫會換成新的目標
    public void Bind(EnemyHealth target, string displayName)
    {
        Unsubscribe();
        health = target;
        if (isActiveAndEnabled) Subscribe();

        if (nameText != null) nameText.text = displayName;
        if (health == null)
        {
            panel.SetActive(false);
            return;
        }

        panel.SetActive(!health.IsDead);
        HandleHealthChanged(health.CurrentHealth, health.MaxHealth);
    }

    private void Subscribe()
    {
        if (health == null) return;

        health.OnHealthChanged += HandleHealthChanged;
        health.OnDied += HandleDied;
    }

    private void Unsubscribe()
    {
        if (health == null) return;

        health.OnHealthChanged -= HandleHealthChanged;
        health.OnDied -= HandleDied;
    }

    private void HandleHealthChanged(int current, int max)
    {
        healthSlider.maxValue = max;
        healthSlider.value = current;
        if (valueText != null) valueText.text = current + " / " + max;
    }

    private void HandleDied()
    {
        panel.SetActive(false);
    }
}
