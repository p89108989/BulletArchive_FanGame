using UnityEngine;
using UnityEngine.UI;

// 斜切的平行四邊形 UI 圖形 (按鈕、色塊)：上緣往右偏移 slant 像素，左右兩邊成斜線；純色，可接收點擊
[RequireComponent(typeof(CanvasRenderer))]
public class SlantedRect : MaskableGraphic
{
    [SerializeField, Tooltip("上緣比下緣往右偏多少 (UI 像素)；負值往左")] private float slant = 30f;

    public float Slant
    {
        get => slant;
        set
        {
            slant = value;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        // 斜邊落在矩形內：下緣從左邊開始，上緣從 slant 開始 (整體寬度不變)
        float half = slant * 0.5f;
        Color32 c = color;
        vh.AddVert(new Vector3(r.xMin - half, r.yMin), c, new Vector2(0f, 0f));
        vh.AddVert(new Vector3(r.xMin + half, r.yMax), c, new Vector2(0f, 1f));
        vh.AddVert(new Vector3(r.xMax + half, r.yMax), c, new Vector2(1f, 1f));
        vh.AddVert(new Vector3(r.xMax - half, r.yMin), c, new Vector2(1f, 0f));
        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(2, 3, 0);
    }
}
