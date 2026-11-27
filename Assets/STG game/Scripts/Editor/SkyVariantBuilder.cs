#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 由白天天空貼圖產生黃昏與黑夜兩張貼圖 (SkyColorGrading)，並在目前場景接好時段切換：
// 天空球改用 STG/SkyBlend 材質、掛上 SkyEnvironment (白天/黃昏/黑夜)、加進 CombatPhaseController 的監聽清單。
// 可重複執行，已存在的貼圖與材質會覆寫
public static class SkyVariantBuilder
{
    private const string SkyFolder = "Assets/STG game/resources/skybox";
    private const string DayTexturePath = SkyFolder + "/skybox_skydays_3.png";
    private const string DuskTexturePath = SkyFolder + "/skybox_skydays_3_dusk.png";
    private const string NightTexturePath = SkyFolder + "/skybox_skydays_3_night.png";
    private const string SourceMaterialPath = SkyFolder + "/Materials/MI_Skybox_v3_Inst.mat";
    private const string BlendMaterialPath = SkyFolder + "/Materials/MI_Skybox_Blend.mat";
    private const string SkyObjectName = "Skybox";
    // 星星位置固定，每次重建結果一致
    private const int StarSeed = 20261123;

    // 黑夜時 Rio 頭頂的補光 (Rio 的頭髮與衣服偏深色，在暗場景裡容易看不清楚)
    private const string CharacterLightName = "NightFillLight";
    private const float NightCharacterLight = 25f;
    // 太陽從相機那側 (場地 +Z) 斜上方打下來：俯視相機看到的是角色受光面，影子往畫面上方倒
    private static readonly Quaternion SunRotation = Quaternion.Euler(50f, 150f, 0f);
    // 天空盒算出的環境光只有 0.2~0.3，角色背光面會發黑；白天、黃昏額外疊加環境光 (黃昏偏橘)，黑夜維持暗、靠角色補光
    private static readonly Color DayAmbientTint = new Color(0.3f, 0.3f, 0.32f);
    private static readonly Color DuskAmbientTint = new Color(0.32f, 0.22f, 0.16f);
    private static readonly Vector3 CharacterLightOffset = new Vector3(0f, 6f, 0f);
    private static readonly Color CharacterLightColor = new Color(0.75f, 0.85f, 1f);

    private const int DayPreset = 0;
    private const int DuskPreset = 1;
    private const int NightPreset = 2;

    [MenuItem("Tools/STG/Build Sky Variants")]
    private static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[SkyVariantBuilder] 請先離開 Play Mode 再執行。");
            return;
        }

        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
        {
            Debug.LogWarning("[SkyVariantBuilder] 場景有未存的變更，請先存檔或還原後再執行 (本工具會存檔場景)");
            return;
        }

        Texture2D day = AssetDatabase.LoadAssetAtPath<Texture2D>(DayTexturePath);
        if (day == null)
        {
            Debug.LogError("[SkyVariantBuilder] 找不到 " + DayTexturePath);
            return;
        }

        Texture2D dusk = WriteVariant(DuskTexturePath, false);
        Texture2D night = WriteVariant(NightTexturePath, true);
        Material blend = GetOrCreateBlendMaterial(day);
        SetupScene(blend, day, dusk, night);
        Debug.Log("[SkyVariantBuilder] 已產生黃昏/黑夜天空並接好場景");
    }

    // 直接讀原始 PNG (不需要改原貼圖的 Read/Write 設定)，逐像素調色後存成新 PNG，匯入設定沿用白天貼圖
    private static Texture2D WriteVariant(string path, bool night)
    {
        Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        source.LoadImage(File.ReadAllBytes(DayTexturePath));
        Color[] pixels = source.GetPixels();
        System.Random random = new System.Random(StarSeed);
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = night ? SkyColorGrading.Night(pixels[i], (float)random.NextDouble()) : SkyColorGrading.Dusk(pixels[i]);
        }

        source.SetPixels(pixels);
        File.WriteAllBytes(path, source.EncodeToPNG());
        Object.DestroyImmediate(source);

        AssetDatabase.ImportAsset(path);
        TextureImporter dayImporter = (TextureImporter)AssetImporter.GetAtPath(DayTexturePath);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        EditorUtility.CopySerialized(dayImporter, importer);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Material GetOrCreateBlendMaterial(Texture2D day)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(BlendMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("STG/SkyBlend"));
            AssetDatabase.CreateAsset(material, BlendMaterialPath);
        }

        material.SetTexture("_MainTex", day);
        material.SetTexture("_BlendTex", day);
        material.SetFloat("_Blend", 1f);
        // 天空球原本的剔除設定 (從球內往外看) 要沿用，否則會看不到
        Material source = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialPath);
        if (source != null && source.HasProperty("_Cull")) material.SetFloat("_Cull", source.GetFloat("_Cull"));
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static void SetupScene(Material blend, Texture2D day, Texture2D dusk, Texture2D night)
    {
        GameObject skyObject = GameObject.Find(SkyObjectName);
        if (skyObject == null || !skyObject.TryGetComponent(out Renderer skyRenderer))
        {
            Debug.LogError("[SkyVariantBuilder] 場景中找不到天空球 " + SkyObjectName);
            return;
        }

        skyRenderer.sharedMaterial = blend;
        SkyEnvironment environment = skyObject.GetComponent<SkyEnvironment>();
        if (environment == null) environment = skyObject.AddComponent<SkyEnvironment>();

        Light sun = RenderSettings.sun;
        if (sun != null) sun.transform.rotation = SunRotation;
        float dayIntensity = sun != null ? sun.intensity : 1f;
        Color dayColor = sun != null ? sun.color : Color.white;
        float transition = 2f;
        CameraPoseTransition cameraTransition = Object.FindFirstObjectByType<CameraPoseTransition>();
        if (cameraTransition != null) transition = new SerializedObject(cameraTransition).FindProperty("transitionDuration").floatValue;

        SerializedObject serialized = new SerializedObject(environment);
        serialized.FindProperty("skyRenderer").objectReferenceValue = skyRenderer;
        serialized.FindProperty("sun").objectReferenceValue = sun;
        Light characterLight = GetOrCreateCharacterLight();
        SerializedProperty characterLights = serialized.FindProperty("characterLights");
        characterLights.arraySize = characterLight != null ? 1 : 0;
        if (characterLight != null) characterLights.GetArrayElementAtIndex(0).objectReferenceValue = characterLight;
        serialized.FindProperty("normalPreset").intValue = DayPreset;
        serialized.FindProperty("bossPreset").intValue = NightPreset;
        serialized.FindProperty("transitionDuration").floatValue = transition;
        SerializedProperty presets = serialized.FindProperty("presets");
        presets.arraySize = 3;
        WritePreset(presets.GetArrayElementAtIndex(DayPreset), SkyEnvironment.CreatePreset("Day", day, dayColor, dayIntensity, 1f, DayAmbientTint, 0f));
        WritePreset(presets.GetArrayElementAtIndex(DuskPreset), SkyEnvironment.CreatePreset("Dusk", dusk, new Color(1f, 0.62f, 0.4f), dayIntensity * 0.85f, 0.7f, DuskAmbientTint, 0f));
        WritePreset(presets.GetArrayElementAtIndex(NightPreset), SkyEnvironment.CreatePreset("Night", night, new Color(0.55f, 0.65f, 1f), dayIntensity * 0.6f, 0.6f, new Color(0.02f, 0.03f, 0.08f), NightCharacterLight));
        serialized.ApplyModifiedPropertiesWithoutUndo();

        AddPhaseListener(environment);
        EditorSceneManager.MarkSceneDirty(skyObject.scene);
        EditorSceneManager.SaveScene(skyObject.scene);
    }

    // 掛在玩家的移動根物件下，跟著 Rio 走；預設關閉，由 SkyEnvironment 依時段開關
    private static Light GetOrCreateCharacterLight()
    {
        PlayerMovement player = Object.FindFirstObjectByType<PlayerMovement>();
        if (player == null)
        {
            Debug.LogWarning("[SkyVariantBuilder] 場景中找不到 PlayerMovement，略過角色補光");
            return null;
        }

        Transform existing = player.transform.Find(CharacterLightName);
        Light light = existing != null ? existing.GetComponent<Light>() : null;
        if (light == null)
        {
            light = new GameObject(CharacterLightName).AddComponent<Light>();
            light.transform.SetParent(player.transform, false);
        }

        // 玩家根物件有縮放 (Rio 是 10 倍)，用世界座標的偏移放燈，高度才不會跟著被放大
        light.transform.position = player.transform.position + CharacterLightOffset;
        light.type = LightType.Point;
        light.color = CharacterLightColor;
        light.range = 14f;
        light.intensity = 0f;
        light.shadows = LightShadows.None;
        light.enabled = false;
        return light;
    }

    private static void WritePreset(SerializedProperty property, SkyEnvironment.SkyPreset preset)
    {
        property.FindPropertyRelative("name").stringValue = preset.name;
        property.FindPropertyRelative("skyTexture").objectReferenceValue = preset.skyTexture;
        property.FindPropertyRelative("sunColor").colorValue = preset.sunColor;
        property.FindPropertyRelative("sunIntensity").floatValue = preset.sunIntensity;
        property.FindPropertyRelative("ambientIntensity").floatValue = preset.ambientIntensity;
        property.FindPropertyRelative("ambientTint").colorValue = preset.ambientTint;
        property.FindPropertyRelative("characterLightIntensity").floatValue = preset.characterLightIntensity;
    }

    private static void AddPhaseListener(SkyEnvironment environment)
    {
        CombatPhaseController controller = Object.FindFirstObjectByType<CombatPhaseController>();
        if (controller == null)
        {
            Debug.LogWarning("[SkyVariantBuilder] 場景中沒有 CombatPhaseController，Boss 戰不會切換天色");
            return;
        }

        SerializedObject serialized = new SerializedObject(controller);
        SerializedProperty listeners = serialized.FindProperty("phaseListeners");
        for (int i = 0; i < listeners.arraySize; i++)
        {
            if (listeners.GetArrayElementAtIndex(i).objectReferenceValue == environment) return;
        }

        listeners.arraySize++;
        listeners.GetArrayElementAtIndex(listeners.arraySize - 1).objectReferenceValue = environment;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
