#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 暫停選單 (由 BattleFlowBuilder 呼叫)：半透明遮罩 + 白色圓角卡片 (底下鋪 title 同款的淡藍三角形圖樣、右下角三角裝飾)，
// 「PAUSE」標題 + 黃色底線，三顆藍色斜切按鈕：繼續 / 重新開始 / 返回大廳。
// 接到場景中 HUD 的 PauseController (暫停鍵打開選單)
public static class PauseMenuBuilder
{
    private const string AtlasPath = "Assets/STG game/resources/model/BattleUI/Atlas/Common.png";
    private const string MaterialPath = "Assets/STG game/resources/prefab/UI/Pause_PanelPattern.mat";
    private const string RestartSceneName = "mian game";
    private const string LobbySceneName = "Title";
    // 字卡 (500) 之上、結算 (600) 之下 (兩者不會同時出現)，轉場 (1000) 在最上面
    private const int CanvasOrder = 550;

    private static readonly Vector2 CardSize = new Vector2(760f, 780f);
    private static readonly Vector2 ButtonSize = new Vector2(520f, 112f);
    // 第一顆按鈕中心 (相對卡片中心)；卡片加高讓最下面的按鈕不壓到右下角裝飾
    private const float FirstButtonY = 140f;
    private const float ButtonSpacing = 140f;
    private const float ButtonSlant = 22f;

    private static readonly Color Dim = new Color(0.03f, 0.06f, 0.12f, 0.6f);
    private static readonly Color TextNavy = new Color32(0x23, 0x3E, 0x65, 0xFF);
    private static readonly Color AccentYellow = new Color32(0xFF, 0xEC, 0x59, 0xFF);
    private static readonly Color ButtonBlue = new Color32(0x4A, 0x9D, 0xF5, 0xFF);
    private static readonly Color ButtonShadow = new Color32(0x2C, 0x5F, 0xA8, 0xFF);
    private static readonly Color ButtonSubText = new Color(1f, 1f, 1f, 0.75f);
    private static readonly Color PatternTop = new Color32(0xE6, 0xF2, 0xFF, 0xFF);
    private static readonly Color PatternBottom = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
    private static readonly Color DecoColor = new Color32(0x9C, 0xC8, 0xF8, 0xFF);

    public static void Create(Font font, SceneTransition transition)
    {
        GameObject canvasObject = new GameObject("PauseCanvas", typeof(RectTransform));
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasOrder;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();
        RectTransform root = (RectTransform)canvasObject.transform;

        RectTransform panel = CharacterSelectBuilder.NewRect("Panel", root);
        CharacterSelectBuilder.Stretch(panel, 0f);
        // 遮罩要擋住點擊，避免點到底下的 HUD 暫停鍵
        RectTransform dim = CharacterSelectBuilder.NewRect("Dim", panel);
        CharacterSelectBuilder.Stretch(dim, 0f);
        CharacterSelectBuilder.AddImage(dim.gameObject, null, Dim).raycastTarget = true;

        RectTransform card = CreateCard(panel, font);
        Button continueButton = CreateButton(card, font, "Continue", "繼續", "C O N T I N U E", AtlasSprite("Common_Icon_Play"), 0);
        Button restartButton = CreateButton(card, font, "Restart", "重新開始", "R E S T A R T", AtlasSprite("common_icon_return"), 1);
        Button lobbyButton = CreateButton(card, font, "Lobby", "返回大廳", "L O B B Y", AtlasSprite("Common_Top_Home_Btn"), 2);

        PauseController controller = Object.FindFirstObjectByType<PauseController>(FindObjectsInactive.Include);
        if (controller == null) Debug.LogWarning("[PauseMenuBuilder] 場景中找不到 PauseController (BattleHUD)，暫停鍵不會打開選單");
        else SetReference(controller, "menu", panel.gameObject);

        PauseMenu menu = canvasObject.AddComponent<PauseMenu>();
        SerializedObject serialized = new SerializedObject(menu);
        serialized.FindProperty("pauseController").objectReferenceValue = controller;
        serialized.FindProperty("continueButton").objectReferenceValue = continueButton;
        serialized.FindProperty("restartButton").objectReferenceValue = restartButton;
        serialized.FindProperty("lobbyButton").objectReferenceValue = lobbyButton;
        serialized.FindProperty("sceneTransition").objectReferenceValue = transition;
        serialized.FindProperty("restartSceneName").stringValue = RestartSceneName;
        serialized.FindProperty("lobbySceneName").stringValue = LobbySceneName;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        panel.gameObject.SetActive(false);
    }

    // 白色圓角卡片：內側鋪淡藍漸層 + 三角形圖樣、右下角三角裝飾、上方 PAUSE 標題與黃色底線
    private static RectTransform CreateCard(RectTransform panel, Font font)
    {
        RectTransform card = CharacterSelectBuilder.NewRect("Card", panel);
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
        card.sizeDelta = CardSize;
        Image background = CharacterSelectBuilder.AddImage(card.gameObject, AtlasSprite("Common_Popup_Bg"), Color.white);
        background.type = Image.Type.Sliced;
        background.raycastTarget = true;

        RectTransform pattern = CharacterSelectBuilder.NewRect("Pattern", card);
        CharacterSelectBuilder.Stretch(pattern, 26f);
        Image patternImage = CharacterSelectBuilder.AddImage(pattern.gameObject, null, Color.white);
        patternImage.material = GetOrCreatePatternMaterial();
        patternImage.raycastTarget = false;

        RectTransform deco = CharacterSelectBuilder.NewRect("Deco", card);
        deco.anchorMin = deco.anchorMax = deco.pivot = new Vector2(1f, 0f);
        deco.sizeDelta = new Vector2(170f, 170f);
        deco.anchoredPosition = new Vector2(-28f, 28f);
        CharacterSelectBuilder.AddImage(deco.gameObject, AtlasSprite("Popup_System_Deco"), DecoColor).raycastTarget = false;

        RectTransform title = CharacterSelectBuilder.NewRect("Title", card);
        title.anchorMin = title.anchorMax = new Vector2(0.5f, 1f);
        title.sizeDelta = new Vector2(500f, 100f);
        title.anchoredPosition = new Vector2(0f, -95f);
        Text titleText = CharacterSelectBuilder.AddText(title, font, "PAUSE", 72, TextNavy);
        titleText.fontStyle = FontStyle.BoldAndItalic;
        titleText.raycastTarget = false;

        RectTransform underline = CharacterSelectBuilder.NewRect("Underline", card);
        underline.anchorMin = underline.anchorMax = new Vector2(0.5f, 1f);
        underline.sizeDelta = new Vector2(150f, 9f);
        underline.anchoredPosition = new Vector2(0f, -150f);
        CharacterSelectBuilder.AddImage(underline.gameObject, null, AccentYellow).raycastTarget = false;
        return card;
    }

    // 藍色斜切按鈕：深藍陰影 + 藍底 + 白色圖示、中文主標與英文副標
    private static Button CreateButton(RectTransform card, Font font, string name, string label, string subLabel, Sprite icon, int index)
    {
        RectTransform rect = CharacterSelectBuilder.NewRect("Button_" + name, card);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = ButtonSize;
        rect.anchoredPosition = new Vector2(0f, FirstButtonY - index * ButtonSpacing);

        SlantedRect shadow = Slanted("Shadow", rect, ButtonShadow);
        shadow.rectTransform.anchoredPosition = new Vector2(6f, -6f);
        shadow.raycastTarget = false;
        SlantedRect body = Slanted("Body", rect, ButtonBlue);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = body;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f);
        colors.pressedColor = new Color(0.85f, 0.9f, 0.97f);
        button.colors = colors;
        rect.gameObject.AddComponent<UiButtonFeedback>();

        RectTransform iconRect = CharacterSelectBuilder.NewRect("Icon", rect);
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.sizeDelta = new Vector2(56f, 56f);
        iconRect.anchoredPosition = new Vector2(100f, 0f);
        Image iconImage = CharacterSelectBuilder.AddImage(iconRect.gameObject, icon, Color.white);
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        Text main = CharacterSelectBuilder.AddText(TextRect(rect, "Label", 160f, 12f, 56f), font, label, 46, Color.white);
        main.alignment = TextAnchor.MiddleLeft;
        main.fontStyle = FontStyle.BoldAndItalic;
        main.raycastTarget = false;
        Text sub = CharacterSelectBuilder.AddText(TextRect(rect, "SubLabel", 163f, -28f, 26f), font, subLabel, 18, ButtonSubText);
        sub.alignment = TextAnchor.MiddleLeft;
        sub.fontStyle = FontStyle.Italic;
        sub.raycastTarget = false;
        return button;
    }

    private static SlantedRect Slanted(string name, RectTransform parent, Color color)
    {
        RectTransform rect = CharacterSelectBuilder.NewRect(name, parent);
        CharacterSelectBuilder.Stretch(rect, 0f);
        SlantedRect graphic = rect.gameObject.AddComponent<SlantedRect>();
        graphic.color = color;
        graphic.Slant = ButtonSlant;
        return graphic;
    }

    // 按鈕裡的一行文字：x 從左邊算，y 是相對按鈕中心的偏移
    private static RectTransform TextRect(RectTransform parent, string name, float x, float y, float height)
    {
        RectTransform rect = CharacterSelectBuilder.NewRect(name, parent);
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = new Vector2(x, y - height * 0.5f);
        rect.offsetMax = new Vector2(-30f, y + height * 0.5f);
        return rect;
    }

    private static Material GetOrCreatePatternMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("STG/UITrianglePattern"));
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.SetColor("_TopColor", PatternTop);
        material.SetColor("_BottomColor", PatternBottom);
        material.SetColor("_TriangleColor", ButtonBlue);
        material.SetFloat("_CellSize", 140f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Sprite AtlasSprite(string name)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(AtlasPath))
        {
            if (asset is Sprite sprite && sprite.name == name) return sprite;
        }

        Debug.LogWarning("[PauseMenuBuilder] 找不到 sprite Common/" + name);
        return null;
    }

    private static void SetReference(Object target, string field, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
