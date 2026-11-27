using System;
using System.Collections;
using UnityEngine;

// Binah 的例行攻擊「換位潑沙」：每 interval 秒隨機換到另一個位置 (Move_Start 潛入 → Move_End 竄出)，
// 竄出時潑沙，並在 Binah 前方 duneDistance 處堆一個沙堆 (場上最多 3 個，滿了不堆)。
// 要拿到攻擊使用權才換位 (換位期間其他攻擊暫停)；光炮特殊機制進行中不計時也不換位
// (機制開始時正在換位就中斷，交給機制自己的「回到中間」)；Boss 死亡後停止
[RequireComponent(typeof(EnemyHealth))]
public class BinahSplashAttack : MonoBehaviour
{
    [Serializable]
    public struct Spot
    {
        [Tooltip("Binah 根物件的位置")] public Vector3 position;
        [Tooltip("面向 (Y 軸角度)，要朝向場地")] public float yaw;
    }

    [Header("References")]
    [SerializeField, Tooltip("潛入/竄出 (含潑沙)；留空則直接瞬移")] private BinahReposition reposition;
    [SerializeField, Tooltip("光炮特殊機制，進行中暫停換位，可留空")] private BinahCannonMechanic mechanic;
    [SerializeField, Tooltip("場上的沙堆 (與光炮機制共用)，可留空")] private BinahDuneField duneField;
    [SerializeField, Tooltip("攻擊使用權：拿到才換位、換位期間佔住；留空時自動抓同物件上的")] private BossAttackGate attackGate;

    [Header("Timing")]
    [SerializeField, Min(0.1f), Tooltip("每隔幾秒換位一次")] private float interval = 30f;

    [Header("Spots")]
    [SerializeField, Tooltip("可以換到的位置 (場地外圍三面)；每次隨機換到目前以外的位置")] private Spot[] spots = Array.Empty<Spot>();

    [Header("Dune")]
    [SerializeField, Min(0f), Tooltip("沙堆堆在 Binah 前方多遠 (m)")] private float duneDistance = 35f;
    [SerializeField, Min(0f), Tooltip("沙堆位置的隨機範圍 (m)")] private float duneRadius = 10f;

    private EnemyHealth health;
    private float timer;
    private Coroutine routine;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        if (attackGate == null) attackGate = GetComponent<BossAttackGate>();
    }

    private void OnEnable()
    {
        health.OnDied += StopRelocating;
        if (mechanic != null) mechanic.OnMechanicStarted += StopRelocating;
    }

    private void OnDisable()
    {
        health.OnDied -= StopRelocating;
        if (mechanic != null) mechanic.OnMechanicStarted -= StopRelocating;
        StopRelocating();
    }

    private void Update()
    {
        if (health.IsDead || routine != null || spots.Length < 2) return;
        if (mechanic != null && mechanic.IsRunning) return;

        timer += Time.deltaTime;
        if (timer < interval) return;

        // 其他攻擊進行中就下一幀再試 (計時停在時間到的狀態)
        if (attackGate != null && !attackGate.TryAcquire(this)) return;

        timer = 0f;
        routine = StartCoroutine(Relocate(spots[PickNextSpot()]));
    }

    private IEnumerator Relocate(Spot spot)
    {
        Quaternion rotation = Quaternion.Euler(0f, spot.yaw, 0f);
        if (reposition != null)
        {
            reposition.OnEmerged += PileDune;
            yield return reposition.MoveTo(spot.position, rotation);
            reposition.OnEmerged -= PileDune;
        }
        else
        {
            transform.SetPositionAndRotation(spot.position, rotation);
            PileDune();
        }

        routine = null;
        if (attackGate != null) attackGate.Release(this);
    }

    // 潑出的沙落在 Binah 前方
    private void PileDune()
    {
        if (duneField == null) return;

        duneField.TrySpawnNear(transform.position + transform.forward * duneDistance, duneRadius);
    }

    // 隨機挑一個「不是目前所在」的位置 (目前所在 = 離 Binah 最近的那個)
    private int PickNextSpot()
    {
        int current = 0;
        float closest = float.MaxValue;
        for (int i = 0; i < spots.Length; i++)
        {
            float distance = (spots[i].position - transform.position).sqrMagnitude;
            if (distance >= closest) continue;
            closest = distance;
            current = i;
        }

        int next = UnityEngine.Random.Range(0, spots.Length - 1);
        return next >= current ? next + 1 : next;
    }

    private void StopRelocating()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        if (reposition != null) reposition.OnEmerged -= PileDune;
        if (attackGate != null) attackGate.Release(this);
        timer = 0f;
    }
}
