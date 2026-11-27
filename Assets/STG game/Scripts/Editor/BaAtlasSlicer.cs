#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// 把蔚藍檔案的 UI 圖集 (BattleUI/Atlas/*.png) 依 NGUI 圖集定義 (_MX/UIs/Atlas/*.asset) 切成 Unity sprite：
// 圖片改成 Sprite (Multiple)，每一格用原本的名稱 (例如 Combat_Time_Icon)，並帶入九宮格邊界，可直接給 uGUI Image 使用。
// sprite 留在原本的大圖裡 (同一張貼圖的 UI 可以合批)。可重複執行：同名的 sprite 沿用原本的 ID，已經引用它的 UI 不會斷掉
public static class BaAtlasSlicer
{
    private const string AtlasDefinitionFolder = "Assets/STG game/resources/prefab/UI_Prefabs/ExportedProject/Assets/_MX/UIs/Atlas";
    private const string AtlasTextureFolder = "Assets/STG game/resources/model/BattleUI/Atlas";
    // UI 要清楚：不壓縮 mipmap、不縮小圖 (最大圖集為 4096)
    private const int MaxTextureSize = 4096;

    [MenuItem("Tools/STG/Slice BA UI Atlases")]
    private static void SliceAll()
    {
        int atlases = 0;
        int sprites = 0;
        foreach (string definitionPath in Directory.GetFiles(AtlasDefinitionFolder, "*.asset"))
        {
            string atlasName = Path.GetFileNameWithoutExtension(definitionPath);
            string texturePath = AtlasTextureFolder + "/" + atlasName + ".png";
            if (!File.Exists(texturePath))
            {
                Debug.LogWarning("[BaAtlasSlicer] 找不到圖集圖片 " + texturePath);
                continue;
            }

            NguiAtlas.Entry[] entries = NguiAtlas.Parse(File.ReadAllText(definitionPath));
            sprites += Slice(texturePath, entries);
            atlases++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[BaAtlasSlicer] 已切 " + atlases + " 張圖集，共 " + sprites + " 個 sprite");
    }

    private static int Slice(string texturePath, NguiAtlas.Entry[] entries)
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = MaxTextureSize;
        importer.SaveAndReimport();

        SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        // 同名沿用原本的 spriteID：重切時已引用該 sprite 的 Image 不會失去參照
        Dictionary<string, GUID> existingIds = new Dictionary<string, GUID>();
        foreach (SpriteRect existing in provider.GetSpriteRects()) existingIds[existing.name] = existing.spriteID;

        importer.GetSourceTextureWidthAndHeight(out _, out int textureHeight);
        List<SpriteRect> rects = new List<SpriteRect>(entries.Length);
        HashSet<string> usedNames = new HashSet<string>();
        foreach (NguiAtlas.Entry entry in entries)
        {
            if (!usedNames.Add(entry.name)) continue;

            rects.Add(new SpriteRect
            {
                name = entry.name,
                spriteID = existingIds.TryGetValue(entry.name, out GUID id) ? id : GUID.Generate(),
                rect = NguiAtlas.ToUnityRect(entry.rect, textureHeight),
                border = NguiAtlas.ToUnityBorder(entry),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
            });
        }

        provider.SetSpriteRects(rects.ToArray());

        // 名稱 ↔ ID 的對照也要一起寫入，sprite 的子資源名稱才會正確
        ISpriteNameFileIdDataProvider nameProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        List<SpriteNameFileIdPair> pairs = new List<SpriteNameFileIdPair>(rects.Count);
        foreach (SpriteRect rect in rects) pairs.Add(new SpriteNameFileIdPair(rect.name, rect.spriteID));
        nameProvider.SetNameFileIdPairs(pairs);

        provider.Apply();
        importer.SaveAndReimport();
        return rects.Count;
    }
}
#endif
