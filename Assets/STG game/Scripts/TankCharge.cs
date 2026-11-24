using System.Collections;
using UnityEngine;

// 戰車衝撞攻擊 (每隔固定時間一輪)：
// 在場內挑一個落點並顯示落地警示 → 播放 Appear 動畫從高空墜落、落地揚起煙塵 (落點內的玩家受傷) →
// 鎖定玩家數秒 (車身轉向玩家，前方顯示長條形衝撞警示區) → 方向定住後直線衝刺，撞到玩家造成傷害 →
// 衝出場地邊界後往下墜落並隱藏，等下一輪。被擊毀時播放爆炸並提早結束這一輪。
// Boss 戰期間不再出場，若正在場上則揚起煙塵後撤離 (直接隱藏)
[RequireComponent(typeof(EnemyHealth))]
public class TankCharge : MonoBehaviour, ICombatPhaseListener
{
    [Header("References")]
    [SerializeField, Tooltip("戰車外觀根物件 (含碰撞體)，不在場上時隱藏")] private GameObject modelRoot;
    [SerializeField, Tooltip("模型上的 Animator (Appear 狀態 = 從空中掉落；參數 IsMoving = 履帶前進)，可留空")] private Animator animator;
    [SerializeField, Tooltip("長條形警示區 Prefab (需帶有 LaneWarning 元件)")] private LaneWarning warningPrefab;
    [SerializeField, Tooltip("落地時播放的煙塵粒子")] private ParticleSystem landingDust;
    [SerializeField, Tooltip("被擊毀或撞牆時生成的爆炸特效 Prefab，可留空")] private GameObject deathEffectPrefab;
    [SerializeField, Tooltip("爆炸特效縮放 (特效以半徑 10 的範圍設計)")] private float deathEffectScale = 1f;
    [SerializeField, Tooltip("爆炸特效存在多久後刪除 (秒)")] private float deathEffectLifetime = 4f;
    [SerializeField, Tooltip("鎖定目標，留空時會自動抓場景中 Tag 為 Player 的角色")] private Transform target;

    [Header("Schedule")]
    [SerializeField, Tooltip("開場後第一次出場的延遲 (秒)")] private float firstAppearDelay = 8f;
    [SerializeField, Tooltip("每次出場的間隔 (秒，從上一次出場開始算)")] private float appearInterval = 20f;
    [SerializeField, Tooltip("每輪從這些屬性中隨機挑一種；None = 紅色無屬性，護盾無法吸收")]
    private BulletPolarity[] randomPolarities = { BulletPolarity.None, BulletPolarity.Blue, BulletPolarity.Yellow };

    [Header("Arena")]
    [SerializeField, Tooltip("場地地板中心 (只取 X/Z)")] private Vector3 arenaCenter = new Vector3(-50.8f, 0f, 39.75f);
    [SerializeField, Tooltip("場地地板半寬 (X) 與半長 (Z)，衝出這個範圍就墜落")] private Vector2 arenaHalfExtents = new Vector2(58f, 48.6f);
    [SerializeField, Tooltip("地板表面高度")] private float groundHeight = 0.31f;
    [SerializeField, Tooltip("落點中心與場地邊緣保持的距離 (m)，約車身長度一半 = 可以落在邊緣")] private float dropEdgeMargin = 10f;
    [SerializeField, Tooltip("落點與玩家至少相距多遠 (m)，留出衝刺的距離；找不到時取最遠的候選點")] private float minDropDistanceFromTarget = 60f;
    [SerializeField, Tooltip("場地牆壁的 Layer，衝刺撞到就爆炸")] private LayerMask wallLayers;
    [SerializeField, Tooltip("落地時被壓毀的敵方單位 Layer (例如砲台)")] private LayerMask crushLayers;

    [Header("Body")]
    [SerializeField, Tooltip("車身寬度 (m)，用於落地範圍、衝撞警示區與命中判定")] private float bodyWidth = 11f;
    [SerializeField, Tooltip("車身長度 (m)")] private float bodyLength = 20f;
    [SerializeField, Tooltip("命中判定的高度 (m)")] private float bodyHeight = 8f;

    [Header("Drop")]
    [SerializeField, Tooltip("戰車出現前，落地警示先顯示多久 (秒)")] private float dropWarningDuration = 1.2f;
    [SerializeField, Tooltip("Appear 動畫從開始到觸地的時間 (秒)，動畫會在警示倒數結束前這麼久開始播，剛好在倒數結束時落地")] private float appearLandTime = 0.58f;
    [SerializeField, Tooltip("落地後到開始鎖定的停頓 (秒)，讓煙塵先揚起")] private float landRecoverDuration = 0.6f;

    [Header("Lock On")]
    [SerializeField, Tooltip("持續鎖定玩家的時間 (秒)")] private float lockDuration = 2f;
    [SerializeField, Tooltip("鎖定期間車身轉向玩家的速度 (度/秒)")] private float lockTurnSpeed = 240f;
    [SerializeField, Tooltip("鎖定結束後方向定住、到真正衝出去的停頓 (秒)，0 = 倒數結束立刻衝刺")] private float chargeWindup;

    [Header("Charge")]
    [SerializeField, Tooltip("衝刺最高速度 (m/s)")] private float chargeSpeed = 150f;
    [SerializeField, Tooltip("衝刺加速度 (m/s²)")] private float chargeAcceleration = 320f;
    [SerializeField, Tooltip("衝出邊界後往下墜落多久才隱藏 (秒)")] private float fallDuration = 1.5f;
    [SerializeField, Tooltip("墜落時往前翻倒的速度 (度/秒)")] private float fallTumbleSpeed = 60f;
    [SerializeField, Tooltip("墜落時的重力倍數 (遊戲尺度較大，正常重力看起來太飄)")] private float fallGravityScale = 4f;
    [SerializeField, Tooltip("墜落時水平速度每秒減少多少 (m/s²)，避免衝太遠才掉下去")] private float fallHorizontalDrag = 40f;

    [Header("Damage")]
    [SerializeField, Tooltip("落地或撞到玩家造成的傷害值")] private int damage = 1;
    [SerializeField, Tooltip("判定命中的目標 Tag")] private string hitTag = "Player";
    [SerializeField, Tooltip("判定時偵測的 Layer")] private LayerMask hitLayers = ~0;

    private static readonly int AppearStateHash = Animator.StringToHash("Appear");
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

    private readonly Collider[] overlapBuffer = new Collider[8];
    private readonly Collider[] crushBuffer = new Collider[16];
    private EnemyHealth health;
    private LaneWarning warning;
    private BulletPolarity polarity;
    // 每次落地/衝刺只對同一個目標判定一次
    private bool hasHitTarget;
    private bool isBossPhase;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        if (RequiredReference.IsMissing(modelRoot, nameof(modelRoot), this)) return;
        if (RequiredReference.IsMissing(warningPrefab, nameof(warningPrefab), this)) return;

        if (target == null)
        {
            GameObject player = GameObject.FindWithTag(hitTag);
            if (player != null) target = player.transform;
        }

        // 警示區不掛在戰車底下，否則會跟著車身移動與旋轉
        warning = Instantiate(warningPrefab);
        warning.Hide();
        modelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        health.OnDied += HandleDied;
        if (!isBossPhase) StartCoroutine(AppearLoop(firstAppearDelay));
    }

    private void OnDisable()
    {
        health.OnDied -= HandleDied;
        StopAllCoroutines();
        if (modelRoot != null) modelRoot.SetActive(false);
        if (warning != null) warning.Hide();
    }

    private void OnDestroy()
    {
        if (warning != null) Destroy(warning.gameObject);
    }

    private IEnumerator AppearLoop(float initialDelay)
    {
        yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            float runStart = Time.time;
            yield return ChargeRun();
            yield return new WaitForSeconds(Mathf.Max(0f, appearInterval - (Time.time - runStart)));
        }
    }

    private IEnumerator ChargeRun()
    {
        if (target == null) yield break;

        health.ResetHealth();
        polarity = PickRandomPolarity();

        yield return Drop(PickDropPoint());
        yield return new WaitForSeconds(landRecoverDuration);
        yield return LockOn();
        yield return Charge();

        modelRoot.SetActive(false);
        warning.Hide();
    }

    // 先顯示落地警示，倒數最後一段開始播 Appear 動畫 (模型自己從空中掉下來)，觸地瞬間揚起煙塵並判定落點傷害
    private IEnumerator Drop(Vector3 landPosition)
    {
        Vector3 toTarget = Flatten(target.position - landPosition);
        Quaternion facing = toTarget.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toTarget, Vector3.up) : Quaternion.identity;
        Vector3 forward = facing * Vector3.forward;
        warning.ShowCentered(landPosition, forward, bodyLength, bodyWidth, polarity);
        transform.SetPositionAndRotation(landPosition, facing);

        float total = Mathf.Max(dropWarningDuration, appearLandTime);
        float appearStart = total - appearLandTime;
        for (float elapsed = 0f; elapsed < total; elapsed += Time.deltaTime)
        {
            warning.SetProgress(elapsed / total);
            if (elapsed >= appearStart && !modelRoot.activeSelf) ShowAndPlayAppear();
            yield return null;
        }

        if (!modelRoot.activeSelf) ShowAndPlayAppear();
        warning.Hide();
        if (landingDust != null) landingDust.Play(true);

        hasHitTarget = false;
        TryHitTarget();
        CrushEnemiesUnderneath();
    }

    // 模型重新啟用時 Animator 會回到預設狀態，所以直接從頭播 Appear
    private void ShowAndPlayAppear()
    {
        modelRoot.SetActive(true);
        if (animator == null) return;
        animator.SetBool(IsMovingHash, false);
        animator.Play(AppearStateHash, 0, 0f);
    }

    private void SetMoving(bool moving)
    {
        if (animator != null && animator.isActiveAndEnabled) animator.SetBool(IsMovingHash, moving);
    }

    // 車身持續轉向玩家，前方的衝撞警示區跟著轉並隨倒數填滿；倒數結束後方向定住再停頓一下
    private IEnumerator LockOn()
    {
        warning.Show(transform.position, transform.forward, LaneLength(), bodyWidth, polarity);

        for (float elapsed = 0f; elapsed < lockDuration; elapsed += Time.deltaTime)
        {
            Vector3 toTarget = Flatten(target.position - transform.position);
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                Quaternion desired = Quaternion.LookRotation(toTarget, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, lockTurnSpeed * Time.deltaTime);
            }

            warning.Place(transform.position, transform.forward, LaneLength(), bodyWidth);
            warning.SetProgress(elapsed / lockDuration);
            yield return null;
        }

        warning.SetProgress(1f);
        if (chargeWindup > 0f) yield return new WaitForSeconds(chargeWindup);
    }

    // 直線加速衝刺，途中撞到玩家造成傷害；撞到牆直接爆炸，衝出場地邊界則保持慣性往下墜並往前翻倒
    private IEnumerator Charge()
    {
        hasHitTarget = false;
        float speed = 0f;
        Vector3 forward = transform.forward;
        SetMoving(true);

        while (IsInsideArena(transform.position))
        {
            speed = Mathf.Min(chargeSpeed, speed + chargeAcceleration * Time.deltaTime);
            float step = speed * Time.deltaTime;
            if (SweepWall(forward, step, out float wallDistance))
            {
                transform.position += forward * wallDistance;
                Explode();
                yield break;
            }

            transform.position += forward * step;
            TryHitTarget();
            yield return null;
        }

        warning.Hide();
        float fallSpeed = 0f;
        for (float elapsed = 0f; elapsed < fallDuration; elapsed += Time.deltaTime)
        {
            fallSpeed += Physics.gravity.y * fallGravityScale * Time.deltaTime;
            speed = Mathf.Max(0f, speed - fallHorizontalDrag * Time.deltaTime);
            transform.position += forward * (speed * Time.deltaTime) + Vector3.up * (fallSpeed * Time.deltaTime);
            transform.Rotate(fallTumbleSpeed * Time.deltaTime, 0f, 0f, Space.Self);
            yield return null;
        }
    }

    // 以車身大小的方盒判定玩家；同極性護盾會吸收這次攻擊
    private void TryHitTarget()
    {
        if (hasHitTarget) return;

        Vector3 center = transform.position + Vector3.up * (bodyHeight * 0.5f);
        Vector3 halfExtents = new Vector3(bodyWidth * 0.5f, bodyHeight * 0.5f, bodyLength * 0.5f);
        int count = Physics.OverlapBoxNonAlloc(center, halfExtents, overlapBuffer, transform.rotation, hitLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider hit = overlapBuffer[i];
            if (!hit.CompareTag(hitTag)) continue;
            if (!hit.TryGetComponent(out IDamageable damageable)) continue;

            hasHitTarget = true;
            if (hit.TryGetComponent(out IPolarityAbsorber absorber) && absorber.TryAbsorb(polarity)) return;

            damageable.TakeDamage(damage);
            return;
        }
    }

    // 高速衝刺一幀可能移動好幾公尺，用整個車身沿移動方向掃過，避免直接穿過薄牆
    private bool SweepWall(Vector3 forward, float distance, out float hitDistance)
    {
        hitDistance = 0f;
        if (wallLayers.value == 0) return false;

        // 方盒底部略高於地板，地板不在牆壁 Layer 也不會誤判
        Vector3 center = transform.position + Vector3.up * (bodyHeight * 0.5f);
        Vector3 halfExtents = new Vector3(bodyWidth * 0.5f, bodyHeight * 0.4f, bodyLength * 0.5f);
        if (!Physics.BoxCast(center, halfExtents, forward, out RaycastHit hit, transform.rotation, distance, wallLayers, QueryTriggerInteraction.Ignore)) return false;

        hitDistance = hit.distance;
        return true;
    }

    // 落地時壓在車身底下的敵方單位 (砲台等) 直接摧毀，由它們自己的死亡流程處理爆炸與重生
    private void CrushEnemiesUnderneath()
    {
        if (crushLayers.value == 0) return;

        Vector3 center = transform.position + Vector3.up * (bodyHeight * 0.5f);
        Vector3 halfExtents = new Vector3(bodyWidth * 0.5f, bodyHeight * 0.5f, bodyLength * 0.5f);
        int count = Physics.OverlapBoxNonAlloc(center, halfExtents, crushBuffer, transform.rotation, crushLayers, QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            EnemyHealth enemy = crushBuffer[i].GetComponentInParent<EnemyHealth>();
            if (enemy == null || enemy == health || enemy.IsDead) continue;
            enemy.TakeDamage(enemy.MaxHealth);
        }
    }

    // 撞牆：原地爆炸並隱藏車身，這一輪直接結束
    private void Explode()
    {
        SpawnExplosion();
        modelRoot.SetActive(false);
        warning.Hide();
    }

    // 被擊毀：播放爆炸、隱藏車身並結束這一輪，間隔後再出場
    private void HandleDied()
    {
        if (modelRoot.activeSelf) SpawnExplosion();

        StopAllCoroutines();
        modelRoot.SetActive(false);
        warning.Hide();
        if (!isBossPhase) StartCoroutine(AppearLoop(appearInterval));
    }

    // Boss 戰：取消這一輪 (在場上的話揚起煙塵後消失) 並停止出場；回到一般戰鬥後隔一個出場間隔再出場
    public void OnCombatPhaseChanged(CombatPhase phase)
    {
        isBossPhase = phase == CombatPhase.Boss;
        if (!isActiveAndEnabled) return;

        StopAllCoroutines();
        if (isBossPhase)
        {
            if (modelRoot.activeSelf && landingDust != null) landingDust.Play(true);
            modelRoot.SetActive(false);
            warning.Hide();
            return;
        }

        StartCoroutine(AppearLoop(appearInterval));
    }

    private void SpawnExplosion()
    {
        if (deathEffectPrefab == null) return;

        GameObject effect = Instantiate(deathEffectPrefab, transform.position + Vector3.up * (bodyHeight * 0.3f), Quaternion.identity);
        effect.transform.localScale = Vector3.one * deathEffectScale;
        Destroy(effect, deathEffectLifetime);
    }

    // 在場內 (含邊緣) 隨機挑落點，盡量離玩家夠遠，讓衝刺有距離可以反應
    private Vector3 PickDropPoint()
    {
        Vector2 range = new Vector2(
            Mathf.Max(0f, arenaHalfExtents.x - dropEdgeMargin),
            Mathf.Max(0f, arenaHalfExtents.y - dropEdgeMargin));
        Vector3 best = arenaCenter;
        float bestDistance = -1f;

        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector3 candidate = new Vector3(
                arenaCenter.x + Random.Range(-range.x, range.x),
                groundHeight,
                arenaCenter.z + Random.Range(-range.y, range.y));
            float distance = Flatten(candidate - target.position).magnitude;
            if (distance >= minDropDistanceFromTarget) return candidate;
            if (distance > bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        best.y = groundHeight;
        return best;
    }

    // 從車身位置沿目前方向到場地邊界的距離 (再多延伸一點，看得出會衝出場外)
    private float LaneLength()
    {
        Vector3 position = transform.position;
        Vector3 forward = transform.forward;
        float distanceX = DistanceToEdge(position.x - arenaCenter.x, forward.x, arenaHalfExtents.x);
        float distanceZ = DistanceToEdge(position.z - arenaCenter.z, forward.z, arenaHalfExtents.y);
        return Mathf.Min(distanceX, distanceZ) + bodyLength * 0.5f;
    }

    private static float DistanceToEdge(float offset, float direction, float halfExtent)
    {
        if (Mathf.Abs(direction) < 0.0001f) return float.MaxValue;
        float edge = direction > 0f ? halfExtent : -halfExtent;
        return Mathf.Max(0f, (edge - offset) / direction);
    }

    private bool IsInsideArena(Vector3 position)
    {
        return Mathf.Abs(position.x - arenaCenter.x) <= arenaHalfExtents.x
            && Mathf.Abs(position.z - arenaCenter.z) <= arenaHalfExtents.y;
    }

    private BulletPolarity PickRandomPolarity()
    {
        if (randomPolarities == null || randomPolarities.Length == 0) return BulletPolarity.None;
        return randomPolarities[Random.Range(0, randomPolarities.Length)];
    }

    private static Vector3 Flatten(Vector3 vector)
    {
        vector.y = 0f;
        return vector;
    }
}
