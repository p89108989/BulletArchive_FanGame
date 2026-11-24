using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Boss 召喚的小 Peroro：出場 → 在 Boss 前方左右來回巡邏 → 每隔一段時間停下、轉向玩家，從眼睛發射三發指定顏色的雷射子彈。
// 受到的傷害同步扣在 Boss 身上 (只算實際扣掉的血量)，被擊倒時 Boss 再額外受到小 Peroro 最大血量的傷害；
// 倒下後播放死亡動畫，在原地停留一段時間才消失
[RequireComponent(typeof(EnemyHealth))]
public class PeroroMinion : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("模型上的 Animator (參數 IsWalking / Attack / IsDead)，可留空")] private Animator animator;
    [SerializeField, Tooltip("子彈發射點 (雙眼)，依序輪流；留空時從身體中心上方發射")] private Transform[] muzzles = System.Array.Empty<Transform>();
    [SerializeField, Tooltip("雷射子彈 Prefab (需帶有 EnemyBullet 元件)")] private GameObject boltPrefab;
    [SerializeField, Tooltip("攻擊目標，留空時自動抓 Tag 為 Player 的角色")] private Transform target;

    [Header("Appear / Patrol")]
    [SerializeField, Min(0f), Tooltip("出場動畫長度 (秒)，播完才開始巡邏")] private float appearDuration = 1.07f;
    [SerializeField, Tooltip("巡邏移動速度 (m/s)")] private float patrolSpeed = 8f;
    [SerializeField, Tooltip("轉向速度 (度/秒)")] private float turnSpeed = 360f;

    [Header("Attack")]
    [SerializeField, Min(0.1f), Tooltip("每隔幾秒攻擊一次 (從上次攻擊結束起算)")] private float attackInterval = 5f;
    [SerializeField, Tooltip("攻擊起手動作長度 (秒)，之後才開始發射")] private float attackWindup = 0.9f;
    [SerializeField, Range(1, 6), Tooltip("每次攻擊發射的子彈數")] private int shotsPerAttack = 3;
    [SerializeField, Tooltip("每發子彈的間隔 (秒)")] private float shotInterval = 0.2f;
    [SerializeField, Tooltip("收招動作長度 (秒)，之後恢復巡邏")] private float attackRecovery = 0.73f;
    [SerializeField, Tooltip("子彈速度 (m/s)")] private float boltSpeed = 30f;
    [SerializeField, Tooltip("瞄準點在玩家身上往上偏移的高度")] private float aimHeight = 5.8f;
    [SerializeField, Tooltip("沒有指定發射點時，子彈離地的高度")] private float fallbackMuzzleHeight = 8f;

    [Header("Death")]
    [SerializeField, Min(0f), Tooltip("倒下後在原地停留幾秒才消失")] private float corpseDuration = 3f;

    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");

    private readonly List<GameObject> bolts = new List<GameObject>();
    private EnemyHealth health;
    private EnemyHealth boss;
    private GameObjectPool boltPool;
    private BulletPolarity polarity = BulletPolarity.None;
    private float patrolMinX;
    private float patrolMaxX;
    private float patrolDirection = 1f;
    private int lastHealth;
    private int nextMuzzleIndex;

    public bool IsDead => health != null && health.IsDead;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        lastHealth = health.CurrentHealth;
        if (RequiredReference.IsMissing(boltPrefab, nameof(boltPrefab), this)) return;
        boltPool = new GameObjectPool(boltPrefab, null, shotsPerAttack);
    }

    // 由召喚者在生成後呼叫：受傷時要同步扣血的 Boss、子彈顏色、巡邏的左右邊界
    public void Initialize(EnemyHealth bossHealth, BulletPolarity boltPolarity, float minX, float maxX)
    {
        if (boss != null) boss.OnDied -= HandleBossDied;
        boss = bossHealth;
        if (boss != null && isActiveAndEnabled) boss.OnDied += HandleBossDied;
        polarity = boltPolarity;
        patrolMinX = Mathf.Min(minX, maxX);
        patrolMaxX = Mathf.Max(minX, maxX);
        patrolDirection = Random.value < 0.5f ? -1f : 1f;
    }

    private void OnEnable()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) target = player.transform;
        }

        health.OnHealthChanged += HandleHealthChanged;
        health.OnDied += HandleDied;
        if (boss != null) boss.OnDied += HandleBossDied;
        if (!health.IsDead) StartCoroutine(BehaviourLoop());
    }

    private void OnDisable()
    {
        health.OnHealthChanged -= HandleHealthChanged;
        health.OnDied -= HandleDied;
        if (boss != null) boss.OnDied -= HandleBossDied;
        StopAllCoroutines();
    }

    // Boss 被擊倒時小怪跟著倒下 (Boss 已死亡，轉過去的傷害會被忽略)
    private void HandleBossDied()
    {
        if (!health.IsDead) health.TakeDamage(health.CurrentHealth);
    }

    // 消失時一併清掉還在飛或在池中的子彈，避免每波小怪都留下閒置物件
    private void OnDestroy()
    {
        for (int i = 0; i < bolts.Count; i++)
        {
            if (bolts[i] != null) Destroy(bolts[i]);
        }
    }

    private IEnumerator BehaviourLoop()
    {
        yield return new WaitForSeconds(appearDuration);

        while (true)
        {
            for (float elapsed = 0f; elapsed < attackInterval; elapsed += Time.deltaTime)
            {
                Patrol();
                yield return null;
            }

            yield return Attack();
        }
    }

    // 沿 X 軸在左右邊界之間來回走，碰到邊界就折返
    private void Patrol()
    {
        SetWalking(true);
        Vector3 position = transform.position;
        if (position.x >= patrolMaxX) patrolDirection = -1f;
        else if (position.x <= patrolMinX) patrolDirection = 1f;

        Vector3 step = new Vector3(patrolDirection * patrolSpeed * Time.deltaTime, 0f, 0f);
        transform.position = position + step;
        TurnTowards(step);
    }

    private IEnumerator Attack()
    {
        SetWalking(false);
        if (animator != null) animator.SetTrigger(AttackHash);

        for (float elapsed = 0f; elapsed < attackWindup; elapsed += Time.deltaTime)
        {
            if (target != null) TurnTowards(target.position - transform.position);
            yield return null;
        }

        WaitForSeconds shotWait = new WaitForSeconds(shotInterval);
        for (int i = 0; i < shotsPerAttack; i++)
        {
            FireBolt();
            if (i < shotsPerAttack - 1) yield return shotWait;
        }

        yield return new WaitForSeconds(attackRecovery);
    }

    private void FireBolt()
    {
        if (boltPool == null) return;

        Vector3 origin = NextMuzzlePosition();
        Vector3 aimPoint = target != null ? target.position + Vector3.up * aimHeight : origin + transform.forward;
        Vector3 direction = (aimPoint - origin).normalized;

        GameObject boltObject = boltPool.Get(origin, Quaternion.LookRotation(direction, Vector3.up));
        if (!bolts.Contains(boltObject)) bolts.Add(boltObject);
        boltObject.GetComponent<EnemyBullet>().Launch(direction, boltSpeed, polarity, ReturnBolt);
    }

    private Vector3 NextMuzzlePosition()
    {
        if (muzzles.Length == 0) return transform.position + Vector3.up * fallbackMuzzleHeight;

        Transform muzzle = muzzles[nextMuzzleIndex];
        nextMuzzleIndex = (nextMuzzleIndex + 1) % muzzles.Length;
        return muzzle != null ? muzzle.position : transform.position + Vector3.up * fallbackMuzzleHeight;
    }

    private void ReturnBolt(EnemyBullet bolt)
    {
        boltPool.Release(bolt.gameObject);
    }

    // 只把實際扣掉的血量轉給 Boss (溢出的傷害不算)
    private void HandleHealthChanged(int current, int max)
    {
        int lost = lastHealth - current;
        lastHealth = current;
        if (lost > 0 && boss != null) boss.TakeDamage(lost);
    }

    // 擊倒獎勵：Boss 額外受到小 Peroro 最大血量的傷害 (剩餘血量已在 HandleHealthChanged 同步扣過)
    private void HandleDied()
    {
        if (boss != null) boss.TakeDamage(health.MaxHealth);
        StopAllCoroutines();
        SetWalking(false);
        if (animator != null) animator.SetBool(IsDeadHash, true);
        Destroy(gameObject, corpseDuration);
    }

    private void TurnTowards(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction, Vector3.up), turnSpeed * Time.deltaTime);
    }

    private void SetWalking(bool walking)
    {
        if (animator != null) animator.SetBool(IsWalkingHash, walking);
    }
}
