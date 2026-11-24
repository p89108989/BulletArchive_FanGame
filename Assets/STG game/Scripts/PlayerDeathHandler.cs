using UnityEngine;

// 玩家死亡時停用操作相關元件 (移動、射擊、切換極性)、隱藏光環與護盾並停下身體，避免死後還能行動
[RequireComponent(typeof(PlayerHealth))]
public class PlayerDeathHandler : MonoBehaviour
{
    [Header("Death")]
    [SerializeField, Tooltip("死亡時要停用的元件 (例如 PlayerMovement、PlayerLaser、PlayerPolarity)")] private Behaviour[] disableOnDeath;
    [SerializeField, Tooltip("死亡時要隱藏的物件 (例如光環、護盾)")] private GameObject[] hideOnDeath = System.Array.Empty<GameObject>();

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

        // 先停用 PlayerPolarity 再隱藏護盾，避免它之後又把護盾打開
        for (int i = 0; i < hideOnDeath.Length; i++)
        {
            if (hideOnDeath[i] != null) hideOnDeath[i].SetActive(false);
        }

        if (body != null && !body.isKinematic) body.linearVelocity = Vector3.zero;
    }
}
