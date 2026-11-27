using System;
using System.Collections;
using UnityEngine;

// Binah 換位：Move_Start 潛入地底 → 換到新位置 (潛在地底期間關掉受擊判定，打不到看不見的身體)
// → Move_End 竄出，探出地面時潑沙並觸發 OnEmerged (例如生成沙堆) → 等竄出動作播完。
// 例行換位攻擊與光炮機制「回到中間」共用；MoveTo 是協程，由呼叫端的協程 yield return 執行
public class BinahReposition : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField, Tooltip("Binah 的 Animator (需有 MoveStart / MoveEnd 觸發參數)，可留空")] private Animator animator;
    [SerializeField, Min(0f), Tooltip("Move_Start 從開始到整隻潛入地底的時間 (秒，含動畫播放速度)")] private float diveDuration = 3.6f;
    [SerializeField, Min(0f), Tooltip("Move_End 開始後多久探出地面 (秒)，此時潑沙")] private float emergeDelay = 0.5f;
    [SerializeField, Min(0f), Tooltip("探出地面後，竄出動作還要播多久才回到 Idle (秒)")] private float settleDuration = 3.2f;

    [Header("Sand Splash")]
    [SerializeField, Tooltip("竄出時的潑沙特效，可留空")] private GameObject splashPrefab;
    [SerializeField, Tooltip("潑沙特效相對 Binah 的位置 (本地座標：往前 = 往場地)")] private Vector3 splashOffset = new Vector3(0f, 5f, 25f);
    [SerializeField, Min(0.1f), Tooltip("潑沙特效的放大倍數")] private float splashScale = 2f;

    private static readonly int MoveStartHash = Animator.StringToHash("MoveStart");
    private static readonly int MoveEndHash = Animator.StringToHash("MoveEnd");

    // 探出地面的那一刻觸發 (此時已在新位置)
    public event Action OnEmerged;
    public bool IsMoving { get; private set; }

    private Collider[] colliders;

    private void Awake()
    {
        colliders = GetComponentsInChildren<Collider>(true);
    }

    // 被中斷 (例如物件停用) 時確保不會停在「打不到」的狀態
    private void OnDisable()
    {
        if (IsMoving) SetHittable(true);
        IsMoving = false;
    }

    public IEnumerator MoveTo(Vector3 position, Quaternion rotation)
    {
        IsMoving = true;
        if (animator != null) animator.SetTrigger(MoveStartHash);
        yield return new WaitForSeconds(diveDuration);

        SetHittable(false);
        transform.SetPositionAndRotation(position, rotation);
        if (animator != null) animator.SetTrigger(MoveEndHash);
        yield return new WaitForSeconds(emergeDelay);

        SetHittable(true);
        if (splashPrefab != null)
        {
            GameObject splash = Instantiate(splashPrefab, transform.TransformPoint(splashOffset), Quaternion.identity);
            splash.transform.localScale = Vector3.one * splashScale;
            splash.SetActive(true);
        }

        OnEmerged?.Invoke();
        yield return new WaitForSeconds(settleDuration);
        IsMoving = false;
    }

    private void SetHittable(bool hittable)
    {
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null) colliders[i].enabled = hittable;
        }
    }
}
