using UnityEngine;

// 開場演出的光束：從發射點往落點延伸 (開場時已經伸出 startFraction)，在 hitAt 碰到地面；
// 碰地之後落點的爆炸才出現並慢慢長大 (定格時停在「剛要炸開」的大小)。
// tube 是內建 Cylinder (沿本地 Y 軸、高 2)
public class IntroBeam : MonoBehaviour, ITitleIntroPart
{
    [Header("Beam")]
    [SerializeField, Tooltip("光束圓柱")] private Transform tube;
    [SerializeField, Tooltip("發射點 (世界座標)")] private Vector3 from;
    [SerializeField, Tooltip("落點 (世界座標)")] private Vector3 to;
    [SerializeField, Min(0.01f), Tooltip("光束粗細")] private float width = 2f;
    [SerializeField, Range(0f, 1f), Tooltip("開場第一格時光束已經伸出的比例")] private float startFraction = 0.5f;
    [SerializeField, Range(0.01f, 1f), Tooltip("光束碰到地面的進度")] private float hitAt = 0.6f;

    [Header("Impact")]
    [SerializeField, Tooltip("落點的光球 (碰地才出現)，可留空")] private Transform impact;
    [SerializeField, Tooltip("落點的爆炸 (碰地才出現並長大)，可留空")] private Transform explosion;
    [SerializeField, Tooltip("定格時爆炸的大小")] private Vector3 explosionScale = Vector3.one;

    public void SetProgress(float progress)
    {
        bool hit = progress >= hitAt;
        float reach = Mathf.Lerp(startFraction, 1f, Mathf.Clamp01(progress / hitAt));
        Vector3 end = Vector3.Lerp(from, to, reach);
        Vector3 span = end - from;
        if (tube != null && span.sqrMagnitude > 0.0001f)
        {
            tube.SetPositionAndRotation((from + end) * 0.5f, Quaternion.FromToRotation(Vector3.up, span));
            tube.localScale = new Vector3(width, span.magnitude * 0.5f, width);
        }

        if (impact != null) impact.gameObject.SetActive(hit);
        if (explosion == null) return;

        explosion.gameObject.SetActive(hit);
        if (!hit) return;

        float grow = hitAt >= 1f ? 1f : Mathf.Clamp01((progress - hitAt) / (1f - hitAt));
        explosion.position = to;
        explosion.localScale = explosionScale * grow;
    }
}
