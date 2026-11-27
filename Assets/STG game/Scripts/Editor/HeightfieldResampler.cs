#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 減面：把「從上往下看是一層表面」的高面數模型 (例如掃描的沙丘) 重新取樣成 resolution x resolution 的規則網格。
// 每個網格點往下找原始模型最上面的表面 (取最高的那一層)，用三角形的重心座標內插高度與頂點色；
// 網格點底下沒有表面就丟掉，周圍的三角形也一起略過 (邊緣只差一個角時補一個三角形，邊緣比較不鋸齒)。
// 為了速度，先把原始三角形依 XZ 範圍分到格子裡，每個網格點只檢查自己所在格子的三角形
public static class HeightfieldResampler
{
    private const float Epsilon = 1e-4f;

    public static Mesh Resample(Vector3[] vertices, int[] triangles, Color[] colors, int resolution)
    {
        resolution = Mathf.Max(2, resolution);
        bool hasColors = colors != null && colors.Length == vertices.Length;

        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < vertices.Length; i++)
        {
            min = Vector2.Min(min, new Vector2(vertices[i].x, vertices[i].z));
            max = Vector2.Max(max, new Vector2(vertices[i].x, vertices[i].z));
        }

        int cells = resolution - 1;
        Vector2 step = new Vector2((max.x - min.x) / cells, (max.y - min.y) / cells);
        List<int>[] bins = BinTriangles(vertices, triangles, min, step, cells);

        // 每個網格點取樣
        int[] gridIndex = new int[resolution * resolution];
        List<Vector3> outVertices = new List<Vector3>();
        List<Color> outColors = new List<Color>();
        for (int j = 0; j < resolution; j++)
        {
            for (int i = 0; i < resolution; i++)
            {
                float x = min.x + i * step.x;
                float z = min.y + j * step.y;
                int cellX = Mathf.Min(i, cells - 1);
                int cellZ = Mathf.Min(j, cells - 1);
                if (Sample(vertices, triangles, colors, hasColors, bins[cellZ * cells + cellX], x, z, out float height, out Color color))
                {
                    gridIndex[j * resolution + i] = outVertices.Count;
                    outVertices.Add(new Vector3(x, height, z));
                    outColors.Add(color);
                }
                else
                {
                    gridIndex[j * resolution + i] = -1;
                }
            }
        }

        List<int> outTriangles = BuildTriangles(gridIndex, resolution);

        Mesh mesh = new Mesh { name = "Resampled" };
        if (outVertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(outVertices);
        if (hasColors) mesh.SetColors(outColors);
        mesh.SetTriangles(outTriangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // 每個三角形放進它 XZ 範圍 (稍微放寬) 碰到的所有格子
    private static List<int>[] BinTriangles(Vector3[] vertices, int[] triangles, Vector2 min, Vector2 step, int cells)
    {
        List<int>[] bins = new List<int>[cells * cells];
        for (int i = 0; i < bins.Length; i++) bins[i] = new List<int>();

        for (int t = 0; t < triangles.Length; t += 3)
        {
            Vector3 a = vertices[triangles[t]];
            Vector3 b = vertices[triangles[t + 1]];
            Vector3 c = vertices[triangles[t + 2]];
            int x0 = CellOf(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - Epsilon, min.x, step.x, cells);
            int x1 = CellOf(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) + Epsilon, min.x, step.x, cells);
            int z0 = CellOf(Mathf.Min(a.z, Mathf.Min(b.z, c.z)) - Epsilon, min.y, step.y, cells);
            int z1 = CellOf(Mathf.Max(a.z, Mathf.Max(b.z, c.z)) + Epsilon, min.y, step.y, cells);
            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++) bins[z * cells + x].Add(t);
            }
        }

        return bins;
    }

    private static int CellOf(float value, float min, float step, int cells)
    {
        if (step <= 0f) return 0;
        return Mathf.Clamp(Mathf.FloorToInt((value - min) / step), 0, cells - 1);
    }

    // 找 (x, z) 正上方最高的表面
    private static bool Sample(Vector3[] vertices, int[] triangles, Color[] colors, bool hasColors, List<int> candidates, float x, float z, out float height, out Color color)
    {
        height = float.MinValue;
        color = Color.white;
        bool found = false;
        for (int n = 0; n < candidates.Count; n++)
        {
            int t = candidates[n];
            Vector3 a = vertices[triangles[t]];
            Vector3 b = vertices[triangles[t + 1]];
            Vector3 c = vertices[triangles[t + 2]];
            if (!Barycentric(a, b, c, x, z, out float u, out float v, out float w)) continue;

            float y = a.y * u + b.y * v + c.y * w;
            if (found && y <= height) continue;

            found = true;
            height = y;
            if (hasColors) color = colors[triangles[t]] * u + colors[triangles[t + 1]] * v + colors[triangles[t + 2]] * w;
        }

        return found;
    }

    // XZ 平面上的重心座標；點在三角形內 (含邊上) 才回傳 true
    private static bool Barycentric(Vector3 a, Vector3 b, Vector3 c, float x, float z, out float u, out float v, out float w)
    {
        float denominator = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
        u = v = w = 0f;
        if (Mathf.Abs(denominator) < 1e-9f) return false;

        u = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / denominator;
        v = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / denominator;
        w = 1f - u - v;
        return u >= -Epsilon && v >= -Epsilon && w >= -Epsilon;
    }

    // 每個格子四個角都有取樣到 → 兩個三角形；只有三個角 → 一個三角形 (順時針 = 從上往下看的正面)
    private static List<int> BuildTriangles(int[] gridIndex, int resolution)
    {
        List<int> result = new List<int>();
        for (int j = 0; j < resolution - 1; j++)
        {
            for (int i = 0; i < resolution - 1; i++)
            {
                int a = gridIndex[j * resolution + i];
                int b = gridIndex[j * resolution + i + 1];
                int c = gridIndex[(j + 1) * resolution + i];
                int d = gridIndex[(j + 1) * resolution + i + 1];
                int valid = (a >= 0 ? 1 : 0) + (b >= 0 ? 1 : 0) + (c >= 0 ? 1 : 0) + (d >= 0 ? 1 : 0);
                if (valid == 4)
                {
                    result.Add(a); result.Add(c); result.Add(d);
                    result.Add(a); result.Add(d); result.Add(b);
                }
                else if (valid == 3)
                {
                    if (a < 0) { result.Add(b); result.Add(c); result.Add(d); }
                    else if (b < 0) { result.Add(a); result.Add(c); result.Add(d); }
                    else if (c < 0) { result.Add(a); result.Add(d); result.Add(b); }
                    else { result.Add(a); result.Add(c); result.Add(b); }
                }
            }
        }

        return result;
    }
}
#endif
