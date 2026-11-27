#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 角色嘴巴破圖的通用修正 (RioMouthBuilder 的做法推廣到每個角色)：
// 模型的嘴巴片取樣自 EyeMouth 貼圖左下角 (UV 0~0.25)，但那一格在原檔是空的 — 原作是遊戲中把共用嘴型圖集
// (Character_Mouth_High，8×8 格) 的某一格疊上去。這裡把指定嘴型烘進放大的 EyeMouth 貼圖，另存一份材質換到模型上。
// 嘴型圖集背景透明、材質又不能開 Alpha 裁切，所以嘴巴區先填滿膚色 (從臉部貼圖上最靠近嘴巴的點取色) 再疊嘴型
public static class CharacterMouthBaker
{
    private const string MouthAtlasPath = "Assets/STG game/resources/model/Common_Character_Mouth/Character_Mouth_High.png";
    private const string TextureProperty = "baseColorTexture";
    private const int OutputSize = 1024;
    // 所有角色的嘴巴 UV 都落在貼圖左下角 0~0.25 的區域
    private const float MouthUvSize = 0.25f;

    // 把 model 底下所有 EyeMouth 材質換成烘好指定嘴型的版本 (烘好的貼圖與材質存到 outputFolder，重複執行會覆寫)
    public static void ApplyTo(GameObject model, int mouthCode, string outputFolder)
    {
        if (!MouthTile.IsValid(mouthCode))
        {
            Debug.LogError("[CharacterMouthBaker] 嘴型編號不在 8×8 圖集範圍內：" + mouthCode);
            return;
        }

        Texture2D atlas = LoadPng(MouthAtlasPath);
        if (atlas == null)
        {
            Debug.LogError("[CharacterMouthBaker] 找不到嘴型圖集 " + MouthAtlasPath);
            return;
        }

        Dictionary<Material, Material> baked = new Dictionary<Material, Material>();
        foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null || !materials[i].name.EndsWith("EyeMouth")) continue;

                if (!baked.TryGetValue(materials[i], out Material replacement))
                {
                    replacement = Bake(renderer, i, atlas, mouthCode, outputFolder);
                    baked[materials[i]] = replacement;
                }

                if (replacement == null) continue;
                materials[i] = replacement;
                changed = true;
            }

            if (changed) renderer.sharedMaterials = materials;
        }

        Object.DestroyImmediate(atlas);
    }

    /// <summary>
    /// 放大 EyeMouth 貼圖 (最近點取樣，眼睛色塊維持原樣)，左下角嘴巴區換成「膚色 + 嘴型圖集那一格」。
    /// 貼圖與圖集都是正方形；像素原點在左下 (Texture2D)，圖集格子的位置以左上為原點 (MouthTile)
    /// </summary>
    public static Color32[] Compose(Color32[] source, int sourceSize, Color32[] atlas, int atlasSize, int mouthCode, Color32 skin, int outputSize, float mouthUvSize)
    {
        Color32[] output = new Color32[outputSize * outputSize];
        for (int y = 0; y < outputSize; y++)
        {
            int sy = y * sourceSize / outputSize;
            for (int x = 0; x < outputSize; x++) output[y * outputSize + x] = source[sy * sourceSize + x * sourceSize / outputSize];
        }

        RectInt tile = MouthTile.ToAtlasPixels(mouthCode, atlasSize);
        int tileBottom = atlasSize - tile.y - tile.height;
        int mouthSize = Mathf.RoundToInt(outputSize * mouthUvSize);
        for (int y = 0; y < mouthSize; y++)
        {
            int ay = tileBottom + y * tile.height / mouthSize;
            for (int x = 0; x < mouthSize; x++)
            {
                int ax = tile.x + x * tile.width / mouthSize;
                output[y * outputSize + x] = OverSkin(atlas[ay * atlasSize + ax], skin);
            }
        }

        return output;
    }

    private static Material Bake(SkinnedMeshRenderer renderer, int submesh, Texture2D atlas, int mouthCode, string outputFolder)
    {
        Material original = renderer.sharedMaterials[submesh];
        Texture eyeMouthTexture = original.GetTexture(TextureProperty);
        if (eyeMouthTexture == null)
        {
            Debug.LogWarning("[CharacterMouthBaker] " + original.name + " 沒有貼圖", original);
            return null;
        }

        Texture2D source = ReadTexture(eyeMouthTexture, eyeMouthTexture.width);
        Color32 skin = SampleSkin(renderer, submesh);
        Color32[] pixels = Compose(source.GetPixels32(), source.width, atlas.GetPixels32(), atlas.width, mouthCode, skin, OutputSize, MouthUvSize);
        Object.DestroyImmediate(source);

        string baseName = original.name + "_Mouth" + mouthCode;
        string texturePath = outputFolder + "/" + baseName + ".png";
        Texture2D output = new Texture2D(OutputSize, OutputSize, TextureFormat.RGBA32, false);
        output.SetPixels32(pixels);
        File.WriteAllBytes(texturePath, output.EncodeToPNG());
        Object.DestroyImmediate(output);
        AssetDatabase.ImportAsset(texturePath);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = OutputSize;
        importer.SaveAndReimport();

        // 材質沿用原本 glTF 材質的光照與不透明設定，只換貼圖
        string materialPath = outputFolder + "/" + baseName + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(original);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else
        {
            material.CopyPropertiesFromMaterial(original);
        }

        material.SetTexture(TextureProperty, AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        EditorUtility.SetDirty(material);
        return material;
    }

    // 嘴巴片的中心 (EyeMouth 子網格裡 UV 落在嘴巴區的頂點) 最近的臉部頂點，在它的貼圖上取色當膚色
    private static Color32 SampleSkin(SkinnedMeshRenderer renderer, int mouthSubmesh)
    {
        Color32 fallback = new Color32(255, 225, 216, 255);
        Mesh mesh = renderer.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        Vector2[] uvs = mesh.uv;
        if (uvs.Length != vertices.Length) return fallback;

        Vector3 mouthCenter = Vector3.zero;
        int mouthCount = 0;
        foreach (int index in mesh.GetTriangles(mouthSubmesh))
        {
            if (uvs[index].x > MouthUvSize || uvs[index].y > MouthUvSize) continue;
            mouthCenter += vertices[index];
            mouthCount++;
        }

        if (mouthCount == 0) return fallback;
        mouthCenter /= mouthCount;

        Material[] materials = renderer.sharedMaterials;
        float closest = float.MaxValue;
        int closestVertex = -1;
        Material closestMaterial = null;
        for (int s = 0; s < mesh.subMeshCount && s < materials.Length; s++)
        {
            if (materials[s] == null || materials[s].name.Contains("Eye") || materials[s].GetTexture(TextureProperty) == null) continue;

            foreach (int index in mesh.GetTriangles(s))
            {
                float distance = (vertices[index] - mouthCenter).sqrMagnitude;
                if (distance >= closest) continue;
                closest = distance;
                closestVertex = index;
                closestMaterial = materials[s];
            }
        }

        if (closestMaterial == null) return fallback;

        Texture texture = closestMaterial.GetTexture(TextureProperty);
        Texture2D readable = ReadTexture(texture, Mathf.Min(texture.width, 1024));
        Vector2 uv = uvs[closestVertex];
        Color32 skin = readable.GetPixel(Mathf.Clamp((int)(Mathf.Repeat(uv.x, 1f) * readable.width), 0, readable.width - 1), Mathf.Clamp((int)(Mathf.Repeat(uv.y, 1f) * readable.height), 0, readable.height - 1));
        Object.DestroyImmediate(readable);
        skin.a = 255;
        return skin;
    }

    // glb 內嵌的貼圖無法直接讀檔，透過 RenderTexture 讀回像素 (sRGB 讀寫，顏色不變)
    private static Texture2D ReadTexture(Texture texture, int size)
    {
        int height = Mathf.Max(1, size * texture.height / texture.width);
        RenderTexture temporary = RenderTexture.GetTemporary(size, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        // Blit 會把 active 換成目標，要在 Blit 之前記下原本的
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(texture, temporary);
        RenderTexture.active = temporary;
        Texture2D readable = new Texture2D(size, height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, size, height), 0, 0);
        readable.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(temporary);
        return readable;
    }

    private static Color32 OverSkin(Color32 mouth, Color32 skin)
    {
        float a = mouth.a / 255f;
        return new Color32(
            (byte)Mathf.RoundToInt(Mathf.Lerp(skin.r, mouth.r, a)),
            (byte)Mathf.RoundToInt(Mathf.Lerp(skin.g, mouth.g, a)),
            (byte)Mathf.RoundToInt(Mathf.Lerp(skin.b, mouth.b, a)),
            255);
    }

    private static Texture2D LoadPng(string path)
    {
        if (!File.Exists(path)) return null;

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(File.ReadAllBytes(path));
        return texture;
    }
}
#endif
