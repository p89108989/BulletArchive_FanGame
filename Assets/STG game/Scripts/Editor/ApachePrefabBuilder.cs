#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Apache.glb 是一體模型 (單一 Mesh)，旋翼無法單獨轉動。這個工具把主旋翼與尾旋翼的幾何
// 從原 Mesh 拆成獨立 Mesh (旋轉中心設在槳轂)，再組成可直接使用的 prefab，原始 .glb 不會被修改。
//
// 拆分依據：Mesh 內部由多個互不相連的零件組成，依零件中心位置判斷屬於哪一部分。
// 原模型座標：機頭朝 -Y、機頂朝 -Z、主旋翼軸平行 Z、尾旋翼軸平行 X。
public static class ApachePrefabBuilder
{
    private const string SourcePath = "Assets/STG game/resources/blue-archive-vehicle-black-market/source/Apache.glb";
    private const string OutputFolder = "Assets/STG game/resources/prefab/Apache";
    private const string PrefabPath = "Assets/STG game/resources/prefab/Apache.prefab";

    // 主旋翼：中心落在槳葉所在高度帶內的零件 (排除更上方固定不轉的 Longbow 雷達罩與下方的機身)
    private const float MainRotorMinZ = -6.9f;
    private const float MainRotorMaxZ = -4.9f;

    // 尾旋翼：位於尾翼右側的槳轂附近 (排除同高度、位於尾翼中線上的垂直尾翼零件)
    private static readonly Vector3 TailRotorHub = new Vector3(1.26f, 12.77f, -3.51f);
    private const float TailRotorMinX = 1f;
    private const float TailRotorRange = 1f;

    // 同位置頂點焊接的精度 (glTF 會因法線/UV 不同拆開同一位置的頂點)
    private const float WeldPrecision = 1000f;

    [MenuItem("Tools/STG/Build Apache Prefab")]
    private static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[ApachePrefabBuilder] 請先離開 Play Mode 再執行。");
            return;
        }

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
        Mesh sourceMesh = source.GetComponent<MeshFilter>().sharedMesh;
        Material[] materials = source.GetComponent<MeshRenderer>().sharedMaterials;
        if (!sourceMesh.isReadable)
        {
            Debug.LogError("[ApachePrefabBuilder] Apache Mesh 不可讀取，請在匯入設定開啟 Read/Write。");
            return;
        }

        EnsureFolder(OutputFolder);

        int[] partOf = ClassifyVertices(sourceMesh, out int mainCount, out int tailCount);
        Mesh body = ExtractMesh(sourceMesh, partOf, Part.Body, "Apache_Body", out _);
        Mesh mainRotor = ExtractMesh(sourceMesh, partOf, Part.MainRotor, "Apache_MainRotor", out Vector3 mainPivot);
        Mesh tailRotor = ExtractMesh(sourceMesh, partOf, Part.TailRotor, "Apache_TailRotor", out Vector3 tailPivot);

        // 已存在的 prefab 就地更新：整個重建會換掉子物件的 ID，場景實例上手動加的元件/子物件 (碰撞體、飛彈發射點) 會失去對應而遺失
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
        GameObject root = exists ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("Apache");
        try
        {
            // 轉正：原模型機頂 (-Z) → 世界 +Y、機頭 (-Y) → 世界 +Z
            Transform model = GetOrCreateChild(root.transform, "Model");
            model.localRotation = Quaternion.LookRotation(new Vector3(0f, -1f, 0f), new Vector3(0f, 0f, -1f));

            SetupPart(model, "Body", body, materials, Vector3.zero);
            Transform main = SetupPart(model, "MainRotor", mainRotor, materials, mainPivot);
            Transform tail = SetupPart(model, "TailRotor", tailRotor, materials, tailPivot);
            if (main.GetComponent<RotorSpin>() == null) AddSpin(main, Vector3.forward, 1080f);
            if (tail.GetComponent<RotorSpin>() == null) AddSpin(tail, Vector3.right, 2160f);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            if (exists) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[ApachePrefabBuilder] 已更新 " + PrefabPath + "（主旋翼零件 " + mainCount + " 個、尾旋翼零件 " + tailCount + " 個）");
    }

    private enum Part { Body, MainRotor, TailRotor }

    // 以 union-find 找出互不相連的零件，再依零件中心決定每個頂點屬於哪一部分
    private static int[] ClassifyVertices(Mesh mesh, out int mainCount, out int tailCount)
    {
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        int[] parent = new int[vertices.Length];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;

        Dictionary<Vector3Int, int> byPosition = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3Int key = Vector3Int.RoundToInt(vertices[i] * WeldPrecision);
            if (byPosition.TryGetValue(key, out int other)) Union(parent, i, other);
            else byPosition[key] = i;
        }
        for (int t = 0; t < triangles.Length; t += 3)
        {
            Union(parent, triangles[t], triangles[t + 1]);
            Union(parent, triangles[t], triangles[t + 2]);
        }

        Dictionary<int, Bounds> componentBounds = new Dictionary<int, Bounds>();
        for (int i = 0; i < vertices.Length; i++)
        {
            int rootIndex = Find(parent, i);
            if (componentBounds.TryGetValue(rootIndex, out Bounds bounds)) bounds.Encapsulate(vertices[i]);
            else bounds = new Bounds(vertices[i], Vector3.zero);
            componentBounds[rootIndex] = bounds;
        }

        Dictionary<int, Part> componentPart = new Dictionary<int, Part>();
        mainCount = 0;
        tailCount = 0;
        foreach (KeyValuePair<int, Bounds> pair in componentBounds)
        {
            Vector3 center = pair.Value.center;
            Part part = Part.Body;
            if (center.z >= MainRotorMinZ && center.z <= MainRotorMaxZ)
            {
                part = Part.MainRotor;
                mainCount++;
            }
            else if (center.x > TailRotorMinX
                     && Mathf.Abs(center.y - TailRotorHub.y) < TailRotorRange
                     && Mathf.Abs(center.z - TailRotorHub.z) < TailRotorRange)
            {
                part = Part.TailRotor;
                tailCount++;
            }
            componentPart[pair.Key] = part;
        }

        int[] partOf = new int[vertices.Length];
        for (int i = 0; i < vertices.Length; i++) partOf[i] = (int)componentPart[Find(parent, i)];
        return partOf;
    }

    // 只保留屬於指定部分的三角形，重新編號頂點；旋翼的頂點平移到以槳轂為原點 (pivot 回傳原位置)
    private static Mesh ExtractMesh(Mesh source, int[] partOf, Part part, string name, out Vector3 pivot)
    {
        Vector3[] vertices = source.vertices;
        Vector3[] normals = source.normals;
        Vector4[] tangents = source.tangents;
        Vector2[] uv = source.uv;
        Color[] colors = source.colors;

        Dictionary<int, int> remap = new Dictionary<int, int>();
        List<int>[] subMeshTriangles = new List<int>[source.subMeshCount];
        for (int s = 0; s < source.subMeshCount; s++)
        {
            subMeshTriangles[s] = new List<int>();
            int[] triangles = source.GetTriangles(s);
            for (int t = 0; t < triangles.Length; t++)
            {
                int index = triangles[t];
                if (partOf[index] != (int)part) continue;
                if (!remap.TryGetValue(index, out int newIndex))
                {
                    newIndex = remap.Count;
                    remap[index] = newIndex;
                }
                subMeshTriangles[s].Add(newIndex);
            }
        }

        int count = remap.Count;
        Vector3[] newVertices = new Vector3[count];
        Bounds bounds = new Bounds();
        bool boundsInitialized = false;
        foreach (KeyValuePair<int, int> pair in remap)
        {
            newVertices[pair.Value] = vertices[pair.Key];
            if (!boundsInitialized) { bounds = new Bounds(vertices[pair.Key], Vector3.zero); boundsInitialized = true; }
            else bounds.Encapsulate(vertices[pair.Key]);
        }

        // 旋翼槳葉左右對稱，零件範圍中心即為槳轂 (旋轉中心)
        pivot = part == Part.Body ? Vector3.zero : bounds.center;
        for (int i = 0; i < count; i++) newVertices[i] -= pivot;

        Mesh mesh = new Mesh { name = name };
        mesh.indexFormat = count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices = newVertices;
        if (normals.Length > 0) mesh.normals = Pick(normals, remap, count);
        if (tangents.Length > 0) mesh.tangents = Pick(tangents, remap, count);
        if (uv.Length > 0) mesh.uv = Pick(uv, remap, count);
        if (colors.Length > 0) mesh.colors = Pick(colors, remap, count);
        mesh.subMeshCount = source.subMeshCount;
        for (int s = 0; s < source.subMeshCount; s++) mesh.SetTriangles(subMeshTriangles[s], s);
        mesh.RecalculateBounds();

        return SaveMesh(mesh, OutputFolder + "/" + name + ".asset");
    }

    private static T[] Pick<T>(T[] source, Dictionary<int, int> remap, int count)
    {
        T[] result = new T[count];
        foreach (KeyValuePair<int, int> pair in remap) result[pair.Value] = source[pair.Key];
        return result;
    }

    // 覆寫既有 Mesh 資產內容而不是刪掉重建，保留 GUID，已引用它的 prefab 不會斷掉
    private static Mesh SaveMesh(Mesh mesh, string path)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        EditorUtility.CopySerialized(mesh, existing);
        Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static Transform SetupPart(Transform parent, string name, Mesh mesh, Material[] materials, Vector3 localPosition)
    {
        Transform part = GetOrCreateChild(parent, name);
        part.localPosition = localPosition;

        MeshFilter filter = part.GetComponent<MeshFilter>();
        if (filter == null) filter = part.gameObject.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        MeshRenderer renderer = part.GetComponent<MeshRenderer>();
        if (renderer == null) renderer = part.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = materials;
        return part;
    }

    private static Transform GetOrCreateChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) return child;

        child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static void AddSpin(Transform rotor, Vector3 localAxis, float degreesPerSecond)
    {
        RotorSpin spin = rotor.gameObject.AddComponent<RotorSpin>();
        SerializedObject serialized = new SerializedObject(spin);
        serialized.FindProperty("localAxis").vector3Value = localAxis;
        serialized.FindProperty("degreesPerSecond").floatValue = degreesPerSecond;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static int Find(int[] parent, int x)
    {
        while (parent[x] != x)
        {
            parent[x] = parent[parent[x]];
            x = parent[x];
        }
        return x;
    }

    private static void Union(int[] parent, int a, int b)
    {
        a = Find(parent, a);
        b = Find(parent, b);
        if (a != b) parent[a] = b;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
    }
}
#endif
