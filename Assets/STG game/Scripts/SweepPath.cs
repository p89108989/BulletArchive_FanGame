using UnityEngine;

// 掃射軌跡：一串 0~1 的正規化折線點 (x 對應世界 X、y 對應世界 Z) 映射到一塊長方形區域，
// 依路徑長度取點，讓掃射速度在每一段都一樣快 (斜線段不會比直線段跑得快)
public static class SweepPath
{
    // t = 0~1 的掃射進度；reverse 時從最後一點往回掃
    public static Vector3 Evaluate(Vector2[] normalizedPoints, Vector2 areaMin, Vector2 areaMax, float t, bool reverse, float height)
    {
        int pointCount = normalizedPoints.Length;
        if (pointCount == 0) return new Vector3(areaMin.x, height, areaMin.y);
        if (pointCount == 1) return ToWorld(normalizedPoints[0], areaMin, areaMax, height);

        float total = 0f;
        for (int i = 1; i < pointCount; i++) total += SegmentLength(normalizedPoints, i, areaMin, areaMax, reverse);

        float remaining = Mathf.Clamp01(t) * total;
        for (int i = 1; i < pointCount; i++)
        {
            float segment = SegmentLength(normalizedPoints, i, areaMin, areaMax, reverse);
            if (remaining <= segment || i == pointCount - 1)
            {
                Vector3 from = ToWorld(PointAt(normalizedPoints, i - 1, reverse), areaMin, areaMax, height);
                Vector3 to = ToWorld(PointAt(normalizedPoints, i, reverse), areaMin, areaMax, height);
                return Vector3.Lerp(from, to, segment > 0f ? remaining / segment : 1f);
            }

            remaining -= segment;
        }

        return ToWorld(PointAt(normalizedPoints, pointCount - 1, reverse), areaMin, areaMax, height);
    }

    // 來回掃 passes 趟的鋸齒折線 (正規化座標)：從 x 大的一側往 x 小的一側均勻推進，
    // 偶數點在 y 大的一側 (近側)、奇數點在 y 小的一側 (遠側)；inset 是離區域邊緣保留的比例
    public static Vector2[] Zigzag(int passes, Vector2 inset)
    {
        passes = Mathf.Max(1, passes);
        int pointCount = passes * 2 + 1;
        Vector2[] points = new Vector2[pointCount];
        float startX = 1f - inset.x;
        float endX = inset.x;
        float nearY = 1f - inset.y;
        float farY = inset.y;
        for (int i = 0; i < pointCount; i++)
        {
            float x = Mathf.Lerp(startX, endX, (float)i / (pointCount - 1));
            points[i] = new Vector2(x, i % 2 == 0 ? nearY : farY);
        }

        return points;
    }

    private static Vector2 PointAt(Vector2[] points, int index, bool reverse) => points[reverse ? points.Length - 1 - index : index];

    private static float SegmentLength(Vector2[] points, int index, Vector2 areaMin, Vector2 areaMax, bool reverse)
    {
        Vector3 from = ToWorld(PointAt(points, index - 1, reverse), areaMin, areaMax, 0f);
        Vector3 to = ToWorld(PointAt(points, index, reverse), areaMin, areaMax, 0f);
        return Vector3.Distance(from, to);
    }

    private static Vector3 ToWorld(Vector2 normalized, Vector2 areaMin, Vector2 areaMax, float height)
    {
        return new Vector3(
            Mathf.LerpUnclamped(areaMin.x, areaMax.x, normalized.x),
            height,
            Mathf.LerpUnclamped(areaMin.y, areaMax.y, normalized.y));
    }
}
