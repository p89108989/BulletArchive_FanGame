using System;
using UnityEngine;

// Boss 吐出的召喚膠囊 (扭蛋)：沿拋物線邊轉邊飛到落點，落地時回報一次 (由召喚者在落點生成小怪)，
// 接著上半殼彈開、整顆縮小消失
public class SummonCapsule : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField, Tooltip("上半殼，落地後彈開")] private Transform topShell;
    [SerializeField, Tooltip("上半殼的 Renderer，每顆隨機套一種顏色")] private Renderer topRenderer;
    [SerializeField, Tooltip("上半殼的隨機顏色")]
    private Color[] topColors =
    {
        new Color(0.95f, 0.25f, 0.25f), new Color(0.25f, 0.5f, 1f), new Color(1f, 0.75f, 0.15f), new Color(1f, 0.92f, 0.3f),
    };

    [Header("Flight")]
    [SerializeField, Tooltip("飛行時的翻滾速度 (度/秒)")] private float spinSpeed = 540f;
    [SerializeField, Min(0f), Tooltip("落地時球心離地面的高度 (= 球的半徑，世界單位)；原點在球心，不墊高會有一半埋進地板")] private float landingHeight;

    [Header("Open")]
    [SerializeField, Min(0.01f), Tooltip("落地後到完全消失的時間 (秒)")] private float openDuration = 0.8f;
    [SerializeField, Tooltip("上半殼彈開的高度 (本地座標)")] private float shellLift = 0.6f;
    [SerializeField, Tooltip("上半殼彈開時的翻轉角度")] private float shellTilt = 70f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private MaterialPropertyBlock propertyBlock;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private Vector3 shellRestPosition;
    private Quaternion shellRestRotation;
    private Vector3 baseScale;
    private float flightDuration;
    private float arcPeak;
    private float elapsed;
    private bool isFlying;
    private bool hasLanded;
    private Action<SummonCapsule> landedCallback;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        baseScale = transform.localScale;
        if (topShell != null)
        {
            shellRestPosition = topShell.localPosition;
            shellRestRotation = topShell.localRotation;
        }
    }

    // 從 from 以 flightTime 秒飛到地面點 to (球心停在 to 上方 landingHeight)，拋物線最高點比直線高 arcHeight；落地時呼叫 onLanded
    public void Launch(Vector3 from, Vector3 to, float flightTime, float arcHeight, Action<SummonCapsule> onLanded)
    {
        startPosition = from;
        targetPosition = to + Vector3.up * landingHeight;
        flightDuration = Mathf.Max(0.01f, flightTime);
        arcPeak = arcHeight;
        landedCallback = onLanded;
        elapsed = 0f;
        isFlying = true;
        hasLanded = false;
        transform.SetPositionAndRotation(from, UnityEngine.Random.rotation);
        transform.localScale = baseScale;

        if (topRenderer != null && topColors.Length > 0)
        {
            propertyBlock.SetColor(BaseColorId, topColors[UnityEngine.Random.Range(0, topColors.Length)]);
            topRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void Update()
    {
        if (isFlying) UpdateFlight();
        else if (hasLanded) UpdateOpen();
    }

    private void UpdateFlight()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / flightDuration);
        transform.position = ArcPath.Evaluate(startPosition, targetPosition, arcPeak, t);
        transform.Rotate(spinSpeed * Time.deltaTime, spinSpeed * 0.6f * Time.deltaTime, 0f, Space.Self);
        if (t < 1f) return;

        // 落地：擺正 (上半殼朝上) 再打開
        transform.SetPositionAndRotation(targetPosition, Quaternion.identity);
        isFlying = false;
        hasLanded = true;
        elapsed = 0f;
        landedCallback?.Invoke(this);
    }

    // 前半段上半殼往上彈開並翻轉，後半段整顆縮小消失
    private void UpdateOpen()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / openDuration);
        if (topShell != null)
        {
            float pop = Mathf.Sin(Mathf.Clamp01(t * 2f) * Mathf.PI * 0.5f);
            topShell.localPosition = shellRestPosition + Vector3.up * (shellLift * pop);
            topShell.localRotation = shellRestRotation * Quaternion.Euler(shellTilt * pop, 0f, 0f);
        }

        transform.localScale = baseScale * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, t)));
        if (t >= 1f) Destroy(gameObject);
    }
}
