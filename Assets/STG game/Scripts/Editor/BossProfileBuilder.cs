#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 建立/更新各 Boss 的 BossProfile 資產 (Tools/STG/Build Boss Profiles)，並把場景的 BossEncounter 與相機切換、天色接起來。
// 已存在的資產只補上 Prefab，不覆蓋在 Inspector 調過的位置與相機數值；場景原本選的 Boss 不會被改掉
public static class BossProfileBuilder
{
    private const string ProfileFolder = "Assets/STG game/resources/prefab/BossProfiles";
    public const string PerorozillaProfilePath = ProfileFolder + "/Boss_Perorozilla.asset";
    public const string BinahProfilePath = ProfileFolder + "/Boss_Binah.asset";
    private const string PerorozillaPrefabPath = "Assets/STG game/resources/prefab/Boss_Perorozilla.prefab";
    // 血條左側的頭像 (蔚藍檔案總力戰 Boss 頭像)
    private const string PortraitFolder = "Assets/STG game/resources/model/BattleUI/02_Tactics/06_Raid/";
    private const string PerorozillaPortraitPath = PortraitFolder + "Portrait_Raidboss_Perorozilla.png";
    private const string BinahPortraitPath = PortraitFolder + "Portrait_Raidboss_Binah.png";

    // Perorozilla：沿用原本場景上的設定 (根物件在海面下 100 m、相機拉低看向遠方的 Boss)
    private static readonly Vector3 PerorozillaSpawn = new Vector3(-50f, -100f, -65f);
    private static readonly Vector3 PerorozillaCameraPosition = new Vector3(-50f, 45f, 85f);
    private static readonly Vector3 PerorozillaCameraRotation = new Vector3(20f, 180f, 0f);
    // 相機只看得到場地一部分，限制玩家在這個範圍內 (X / Z)，不會跑出畫面；Binah 相機看得到整個場地，不限制
    private static readonly Vector2 PerorozillaPlayerAreaMin = new Vector2(-104f, -4f);
    private static readonly Vector2 PerorozillaPlayerAreaMax = new Vector2(3f, 47f);

    // Binah：場地遠側邊緣外 (場地遠側邊緣 Z = -4)，根物件略低於地面：模型從原點往上長，身體下端是切面，
    // 離場地太遠時會從場地邊緣後方露出來，靠近場地讓場地邊緣擋住下端。
    // 相機：比一般視角高、往後退，俯角 45°，場地加上左右與遠側外圍都在畫面內 (Binah 會從這三面竄出)；水平朝向不變
    private static readonly Vector3 BinahSpawn = new Vector3(-50f, -5f, -15f);
    // Binah 戰在黃昏 (SkyEnvironment presets：0 白天、1 黃昏、2 黑夜)；Perorozilla 沿用 SkyEnvironment 預設的黑夜
    private const int BinahSkyPreset = 1;
    private static readonly Vector3 BinahCameraPosition = new Vector3(-50f, 95f, 105f);
    private static readonly Vector3 BinahCameraRotation = new Vector3(45f, 180f, 0f);

    [MenuItem("Tools/STG/Build Boss Profiles")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[BossProfileBuilder] 請先離開 Play Mode 再執行。");
            return;
        }

        if (!AssetDatabase.IsValidFolder(ProfileFolder)) AssetDatabase.CreateFolder("Assets/STG game/resources/prefab", "BossProfiles");

        BossProfile perorozilla = GetOrCreate(PerorozillaProfilePath, PerorozillaPrefabPath, PerorozillaPortraitPath, "Perorozilla",
            PerorozillaSpawn, PerorozillaCameraPosition, PerorozillaCameraRotation, -1, true);
        GetOrCreate(BinahProfilePath, BinahPrefabBuilder.PrefabPath, BinahPortraitPath, "Binah", BinahSpawn, BinahCameraPosition, BinahCameraRotation, BinahSkyPreset, false);
        AssetDatabase.SaveAssets();

        WireScene(perorozilla);
    }

    private static BossProfile GetOrCreate(string path, string prefabPath, string portraitPath, string displayName, Vector3 spawn, Vector3 cameraPosition, Vector3 cameraRotation, int skyPreset, bool limitPlayerArea)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) Debug.LogWarning("[BossProfileBuilder] 找不到 Boss Prefab " + prefabPath + "，請先建立 Boss");

        BossProfile profile = AssetDatabase.LoadAssetAtPath<BossProfile>(path);
        bool isNew = profile == null;
        if (isNew)
        {
            profile = ScriptableObject.CreateInstance<BossProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }

        SerializedObject serialized = new SerializedObject(profile);
        serialized.FindProperty("bossPrefab").objectReferenceValue = prefab;
        serialized.FindProperty("portrait").objectReferenceValue = LoadSprite(portraitPath);
        if (isNew)
        {
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("spawnPosition").vector3Value = spawn;
            serialized.FindProperty("cameraPosition").vector3Value = cameraPosition;
            serialized.FindProperty("cameraRotation").vector3Value = cameraRotation;
            serialized.FindProperty("skyPreset").intValue = skyPreset;
            serialized.FindProperty("limitPlayerArea").boolValue = limitPlayerArea;
            serialized.FindProperty("playerAreaMin").vector2Value = PerorozillaPlayerAreaMin;
            serialized.FindProperty("playerAreaMax").vector2Value = PerorozillaPlayerAreaMax;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
        return profile;
    }

    // 頭像圖片匯入成 Sprite (血條的 Image 需要 Sprite)
    private static Sprite LoadSprite(string path)
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null)
        {
            Debug.LogWarning("[BossProfileBuilder] 找不到頭像 " + path);
            return null;
        }

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // 場景沒選 Boss 時預設選 Perorozilla (維持原本的遊戲內容)；相機切換改從 BossEncounter 取得 Boss 的相機位置
    private static void WireScene(BossProfile defaultProfile)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.isDirty)
        {
            Debug.LogWarning("[BossProfileBuilder] 場景有未儲存的修改，已建立 Profile 但沒有接到場景；請先存檔再執行一次。");
            return;
        }

        BossEncounter encounter = Object.FindFirstObjectByType<BossEncounter>();
        if (encounter == null)
        {
            Debug.LogWarning("[BossProfileBuilder] 場景中沒有 BossEncounter");
            return;
        }

        SerializedObject encounterSerialized = new SerializedObject(encounter);
        SerializedProperty profileProperty = encounterSerialized.FindProperty("bossProfile");
        if (profileProperty.objectReferenceValue == null) profileProperty.objectReferenceValue = defaultProfile;
        // 玩家身上的活動範圍限制 (要不要限制由各 Boss 的 Profile 決定)
        PlayerMovement player = Object.FindFirstObjectByType<PlayerMovement>();
        if (player != null)
        {
            PlayerAreaLimiter limiter = player.GetComponent<PlayerAreaLimiter>();
            if (limiter == null) limiter = player.gameObject.AddComponent<PlayerAreaLimiter>();
            encounterSerialized.FindProperty("playerAreaLimiter").objectReferenceValue = limiter;
        }

        encounterSerialized.ApplyModifiedPropertiesWithoutUndo();

        CameraPoseTransition cameraTransition = Object.FindFirstObjectByType<CameraPoseTransition>();
        if (cameraTransition != null)
        {
            SerializedObject cameraSerialized = new SerializedObject(cameraTransition);
            cameraSerialized.FindProperty("bossEncounter").objectReferenceValue = encounter;
            cameraSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        SkyEnvironment sky = Object.FindFirstObjectByType<SkyEnvironment>();
        if (sky != null)
        {
            SerializedObject skySerialized = new SerializedObject(sky);
            skySerialized.FindProperty("bossEncounter").objectReferenceValue = encounter;
            skySerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BossProfileBuilder] 已接上場景：Boss = " + ((BossProfile)profileProperty.objectReferenceValue).name);
    }
}
#endif
