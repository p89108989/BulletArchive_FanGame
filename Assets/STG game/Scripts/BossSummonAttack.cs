using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Boss 召喚攻擊：播放召喚動畫 (Exs03)，張嘴期間從嘴裡依序吐出膠囊，膠囊落地後在落點生成小怪。
// 場上還有上一波的小怪時不再召喚，避免小怪無限累積
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(BossAttackGate))]
public class BossSummonAttack : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("Boss 模型上的 Animator (需有 Summon 觸發參數)")] private Animator animator;
    [SerializeField, Tooltip("膠囊吐出的位置 (嘴巴的 fire_01 骨骼)")] private Transform mouth;
    [SerializeField, Tooltip("召喚膠囊 Prefab (需帶有 SummonCapsule 元件)")] private GameObject capsulePrefab;
    [SerializeField, Tooltip("落地後生成的小怪 Prefab")] private GameObject minionPrefab;
    [SerializeField, Tooltip("小怪生成時面向的目標，也是落點要避開的位置；留空時自動抓 Tag 為 Player 的角色")] private Transform target;

    [Header("Schedule")]
    [SerializeField, Min(0f), Tooltip("Boss 出場後第一次召喚的延遲 (秒)")] private float firstSummonDelay = 4f;
    [SerializeField, Min(1f), Tooltip("每隔幾秒檢查一次是否召喚 (場上還有小怪時跳過)")] private float summonInterval = 25f;

    [Header("Summon")]
    [SerializeField, Range(1, 6), Tooltip("每次召喚的小怪數量")] private int summonCount = 3;
    [SerializeField, Tooltip("動畫開始後第幾秒吐出每一顆膠囊 (Exs03 約 2.25 ~ 4.25 秒張嘴)；數量不足時沿用最後一個時間")]
    private float[] spitTimes = { 2.4f, 2.9f, 3.4f };
    [SerializeField, Tooltip("召喚動畫 (Exs03) 的長度 (秒)，播完才讓出攻擊權")] private float summonAnimationDuration = 5f;
    [SerializeField, Min(0.1f), Tooltip("膠囊飛行時間 (秒)")] private float capsuleFlightTime = 1.4f;
    [SerializeField, Tooltip("膠囊拋物線最高點比直線高多少 (m)")] private float capsuleArcHeight = 25f;
    [SerializeField, Tooltip("每隻小怪子彈的顏色，依召喚順序套用 (None = 紅色，護盾無法吸收)")]
    private BulletPolarity[] minionPolarities = { BulletPolarity.None, BulletPolarity.Yellow, BulletPolarity.Blue };
    [SerializeField, Tooltip("開始召喚的聲音")] private SoundEffect summonSound = new SoundEffect();

    [Header("Landing Impact")]
    [SerializeField, Tooltip("落點警示與落地傷害 (AreaStrike Prefab)，倒數時間 = 膠囊飛行時間；留空時不警示也不傷害")]
    private GameObject landingStrikePrefab;
    [SerializeField, Tooltip("落地傷害範圍半徑 (m)")] private float landingStrikeRadius = 9f;

    [Header("Landing")]
    [SerializeField, Tooltip("落點範圍的最小 X / Z (Boss 戰的玩家活動範圍內)")] private Vector2 landingAreaMin = new Vector2(-98f, 2f);
    [SerializeField, Tooltip("落點範圍的最大 X / Z")] private Vector2 landingAreaMax = new Vector2(-3f, 44f);
    [SerializeField, Tooltip("落點彼此至少相距 (m)")] private float minLandingSeparation = 25f;
    [SerializeField, Tooltip("落點與玩家至少相距 (m)")] private float avoidTargetRadius = 15f;
    [SerializeField, Tooltip("地板表面高度")] private float groundHeight = 0.31f;

    private static readonly int SummonHash = Animator.StringToHash("Summon");

    private readonly List<PeroroMinion> minions = new List<PeroroMinion>();
    private Vector3[] landingPoints;
    private EnemyHealth health;
    private BossAttackGate gate;
    private GameObjectPool strikePool;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        gate = GetComponent<BossAttackGate>();
        landingPoints = new Vector3[summonCount];
        if (landingStrikePrefab != null) strikePool = new GameObjectPool(landingStrikePrefab, null, summonCount);
        if (RequiredReference.IsMissing(animator, nameof(animator), this)) return;
        if (RequiredReference.IsMissing(mouth, nameof(mouth), this)) return;
        if (RequiredReference.IsMissing(capsulePrefab, nameof(capsulePrefab), this)) return;
        RequiredReference.IsMissing(minionPrefab, nameof(minionPrefab), this);
    }

    private void OnEnable()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) target = player.transform;
        }

        StartCoroutine(SummonLoop());
    }

    // 停用時若正在召喚要讓出攻擊權，否則其他攻擊會永遠等不到
    private void OnDisable()
    {
        StopAllCoroutines();
        if (gate != null) gate.Release(this);
    }

    // 輪到召喚時若 Boss 正在進行其他攻擊，等到空檔再召喚
    private IEnumerator SummonLoop()
    {
        yield return new WaitForSeconds(firstSummonDelay);

        WaitForSeconds interval = new WaitForSeconds(summonInterval);
        while (true)
        {
            if (!health.IsDead && CountAliveMinions() == 0)
            {
                while (!gate.TryAcquire(this)) yield return null;

                float startTime = Time.time;
                yield return SummonRoutine();
                float remaining = summonAnimationDuration - (Time.time - startTime);
                if (remaining > 0f) yield return new WaitForSeconds(remaining);
                gate.Release(this);
            }
            yield return interval;
        }
    }

    // 播動畫後依 spitTimes 逐顆吐出膠囊；落點在動畫開始時一次決定，彼此不重疊
    private IEnumerator SummonRoutine()
    {
        Vector3 avoid = target != null ? target.position : new Vector3(9999f, 0f, 9999f);
        int count = SummonPlacement.PickPoints(landingAreaMin, landingAreaMax, minLandingSeparation, avoid, avoidTargetRadius, groundHeight, landingPoints);

        animator.SetTrigger(SummonHash);
        summonSound.Play();
        float startTime = Time.time;
        for (int i = 0; i < count; i++)
        {
            float spitTime = spitTimes.Length > 0 ? spitTimes[Mathf.Min(i, spitTimes.Length - 1)] : 0f;
            while (Time.time - startTime < spitTime) yield return null;
            if (health.IsDead) yield break;

            SpitCapsule(landingPoints[i], i);
        }
    }

    // 吐出膠囊的同時在落點顯示紅色倒數圈，倒數與飛行時間相同，膠囊落地瞬間判定傷害
    private void SpitCapsule(Vector3 landingPoint, int index)
    {
        if (strikePool != null)
        {
            GameObject strikeObject = strikePool.Get(landingPoint + Vector3.up * 0.04f, Quaternion.identity);
            strikeObject.GetComponent<AreaStrike>().Begin(landingStrikeRadius, capsuleFlightTime, BulletPolarity.None, ReturnStrike);
        }

        GameObject capsuleObject = Instantiate(capsulePrefab, mouth.position, Quaternion.identity);
        SummonCapsule capsule = capsuleObject.GetComponent<SummonCapsule>();
        BulletPolarity polarity = minionPolarities.Length > 0 ? minionPolarities[index % minionPolarities.Length] : BulletPolarity.None;
        capsule.Launch(mouth.position, landingPoint, capsuleFlightTime, capsuleArcHeight, landed => SpawnMinion(landingPoint, polarity));
    }

    private void ReturnStrike(AreaStrike strike)
    {
        strikePool.Release(strike.gameObject);
    }

    // 小怪面向玩家生成，巡邏範圍為落點範圍的左右邊界，受到的傷害會轉給 Boss
    private void SpawnMinion(Vector3 position, BulletPolarity polarity)
    {
        if (minionPrefab == null) return;

        Vector3 facing = target != null ? target.position - position : Vector3.forward;
        facing.y = 0f;
        Quaternion rotation = facing.sqrMagnitude > 0.01f ? Quaternion.LookRotation(facing, Vector3.up) : Quaternion.identity;
        PeroroMinion minion = Instantiate(minionPrefab, position, rotation).GetComponent<PeroroMinion>();
        minion.Initialize(health, polarity, landingAreaMin.x, landingAreaMax.x);
        minions.Add(minion);
    }

    // 已倒下 (屍體停留中) 或已消失的小怪不算在場上
    private int CountAliveMinions()
    {
        int alive = 0;
        for (int i = minions.Count - 1; i >= 0; i--)
        {
            if (minions[i] == null)
            {
                minions.RemoveAt(i);
                continue;
            }
            if (!minions[i].IsDead) alive++;
        }
        return alive;
    }
}
