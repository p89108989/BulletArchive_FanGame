using System;
using UnityEngine;

// 長方形地面警示區：淡色底 + 四邊框線 + 由起點往前推進的填充 (倒數進度)。
// 根物件放在起點、+Z 朝延伸方向；子物件的 Quad 依長寬即時縮放，邊框線寬維持固定不會被拉伸。
// 依屬性變色：紅色 (None) 無法被護盾吸收；藍/黃與玩家護盾同極性時會被吸收
public class LaneWarning : MonoBehaviour
{
    [Serializable]
    private struct PolarityStyle
    {
        [Tooltip("對應的屬性")] public BulletPolarity polarity;
        [ColorUsage(true, true), Tooltip("邊框顏色")] public Color borderColor;
        [ColorUsage(true, true), Tooltip("淡色底的顏色")] public Color areaColor;
        [ColorUsage(true, true), Tooltip("倒數填充顏色")] public Color fillColor;

        public PolarityStyle(BulletPolarity polarity, Color borderColor, Color areaColor, Color fillColor)
        {
            this.polarity = polarity;
            this.borderColor = borderColor;
            this.areaColor = areaColor;
            this.fillColor = fillColor;
        }
    }

    [Header("Parts")]
    [SerializeField, Tooltip("整塊範圍的淡色底")] private Renderer areaRenderer;
    [SerializeField, Tooltip("由起點往前推進的倒數填充")] private Renderer fillRenderer;
    [SerializeField, Tooltip("四條邊框線，順序：左、右、後、前")] private Renderer[] borderRenderers = new Renderer[4];

    [Header("Look")]
    [SerializeField, Min(0.05f), Tooltip("邊框線寬 (m)")] private float borderWidth = 0.6f;
    [SerializeField, Tooltip("警示區離地高度，避免與地板 Z-fighting")] private float heightAboveGround = 0.05f;
    [SerializeField, Tooltip("每種屬性的配色")]
    private PolarityStyle[] styles =
    {
        new PolarityStyle(BulletPolarity.None, new Color(2f, 0.15f, 0.1f, 1f), new Color(1f, 0.1f, 0.05f, 0.15f), new Color(1f, 0.1f, 0.05f, 0.45f)),
        new PolarityStyle(BulletPolarity.Blue, new Color(0.2f, 0.6f, 2.5f, 1f), new Color(0.1f, 0.4f, 1f, 0.15f), new Color(0.1f, 0.4f, 1f, 0.45f)),
        new PolarityStyle(BulletPolarity.Yellow, new Color(2.5f, 1.8f, 0.1f, 1f), new Color(1f, 0.75f, 0.05f, 0.15f), new Color(1f, 0.75f, 0.05f, 0.45f)),
    };

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private MaterialPropertyBlock propertyBlock;
    private float length;
    private float width;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        if (RequiredReference.IsMissing(areaRenderer, nameof(areaRenderer), this)) return;
        if (RequiredReference.IsMissing(fillRenderer, nameof(fillRenderer), this)) return;
        for (int i = 0; i < borderRenderers.Length; i++)
        {
            if (RequiredReference.IsMissing(borderRenderers[i], nameof(borderRenderers), this)) return;
        }
    }

    // 顯示警示區：start 為貼地的起點，direction 只取水平方向，往前延伸 laneLength
    public void Show(Vector3 start, Vector3 direction, float laneLength, float laneWidth, BulletPolarity polarity)
    {
        if (!enabled) return;

        PolarityStyle style = GetStyle(polarity);
        SetColor(areaRenderer, style.areaColor);
        SetColor(fillRenderer, style.fillColor);
        for (int i = 0; i < borderRenderers.Length; i++) SetColor(borderRenderers[i], style.borderColor);

        gameObject.SetActive(true);
        SetProgress(0f);
        Place(start, direction, laneLength, laneWidth);
    }

    // 顯示以 center 為中心的警示區 (例如落地範圍)
    public void ShowCentered(Vector3 center, Vector3 direction, float laneLength, float laneWidth, BulletPolarity polarity)
    {
        Vector3 forward = Flatten(direction);
        Show(center - forward * (laneLength * 0.5f), forward, laneLength, laneWidth, polarity);
    }

    // 移動/轉向警示區 (例如鎖定期間跟著玩家轉動)，不改變顏色與進度
    public void Place(Vector3 start, Vector3 direction, float laneLength, float laneWidth)
    {
        length = Mathf.Max(0.01f, laneLength);
        width = Mathf.Max(0.01f, laneWidth);
        Vector3 forward = Flatten(direction);
        if (forward.sqrMagnitude < 0.0001f) forward = transform.forward;
        transform.SetPositionAndRotation(start, Quaternion.LookRotation(forward, Vector3.up));

        SetQuad(areaRenderer.transform, 0f, length * 0.5f, width, length, 0f);
        float halfWidth = width * 0.5f - borderWidth * 0.5f;
        SetQuad(borderRenderers[0].transform, -halfWidth, length * 0.5f, borderWidth, length, 0.02f);
        SetQuad(borderRenderers[1].transform, halfWidth, length * 0.5f, borderWidth, length, 0.02f);
        SetQuad(borderRenderers[2].transform, 0f, borderWidth * 0.5f, width, borderWidth, 0.02f);
        SetQuad(borderRenderers[3].transform, 0f, length - borderWidth * 0.5f, width, borderWidth, 0.02f);
    }

    // 倒數進度 0~1：填充由起點往前推進
    public void SetProgress(float progress)
    {
        float filled = length * Mathf.Clamp01(progress);
        fillRenderer.enabled = filled > 0.001f;
        SetQuad(fillRenderer.transform, 0f, filled * 0.5f, width, filled, 0.01f);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // Quad 已在 Prefab 中繞 X 轉 90 度平貼地面，localScale 的 x/y 分別是寬與長
    private void SetQuad(Transform quad, float x, float z, float quadWidth, float quadLength, float extraHeight)
    {
        quad.localPosition = new Vector3(x, heightAboveGround + extraHeight, z);
        quad.localScale = new Vector3(quadWidth, Mathf.Max(0.001f, quadLength), 1f);
    }

    private PolarityStyle GetStyle(BulletPolarity polarity)
    {
        for (int i = 0; i < styles.Length; i++)
        {
            if (styles[i].polarity == polarity) return styles[i];
        }

        return styles.Length > 0 ? styles[0] : default;
    }

    private void SetColor(Renderer target, Color color)
    {
        propertyBlock.SetColor(BaseColorId, color);
        target.SetPropertyBlock(propertyBlock);
    }

    private static Vector3 Flatten(Vector3 vector)
    {
        vector.y = 0f;
        return vector.normalized;
    }
}
