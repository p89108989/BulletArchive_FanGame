using System.Collections.Generic;
using UnityEngine;

// 玩家的一般射擊 (純邏輯 + 外觀池，不含輸入)：每發子彈只是一小段往前飛的光束，沒有剛體或碰撞體，
// 每幀用 SphereCast 檢查這一步會不會打到敵人 (只查敵人 Layer，所以不會消除敵方子彈)。
// 外觀是預先生成的 LineRenderer 池，發射/命中只開關物件，不產生 GC
public class PlayerShotVolley
{
    private struct Shot
    {
        public bool active;
        public Vector3 head;
        public Vector3 direction;
        public float traveled;
        public float speed;
        public float range;
        public float length;
        public int damage;
        public LineRenderer line;
    }

    private const int HitBufferSize = 8;

    private readonly Shot[] shots;
    private readonly float hitRadius;
    private readonly LayerMask enemyLayers;
    private readonly RaycastHit[] hits = new RaycastHit[HitBufferSize];
    private readonly Dictionary<Collider, EnemyHealth> enemyCache = new Dictionary<Collider, EnemyHealth>();

    public int ActiveCount { get; private set; }

    // shotPrefab：帶 LineRenderer 的外觀 (可為 null，只算判定不顯示)
    public PlayerShotVolley(GameObject shotPrefab, int capacity, float hitRadius, LayerMask enemyLayers)
    {
        shots = new Shot[Mathf.Max(1, capacity)];
        this.hitRadius = hitRadius;
        this.enemyLayers = enemyLayers;
        if (shotPrefab == null) return;

        for (int i = 0; i < shots.Length; i++)
        {
            GameObject visual = Object.Instantiate(shotPrefab);
            visual.name = shotPrefab.name;
            shots[i].line = visual.GetComponent<LineRenderer>();
            if (shots[i].line != null) shots[i].line.positionCount = 2;
            visual.SetActive(false);
        }
    }

    // 發射一發；池滿時覆寫飛最遠 (最舊) 的那發，連射永遠不會卡住
    public void Fire(Vector3 start, Vector3 direction, float speed, float range, float length, int damage, Color color)
    {
        int index = FindSlot();
        if (!shots[index].active) ActiveCount++;

        shots[index].active = true;
        shots[index].head = start;
        shots[index].direction = direction.normalized;
        shots[index].traveled = 0f;
        shots[index].speed = speed;
        shots[index].range = range;
        shots[index].length = length;
        shots[index].damage = damage;

        LineRenderer line = shots[index].line;
        if (line == null) return;

        // 尾端淡、前端亮，看起來像往前竄的光
        line.startColor = new Color(color.r, color.g, color.b, 0f);
        line.endColor = color;
        UpdateLine(ref shots[index]);
        line.gameObject.SetActive(true);
    }

    public void Tick(float deltaTime)
    {
        for (int i = 0; i < shots.Length; i++)
        {
            if (!shots[i].active) continue;

            float step = Mathf.Min(shots[i].speed * deltaTime, shots[i].range - shots[i].traveled);
            if (TryHit(shots[i].head, shots[i].direction, step, out EnemyHealth enemy))
            {
                enemy.TakeDamage(shots[i].damage);
                Deactivate(ref shots[i]);
                continue;
            }

            shots[i].head += shots[i].direction * step;
            shots[i].traveled += step;
            if (shots[i].traveled >= shots[i].range)
            {
                Deactivate(ref shots[i]);
                continue;
            }

            UpdateLine(ref shots[i]);
        }
    }

    public void Clear()
    {
        for (int i = 0; i < shots.Length; i++)
        {
            if (shots[i].active) Deactivate(ref shots[i]);
        }
    }

    public void DestroyVisuals()
    {
        for (int i = 0; i < shots.Length; i++)
        {
            if (shots[i].line != null) Object.Destroy(shots[i].line.gameObject);
        }
    }

    private bool TryHit(Vector3 from, Vector3 direction, float distance, out EnemyHealth target)
    {
        target = null;
        if (distance <= 0f) return false;

        int count = Physics.SphereCastNonAlloc(from, hitRadius, direction, hits, distance, enemyLayers, QueryTriggerInteraction.Collide);
        float nearest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].distance >= nearest) continue;

            EnemyHealth enemy = GetEnemy(hits[i].collider);
            if (enemy == null || enemy.IsDead) continue;

            target = enemy;
            nearest = hits[i].distance;
        }

        return target != null;
    }

    // 敵人的血量元件可能在父物件上 (碰撞體掛在子物件)
    private EnemyHealth GetEnemy(Collider collider)
    {
        if (enemyCache.TryGetValue(collider, out EnemyHealth enemy)) return enemy;

        enemy = collider.GetComponentInParent<EnemyHealth>();
        enemyCache[collider] = enemy;
        return enemy;
    }

    private int FindSlot()
    {
        int oldest = 0;
        for (int i = 0; i < shots.Length; i++)
        {
            if (!shots[i].active) return i;
            if (shots[i].traveled > shots[oldest].traveled) oldest = i;
        }

        return oldest;
    }

    private void Deactivate(ref Shot shot)
    {
        shot.active = false;
        ActiveCount--;
        if (shot.line != null) shot.line.gameObject.SetActive(false);
    }

    // 剛射出時尾端不超過槍口，飛出去後維持固定長度
    private static void UpdateLine(ref Shot shot)
    {
        if (shot.line == null) return;

        float tail = Mathf.Min(shot.length, shot.traveled);
        shot.line.SetPosition(0, shot.head - shot.direction * tail);
        shot.line.SetPosition(1, shot.head);
    }
}
