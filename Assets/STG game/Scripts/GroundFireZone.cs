using UnityEngine;

// 地面殘留的火焰 (Binah 長條光炮打過的地方)：長條範圍內從出現當下開始每隔 tickInterval 秒對站在上面的玩家造成傷害，duration 秒後熄滅移除。
// Prefab 平常是停用的，Begin 時才擺到長條的位置並啟用；visual 依長條大小縮放 (本地 X = 寬、Z = 長，大小 1 的特效)
public class GroundFireZone : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField, Min(0.1f), Tooltip("火焰持續多久 (秒)")] private float duration = 5f;
    [SerializeField, Min(0.05f), Tooltip("每隔幾秒扣一次血")] private float tickInterval = 1f;
    [SerializeField, Min(0), Tooltip("每次扣的血")] private int damage = 1;

    [Header("Visual")]
    [SerializeField, Tooltip("火焰外觀 (依長條的寬與長縮放)，可留空")] private Transform visual;

    private Vector3 start;
    private Vector3 direction;
    private float length;
    private float width;
    private PlayerHealth target;
    private float endTime;
    private float nextTick;
    private bool begun;

    public void Begin(Vector3 laneStart, Vector3 laneDirection, float laneLength, float laneWidth, PlayerHealth player)
    {
        Vector3 flat = new Vector3(laneDirection.x, 0f, laneDirection.z);
        if (flat.sqrMagnitude < 1e-6f) flat = Vector3.forward;
        flat.Normalize();

        start = laneStart;
        direction = flat;
        length = laneLength;
        width = laneWidth;
        target = player;
        transform.SetPositionAndRotation(laneStart + flat * (laneLength * 0.5f), Quaternion.LookRotation(flat, Vector3.up));
        if (visual != null) visual.localScale = new Vector3(laneWidth, 1f, laneLength);

        endTime = Time.time + duration;
        // 火焰一出現就判定一次 (之後每 tickInterval 秒)，否則站在光炮路徑上的玩家會有一段免費時間
        nextTick = Time.time;
        begun = true;
        gameObject.SetActive(true);
    }

    public bool Contains(Vector3 point)
    {
        return begun && LaneGeometry.Contains(start, direction, length, width, point);
    }

    private void Update()
    {
        if (!begun) return;

        if (Time.time >= nextTick)
        {
            nextTick += tickInterval;
            if (target != null && Contains(target.transform.position)) target.TakeDamage(damage);
        }

        if (Time.time >= endTime) Destroy(gameObject);
    }
}
