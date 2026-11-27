#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 在戰鬥場景 (mian game) 建立戰鬥演出 (Tools/STG/Build Battle Flow)：
// 稜鏡轉場、字卡 (START / WARNING / VICTORY / DEFEAT)、結算面板、BattleFlow + BattleStats，並接到 BossEncounter 與階段控制；
// 移除舊的 Game Over / Boss 結算物件。會儲存戰鬥場景，所以場景有未存的修改時不執行；可重複執行
public static class BattleFlowBuilder
{
    private const string GameScenePath = "Assets/STG game/Scenes/mian game.unity";
    private const string ImageRoot = "Assets/STG game/resources/prefab/UI_Prefabs/ExportedProject/Assets/_MX/AddressableAsset/UIs/00_Images/";
    private const string StartWordPath = ImageRoot + "ImageFont/Ingame_Image_Start.png";
    private const string StartLightPath = ImageRoot + "Ingame_Start_Light.png";
    private const string WarningWordPath = ImageRoot + "ImageFont/Ingame_Warning_Text_1.png";
    private const string WarningStripePath = ImageRoot + "Ingame_Warning_Deco_1.png";
    private const string VictoryWordPath = ImageRoot + "ImageFont/ImageFont_Result_Victory.png";
    private const string DefeatWordPath = ImageRoot + "ImageFont/ImageFont_Result_Defeat.png";
    private const string ResultBackgroundPath = ImageRoot + "BG/Growth_Bg.png";
    private const string ArtFolder = "Assets/STG game/resources/model/BattleUI/charaterfullimg/";
    private const string SelectionPath = "Assets/STG game/resources/prefab/CharacterSelect/GameSelection.asset";
    private const string LightMaterialPath = SceneTransitionBuilder.Folder + "/StartLight_Additive.mat";
    private const string TitleSceneName = "Title";

    // 舊的 Game Over / Boss 結算物件 (腳本已刪除)
    private static readonly string[] ObsoleteObjects = { "GameOverScreen", "BossResultScreen" };

    private static readonly (string id, string file)[] CharacterArts =
    {
        ("CH0331", "Rio_(Battle)_00.png"),
        ("CH0187", "Toki_00.png"),
        ("CH0334", "Arisu_(Battle)_00.png"),
        ("CH0175", "Wakamo_(Swimsuit)_diorama_00.png"),
    };

    private static readonly Color Navy = new Color(0.17f, 0.24f, 0.36f);
    private static readonly Color TitleYellow = new Color(1f, 0.84f, 0.2f);
    private static readonly Color ConfirmYellow = new Color(1f, 0.88f, 0.25f);
    private static readonly Color RowLine = new Color(0.55f, 0.6f, 0.68f, 0.6f);
    // 字卡在字之上、結算面板在字卡之上，轉場 (1000) 在最上面
    private const int BannerOrder = 500;
    private const int ResultOrder = 600;

    [MenuItem("Tools/STG/Build Battle Flow")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) return;

        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            if (!UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) continue;

            Debug.LogWarning("[BattleFlowBuilder] 場景 " + UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).name + " 有未儲存的修改，請先存檔再執行。");
            return;
        }

        var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        foreach (string name in new[] { "SceneTransition", "BattleBanner", "ResultCanvas", "BattleFlow", "PauseCanvas" })
        {
            GameObject old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);
        }

        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t != null && System.Array.IndexOf(ObsoleteObjects, t.name) >= 0) Object.DestroyImmediate(t.gameObject);
        }

        Font font = AssetDatabase.LoadAssetAtPath<Font>(CharacterSelectBuilder.FontPath);
        GameSelection selection = AssetDatabase.LoadAssetAtPath<GameSelection>(SelectionPath);
        SceneTransition transition = SceneTransitionBuilder.Create();
        BattleBanner banner = CreateBanner();
        ResultPanel results = CreateResultPanel(font, selection, transition);
        PauseMenuBuilder.Create(font, transition);

        GameObject flowObject = new GameObject("BattleFlow");
        BattleStats stats = flowObject.AddComponent<BattleStats>();
        BattleFlow flow = flowObject.AddComponent<BattleFlow>();
        BossEncounter encounter = Object.FindFirstObjectByType<BossEncounter>();
        PlayerHealth player = Object.FindFirstObjectByType<PlayerHealth>();
        SerializedObject flowSettings = new SerializedObject(flow);
        flowSettings.FindProperty("banner").objectReferenceValue = banner;
        flowSettings.FindProperty("stats").objectReferenceValue = stats;
        flowSettings.FindProperty("resultPanel").objectReferenceValue = results;
        flowSettings.FindProperty("playerHealth").objectReferenceValue = player;
        flowSettings.FindProperty("bossEncounter").objectReferenceValue = encounter;
        flowSettings.FindProperty("gameSelection").objectReferenceValue = selection;
        flowSettings.FindProperty("stageLabel").objectReferenceValue = FindHudText("Label_Stage");
        flowSettings.FindProperty("warningWord").objectReferenceValue = CharacterSelectBuilder.LoadSprite(WarningWordPath);
        flowSettings.FindProperty("victoryWord").objectReferenceValue = CharacterSelectBuilder.LoadSprite(VictoryWordPath);
        flowSettings.FindProperty("defeatWord").objectReferenceValue = CharacterSelectBuilder.LoadSprite(DefeatWordPath);
        flowSettings.ApplyModifiedPropertiesWithoutUndo();

        if (encounter != null)
        {
            SerializedObject encounterSettings = new SerializedObject(encounter);
            encounterSettings.FindProperty("battleFlow").objectReferenceValue = flow;
            encounterSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        AddPhaseListener(flow);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BattleFlowBuilder] 已在戰鬥場景建立轉場、字卡、結算面板");
    }

    // HUD (BattleHUD prefab 的場景實例) 裡的文字，例如左上角關卡名稱
    private static Text FindHudText(string name)
    {
        foreach (Text text in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (text.name == name) return text;
        }

        Debug.LogWarning("[BattleFlowBuilder] 找不到 HUD 文字 " + name);
        return null;
    }

    private static void AddPhaseListener(BattleFlow flow)
    {
        CombatPhaseController controller = Object.FindFirstObjectByType<CombatPhaseController>();
        if (controller == null) return;

        SerializedObject serialized = new SerializedObject(controller);
        SerializedProperty listeners = serialized.FindProperty("phaseListeners");
        // 清掉已刪除腳本留下的空位，再加上 BattleFlow
        for (int i = listeners.arraySize - 1; i >= 0; i--)
        {
            if (listeners.GetArrayElementAtIndex(i).objectReferenceValue == null) listeners.DeleteArrayElementAtIndex(i);
        }

        listeners.arraySize++;
        listeners.GetArrayElementAtIndex(listeners.arraySize - 1).objectReferenceValue = flow;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---- 字卡 ----

    private static BattleBanner CreateBanner()
    {
        RectTransform root = CreateCanvas("BattleBanner", BannerOrder);
        Image dark = Stretch(AddImage(NewRect("Dark", root), null, new Color(0f, 0f, 0f, 0f)));
        RawImage topStripe = CreateStripe(root, "StripeTop", 150f);
        RawImage bottomStripe = CreateStripe(root, "StripeBottom", -150f);

        RectTransform wordRect = NewRect("Word", root);
        wordRect.sizeDelta = new Vector2(1150f, 288f);
        Image word = AddImage(wordRect, CharacterSelectBuilder.LoadSprite(StartWordPath), Color.white);
        word.preserveAspect = true;

        // START 左上的閃光 (黑底貼圖，用加算混合)，跟著字一起移動
        RectTransform lightRect = NewRect("Light", wordRect);
        lightRect.anchoredPosition = new Vector2(-430f, 70f);
        lightRect.sizeDelta = new Vector2(620f, 590f);
        Image light = AddImage(lightRect, CharacterSelectBuilder.LoadSprite(StartLightPath), Color.white);
        light.material = GetOrCreateAdditiveMaterial();

        BattleBanner banner = root.gameObject.AddComponent<BattleBanner>();
        SerializedObject serialized = new SerializedObject(banner);
        serialized.FindProperty("darkOverlay").objectReferenceValue = dark;
        serialized.FindProperty("word").objectReferenceValue = word;
        serialized.FindProperty("startWord").objectReferenceValue = word.sprite;
        serialized.FindProperty("startLight").objectReferenceValue = light;
        SerializedProperty stripes = serialized.FindProperty("warningStripes");
        stripes.arraySize = 2;
        stripes.GetArrayElementAtIndex(0).objectReferenceValue = topStripe;
        stripes.GetArrayElementAtIndex(1).objectReferenceValue = bottomStripe;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return banner;
    }

    // 橫跨畫面的警示條紋，貼圖橫向重複 (要設成 Repeat 才能捲動)
    private static RawImage CreateStripe(RectTransform parent, string name, float y)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(WarningStripePath);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(WarningStripePath);
        if (importer != null && importer.wrapMode != TextureWrapMode.Repeat)
        {
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(WarningStripePath);
        }

        RectTransform rect = NewRect(name, parent);
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(0f, 64f);
        rect.anchoredPosition = new Vector2(0f, y);
        RawImage stripe = rect.gameObject.AddComponent<RawImage>();
        stripe.texture = texture;
        // 條紋高 64 時貼圖寬約 1660，1920 寬的畫面約重複 1.16 次
        stripe.uvRect = new Rect(0f, 0f, 1.16f, 1f);
        stripe.raycastTarget = false;
        return stripe;
    }

    private static Material GetOrCreateAdditiveMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(LightMaterialPath);
        if (material != null) return material;

        material = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
        AssetDatabase.CreateAsset(material, LightMaterialPath);
        return material;
    }

    // ---- 結算面板 ----

    private static ResultPanel CreateResultPanel(Font font, GameSelection selection, SceneTransition transition)
    {
        RectTransform root = CreateCanvas("ResultCanvas", ResultOrder);
        RectTransform panel = Stretch(NewRect("Panel", root));

        Image background = Stretch(AddImage(NewRect("Background", panel), CharacterSelectBuilder.LoadSprite(ResultBackgroundPath), Color.white));
        background.preserveAspect = false;

        // 右側斜切的白色區塊 + 左緣藍線 (立繪站在上面)
        RectTransform slantLine = NewRect("SlantLine", panel);
        Slant(slantLine, 0.5f, 22f);
        AddImage(slantLine, null, new Color(0.3f, 0.62f, 0.95f, 0.9f));
        RectTransform slant = NewRect("Slant", panel);
        Slant(slant, 0.51f, 0f);
        AddImage(slant, null, new Color(1f, 1f, 1f, 0.82f));

        RectTransform artRect = NewRect("Character", panel);
        artRect.anchorMin = artRect.anchorMax = new Vector2(0.74f, 0f);
        artRect.pivot = new Vector2(0.5f, 0f);
        artRect.sizeDelta = new Vector2(980f, 1040f);
        artRect.anchoredPosition = new Vector2(0f, 0f);
        Image art = AddImage(artRect, null, Color.white);
        art.preserveAspect = true;

        Text title = AddText(TopLeft(NewRect("Title", panel), 60f, -30f, 900f, 300f), font, "BATTLE\nCOMPLETE", 128, TitleYellow);
        title.alignment = TextAnchor.UpperLeft;
        title.fontStyle = FontStyle.BoldAndItalic;
        title.lineSpacing = 0.85f;
        Outline outline = title.gameObject.AddComponent<Outline>();
        outline.effectColor = Navy;
        outline.effectDistance = new Vector2(5f, -5f);

        Tag(panel, font, "關卡名稱", 360f);
        Text stage = AddText(TopLeft(NewRect("Stage", panel), 80f, -440f, 760f, 80f), font, "", 58, Color.black);
        stage.alignment = TextAnchor.MiddleLeft;
        stage.fontStyle = FontStyle.Bold;

        Tag(panel, font, "戰鬥記錄", 570f);
        Text kills = Row(panel, font, "擊殺敵方單位數", 650f);
        Text time = Row(panel, font, "使用時間", 730f);

        RectTransform confirmRect = NewRect("Confirm", panel);
        confirmRect.anchorMin = confirmRect.anchorMax = new Vector2(1f, 0f);
        confirmRect.pivot = new Vector2(1f, 0f);
        confirmRect.sizeDelta = new Vector2(430f, 120f);
        confirmRect.anchoredPosition = new Vector2(-70f, 50f);
        Image confirmImage = AddImage(confirmRect, CharacterSelectBuilder.RoundedSprite(), ConfirmYellow);
        // AddImage 預設不接收點擊 (給背景、裝飾用)；按鈕本體要能被點到
        confirmImage.raycastTarget = true;
        Button confirm = confirmRect.gameObject.AddComponent<Button>();
        confirm.targetGraphic = confirmImage;
        Stretch(AddText(NewRect("Label", confirmRect), font, "確認", 56, Navy).rectTransform);

        ResultPanel result = root.gameObject.AddComponent<ResultPanel>();
        SerializedObject serialized = new SerializedObject(result);
        serialized.FindProperty("panel").objectReferenceValue = panel.gameObject;
        serialized.FindProperty("stageText").objectReferenceValue = stage;
        serialized.FindProperty("killsText").objectReferenceValue = kills;
        serialized.FindProperty("timeText").objectReferenceValue = time;
        serialized.FindProperty("characterImage").objectReferenceValue = art;
        serialized.FindProperty("confirmButton").objectReferenceValue = confirm;
        serialized.FindProperty("gameSelection").objectReferenceValue = selection;
        serialized.FindProperty("sceneTransition").objectReferenceValue = transition;
        serialized.FindProperty("titleSceneName").stringValue = TitleSceneName;
        SerializedProperty arts = serialized.FindProperty("characterArts");
        arts.arraySize = CharacterArts.Length;
        for (int i = 0; i < CharacterArts.Length; i++)
        {
            SerializedProperty element = arts.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("characterId").stringValue = CharacterArts[i].id;
            element.FindPropertyRelative("art").objectReferenceValue = CharacterSelectBuilder.LoadSprite(ArtFolder + CharacterArts[i].file);
        }

        // 直接從戰鬥場景開始 (沒有選角) 時用 リオ
        serialized.FindProperty("defaultArt").objectReferenceValue = arts.GetArrayElementAtIndex(0).FindPropertyRelative("art").objectReferenceValue;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        panel.gameObject.SetActive(false);
        return result;
    }

    // 深藍底白字的斜體小標籤 (關卡名稱、戰鬥記錄)
    private static void Tag(RectTransform panel, Font font, string label, float top)
    {
        RectTransform rect = TopLeft(NewRect("Tag_" + label, panel), 60f, -top, 240f, 62f);
        AddImage(rect, CharacterSelectBuilder.RoundedSprite(), Navy);
        Text text = AddText(Stretch(NewRect("Label", rect)), font, label, 34, Color.white);
        text.fontStyle = FontStyle.BoldAndItalic;
    }

    // 一列戰鬥記錄：左邊名稱、右邊數值，底下一條細線；回傳數值文字
    private static Text Row(RectTransform panel, Font font, string label, float top)
    {
        RectTransform row = TopLeft(NewRect("Row_" + label, panel), 70f, -top, 620f, 70f);
        Text name = AddText(Stretch(NewRect("Name", row)), font, label, 38, new Color(0.2f, 0.22f, 0.26f));
        name.alignment = TextAnchor.MiddleLeft;
        Text value = AddText(Stretch(NewRect("Value", row)), font, "", 40, new Color(0.2f, 0.22f, 0.26f));
        value.alignment = TextAnchor.MiddleRight;
        RectTransform line = NewRect("Line", row);
        line.anchorMin = new Vector2(0f, 0f);
        line.anchorMax = new Vector2(1f, 0f);
        line.sizeDelta = new Vector2(0f, 2f);
        AddImage(line, null, RowLine);
        return value;
    }

    // 往右傾斜的平行四邊形：一塊旋轉過的長方形，左緣落在畫面 X = leftAnchor 處
    private static void Slant(RectTransform rect, float leftAnchor, float extraWidth)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(leftAnchor, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(1400f + extraWidth, 1700f);
        rect.anchoredPosition = new Vector2(-extraWidth, 0f);
        rect.localRotation = Quaternion.Euler(0f, 0f, -8f);
    }

    // ---- UI 小工具 ----

    private static RectTransform CreateCanvas(string name, int order)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return (RectTransform)go.transform;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        return CharacterSelectBuilder.NewRect(name, parent);
    }

    private static RectTransform TopLeft(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static RectTransform Stretch(RectTransform rect)
    {
        CharacterSelectBuilder.Stretch(rect, 0f);
        return rect;
    }

    private static Image Stretch(Image image)
    {
        Stretch(image.rectTransform);
        return image;
    }

    private static Image AddImage(RectTransform rect, Sprite sprite, Color color)
    {
        Image image = CharacterSelectBuilder.AddImage(rect.gameObject, sprite, color);
        image.raycastTarget = false;
        return image;
    }

    private static Text AddText(RectTransform rect, Font font, string content, int size, Color color)
    {
        return CharacterSelectBuilder.AddText(rect, font, content, size, color);
    }
}
#endif
