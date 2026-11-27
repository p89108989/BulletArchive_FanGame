using UnityEngine;

// 鏡頭震動 (例如 Binah 光炮發射時)：在 LateUpdate 對鏡頭位置加上隨機偏移，震動幅度隨時間衰減，結束時準確回到原位。
// 其他系統 (視角轉場) 也會改鏡頭位置，所以每幀先確認位置是不是上一幀自己寫的：是 → 沿用記下的原位；
// 不是 → 代表被別人移動過，以新位置為原位
public class CameraShake : MonoBehaviour
{
    private Vector3 basePosition;
    private Vector3 lastWritten;
    private float amplitude;
    private float duration;
    private float endTime;
    private bool isShaking;

    // 開始震動；震動中再次呼叫時取較大的幅度、較晚的結束時間
    public void Shake(float shakeAmplitude, float shakeDuration)
    {
        if (shakeDuration <= 0f || shakeAmplitude <= 0f) return;

        float newEnd = Time.time + shakeDuration;
        if (!isShaking || newEnd > endTime)
        {
            endTime = newEnd;
            duration = shakeDuration;
        }

        if (!isShaking)
        {
            basePosition = transform.position;
            lastWritten = basePosition;
        }

        amplitude = isShaking ? Mathf.Max(amplitude, shakeAmplitude) : shakeAmplitude;
        isShaking = true;
    }

    private void LateUpdate()
    {
        if (!isShaking) return;

        // 沒被別人移動過就沿用記下的原位 (不用「目前位置 - 偏移」反推，避免浮點誤差累積)
        if (transform.position != lastWritten) basePosition = transform.position;
        float remaining = endTime - Time.time;
        if (remaining <= 0f)
        {
            transform.position = basePosition;
            isShaking = false;
            return;
        }

        transform.position = basePosition + Random.insideUnitSphere * (amplitude * Mathf.Clamp01(remaining / duration));
        lastWritten = transform.position;
    }
}
