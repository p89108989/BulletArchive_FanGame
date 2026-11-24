using System.Collections;
using UnityEngine;

// Boss 普通攻擊：播放攻擊動畫 (Start → Ing → End)，嘴巴張開時朝玩家當下的位置吐出一塊碎塊，
// 落點顯示紅色倒數圈 (無法以護盾吸收)，碎塊落地瞬間判定傷害
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(BossAttackGate))]
public class BossDebrisAttack : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("Boss 模型上的 Animator (需有 Attack 觸發參數)")] private Animator animator;
    [SerializeField, Tooltip("碎塊吐出的位置 (嘴巴的 fire_01 骨骼)")] private Transform mouth;
    [SerializeField, Tooltip("碎塊 Prefab，每次隨機挑一個 (需帶有 ArcProjectile 元件)")] private GameObject[] debrisPrefabs = System.Array.Empty<GameObject>();
    [SerializeField, Tooltip("落點警示與落地傷害 (AreaStrike Prefab，建議用只有煙塵的 AreaStrike_Dust)")] private GameObject strikePrefab;
    [SerializeField, Tooltip("攻擊目標，留空時自動抓 Tag 為 Player 的角色")] private Transform target;

    [Header("Schedule")]
    [SerializeField, Min(0f), Tooltip("Boss 出場後第一次普通攻擊的延遲 (秒)")] private float firstAttackDelay = 8f;
    [SerializeField, Min(0.5f), Tooltip("普通攻擊的間隔 (秒)；Boss 正在進行其他攻擊時這次跳過")] private float attackInterval = 5f;

    [Header("Timing")]
    [SerializeField, Tooltip("觸發攻擊後第幾秒吐出碎塊 (起手 1.1 秒 + Ing 張嘴約 0.45 秒)")] private float spitDelay = 1.55f;
    [SerializeField, Tooltip("整段攻擊動畫的長度 (秒)，結束後才讓出攻擊權")] private float attackDuration = 4f;
    [SerializeField, Min(0.1f), Tooltip("碎塊飛行時間 (秒)，也是警示圈的倒數時間")] private float flightTime = 1.3f;
    [SerializeField, Tooltip("碎塊拋物線最高點比直線高多少 (m)")] private float arcHeight = 20f;

    [Header("Impact")]
    [SerializeField, Tooltip("落地傷害範圍半徑 (m)")] private float strikeRadius = 8f;
    [SerializeField, Tooltip("地板表面高度")] private float groundHeight = 0.31f;

    private static readonly int AttackHash = Animator.StringToHash("Attack");

    private EnemyHealth health;
    private BossAttackGate gate;
    private GameObjectPool strikePool;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        gate = GetComponent<BossAttackGate>();
        if (RequiredReference.IsMissing(animator, nameof(animator), this)) return;
        if (RequiredReference.IsMissing(mouth, nameof(mouth), this)) return;
        if (RequiredReference.IsMissing(strikePrefab, nameof(strikePrefab), this)) return;
        if (debrisPrefabs.Length == 0)
        {
            Debug.LogError("[BossDebrisAttack] 必填欄位 debrisPrefabs 未指定", this);
            enabled = false;
            return;
        }

        strikePool = new GameObjectPool(strikePrefab, null, 2);
    }

    private void OnEnable()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) target = player.transform;
        }

        StartCoroutine(AttackLoop());
    }

    // 停用時若正在攻擊要讓出攻擊權，否則其他攻擊會永遠等不到
    private void OnDisable()
    {
        StopAllCoroutines();
        if (gate != null) gate.Release(this);
    }

    private IEnumerator AttackLoop()
    {
        yield return new WaitForSeconds(firstAttackDelay);

        WaitForSeconds interval = new WaitForSeconds(attackInterval);
        while (true)
        {
            if (!health.IsDead && target != null && gate.TryAcquire(this)) StartCoroutine(AttackRoutine());
            yield return interval;
        }
    }

    private IEnumerator AttackRoutine()
    {
        animator.SetTrigger(AttackHash);
        yield return new WaitForSeconds(spitDelay);

        if (!health.IsDead) SpitDebris();

        yield return new WaitForSeconds(Mathf.Max(0f, attackDuration - spitDelay));
        gate.Release(this);
    }

    // 落點取吐出當下的玩家位置；紅圈與碎塊同時出發，倒數結束時碎塊剛好落地
    private void SpitDebris()
    {
        Vector3 landingPoint = new Vector3(target.position.x, groundHeight, target.position.z);

        GameObject strikeObject = strikePool.Get(landingPoint + Vector3.up * 0.04f, Quaternion.identity);
        strikeObject.GetComponent<AreaStrike>().Begin(strikeRadius, flightTime, BulletPolarity.None, ReturnStrike);

        GameObject prefab = debrisPrefabs[Random.Range(0, debrisPrefabs.Length)];
        ArcProjectile debris = Instantiate(prefab, mouth.position, Quaternion.identity).GetComponent<ArcProjectile>();
        debris.Launch(mouth.position, landingPoint, flightTime, arcHeight, null);
    }

    private void ReturnStrike(AreaStrike strike)
    {
        strikePool.Release(strike.gameObject);
    }
}
