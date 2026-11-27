#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// glb 匯出時沒寫 metallicFactor，glTF 預設是 1 (全金屬)：金屬沒有漫射光，只反射環境，模型會整個發灰。
// 這裡每個材質另存一份非金屬複本 (存到 folder，重複執行會覆寫) 換到模型上，原本 glb 的材質不受影響
public static class NonMetalMaterials
{
    private const string MetallicProperty = "metallicFactor";

    public static void ApplyTo(GameObject model, string folder)
    {
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null || !materials[i].HasProperty(MetallicProperty)) continue;
                materials[i] = GetCopy(materials[i], folder);
                changed = true;
            }

            if (changed) renderer.sharedMaterials = materials;
        }
    }

    private static Material GetCopy(Material source, string folder)
    {
        string path = folder + "/" + source.name + ".mat";
        // 已經是這個資料夾裡的複本 (例如嘴型烘焙換上的材質) 就直接改它
        if (AssetDatabase.GetAssetPath(source) == path)
        {
            source.SetFloat(MetallicProperty, 0f);
            EditorUtility.SetDirty(source);
            return source;
        }

        Material copy = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (copy == null)
        {
            copy = new Material(source);
            AssetDatabase.CreateAsset(copy, path);
        }
        else
        {
            copy.shader = source.shader;
            copy.CopyPropertiesFromMaterial(source);
        }

        copy.SetFloat(MetallicProperty, 0f);
        EditorUtility.SetDirty(copy);
        return copy;
    }
}
#endif
