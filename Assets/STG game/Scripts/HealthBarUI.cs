using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("要顯示血量的玩家，留空時會自動抓場景中 Tag 為 Player 的角色")] private PlayerHealth playerHealth;
    [SerializeField, Tooltip("血量條")] private Slider healthSlider;
    [SerializeField, Tooltip("血量數字文字")] private Text healthText;

    private bool started;

    private void Awake()
    {
        if (RequiredReference.IsMissing(healthSlider, nameof(healthSlider), this)) return;
        if (RequiredReference.IsMissing(healthText, nameof(healthText), this)) return;
        if (playerHealth == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) playerHealth = player.GetComponent<PlayerHealth>();
        }
    }

    private void OnEnable()
    {
        if (playerHealth == null) return;

        playerHealth.OnHealthChanged += HandleHealthChanged;
        if (started) RefreshFromPlayer();
    }

    // 初次顯示放在 Start：此時同場景所有物件的 Awake 都已執行，玩家血量必定已初始化，不受物件初始化順序影響
    private void Start()
    {
        started = true;
        RefreshFromPlayer();
    }

    private void RefreshFromPlayer()
    {
        if (playerHealth != null) HandleHealthChanged(playerHealth.CurrentHealth, playerHealth.MaxHealth);
    }

    private void OnDisable()
    {
        if (playerHealth == null) return;

        playerHealth.OnHealthChanged -= HandleHealthChanged;
    }

    // 血量變化時更新血條與數字顯示
    private void HandleHealthChanged(int current, int max)
    {
        healthSlider.maxValue = max;
        healthSlider.value = current;
        healthText.text = current + " / " + max;
    }
}
