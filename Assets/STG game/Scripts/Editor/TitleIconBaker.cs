#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

// 開始畫面按鈕的圖示 (▶ 開始、學士帽 教學、離開)：專案裡沒有這類素材，用簡單幾何畫成白色 PNG (使用時再上色)。
// 每個像素 4×4 超取樣做抗鋸齒；重複執行會覆寫
public static class TitleIconBaker
{
    private const int Size = 128;
    private const int Supersample = 4;

    public static Sprite Play(string folder) => Bake(folder + "/Icon_Play.png", PlayShape);
    public static Sprite Cap(string folder) => Bake(folder + "/Icon_Tutorial.png", CapShape);
    public static Sprite Exit(string folder) => Bake(folder + "/Icon_Exit.png", ExitShape);
    // 遊戲名稱上方的標誌：兩個傾斜的同心橢圓軌道 + 一條長斜線與一條短陡線交叉 (像準心)
    public static Sprite Emblem(string folder) => Bake(folder + "/Logo_Emblem.png", EmblemShape, EmblemWidth, EmblemHeight);

    private const int EmblemWidth = 800;
    private const int EmblemHeight = 300;

    // 座標 0~1：x 是寬度比例、y 是高度比例 (寬高比 8:3)；以 400×150 的設計格換算
    private static bool EmblemShape(Vector2 uv)
    {
        Vector2 p = new Vector2(uv.x * 400f, uv.y * 150f);
        const float stroke = 3.2f;
        bool longLine = SegmentDistance(p, new Vector2(10f, 6f), new Vector2(388f, 142f)) < stroke;
        bool shortLine = SegmentDistance(p, new Vector2(172f, 144f), new Vector2(226f, 6f)) < stroke;
        bool outer = EllipseRing(p, new Vector2(196f, 80f), 132f, 40f, 20f, stroke);
        bool inner = EllipseRing(p, new Vector2(208f, 86f), 76f, 22f, 20f, stroke);
        return longLine || shortLine || outer || inner;
    }

    // 旋轉橢圓的邊線 (以正規化距離近似，線寬約 stroke 像素)
    private static bool EllipseRing(Vector2 p, Vector2 center, float a, float b, float degrees, float stroke)
    {
        float angle = -degrees * Mathf.Deg2Rad;
        Vector2 d = p - center;
        Vector2 local = new Vector2(d.x * Mathf.Cos(angle) - d.y * Mathf.Sin(angle), d.x * Mathf.Sin(angle) + d.y * Mathf.Cos(angle));
        float k = Mathf.Sqrt(local.x * local.x / (a * a) + local.y * local.y / (b * b));
        // 正規化距離乘上該方向的半徑 ≈ 到邊線的像素距離
        float radius = local.magnitude / Mathf.Max(k, 0.0001f);
        return Mathf.Abs(k - 1f) * radius < stroke;
    }

    // 座標 0~1 (左下為原點)
    private static bool PlayShape(Vector2 p)
    {
        return InPolygon(p, new Vector2(0.24f, 0.14f), new Vector2(0.24f, 0.86f), new Vector2(0.86f, 0.5f));
    }

    // 學士帽：扁菱形帽頂 + 下方帽身 + 右側流蘇
    private static bool CapShape(Vector2 p)
    {
        bool board = InPolygon(p, new Vector2(0.04f, 0.62f), new Vector2(0.5f, 0.84f), new Vector2(0.96f, 0.62f), new Vector2(0.5f, 0.4f));
        bool body = InPolygon(p, new Vector2(0.24f, 0.52f), new Vector2(0.76f, 0.52f), new Vector2(0.76f, 0.3f), new Vector2(0.5f, 0.2f), new Vector2(0.24f, 0.3f));
        bool tassel = SegmentDistance(p, new Vector2(0.86f, 0.6f), new Vector2(0.86f, 0.3f)) < 0.025f
            || (p - new Vector2(0.86f, 0.27f)).magnitude < 0.05f;
        return board || body || tassel;
    }

    // 離開：右側開口朝左的門框 + 從左邊往右指進門框的箭頭
    private static bool ExitShape(Vector2 p)
    {
        const float stroke = 0.045f;
        bool frame = SegmentDistance(p, new Vector2(0.5f, 0.86f), new Vector2(0.86f, 0.86f)) < stroke
            || SegmentDistance(p, new Vector2(0.86f, 0.86f), new Vector2(0.86f, 0.14f)) < stroke
            || SegmentDistance(p, new Vector2(0.86f, 0.14f), new Vector2(0.5f, 0.14f)) < stroke;
        bool shaft = SegmentDistance(p, new Vector2(0.12f, 0.5f), new Vector2(0.6f, 0.5f)) < stroke;
        bool head = SegmentDistance(p, new Vector2(0.62f, 0.5f), new Vector2(0.44f, 0.68f)) < stroke
            || SegmentDistance(p, new Vector2(0.62f, 0.5f), new Vector2(0.44f, 0.32f)) < stroke;
        return frame || shaft || head;
    }

    private static Sprite Bake(string path, System.Func<Vector2, bool> shape)
    {
        return Bake(path, shape, Size, Size);
    }

    private static Sprite Bake(string path, System.Func<Vector2, bool> shape, int width, int height)
    {
        Color32[] pixels = new Color32[width * height];
        float stepX = 1f / (width * Supersample);
        float stepY = 1f / (height * Supersample);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < Supersample; sy++)
                {
                    for (int sx = 0; sx < Supersample; sx++)
                    {
                        Vector2 p = new Vector2((x * Supersample + sx + 0.5f) * stepX, (y * Supersample + sy + 0.5f) * stepY);
                        if (shape(p)) hits++;
                    }
                }

                byte alpha = (byte)(255 * hits / (Supersample * Supersample));
                pixels[y * width + x] = new Color32(255, 255, 255, alpha);
            }
        }

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static bool InPolygon(Vector2 p, params Vector2[] points)
    {
        bool inside = false;
        for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
        {
            if ((points[i].y > p.y) != (points[j].y > p.y)
                && p.x < (points[j].x - points[i].x) * (p.y - points[i].y) / (points[j].y - points[i].y) + points[i].x)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return (p - (a + ab * t)).magnitude;
    }
}
#endif
