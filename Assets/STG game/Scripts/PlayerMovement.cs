using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerHealth))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Range(0.5f, 20f), Tooltip("水平移動速度 (m/s)")] private float moveSpeed = 5f;
    [SerializeField, Tooltip("轉向面對移動方向的速度 (度/秒)")] private float rotationSpeed = 720f;
    [SerializeField, Tooltip("決定 WASD 前後左右方向的參考相機，預設抓場景中的 Up Camera")]
    private Transform directionReference;

    [Header("Ground Stick")]
    [SerializeField, Tooltip("腳底往下偵測地面的距離")] private float groundCheckDistance = 0.15f;
    [SerializeField, Tooltip("貼地時套用的向下速度，避免碰撞體邊緣造成的浮空飄移")] private float groundedStickSpeed = 3f;
    [SerializeField, Tooltip("視為地面的 Layer")] private LayerMask groundLayer = ~0;

    [Header("Dash")]
    [SerializeField, Tooltip("衝刺速度為移動速度的倍數")] private float dashSpeedMultiplier = 2.5f;
    [SerializeField, Tooltip("衝刺持續時間 (秒)")] private float dashDuration = 0.2f;
    [SerializeField, Min(0f), Tooltip("衝刺冷卻時間 (秒)，從衝刺開始時起算；避免連按衝刺一直維持無敵")] private float dashCooldown = 2f;
    [SerializeField, Tooltip("衝刺期間的無敵時間 (秒)；與受傷後的無敵時間 (PlayerHealth) 重疊時以較晚結束的為準，不會縮短它")] private float dashInvincibilityDuration = 0.2f;

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsDashingHash = Animator.StringToHash("IsDashing");

    private Rigidbody rb;
    private Animator animator;
    private CapsuleCollider capsule;
    private PlayerHealth playerHealth;
    private Vector3 moveDirection;
    private Vector3 forwardDir;
    private Vector3 rightDir;

    private bool isDashing;
    private float dashTimer;
    private float nextDashTime;
    private Vector3 dashDirection;

    private bool hasFacingOverride;
    private Vector3 facingOverride;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        capsule = GetComponent<CapsuleCollider>();
        playerHealth = GetComponent<PlayerHealth>();
        animator.applyRootMotion = false;

        if (directionReference == null)
        {
            GameObject upCamera = GameObject.Find("Up Camera");
            if (upCamera != null) directionReference = upCamera.transform;
        }

        CacheReferenceDirections();
    }

    // 依參考相機的水平朝向換算出前後左右方向，忽略相機的俯仰角
    private void CacheReferenceDirections()
    {
        if (directionReference == null)
        {
            forwardDir = Vector3.forward;
            rightDir = Vector3.right;
            return;
        }

        forwardDir = Vector3.ProjectOnPlane(directionReference.forward, Vector3.up).normalized;
        rightDir = Vector3.ProjectOnPlane(directionReference.right, Vector3.up).normalized;
    }

    // 每幀讀取 WASD 輸入，換算成以參考相機為基準的移動方向
    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            moveDirection = Vector3.zero;
            animator.SetBool(IsMovingHash, false);
            return;
        }

        float horizontal = 0f;
        float vertical = 0f;

        if (keyboard.wKey.isPressed) vertical += 1f;
        if (keyboard.sKey.isPressed) vertical -= 1f;
        if (keyboard.dKey.isPressed) horizontal += 1f;
        if (keyboard.aKey.isPressed) horizontal -= 1f;

        moveDirection = (forwardDir * vertical + rightDir * horizontal).normalized;
        animator.SetBool(IsMovingHash, moveDirection.sqrMagnitude > 0f);

        bool dashPressed = keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame;
        if (dashPressed) TryStartDash();

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)
            {
                isDashing = false;
                animator.SetBool(IsDashingHash, false);
            }
        }
    }

    // 嘗試開始衝刺；衝刺中或冷卻中回傳 false。方向固定為觸發當下角色面對的方向
    public bool TryStartDash()
    {
        if (isDashing || Time.time < nextDashTime) return false;

        nextDashTime = Time.time + dashCooldown;
        isDashing = true;
        dashTimer = dashDuration;
        dashDirection = transform.forward;
        animator.SetBool(IsDashingHash, true);
        playerHealth.GrantInvincibility(dashInvincibilityDuration);
        return true;
    }

    // 用物理引擎移動並轉向，讓角色維持在地板碰撞體上
    private void FixedUpdate()
    {
        Vector3 horizontalVelocity = isDashing
            ? dashDirection * (moveSpeed * dashSpeedMultiplier)
            : moveDirection * moveSpeed;
        float verticalVelocity = IsGrounded() ? -groundedStickSpeed : rb.linearVelocity.y;
        rb.linearVelocity = new Vector3(horizontalVelocity.x, verticalVelocity, horizontalVelocity.z);

        Vector3 facing = hasFacingOverride ? facingOverride : moveDirection;
        if (!isDashing && facing.sqrMagnitude > 0f)
        {
            // 用 rb.rotation 直接指定角度，避免 MoveRotation 的物理內插對 Halo 等偏離中心的子物件造成延遲飄移
            Quaternion targetRotation = Quaternion.LookRotation(facing, Vector3.up);
            rb.rotation = Quaternion.RotateTowards(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    // 由外部 (例如射擊時) 指定角色要面向的水平方向，優先於移動方向的轉向
    public void SetFacingOverride(Vector3 direction)
    {
        direction.y = 0f;
        hasFacingOverride = direction.sqrMagnitude > 0.0001f;
        facingOverride = direction.normalized;
    }

    public void ClearFacingOverride()
    {
        hasFacingOverride = false;
    }

    // 從腳底往下打一條射線偵測是否貼地，貼地時強制蓋掉物理碰撞造成的浮空速度
    private bool IsGrounded()
    {
        Vector3 origin = capsule.bounds.center;
        float castDistance = capsule.bounds.extents.y + groundCheckDistance;
        return Physics.Raycast(origin, Vector3.down, castDistance, groundLayer, QueryTriggerInteraction.Ignore);
    }
}
