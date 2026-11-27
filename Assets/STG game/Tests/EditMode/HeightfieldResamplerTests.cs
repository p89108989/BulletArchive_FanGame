using NUnit.Framework;
using UnityEngine;

// 把高面數的「從上往下看是一層表面」的模型 (掃描沙丘) 重新取樣成規則網格：
// 每個網格點往下找原始模型最上面的表面，取它的高度與頂點色；網格點底下沒有表面就丟掉
public class HeightfieldResamplerTests
{
    // 一個 10 x 10 的斜面：高度 = x (0 ~ 10)，顏色左黑右白
    private static void Slope(out Vector3[] vertices, out int[] triangles, out Color[] colors)
    {
        vertices = new[]
        {
            new Vector3(0f, 0f, 0f), new Vector3(10f, 10f, 0f), new Vector3(10f, 10f, 10f), new Vector3(0f, 0f, 10f),
        };
        triangles = new[] { 0, 2, 1, 0, 3, 2 };
        colors = new[] { Color.black, Color.white, Color.white, Color.black };
    }

    private static Vector3 VertexNear(Mesh mesh, float x, float z)
    {
        Vector3 best = Vector3.zero;
        float bestDistance = float.MaxValue;
        foreach (Vector3 vertex in mesh.vertices)
        {
            float distance = (vertex.x - x) * (vertex.x - x) + (vertex.z - z) * (vertex.z - z);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = vertex;
        }

        return best;
    }

    [Test]
    public void SamplesTheSurfaceHeight()
    {
        Slope(out Vector3[] vertices, out int[] triangles, out Color[] colors);

        Mesh mesh = HeightfieldResampler.Resample(vertices, triangles, colors, 11);

        Vector3 middle = VertexNear(mesh, 5f, 5f);
        Assert.AreEqual(5f, middle.x, 0.01f);
        Assert.AreEqual(5f, middle.y, 0.01f, "height = x on the slope");
        Assert.AreEqual(121, mesh.vertexCount, "11 x 11 grid fully covered");
        Assert.AreEqual(10 * 10 * 2 * 3, mesh.triangles.Length);
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void InterpolatesVertexColors()
    {
        Slope(out Vector3[] vertices, out int[] triangles, out Color[] colors);

        Mesh mesh = HeightfieldResampler.Resample(vertices, triangles, colors, 11);

        int index = System.Array.IndexOf(mesh.vertices, VertexNear(mesh, 5f, 5f));
        Assert.AreEqual(0.5f, mesh.colors[index].r, 0.02f);
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void DropsGridPointsWithNoSurfaceBelow()
    {
        // 只有一個三角形 (斜面的一半)：另一半的網格點底下沒有表面
        Slope(out Vector3[] vertices, out _, out Color[] colors);
        int[] half = { 0, 2, 1 };

        Mesh mesh = HeightfieldResampler.Resample(vertices, half, colors, 11);

        Assert.Less(mesh.vertexCount, 121);
        Assert.Greater(mesh.vertexCount, 50);
        foreach (Vector3 vertex in mesh.vertices) Assert.GreaterOrEqual(vertex.x, vertex.z - 0.01f, "only the covered triangle (x >= z)");
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void TakesTheTopSurfaceWhenLayersOverlap()
    {
        // 兩層平面：y = 1 與 y = 4，取上面那層
        Vector3[] vertices =
        {
            new Vector3(0f, 1f, 0f), new Vector3(10f, 1f, 0f), new Vector3(10f, 1f, 10f), new Vector3(0f, 1f, 10f),
            new Vector3(0f, 4f, 0f), new Vector3(10f, 4f, 0f), new Vector3(10f, 4f, 10f), new Vector3(0f, 4f, 10f),
        };
        int[] triangles = { 0, 2, 1, 0, 3, 2, 4, 6, 5, 4, 7, 6 };
        Color[] colors = new Color[8];

        Mesh mesh = HeightfieldResampler.Resample(vertices, triangles, colors, 5);

        foreach (Vector3 vertex in mesh.vertices) Assert.AreEqual(4f, vertex.y, 0.01f);
        Object.DestroyImmediate(mesh);
    }
}
