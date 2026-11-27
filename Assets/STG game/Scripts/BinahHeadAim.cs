using UnityEngine;

// Binah 的程式頭部動作 (模型沒有光炮的動畫)：在原本動畫之上把頸部與頭部轉向地面目標、張開下顎。
// 在 LateUpdate 疊加在 Animator 的姿勢之後，權重平滑變化，開始/結束時不會瞬間跳動
public class BinahHeadAim : MonoBehaviour
{
    [Header("Bones")]
    [SerializeField, Tooltip("頸部骨骼 (Bone_Neck)")] private Transform neck;
    [SerializeField, Tooltip("頭部骨骼 (Bone_Head)")] private Transform head;
    [SerializeField, Tooltip("下顎骨骼 (Bone_L_JAW_01、Bone_R_JAW_01)")] private Transform[] jaws = System.Array.Empty<Transform>();
    [SerializeField, Tooltip("嘴的朝向參考 (Fire_01)，頭會轉到讓它對準目標")] private Transform muzzle;

    [Header("Aim")]
    [SerializeField, Range(0f, 1f), Tooltip("頸部分擔的轉動比例，其餘由頭部完成")] private float neckShare = 0.4f;
    [SerializeField, Min(0.1f), Tooltip("轉向/回正的速度 (每秒權重變化)")] private float blendSpeed = 1.5f;
    [SerializeField, Tooltip("下顎張開的角度 (度)；左右下顎各自一個，方向相反時用正負號區分")] private float[] jawOpenAngles = { 25f, -25f };
    [SerializeField, Tooltip("下顎轉動的本地軸 (Binah 的下顎骨骼本地 Y 軸 = 世界左右方向)")] private Vector3 jawLocalAxis = Vector3.up;

    private Vector3 target;
    private bool isAiming;
    private float weight;

    public void AimAt(Vector3 worldTarget)
    {
        target = worldTarget;
        isAiming = true;
    }

    public void StopAiming()
    {
        isAiming = false;
    }

    private void LateUpdate()
    {
        weight = Mathf.MoveTowards(weight, isAiming ? 1f : 0f, blendSpeed * Time.deltaTime);
        if (weight <= 0f || head == null || muzzle == null) return;

        // 目前嘴的方向 → 指向目標的方向，依比例分給頸部和頭部
        Vector3 current = muzzle.forward;
        Vector3 desired = (target - muzzle.position).normalized;
        Quaternion fullTurn = Quaternion.FromToRotation(current, desired);
        Quaternion weighted = Quaternion.Slerp(Quaternion.identity, fullTurn, weight);

        if (neck != null)
        {
            Quaternion neckTurn = Quaternion.Slerp(Quaternion.identity, weighted, neckShare);
            neck.rotation = neckTurn * neck.rotation;
            // 頭是頸的子物件，已經跟著轉了 neckTurn；剩下要轉的是 weighted × neckTurn⁻¹ (順序不能反)
            weighted = weighted * Quaternion.Inverse(neckTurn);
        }

        head.rotation = weighted * head.rotation;

        for (int i = 0; i < jaws.Length; i++)
        {
            if (jaws[i] == null || i >= jawOpenAngles.Length) continue;
            jaws[i].localRotation *= Quaternion.AngleAxis(jawOpenAngles[i] * weight, jawLocalAxis);
        }
    }
}
