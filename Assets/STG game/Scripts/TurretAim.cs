using UnityEngine;

// 讓砲台持續轉向目標：只做水平旋轉 (Yaw)，以固定角速度平滑轉過去，看起來像砲台在追蹤玩家
public class TurretAim : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("實際旋轉的部分 (砲台本體)，留空時旋轉自己")] private Transform pivot;
    [SerializeField, Tooltip("追蹤目標，留空時會自動抓場景中 Tag 為 Player 的角色")] private Transform target;

    [Header("Turning")]
    [SerializeField, Tooltip("轉向速度 (度/秒)")] private float turnSpeed = 120f;

    private void Awake()
    {
        if (pivot == null) pivot = transform;

        if (target == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) target = player.transform;
        }
    }

    private void Update()
    {
        if (target == null) return;

        Vector3 toTarget = target.position - pivot.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(toTarget, Vector3.up);
        pivot.rotation = Quaternion.RotateTowards(pivot.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }
}
