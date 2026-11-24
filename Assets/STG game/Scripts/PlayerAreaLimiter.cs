using UnityEngine;

// Boss 戰期間把玩家限制在指定的水平範圍內 (Boss 視角看得到的區域)；一般戰鬥不限制。
// 以修正剛體速度的方式限制，不直接改位置，避免穿模與抖動；在 PlayerMovement 設定完速度之後才執行
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Rigidbody))]
public class PlayerAreaLimiter : MonoBehaviour, ICombatPhaseListener
{
    [Header("Boss Area (X/Z)")]
    [SerializeField, Tooltip("Boss 戰時可活動範圍的最小 X / Z")] private Vector2 bossAreaMin = new Vector2(-104f, -4f);
    [SerializeField, Tooltip("Boss 戰時可活動範圍的最大 X / Z")] private Vector2 bossAreaMax = new Vector2(3f, 50f);
    [SerializeField, Min(0f), Tooltip("進入 Boss 戰時若已在範圍外，被推回範圍內的速度 (m/s)")] private float returnSpeed = 30f;

    private Rigidbody body;
    private bool isLimited;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    public void OnCombatPhaseChanged(CombatPhase phase)
    {
        isLimited = phase == CombatPhase.Boss;
    }

    private void FixedUpdate()
    {
        if (!isLimited) return;

        Vector3 position = body.position;
        Vector3 velocity = body.linearVelocity;
        float dt = Time.fixedDeltaTime;
        velocity.x = LimitAxis(position.x, velocity.x, bossAreaMin.x, bossAreaMax.x, dt);
        velocity.z = LimitAxis(position.z, velocity.z, bossAreaMin.y, bossAreaMax.y, dt);
        body.linearVelocity = velocity;
    }

    // 範圍內：速度最多只能剛好走到邊界；範圍外：至少以 returnSpeed 往範圍內移動
    private float LimitAxis(float position, float velocity, float min, float max, float dt)
    {
        if (position > max) return Mathf.Min(velocity, -returnSpeed);
        if (position < min) return Mathf.Max(velocity, returnSpeed);
        return Mathf.Clamp(velocity, (min - position) / dt, (max - position) / dt);
    }
}
