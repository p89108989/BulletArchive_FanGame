#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 在敵人 Prefab 底下建立頭上血條 (WorldHealthBar)：深色底板 + 紅色填充兩片平面，不受光照 (夜晚也看得清楚)
public static class WorldHealthBarBuilder
{
    private const string MaterialFolder = "Assets/STG game/resources/prefab/Materials";
    private static readonly Color BackColor = new Color(0.08f, 0.08f, 0.1f, 1f);
    private static readonly Color FillColor = new Color(0.9f, 0.18f, 0.2f, 1f);
    // 底板比填充多出的邊框 (m)
    private const float BorderSize = 0.15f;

    // localPosition：血條中心相對 parent 的位置；width/height 為世界尺寸 (parent 需無縮放)
    public static WorldHealthBar Create(Transform parent, Vector3 localPosition, float width, float height)
    {
        Material back = GetOrCreateMaterial("HealthBar_Back", BackColor);
        Material fill = GetOrCreateMaterial("HealthBar_Fill", FillColor);
        Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

        GameObject root = new GameObject("HealthBar");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPosition;

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        CreateQuad(visual.transform, "Back", quad, back, new Vector3(width + BorderSize * 2f, height + BorderSize * 2f, 1f));
        Transform fillQuad = CreateQuad(visual.transform, "Fill", quad, fill, new Vector3(width, height, 1f));

        WorldHealthBar bar = root.AddComponent<WorldHealthBar>();
        SerializedObject serialized = new SerializedObject(bar);
        serialized.FindProperty("visual").objectReferenceValue = visual;
        serialized.FindProperty("fill").objectReferenceValue = fillQuad;
        serialized.FindProperty("width").floatValue = width;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return bar;
    }

    private static Transform CreateQuad(Transform parent, string name, Mesh mesh, Material material, Vector3 scale)
    {
        GameObject quad = new GameObject(name);
        quad.transform.SetParent(parent, false);
        quad.transform.localScale = scale;
        quad.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = quad.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return quad.transform;
    }

    private static Material GetOrCreateMaterial(string name, Color color)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(material);
        return material;
    }
}
#endif
