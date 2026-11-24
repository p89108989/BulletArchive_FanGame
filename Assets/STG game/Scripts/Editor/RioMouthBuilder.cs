#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

// Rio 嘴巴破圖的修正：模型的嘴巴 (EyeMouth 子網格裡的嘴巴片) 取樣自家 128×128 EyeMouth 貼圖的左下角，
// 但那一格是空的 — 原作是在遊戲中把共用嘴型圖集 (Character_Mouth_High，8×8 格) 的某一格疊上去。
// 這裡把指定嘴型 (BA 編號，見 MouthTile) 烘進放大的 EyeMouth 貼圖 (嘴巴那格 256px)，套到 Rio 的 EyeMouth 材質，
// 沿用原本 glTF 的光照與不透明材質設定，不需要另寫 Shader。
// 嘴型圖集的背景是透明的，但這個材質不能用 Alpha 裁切 (網格的頂點色 Alpha 另有用途，開裁切會把整片眼睛嘴巴切掉)，
// 所以先把嘴巴那格填滿臉部膚色、再疊上嘴型，嘴巴片看起來就是「皮膚 + 嘴」
// 之後要隨動畫事件 SetMouthTile 換嘴型時，可用同樣方式每個編號各烘一張，執行時換貼圖即可
public static class RioMouthBuilder
{
    private const string SourceEyeMouthPath = "Assets/STG game/resources/model/CH0331_調月リオ/bundles/Assets/_MX/Characters/CH0331/Model/Texture/CH0331_EyeMouth.png";
    private const string MouthAtlasPath = "Assets/STG game/resources/model/Common_Character_Mouth/Character_Mouth_High.png";
    private const string OutputFolder = "Assets/STG game/resources/prefab/Materials";
    private static readonly string[] EyeMouthMaterialPaths =
    {
        OutputFolder + "/Rio_CH0331_EyeMouth.mat",
        OutputFolder + "/Rio_FX_CH0331_EyeMouth.mat",
    };

    // 待機、走路等動畫使用的嘴型
    private const int DefaultMouthCode = 401;
    private const int OutputSize = 1024;
    // 模型的嘴巴 UV 落在貼圖左下角 0~0.25 的區域 (中心約 0.125, 0.13)
    private const float MouthUvSize = 0.25f;
    // 臉部貼圖 (image_1) 嘴巴周圍的膚色，均勻單色
    private static readonly Color32 SkinColor = new Color32(255, 225, 216, 255);

    [MenuItem("Tools/STG/Build Rio Mouth")]
    private static void Build()
    {
        Texture2D baked = BakeEyeMouth(DefaultMouthCode);
        if (baked == null) return;

        foreach (string path in EyeMouthMaterialPaths)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Debug.LogWarning("[RioMouthBuilder] 找不到材質 " + path);
                continue;
            }

            material.SetTexture("baseColorTexture", baked);
            // 維持原本的不透明設定 (先前若開過 Alpha 裁切在這裡還原)
            material.SetFloat("_AlphaClip", 0f);
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            EditorUtility.SetDirty(material);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[RioMouthBuilder] 已套用嘴型 " + DefaultMouthCode);
    }

    // 放大原本的 EyeMouth 貼圖 (最近點取樣，眼睛的色塊維持原樣)，再把嘴型圖集的那一格貼進左下角的嘴巴區
    private static Texture2D BakeEyeMouth(int mouthCode)
    {
        if (!MouthTile.IsValid(mouthCode))
        {
            Debug.LogError("[RioMouthBuilder] 嘴型編號不在 8×8 圖集範圍內：" + mouthCode);
            return null;
        }

        Texture2D source = LoadReadable(SourceEyeMouthPath);
        Texture2D atlas = LoadReadable(MouthAtlasPath);
        if (source == null || atlas == null)
        {
            Debug.LogError("[RioMouthBuilder] 找不到 EyeMouth 貼圖或嘴型圖集");
            return null;
        }

        Texture2D output = new Texture2D(OutputSize, OutputSize, TextureFormat.RGBA32, false);
        Color32[] sourcePixels = source.GetPixels32();
        Color32[] outputPixels = new Color32[OutputSize * OutputSize];
        for (int y = 0; y < OutputSize; y++)
        {
            int sy = y * source.height / OutputSize;
            for (int x = 0; x < OutputSize; x++)
            {
                outputPixels[y * OutputSize + x] = sourcePixels[sy * source.width + x * source.width / OutputSize];
            }
        }

        // 圖集格子的裁切範圍原點在左上；Texture2D 像素原點在左下，要換算
        RectInt tile = MouthTile.ToAtlasPixels(mouthCode, atlas.width);
        int tileBottom = atlas.height - tile.y - tile.height;
        int mouthSize = Mathf.RoundToInt(OutputSize * MouthUvSize);
        Color32[] atlasPixels = atlas.GetPixels32();
        for (int y = 0; y < mouthSize; y++)
        {
            int ay = tileBottom + y * tile.height / mouthSize;
            for (int x = 0; x < mouthSize; x++)
            {
                int ax = tile.x + x * tile.width / mouthSize;
                outputPixels[y * OutputSize + x] = OverSkin(atlasPixels[ay * atlas.width + ax]);
            }
        }

        output.SetPixels32(outputPixels);
        string outputPath = OutputFolder + "/Rio_CH0331_EyeMouth_Mouth" + mouthCode + ".png";
        File.WriteAllBytes(outputPath, output.EncodeToPNG());
        Object.DestroyImmediate(output);
        Object.DestroyImmediate(source);
        Object.DestroyImmediate(atlas);

        AssetDatabase.ImportAsset(outputPath);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(outputPath);
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = OutputSize;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
    }

    // 嘴型疊在膚色上 (依嘴型的 Alpha 混合)，結果不透明
    private static Color32 OverSkin(Color32 mouth)
    {
        float a = mouth.a / 255f;
        return new Color32(
            (byte)Mathf.RoundToInt(Mathf.Lerp(SkinColor.r, mouth.r, a)),
            (byte)Mathf.RoundToInt(Mathf.Lerp(SkinColor.g, mouth.g, a)),
            (byte)Mathf.RoundToInt(Mathf.Lerp(SkinColor.b, mouth.b, a)),
            255);
    }

    // 直接讀 PNG 檔，不需要改原圖的 Read/Write 匯入設定
    private static Texture2D LoadReadable(string path)
    {
        if (!File.Exists(path)) return null;

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(File.ReadAllBytes(path));
        return texture;
    }
}
#endif
