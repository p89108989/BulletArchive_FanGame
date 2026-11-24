using UnityEngine;

// 光束打在地面的命中判定：以水平距離判斷目標是否在落點半徑內。
// 屬於紅色攻擊，不經過護盾 (IPolarityAbsorber) 吸收；只有目標自身的無敵 (例如衝刺) 能躲掉
public static class BeamGroundHit
{
    // 目標在範圍內時造成傷害並回傳 true (是否真的扣血由目標的無敵狀態決定)
    public static bool TryDamage(Transform target, IDamageable damageable, Vector3 impactPoint, float radius, int damage)
    {
        if (target == null || damageable == null) return false;

        Vector3 offset = target.position - impactPoint;
        offset.y = 0f;
        if (offset.sqrMagnitude > radius * radius) return false;

        damageable.TakeDamage(damage);
        return true;
    }
}
