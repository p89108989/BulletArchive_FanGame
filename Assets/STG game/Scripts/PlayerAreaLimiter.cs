using UnityEngine;

// 把玩家限制在指定的水平範圍內 (Boss 視角看得到的區域)。要不要限制、範圍多大由 BossEncounter 依 BossProfile 開關：
// Perorozilla 的相機只看得到場地一部分要限制，Binah 看得到整個場地不限制；一般戰鬥不限制。
// 以修正剛體速度的方式限制，不直接改位置，避免穿模與抖動；在 PlayerMovement 設定完速度之後才執行
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Rigidbody))]
public class PlayerAreaLimiter : MonoBehaviour
{
    [SerializeField, Min(0f), Tooltip("開始限制時若已在範圍外，被推回範圍內的速度 (m/s)")] private float returnSpeed = 30f;

    private Rigidbody body;
    private Vector2 areaMin;
    private Vector2 areaMax;

    public bool IsRestricting { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    // 開始限制在 min ~ max (X / Z) 之間
    public void Restrict(Vector2 min, Vector2 max)
    {
        areaMin = Vector2.Min(min, max);
        areaMax = Vector2.Max(min, max);
        IsRestricting = true;
    }

    public void Release()
    {
        IsRestricting = false;
    }

    private void FixedUpdate()
    {
        if (!IsRestricting) return;

        Vector3 position = body.position;
        Vector3 velocity = body.linearVelocity;
        float dt = Time.fixedDeltaTime;
        velocity.x = LimitAxis(position.x, velocity.x, areaMin.x, areaMax.x, dt);
        velocity.z = LimitAxis(position.z, velocity.z, areaMin.y, areaMax.y, dt);
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
