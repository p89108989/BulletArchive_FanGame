using System.Collections;
using UnityEngine;

// 依戰鬥階段把固定視角相機平滑移到對應的位置/角度：Boss 戰移到目前選定 Boss 的相機位置 (BossProfile)，回到一般戰鬥時回到原本的位置。
// 只改位置與俯仰，Boss 視角的水平朝向要與一般視角相同，玩家的 WASD 方向 (以相機水平朝向為準) 才不會改變
public class CameraPoseTransition : MonoBehaviour, ICombatPhaseListener
{
    [Header("Camera")]
    [SerializeField, Tooltip("要移動的相機，預設抓場景中的 Up Camera")] private Transform cameraTransform;
    [SerializeField, Tooltip("目前選定的 Boss 從這裡取得相機位置；留空或沒有選 Boss 時用下面的預設值")] private BossEncounter bossEncounter;

    [Header("Default Boss Pose")]
    [SerializeField, Tooltip("沒有 BossProfile 時，Boss 戰的相機位置")] private Vector3 bossPosition = new Vector3(-50f, 30f, 85f);
    [SerializeField, Tooltip("沒有 BossProfile 時，Boss 戰的相機旋轉 (尤拉角)")] private Vector3 bossRotation = new Vector3(20f, 180f, 0f);
    [SerializeField, Min(0f), Tooltip("切換視角花的時間 (秒)")] private float transitionDuration = 2f;

    private Vector3 normalPosition;
    private Quaternion normalRotation;

    private void Awake()
    {
        if (cameraTransform == null)
        {
            GameObject upCamera = GameObject.Find("Up Camera");
            if (upCamera != null) cameraTransform = upCamera.transform;
        }

        if (RequiredReference.IsMissing(cameraTransform, nameof(cameraTransform), this)) return;
        normalPosition = cameraTransform.position;
        normalRotation = cameraTransform.rotation;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    public void OnCombatPhaseChanged(CombatPhase phase)
    {
        if (!isActiveAndEnabled) return;

        StopAllCoroutines();
        if (phase != CombatPhase.Boss)
        {
            StartCoroutine(MoveTo(normalPosition, normalRotation));
            return;
        }

        BossProfile profile = bossEncounter != null ? bossEncounter.Profile : null;
        if (profile != null) StartCoroutine(MoveTo(profile.CameraPosition, profile.CameraRotation));
        else StartCoroutine(MoveTo(bossPosition, Quaternion.Euler(bossRotation)));
    }

    private IEnumerator MoveTo(Vector3 position, Quaternion rotation)
    {
        Vector3 fromPosition = cameraTransform.position;
        Quaternion fromRotation = cameraTransform.rotation;

        for (float elapsed = 0f; elapsed < transitionDuration; elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / transitionDuration);
            cameraTransform.SetPositionAndRotation(Vector3.Lerp(fromPosition, position, t), Quaternion.Slerp(fromRotation, rotation, t));
            yield return null;
        }

        cameraTransform.SetPositionAndRotation(position, rotation);
    }
}
