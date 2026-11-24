#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 建立 Boss 普通攻擊用的 Prefab，由 PerorozillaPrefabBuilder 一起呼叫：
// Boss_Debris_01~03 (brick_01~03 網格的碎塊，ArcProjectile 拋物線飛行)
public static class PerorozillaAttackBuilder
{
    private const string MeshFolder = "Assets/STG game/resources/model/Boss_Perorozilla/bundles/Assets/_MX/NPCs/Perorozilla_Original/Perorozilla_Original/Effect/Mesh/";
    private const string OutputFolder = "Assets/STG game/resources/prefab";
    private const string DebrisMaterialPath = OutputFolder + "/Boss_Debris.mat";

    private static readonly string[] DebrisMeshNames = { "brick_01", "brick_02", "brick_03" };
    // brick 網格長約 0.9，放大後約 9 m 的大塊碎石 (Boss 本體放大 10 倍)
    private const float DebrisScale = 10f;

    public static GameObject[] BuildDebris()
    {
        Material material = GetOrCreateDebrisMaterial();
        GameObject[] prefabs = new GameObject[DebrisMeshNames.Length];
        for (int i = 0; i < DebrisMeshNames.Length; i++)
        {
            GameObject root = new GameObject("Boss_Debris_0" + (i + 1));
            try
            {
                root.transform.localScale = Vector3.one * DebrisScale;
                CreateMeshObject(root.transform, "Mesh", FindMesh(DebrisMeshNames[i]), material);
                root.AddComponent<ArcProjectile>();
                prefabs[i] = PrefabUtility.SaveAsPrefabAsset(root, OutputFolder + "/" + root.name + ".prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
        return prefabs;
    }

    private static void CreateMeshObject(Transform parent, string name, Mesh mesh, Material material)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
    }

    // 灰褐色的石塊質感
    private static Material GetOrCreateDebrisMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(DebrisMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, DebrisMaterialPath);
        }

        material.SetColor("_BaseColor", new Color(0.55f, 0.48f, 0.42f));
        material.SetFloat("_Smoothness", 0.15f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Mesh FindMesh(string fileName)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(MeshFolder + fileName + ".glb"))
        {
            if (asset is Mesh mesh) return mesh;
        }

        Debug.LogError("[PerorozillaAttackBuilder] 找不到網格 " + fileName);
        return null;
    }
}
#endif
