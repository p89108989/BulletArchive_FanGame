using System;
using UnityEngine;

// 沿拋物線邊翻滾邊飛到落點的投擲物 (例如 Boss 吐出的碎塊)：落地時回報一次，之後縮小消失。
// 傷害與警示由投擲者另外處理 (例如在落點放 AreaStrike)
public class ArcProjectile : MonoBehaviour
{
    [Header("Flight")]
    [SerializeField, Tooltip("飛行時的翻滾速度 (度/秒)")] private float spinSpeed = 420f;

    [Header("Landing")]
    [SerializeField, Min(0.01f), Tooltip("落地後縮小消失的時間 (秒)")] private float fadeDuration = 0.6f;

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private Vector3 spinAxis;
    private Vector3 baseScale;
    private float flightDuration;
    private float arcPeak;
    private float elapsed;
    private bool isFlying;
    private bool hasLanded;
    private Action<ArcProjectile> landedCallback;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    // 從 from 以 flightTime 秒飛到 to，拋物線最高點比直線高 arcHeight；落地時呼叫 onLanded
    public void Launch(Vector3 from, Vector3 to, float flightTime, float arcHeight, Action<ArcProjectile> onLanded)
    {
        startPosition = from;
        targetPosition = to;
        flightDuration = Mathf.Max(0.01f, flightTime);
        arcPeak = arcHeight;
        landedCallback = onLanded;
        elapsed = 0f;
        isFlying = true;
        hasLanded = false;
        spinAxis = UnityEngine.Random.onUnitSphere;
        transform.SetPositionAndRotation(from, UnityEngine.Random.rotation);
        transform.localScale = baseScale;
    }

    private void Update()
    {
        if (isFlying) UpdateFlight();
        else if (hasLanded) UpdateFade();
    }

    private void UpdateFlight()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / flightDuration);
        transform.position = ArcPath.Evaluate(startPosition, targetPosition, arcPeak, t);
        transform.Rotate(spinAxis, spinSpeed * Time.deltaTime, Space.World);
        if (t < 1f) return;

        transform.position = targetPosition;
        isFlying = false;
        hasLanded = true;
        elapsed = 0f;
        landedCallback?.Invoke(this);
    }

    private void UpdateFade()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / fadeDuration);
        transform.localScale = baseScale * (1f - Mathf.SmoothStep(0f, 1f, t));
        if (t >= 1f) Destroy(gameObject);
    }
}
