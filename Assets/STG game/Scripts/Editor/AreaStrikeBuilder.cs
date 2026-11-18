#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 一鍵建立圈形範圍攻擊 prefab：外圈 + 由中心擴張的填充圓盤，貼圖與材質都由程式產生。
// 材質本身的顏色只是預設值，實際顏色由 AreaStrike 依屬性以 MaterialPropertyBlock 覆寫。
// 可重複執行：已存在的資源會沿用並以這裡的數值重新設定。
public static class AreaStrikeBuilder
{
    private const string OutputFolder = "Assets/STG game/resources/prefab/AreaStrike";
    private const string PrefabPath = "Assets/STG game/resources/prefab/AreaStrike.prefab";
    private const int TextureSize = 256;

    // 外圈線寬 (佔半徑比例)
    private const float RingThickness = 0.06f;
    // 外圈內部常駐的淡紅底色，讓玩家看得出整個危險範圍
    private const float RingInnerAlpha = 0.12f;

    private static readonly Color RingColor = new Color(2f, 0.15f, 0.1f, 1f);
    private static readonly Color FillColor = new Color(1f, 0.1f, 0.05f, 0.45f);

    [MenuItem("Tools/STG/Build Area Strike")]
    private static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[AreaStrikeBuilder] 請先離開 Play Mode 再執行。");
            return;
        }

        EnsureFolder(OutputFolder);

        Texture2D ringTexture = GetOrCreateTexture("AreaStrike_Ring_Tex", RingAlpha);
        Texture2D fillTexture = GetOrCreateTexture("AreaStrike_Fill_Tex", FillAlpha);
        Material ringMaterial = GetOrCreateMaterial("AreaStrike_Ring", ringTexture, RingColor, 1);
        Material fillMaterial = GetOrCreateMaterial("AreaStrike_Fill", fillTexture, FillColor, 0);

        GameObject root = new GameObject("AreaStrike");
        try
        {
            Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            Renderer ringRenderer = CreateFlatQuad(root.transform, "Ring", quad, ringMaterial, 0.02f);
            Renderer fillRenderer = CreateFlatQuad(root.transform, "Fill", quad, fillMaterial, 0.01f);
            ParticleSystem explosion = AreaStrikeExplosionBuilder.Build(root.transform, OutputFolder);

            AreaStrike strike = root.AddComponent<AreaStrike>();
            SerializedObject serialized = new SerializedObject(strike);
            serialized.FindProperty("fillDisc").objectReferenceValue = fillRenderer.transform;
            serialized.FindProperty("fillRenderer").objectReferenceValue = fillRenderer;
            serialized.FindProperty("ringRenderer").objectReferenceValue = ringRenderer;
            serialized.FindProperty("explosionParticles").objectReferenceValue = explosion;
            // 只偵測玩家：範圍內的地板、牆壁、砲台不會佔用判定緩衝區
            serialized.FindProperty("hitLayers").intValue = LayerMask.GetMask("Player");
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[AreaStrikeBuilder] 已更新 " + PrefabPath);
    }

    // 外圈：邊緣一圈實線，內部淡淡的底色
    private static float RingAlpha(float distance)
    {
        if (distance > 1f) return 0f;
        float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f - RingThickness - 0.02f, 1f - RingThickness, distance));
        float outerFade = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.985f, 1f, distance));
        return Mathf.Max(RingInnerAlpha, edge) * outerFade;
    }

    // 填充圓盤：越靠近邊緣越濃，擴張時前緣看起來像一道推進的波
    private static float FillAlpha(float distance)
    {
        if (distance > 1f) return 0f;
        float outerFade = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.97f, 1f, distance));
        return Mathf.Lerp(0.35f, 1f, distance * distance) * outerFade;
    }

    private static Renderer CreateFlatQuad(Transform parent, string name, Mesh mesh, Material material, float height)
    {
        GameObject quad = new GameObject(name);
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = new Vector3(0f, height, 0f);
        // Quad 預設面向 -Z，繞 X 轉 90 度後朝上平貼地面
        quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        quad.AddComponent<MeshFilter>().sharedMesh = mesh;

        MeshRenderer renderer = quad.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private static Texture2D GetOrCreateTexture(string name, System.Func<float, float> alphaByDistance)
    {
        string path = OutputFolder + "/" + name + ".asset";
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        bool isNew = texture == null;
        if (isNew) texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, true);

        texture.wrapMode = TextureWrapMode.Clamp;
        float half = (TextureSize - 1) * 0.5f;
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float distance = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alphaByDistance(distance)));
            }
        }
        texture.Apply();

        if (isNew) AssetDatabase.CreateAsset(texture, path);
        else EditorUtility.SetDirty(texture);
        return texture;
    }

    // URP Unlit 半透明材質；透過腳本設定時不會經過 ShaderGUI，所以混合狀態要手動寫齊
    private static Material GetOrCreateMaterial(string name, Texture2D texture, Color color, int queueOffset)
    {
        string path = OutputFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        // 外圈畫在填充之上
        material.renderQueue = (int)RenderQueue.Transparent + queueOffset;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
    }
}
#endif
