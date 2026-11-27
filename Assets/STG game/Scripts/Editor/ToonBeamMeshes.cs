#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// 卡通光束用的密圓柱：規格同內建 Cylinder (沿 Y 軸、高 2 從 -1 到 1、半徑 0.5)，但沿長度切很多段，
// STG/ToonBeam 的波動鼓起才看得出來 (內建 Cylinder 長度方向只有頭尾兩圈頂點)。三角形朝外，兩端有蓋子；
// uv.x 繞一圈、uv.y 沿長度 (STG/BeamDarkLightning 用)
public static class ToonBeamMeshes
{
    private const string CylinderPath = ToonBeamMaterials.Folder + "/ToonBeam_Cylinder.asset";
    private const int SegmentsAround = 24;
    private const int SegmentsAlong = 120;

    public static Mesh GetOrCreateCylinder()
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(CylinderPath);
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh { name = "ToonBeam_Cylinder" };
        mesh.Clear();

        int columns = SegmentsAround + 1;
        int rows = SegmentsAlong + 1;
        int sideCount = columns * rows;
        // 側面頂點 + 兩個蓋子 (各一圈 + 中心點)
        Vector3[] vertices = new Vector3[sideCount + (columns + 1) * 2];
        Vector3[] normals = new Vector3[vertices.Length];
        // uv.x 繞一圈 (0~1)、uv.y 沿長度 (0~1)；光炮外圍的黑色雷電依這個畫 (蓋子用不到，維持 0)
        Vector2[] uvs = new Vector2[vertices.Length];
        for (int row = 0; row < rows; row++)
        {
            float y = Mathf.Lerp(-1f, 1f, (float)row / SegmentsAlong);
            for (int column = 0; column < columns; column++)
            {
                Vector3 radial = Radial(column);
                int index = row * columns + column;
                vertices[index] = radial * 0.5f + new Vector3(0f, y, 0f);
                normals[index] = radial;
                uvs[index] = new Vector2((float)column / SegmentsAround, (float)row / SegmentsAlong);
            }
        }

        int bottom = sideCount;
        int top = bottom + columns + 1;
        for (int column = 0; column < columns; column++)
        {
            vertices[bottom + column] = Radial(column) * 0.5f + Vector3.down;
            normals[bottom + column] = Vector3.down;
            vertices[top + column] = Radial(column) * 0.5f + Vector3.up;
            normals[top + column] = Vector3.up;
        }

        vertices[bottom + columns] = Vector3.down;
        normals[bottom + columns] = Vector3.down;
        vertices[top + columns] = Vector3.up;
        normals[top + columns] = Vector3.up;

        int[] triangles = new int[SegmentsAround * SegmentsAlong * 6 + SegmentsAround * 6];
        int t = 0;
        for (int row = 0; row < SegmentsAlong; row++)
        {
            for (int column = 0; column < SegmentsAround; column++)
            {
                int a = row * columns + column;
                int b = a + columns;
                triangles[t++] = a; triangles[t++] = a + 1; triangles[t++] = b;
                triangles[t++] = a + 1; triangles[t++] = b + 1; triangles[t++] = b;
            }
        }

        for (int column = 0; column < SegmentsAround; column++)
        {
            triangles[t++] = bottom + columns; triangles[t++] = bottom + column + 1; triangles[t++] = bottom + column;
            triangles[t++] = top + columns; triangles[t++] = top + column; triangles[t++] = top + column + 1;
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        // 波動會讓頂點往外鼓，外框放大一點避免被提早裁掉
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(2f, 2f, 2f));

        if (isNew) AssetDatabase.CreateAsset(mesh, CylinderPath);
        else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    // 繞 Y 軸一圈 (從 +Z 往 +X)；配合上面的三角形順序，正面朝外
    private static Vector3 Radial(int column)
    {
        float angle = (float)column / SegmentsAround * Mathf.PI * 2f;
        return new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
    }
}
#endif
