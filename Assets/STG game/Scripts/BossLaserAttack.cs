using System.Collections;
using UnityEngine;

// Boss 雷射攻擊：播放 Exs01，蓄力期間地面顯示紅圈警示，蓄力結束後雙眼各射出一道光束落在同一點，隨機兩種軌跡：
// 追蹤 — 蓄力期間紅圈緊貼玩家；雷射出現後落點是玩家「0.5 秒前的位置」，停下來就會被打到。
// M 型掃射 — 落點在玩家側與 Boss 側之間來回數趟 (像連續的 M)，由畫面一側推進到另一側 (隨機方向)；警示圈只標出掃射起點。
// 光束外觀雖是藍白色，判定屬於紅色攻擊：護盾擋不住，只能靠衝刺的無敵躲開
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(BossAttackGate))]
public class BossLaserAttack : MonoBehaviour
{
    private enum Trajectory
    {
        Tracking,
        Sweep,
    }

    [Header("References")]
    [SerializeField, Tooltip("Boss 模型上的 Animator (需有 Laser 觸發參數)")] private Animator animator;
    [SerializeField, Tooltip("光束起點 (雙眼的 FX_Eye_L_DM / FX_Eye_R_DM)，每個起點一道光束")] private Transform[] beamOrigins = System.Array.Empty<Transform>();
    [SerializeField, Tooltip("光束外觀 Prefab (需帶有 BossBeamVisual 元件)")] private BossBeamVisual beamPrefab;
    [SerializeField, Tooltip("地面紅圈警示 Prefab (需帶有 GroundWarningCircle 元件)，留空時不警示")] private GroundWarningCircle warningPrefab;
    [SerializeField, Tooltip("攻擊目標，留空時自動抓 Tag 為 Player 的角色")] private Transform target;

    [Header("Schedule")]
    [SerializeField, Min(0f), Tooltip("Boss 出場後第一次雷射的延遲 (秒)")] private float firstLaserDelay = 14f;
    [SerializeField, Min(1f), Tooltip("雷射的間隔 (秒，從上次雷射結束起算)；Boss 正在進行其他攻擊時會等到空檔")] private float laserInterval = 30f;

    [Header("Timing (Exs01)")]
    [SerializeField, Tooltip("觸發後第幾秒開始發射 (蓄力結束)；蓄力期間顯示警示")] private float beamStartTime = 3.5f;
    [SerializeField, Tooltip("光束展開的時間 (秒)")] private float expandDuration = 0.2f;
    [SerializeField, Tooltip("光束維持的時間 (秒)；M 型掃射在這段時間內掃完全程")] private float holdDuration = 10f;
    [SerializeField, Tooltip("光束消散的時間 (秒)")] private float fadeDuration = 0.5f;
    [SerializeField, Tooltip("整段動畫長度 (秒)，結束後才讓出攻擊權")] private float animationDuration = 16.5f;
    [SerializeField, Tooltip("地板表面高度")] private float groundHeight = 0.31f;

    [Header("Sound")]
    [SerializeField, Tooltip("開始蓄力的聲音")] private SoundEffect chargeSound = new SoundEffect();
    [SerializeField, Tooltip("光束發射的聲音")] private SoundEffect fireSound = new SoundEffect();

    [Header("Damage")]
    [SerializeField, Min(0.1f), Tooltip("光束落點的傷害半徑 (m)，警示圈也用這個大小")] private float hitRadius = 5f;
    [SerializeField, Min(1), Tooltip("每次命中的傷害；玩家受傷後的無敵時間決定多久會再被打一次")] private int damage = 2;
    [SerializeField, Range(0f, 1f), Tooltip("光束強度達到多少才有傷害 (展開初期與消散時不判定)")] private float damageMinIntensity = 0.5f;

    [Header("Trajectory")]
    [SerializeField, Range(0f, 1f), Tooltip("選到追蹤軌跡的機率，其餘為 M 型掃射")] private float trackingChance = 0.5f;
    [SerializeField, Min(0f), Tooltip("追蹤：落點是玩家幾秒前的位置")] private float trackingDelay = 0.5f;
    [SerializeField, Tooltip("M 型掃射：玩家活動區的最小角 (世界 X, Z)")] private Vector2 sweepAreaMin = new Vector2(-104f, -4f);
    [SerializeField, Tooltip("M 型掃射：玩家活動區的最大角 (世界 X, Z)")] private Vector2 sweepAreaMax = new Vector2(3f, 47f);
    [SerializeField, Min(1), Tooltip("M 型掃射：在近側 (玩家側) 與遠側 (Boss 側) 之間來回幾趟，越多越密集；遊戲中修改會套用到下一次雷射")] private int sweepPasses = 5;
    // 預設往內縮約一個傷害半徑：落點範圍仍蓋得到角落，警示圈也不會有一半在場外
    [SerializeField, Tooltip("M 型掃射：離活動區邊緣保留的比例 (x 左右、y 前後)")] private Vector2 sweepEdgeInset = new Vector2(0.05f, 0.1f);

    private static readonly int LaserHash = Animator.StringToHash("Laser");
    // 追蹤要回查幾秒前的位置；512 筆以 240fps 估算可回查 2 秒，仍留有餘裕
    private const int TrackingHistoryCapacity = 512;

    private readonly DelayedPositionTracker targetHistory = new DelayedPositionTracker(TrackingHistoryCapacity);
    private BossBeamVisual[] beams = System.Array.Empty<BossBeamVisual>();
    private Vector2[] sweepPoints = System.Array.Empty<Vector2>();
    private GroundWarningCircle warning;
    private IDamageable targetDamageable;
    private EnemyHealth health;
    private BossAttackGate gate;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        gate = GetComponent<BossAttackGate>();
        if (RequiredReference.IsMissing(animator, nameof(animator), this)) return;
        if (RequiredReference.IsMissing(beamPrefab, nameof(beamPrefab), this)) return;

        // 光束與警示不掛在 Boss 底下，直接用世界座標設定
        beams = new BossBeamVisual[beamOrigins.Length];
        for (int i = 0; i < beams.Length; i++)
        {
            beams[i] = Instantiate(beamPrefab);
            beams[i].Hide();
        }

        if (warningPrefab != null)
        {
            warning = Instantiate(warningPrefab);
            warning.Hide();
        }
    }

    private void OnEnable()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) target = player.transform;
        }

        targetDamageable = target != null ? target.GetComponent<IDamageable>() : null;
        StartCoroutine(LaserLoop());
    }

    // 停用時若正在攻擊要讓出攻擊權並收起光束與警示
    private void OnDisable()
    {
        StopAllCoroutines();
        HideEffects();
        if (gate != null) gate.Release(this);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < beams.Length; i++)
        {
            if (beams[i] != null) Destroy(beams[i].gameObject);
        }

        if (warning != null) Destroy(warning.gameObject);
    }

    private IEnumerator LaserLoop()
    {
        yield return new WaitForSeconds(firstLaserDelay);

        while (true)
        {
            while (health.IsDead || !gate.TryAcquire(this)) yield return null;

            yield return LaserRoutine();
            gate.Release(this);
            yield return new WaitForSeconds(laserInterval);
        }
    }

    private IEnumerator LaserRoutine()
    {
        Trajectory trajectory = Random.value < trackingChance ? Trajectory.Tracking : Trajectory.Sweep;
        bool reverseSweep = Random.value < 0.5f;
        // 每次攻擊開始時依目前的趟數重算鋸齒路徑：遊戲中 (Inspector、外部數值表) 改了趟數，下一次雷射就生效；
        // 掃射期間每幀只取點，不產生 GC。
        // 正規化座標：x 大 = 世界 +X = Boss 戰鏡頭 (朝 -Z) 的畫面左邊；y 小 = 世界 -Z = 靠 Boss 那側
        sweepPoints = SweepPath.Zigzag(sweepPasses, sweepEdgeInset);
        targetHistory.Clear();
        RecordTarget();
        animator.SetTrigger(LaserHash);
        chargeSound.Play();

        // 蓄力：顯示警示 (追蹤緊貼玩家目前的位置；掃射固定在起點)
        if (warning != null) warning.Show(WarningPoint(trajectory, reverseSweep), hitRadius);
        for (float elapsed = 0f; elapsed < beamStartTime && !health.IsDead; elapsed += Time.deltaTime)
        {
            RecordTarget();
            if (warning != null)
            {
                warning.MoveTo(WarningPoint(trajectory, reverseSweep));
                warning.SetProgress(elapsed / beamStartTime);
            }

            yield return null;
        }

        if (warning != null) warning.Hide();
        if (!health.IsDead) fireSound.Play();

        float beamDuration = expandDuration + holdDuration + fadeDuration;
        for (float elapsed = 0f; elapsed < beamDuration && !health.IsDead; elapsed += Time.deltaTime)
        {
            RecordTarget();
            float intensity = elapsed < expandDuration
                ? elapsed / expandDuration
                : Mathf.Clamp01((beamDuration - elapsed) / fadeDuration);
            float sweepProgress = Mathf.Clamp01((elapsed - expandDuration) / holdDuration);
            Vector3 aimPoint = AimPoint(trajectory, reverseSweep, sweepProgress);

            for (int i = 0; i < beams.Length; i++) beams[i].SetBeam(beamOrigins[i].position, aimPoint, intensity);
            if (intensity >= damageMinIntensity) BeamGroundHit.TryDamage(target, targetDamageable, aimPoint, hitRadius, damage);
            yield return null;
        }

        HideEffects();
        float remaining = animationDuration - beamStartTime - beamDuration;
        if (remaining > 0f) yield return new WaitForSeconds(remaining);
    }

    private void RecordTarget()
    {
        if (target != null) targetHistory.Record(Time.time, target.position);
    }

    // 蓄力期間的警示位置 (貼地)：追蹤緊貼目標目前的位置，掃射標出起點
    private Vector3 WarningPoint(Trajectory trajectory, bool reverseSweep)
    {
        if (trajectory == Trajectory.Tracking && target != null)
        {
            return new Vector3(target.position.x, groundHeight, target.position.z);
        }

        return AimPoint(trajectory, reverseSweep, 0f);
    }

    // 目前的落點 (貼地)：追蹤取目標延遲後的位置，掃射依進度取 M 字路徑上的點
    private Vector3 AimPoint(Trajectory trajectory, bool reverseSweep, float sweepProgress)
    {
        if (trajectory == Trajectory.Sweep)
        {
            return SweepPath.Evaluate(sweepPoints, sweepAreaMin, sweepAreaMax, sweepProgress, reverseSweep, groundHeight);
        }

        Vector3 delayed = targetHistory.Sample(Time.time - trackingDelay);
        return new Vector3(delayed.x, groundHeight, delayed.z);
    }

    private void HideEffects()
    {
        for (int i = 0; i < beams.Length; i++)
        {
            if (beams[i] != null) beams[i].Hide();
        }

        if (warning != null) warning.Hide();
    }
}
