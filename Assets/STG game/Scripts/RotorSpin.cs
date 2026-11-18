using UnityEngine;

// 讓旋翼繞自身的某個軸持續旋轉；啟用時從靜止加速到全速，看起來像引擎發動
public class RotorSpin : MonoBehaviour
{
    [Header("Spin")]
    [SerializeField, Tooltip("旋轉軸 (自身座標)")] private Vector3 localAxis = Vector3.up;
    [SerializeField, Tooltip("全速時的轉速 (度/秒)，負值反轉")] private float degreesPerSecond = 1080f;
    [SerializeField, Tooltip("從靜止加速到全速所需時間 (秒)，0 = 一啟用就全速")] private float spinUpDuration = 1.5f;

    private float currentSpeed;

    private void OnEnable()
    {
        currentSpeed = spinUpDuration > 0f ? 0f : degreesPerSecond;
    }

    private void Update()
    {
        if (spinUpDuration > 0f)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, degreesPerSecond, Mathf.Abs(degreesPerSecond) / spinUpDuration * Time.deltaTime);
        }

        transform.Rotate(localAxis, currentSpeed * Time.deltaTime, Space.Self);
    }
}
