using UnityEngine;

// 可移動的地面圓形警示：外圈標出危險範圍，內圈依倒數進度由中心擴張。
// 不像 AreaStrike 會在倒數結束時自己引爆，位置與進度完全由呼叫端控制，適合跟著目標移動的警示
public class GroundWarningCircle : MonoBehaviour
{
    [SerializeField, Tooltip("外圈 (直徑 1 的平貼圓形，縮放成範圍大小)")] private Transform ring;
    [SerializeField, Tooltip("倒數填充 (直徑 1 的平貼圓形，由中心往外擴張)")] private Transform fill;
    [SerializeField, Tooltip("警示離地高度，避免與地板 Z-fighting")] private float heightAboveGround = 0.04f;

    private float radius;

    private void Awake()
    {
        if (RequiredReference.IsMissing(ring, nameof(ring), this)) return;
        RequiredReference.IsMissing(fill, nameof(fill), this);
    }

    // 顯示在貼地的 center，倒數歸零
    public void Show(Vector3 center, float warningRadius)
    {
        radius = warningRadius;
        gameObject.SetActive(true);
        MoveTo(center);
        if (ring != null) ring.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
        SetProgress(0f);
    }

    public void MoveTo(Vector3 center)
    {
        transform.position = center + Vector3.up * heightAboveGround;
    }

    // 倒數進度 0~1
    public void SetProgress(float progress)
    {
        if (fill == null) return;

        float size = radius * 2f * Mathf.Clamp01(progress);
        fill.localScale = new Vector3(size, size, 1f);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
