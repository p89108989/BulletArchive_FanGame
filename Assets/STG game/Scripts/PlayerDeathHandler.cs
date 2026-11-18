using UnityEngine;

// 玩家死亡時停用操作相關元件 (移動、射擊、切換極性) 並停下身體，避免死後還能行動
[RequireComponent(typeof(PlayerHealth))]
public class PlayerDeathHandler : MonoBehaviour
{
    [Header("Death")]
    [SerializeField, Tooltip("死亡時要停用的元件 (例如 PlayerMovement、PlayerLaser、PlayerPolarity)")] private Behaviour[] disableOnDeath;

    private PlayerHealth playerHealth;
    private Rigidbody body;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        TryGetComponent(out body);
    }

    private void OnEnable()
    {
        playerHealth.OnDied += HandleDied;
    }

    private void OnDisable()
    {
        playerHealth.OnDied -= HandleDied;
    }

    private void HandleDied()
    {
        for (int i = 0; i < disableOnDeath.Length; i++)
        {
            if (disableOnDeath[i] != null) disableOnDeath[i].enabled = false;
        }

        if (body != null && !body.isKinematic) body.linearVelocity = Vector3.zero;
    }
}
