using System.Collections.Generic;
using UnityEngine;

// 雷射光束的命中判定 (純邏輯，不含外觀與輸入)：
// 敵人與子彈分兩次查詢、各用獨立緩衝區，子彈再多也不會把敵人從結果中擠掉；
// 碰撞體 → 元件的查詢結果會快取，避免每幀對每個命中呼叫 GetComponent
public class LaserBeamTracer
{
    private const int EnemyBufferSize = 16;
    private const int BulletBufferSize = 64;

    private readonly float hitRadius;
    private readonly float maxRange;
    private readonly LayerMask enemyLayers;
    private readonly LayerMask bulletLayers;
    private readonly RaycastHit[] enemyHits = new RaycastHit[EnemyBufferSize];
    private readonly RaycastHit[] bulletHits = new RaycastHit[BulletBufferSize];
    private readonly Dictionary<Collider, EnemyHealth> enemyCache = new Dictionary<Collider, EnemyHealth>();
    private readonly Dictionary<Collider, EnemyBullet> bulletCache = new Dictionary<Collider, EnemyBullet>();

    public LaserBeamTracer(float hitRadius, float maxRange, LayerMask enemyLayers, LayerMask bulletLayers)
    {
        this.hitRadius = hitRadius;
        this.maxRange = maxRange;
        this.enemyLayers = enemyLayers;
        this.bulletLayers = bulletLayers;
    }

    // 回傳光束長度：停在最近的存活敵人身上 (沒打到則為最大射程)；途中與光束異色的子彈一律攔截
    public float Trace(Vector3 start, Vector3 direction, BulletPolarity beamPolarity, out EnemyHealth target)
    {
        float length = FindNearestEnemy(start, direction, out target);
        InterceptBullets(start, direction, length, beamPolarity);
        return length;
    }

    private float FindNearestEnemy(Vector3 start, Vector3 direction, out EnemyHealth target)
    {
        int count = Physics.SphereCastNonAlloc(start, hitRadius, direction, enemyHits, maxRange, enemyLayers, QueryTriggerInteraction.Collide);

        target = null;
        float length = maxRange;
        for (int i = 0; i < count; i++)
        {
            if (enemyHits[i].distance >= length) continue;

            EnemyHealth enemy = GetCached(enemyCache, enemyHits[i].collider, true);
            if (enemy == null || enemy.IsDead) continue;

            target = enemy;
            length = enemyHits[i].distance;
        }
        return length;
    }

    private void InterceptBullets(Vector3 start, Vector3 direction, float length, BulletPolarity beamPolarity)
    {
        int count = Physics.SphereCastNonAlloc(start, hitRadius, direction, bulletHits, length, bulletLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            EnemyBullet bullet = GetCached(bulletCache, bulletHits[i].collider, false);
            if (bullet != null && bullet.Polarity != beamPolarity) bullet.Intercept();
        }
    }

    // 敵人的血量元件可能在父物件上 (碰撞體掛在子物件)，子彈則與碰撞體同物件
    private static T GetCached<T>(Dictionary<Collider, T> cache, Collider collider, bool searchParents) where T : Component
    {
        if (cache.TryGetValue(collider, out T component)) return component;

        component = searchParents ? collider.GetComponentInParent<T>() : collider.GetComponent<T>();
        cache[collider] = component;
        return component;
    }
}
