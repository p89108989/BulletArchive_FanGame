using UnityEngine;

[RequireComponent(typeof(Animator))]
public class HeadPoseCorrection : MonoBehaviour
{
    [Header("Head Adjustment")]
    [SerializeField, Tooltip("動畫播完後疊加在頭部骨骼上的額外旋轉 (骨骼本地空間)，用來把頭抬高，數值請在 Play 模式邊調邊看")]
    private Vector3 headRotationOffset = new Vector3(-15f, 0f, 0f);

    private Animator animator;
    private Transform headBone;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        headBone = animator.GetBoneTransform(HumanBodyBones.Head);
    }

    // 動畫 (Run/Idle) 評估完成後才校正頭部角度，讓兩個動作都套用同一個抬頭修正
    private void LateUpdate()
    {
        if (headBone == null) return;
        headBone.localRotation *= Quaternion.Euler(headRotationOffset);
    }
}
