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
    [SerializeField, Tooltip("跟隨角色的視角下，A/D 原地轉向的速度 (度/秒)")] private float turnSpeed = 150f;

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

    private Vector3 pushDestination;
    private float pushEndTime;

    private bool hasFacingOverride;
    private Vector3 facingOverride;

    // 跟隨角色的視角 (相機掛在角色身上) 改用「W/S 沿面向前後、A/D 轉向」，否則角色轉向會帶著相機一起轉，方向鍵跟著改變
    private bool facingRelativeControls;
    private float turnInput;

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

        if (facingRelativeControls)
        {
            Vector3 facingForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            moveDirection = facingForward * vertical;
            turnInput = horizontal;
        }
        else
        {
            moveDirection = (forwardDir * vertical + rightDir * horizontal).normalized;
            turnInput = 0f;
        }
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

    // 衝刺開始時觸發一次 (角色語音等)
    public event System.Action OnDashStarted;

    public float DashCooldown => dashCooldown;
    // 衝刺還要等幾秒才能再用 (技能卡顯示冷卻用)
    public float DashCooldownRemaining => Mathf.Max(0f, nextDashTime - Time.time);

    // 嘗試開始衝刺；衝刺中或冷卻中回傳 false。
    // 方向在觸發當下決定並固定：有按方向鍵就往方向鍵的方向 (射擊時角色會被鎖定面向，不能用面向判斷)，沒按才往角色面向；
    // 角色同時轉向衝刺方向
    public bool TryStartDash()
    {
        if (isDashing || IsBeingPushed || Time.time < nextDashTime) return false;

        nextDashTime = Time.time + dashCooldown;
        isDashing = true;
        dashTimer = dashDuration;
        dashDirection = moveDirection.sqrMagnitude > 0f
            ? moveDirection.normalized
            : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        // 衝刺很短，直接轉向衝刺方向 (不用 RotateTowards 慢慢轉)；衝刺期間 FixedUpdate 不再轉向，衝完才回到射擊/移動的面向
        if (dashDirection.sqrMagnitude > 0f) rb.rotation = Quaternion.LookRotation(dashDirection, Vector3.up);
        animator.SetBool(IsDashingHash, true);
        playerHealth.GrantInvincibility(dashInvincibilityDuration);
        OnDashStarted?.Invoke();
        return true;
    }

    // 用物理引擎移動並轉向，讓角色維持在地板碰撞體上
    private void FixedUpdate()
    {
        if (IsBeingPushed)
        {
            // 每一步依「剩餘距離 / 剩餘時間」重新算速度：被地面摩擦或碰撞拖慢時會自己補回來，準時推到目的地
            Vector3 remaining = pushDestination - rb.position;
            remaining.y = 0f;
            Vector3 pushVelocity = remaining / Mathf.Max(pushEndTime - Time.time, Time.fixedDeltaTime);
            float pushVertical = IsGrounded() ? -groundedStickSpeed : rb.linearVelocity.y;
            rb.linearVelocity = new Vector3(pushVelocity.x, pushVertical, pushVelocity.z);
            return;
        }

        Vector3 horizontalVelocity = isDashing
            ? dashDirection * (moveSpeed * dashSpeedMultiplier)
            : moveDirection * moveSpeed;
        float verticalVelocity = IsGrounded() ? -groundedStickSpeed : rb.linearVelocity.y;
        rb.linearVelocity = new Vector3(horizontalVelocity.x, verticalVelocity, horizontalVelocity.z);

        if (isDashing) return;

        // 用 rb.rotation 直接指定角度，避免 MoveRotation 的物理內插對 Halo 等偏離中心的子物件造成延遲飄移
        if (!hasFacingOverride && facingRelativeControls)
        {
            rb.rotation *= Quaternion.Euler(0f, turnInput * turnSpeed * Time.fixedDeltaTime, 0f);
            return;
        }

        Vector3 facing = hasFacingOverride ? facingOverride : moveDirection;
        if (facing.sqrMagnitude > 0f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(facing, Vector3.up);
            rb.rotation = Quaternion.RotateTowards(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    // 是否正被外力推送 (推送期間不吃移動輸入、也不能衝刺)
    public bool IsBeingPushed => Time.time < pushEndTime;

    // 在 duration 秒內把角色等速推到 destination (只看水平位置)，例如 Binah 潑水把玩家沖到場地最下方
    public void PushTo(Vector3 destination, float duration)
    {
        pushDestination = destination;
        pushEndTime = Time.time + Mathf.Max(duration, Time.fixedDeltaTime);
        isDashing = false;
        animator.SetBool(IsDashingHash, false);
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

    // true = W/S 沿角色面向前後移動、A/D 原地轉向 (跟隨角色的視角)；false = WASD 以參考相機的方向移動
    public void SetFacingRelativeControls(bool enabled)
    {
        facingRelativeControls = enabled;
    }

    // 從腳底往下打一條射線偵測是否貼地，貼地時強制蓋掉物理碰撞造成的浮空速度
    private bool IsGrounded()
    {
        Vector3 origin = capsule.bounds.center;
        float castDistance = capsule.bounds.extents.y + groundCheckDistance;
        return Physics.Raycast(origin, Vector3.down, castDistance, groundLayer, QueryTriggerInteraction.Ignore);
    }
}
