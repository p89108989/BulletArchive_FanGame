using System.Collections;
using UnityEngine;

// Binah 的長條光炮 (取代原本預計的場中龍車)：每 interval 秒一次 (等其他攻擊讓出使用權才開始)。
// 從 Binah 腳下朝玩家當下的方向拉一條長條警示 (方向在警示開始時鎖定，警示從 Binah 這端漸漸塗滿)，
// warningDuration 秒後光炮從 Binah 腳下沿長條往遠處掃 (頭跟著抬起)：被掃到的玩家扣 damage 血 (只扣一次；紅色攻擊，只能靠衝刺無敵閃)，
// 打過的地面留下火焰 (GroundFireZone，持續期間站在上面會持續扣血)
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(BossAttackGate))]
public class BinahLaneBeamAttack : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("攻擊時把頭轉向玩家，可留空")] private BinahHeadAim headAim;
    [SerializeField, Tooltip("光炮發射點 (嘴裡的 Fire_01)，可留空")] private Transform muzzle;
    [SerializeField, Tooltip("瞄準目標，留空時自動抓 Tag 為 Player 的角色")] private Transform target;
    [SerializeField, Tooltip("受傷對象，留空時從目標身上找")] private PlayerHealth playerHealth;
    [SerializeField, Tooltip("長條警示 (依長條的寬與長縮放的根物件)，可留空")] private Transform laneWarning;
    [SerializeField, Tooltip("長條警示裡漸漸塗滿的部分，可留空")] private WarningFill laneWarningFill;
    [SerializeField, Tooltip("光炮外觀 (Y 軸為長度、高 2 單位的圓柱)，可留空")] private GameObject beamEffect;
    [SerializeField, Tooltip("地面殘留的火焰 Prefab，可留空")] private GroundFireZone fireZonePrefab;

    [Header("Schedule")]
    [SerializeField, Min(0f), Tooltip("Boss 出場後第一次長條光炮的延遲 (秒)")] private float firstDelay = 6f;
    [SerializeField, Min(0.1f), Tooltip("每次長條光炮的間隔 (秒，從上一次結束起算)")] private float interval = 12f;

    [Header("Lane")]
    [SerializeField, Min(0.1f), Tooltip("長條寬度 (m)")] private float laneWidth = 12f;
    [SerializeField, Min(1f), Tooltip("長條長度 (m)，要貫穿整個場地")] private float laneLength = 200f;
    [SerializeField, Tooltip("地板高度")] private float groundHeight = 0.35f;

    [Header("Timing")]
    [SerializeField, Min(0.05f), Tooltip("警示時間 (秒)")] private float warningDuration = 1f;
    [SerializeField, Min(0.05f), Tooltip("光炮從 Binah 腳下掃到長條另一端的時間 (秒)")] private float fireDuration = 0.8f;
    [SerializeField, Range(0f, 1f), Tooltip("掃射開始時光炮已經打到長條的多少比例 (從 Binah 腳下附近開始)")] private float sweepStart = 0.05f;

    [Header("Damage")]
    [SerializeField, Min(0), Tooltip("被光炮打中扣的血")] private int damage = 2;
    [SerializeField, Min(0.1f), Tooltip("光炮外觀的粗細 (m)")] private float beamWidth = 12f;

    public bool IsWarning { get; private set; }
    public bool IsAttacking { get; private set; }
    // 光炮正在掃射中
    public bool IsFiring { get; private set; }
    // 掃射進度 (0 = 剛從 Binah 腳下打出，1 = 掃到長條的另一端)
    public float SweepProgress { get; private set; }

    private EnemyHealth health;
    private BossAttackGate gate;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        gate = GetComponent<BossAttackGate>();
        HideVisuals();
    }

    private void OnEnable()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) target = player.transform;
        }

        if (playerHealth == null && target != null) playerHealth = target.GetComponentInParent<PlayerHealth>();
        StartCoroutine(AttackLoop());
    }

    // 停用時若正在攻擊要讓出攻擊權，否則其他攻擊會永遠等不到
    private void OnDisable()
    {
        StopAllCoroutines();
        HideVisuals();
        IsWarning = false;
        IsFiring = false;
        IsAttacking = false;
        if (headAim != null) headAim.StopAiming();
        if (gate != null) gate.Release(this);
    }

    private IEnumerator AttackLoop()
    {
        if (firstDelay > 0f) yield return new WaitForSeconds(firstDelay);

        while (true)
        {
            while (health.IsDead || target == null || !gate.TryAcquire(this)) yield return null;
            yield return Attack();
            yield return new WaitForSeconds(interval);
        }
    }

    private IEnumerator Attack()
    {
        IsAttacking = true;
        Vector3 start = new Vector3(transform.position.x, groundHeight, transform.position.z);
        Vector3 aimPoint = new Vector3(target.position.x, groundHeight, target.position.z);
        Vector3 direction = aimPoint - start;
        if (direction.sqrMagnitude < 1e-4f) direction = new Vector3(transform.forward.x, 0f, transform.forward.z);
        direction.Normalize();
        if (headAim != null) headAim.AimAt(aimPoint);

        // 警示：從 Binah 這端漸漸塗滿到另一端
        IsWarning = true;
        ShowWarning(start, direction);
        for (float elapsed = 0f; elapsed < warningDuration; elapsed += Time.deltaTime)
        {
            if (laneWarningFill != null) laneWarningFill.SetProgress(elapsed / warningDuration);
            yield return null;
        }

        IsWarning = false;
        if (laneWarning != null) laneWarning.gameObject.SetActive(false);

        // 光炮：落點從 Binah 腳下附近沿長條掃到另一端 (頭跟著落點轉，由低頭慢慢抬起)；
        // 掃過的部分內有玩家就扣一次血 (衝刺無敵、受傷無敵照常生效)
        if (beamEffect != null) beamEffect.SetActive(true);
        IsFiring = true;
        bool hit = false;
        for (float elapsed = 0f; elapsed < fireDuration; elapsed += Time.deltaTime)
        {
            SweepProgress = Mathf.Lerp(sweepStart, 1f, elapsed / fireDuration);
            float reach = laneLength * SweepProgress;
            Vector3 tip = start + direction * reach;
            PlaceBeam(tip);
            if (headAim != null) headAim.AimAt(tip);
            if (!hit && playerHealth != null && LaneGeometry.Contains(start, direction, reach, laneWidth, playerHealth.transform.position))
            {
                int before = playerHealth.CurrentHealth;
                playerHealth.TakeDamage(damage);
                hit = playerHealth.CurrentHealth < before;
            }

            yield return null;
        }

        // 最後一幀補檢查整條 (幀率低時最後一段可能沒掃到)
        if (!hit && playerHealth != null && LaneGeometry.Contains(start, direction, laneLength, laneWidth, playerHealth.transform.position)) playerHealth.TakeDamage(damage);
        SweepProgress = 1f;
        IsFiring = false;
        if (beamEffect != null) beamEffect.SetActive(false);
        if (fireZonePrefab != null) Instantiate(fireZonePrefab).Begin(start, direction, laneLength, laneWidth, playerHealth);
        if (headAim != null) headAim.StopAiming();

        IsAttacking = false;
        gate.Release(this);
    }

    // 長條警示：根物件放在起點、朝長條方向，依寬與長縮放 (子物件以大小 1 製作)
    private void ShowWarning(Vector3 start, Vector3 direction)
    {
        if (laneWarning == null) return;

        laneWarning.SetPositionAndRotation(start, Quaternion.LookRotation(direction, Vector3.up));
        laneWarning.localScale = new Vector3(laneWidth, 1f, laneLength);
        if (laneWarningFill != null) laneWarningFill.SetProgress(0f);
        laneWarning.gameObject.SetActive(true);
    }

    // 光炮外觀：從嘴 (沒有嘴就從起點上方) 拉一條圓柱到長條的另一端
    private void PlaceBeam(Vector3 end)
    {
        if (beamEffect == null) return;

        Vector3 from = muzzle != null ? muzzle.position : transform.position + Vector3.up * 20f;
        Vector3 span = end - from;
        beamEffect.transform.SetPositionAndRotation((from + end) * 0.5f, Quaternion.FromToRotation(Vector3.up, span));
        beamEffect.transform.localScale = new Vector3(beamWidth, span.magnitude * 0.5f, beamWidth);
    }

    private void HideVisuals()
    {
        if (laneWarning != null) laneWarning.gameObject.SetActive(false);
        if (beamEffect != null) beamEffect.SetActive(false);
    }
}
