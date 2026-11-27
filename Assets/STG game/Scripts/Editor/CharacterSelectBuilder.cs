#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 產生選角場景 (CharacterSelect.unity)：中間四個 Q 版角色 (Echelon 模型，Idle / Pickup)、腳下名牌、
// 左側 Boss 分頁 (Perorozilla / Binah / HoverCraft / 無盡模式)、右下出擊按鈕；
// 並把同一份 GameSelection 接到戰鬥場景的 BossEncounter、把選角場景排在 Build Settings 第一個。
// 會切換並儲存場景，所以開著的場景有未存的修改時不執行
public static class CharacterSelectBuilder
{
    private const string ScenePath = "Assets/STG game/Scenes/CharacterSelect.unity";
    private const string GameScenePath = "Assets/STG game/Scenes/mian game.unity";
    public const string TitleScenePath = "Assets/STG game/Scenes/Title.unity";
    private const string OutputFolder = "Assets/STG game/resources/prefab/CharacterSelect";
    private const string SelectionPath = OutputFolder + "/GameSelection.asset";
    private const string ProfileFolder = "Assets/STG game/resources/prefab/BossProfiles/";
    private const string RaidFolder = "Assets/STG game/resources/model/BattleUI/02_Tactics/06_Raid/";
    private const string BackgroundPath = "Assets/STG game/resources/prefab/UI_Prefabs/ExportedProject/Assets/_MX/AddressableAsset/UIs/00_Images/BG/Growth_Bg.png";
    internal const string FontPath = "Assets/STG game/resources/prefab/UI_Prefabs/ExportedProject/Assets/_MX/UIs/_MXCommon/Fonts/CCG_LINESeedKR_Bd.ttf";

    private static readonly Color BackgroundColor = new Color(0.80f, 0.89f, 0.96f);
    private static readonly Color TabSelectedColor = new Color(1f, 0.85f, 0.25f);
    private static readonly Color TabNormalColor = new Color(1f, 1f, 1f, 0.9f);
    private static readonly Color Navy = new Color(0.17f, 0.24f, 0.36f);
    private static readonly Color MobilizeYellow = new Color(1f, 0.86f, 0.2f);

    // 角色腳下在畫面上的位置 (0~1，由左到右；左邊留給 Boss 分頁)，世界座標由相機反推，
    // 所以改相機也不會讓順序左右顛倒 (相機從角色正面拍，世界 +X 在畫面左邊)
    private static readonly float[] CharacterScreenX = { 0.33f, 0.5f, 0.67f, 0.84f };
    private const float CharacterFootScreenY = 0.3f;
    private const float CharacterSlotWidth = 1.2f;
    // 角色模型的縮放 (Echelon 模型原本約 1m 高)
    private const float CharacterScale = 1.5f;
    // 待機時的嘴型 (BA 動畫事件 SetMouthTile 的編號；待機、編隊待機都是 401)
    private const int DefaultMouthCode = 401;
    private const string MouthFolder = OutputFolder + "/Mouth";
    private const string MaterialFolder = OutputFolder + "/Materials";
    private static readonly Vector3 CameraPosition = new Vector3(0f, 1.3f, 7.5f);
    private static readonly Vector3 CameraLookAt = new Vector3(0f, 0.6f, 0f);

    private struct CharacterSpec
    {
        public string id;
        public string displayName;
        public string modelPath;
        // 模型裡要隱藏的部件 (例如トキ的機甲)
        public string[] hiddenParts;
    }

    private static readonly CharacterSpec[] Characters =
    {
        new CharacterSpec { id = "CH0331", displayName = "リオ", modelPath = "Assets/STG game/resources/model/CH0331_調月リオ/bundles/Assets/_MX/AddressableAsset/Character/CH0331/Echelon/Echelon_CH0331.glb" },
        new CharacterSpec { id = "CH0187", displayName = "トキ", modelPath = "Assets/STG game/resources/model/CH0187_飛鳥馬トキ/bundles/Assets/_MX/AddressableAsset/Character/CH0187/CH0187.glb", hiddenParts = new[] { "CH0187_B_Body", "CH0187_Machine" } },
        // トキ用戰鬥模型：一般模樣 = Body + A_Body；機甲模式 = Body + B_Body + Machine (留給戰鬥中的特殊技能切換)。
        // CH0187_CafeOnly 的長裙骨架匯入後是壞的 (不論播哪個動畫裙子都變尖刺)，所以不用
        new CharacterSpec { id = "CH0334", displayName = "アリス", modelPath = "Assets/STG game/resources/model/CH0334_天童アリス/bundles/Assets/_MX/AddressableAsset/Character/CH0334/Echelon/Echelon_CH0334.glb" },
        new CharacterSpec { id = "CH0175", displayName = "ワカモ", modelPath = "Assets/STG game/resources/model/CH0175_狐坂ワカモ/bundles/Assets/_MX/AddressableAsset/Character/CH0175/Echelon/Echelon_CH0175.glb" },
    };

    [MenuItem("Tools/STG/Build Character Select")]
    public static void Build()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (!SceneManager.GetSceneAt(i).isDirty) continue;

            Debug.LogWarning("[CharacterSelectBuilder] 場景 " + SceneManager.GetSceneAt(i).name + " 有未儲存的修改，請先存檔再執行。");
            return;
        }

        EnsureFolder(OutputFolder);
        EnsureFolder(MouthFolder);
        EnsureFolder(MaterialFolder);
        GetOrCreateSelection();

        // 切換場景會卸載沒被參照的資產，所以資產都在切換之後才載入
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameSelection selection = GetOrCreateSelection();
        Camera camera = CreateCamera();
        CreateLight();
        CreateEventSystem();
        Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        CreateBackground(camera);
        RectTransform canvas = CreateCanvas();

        SelectableCharacter[] characters = new SelectableCharacter[Characters.Length];
        for (int i = 0; i < Characters.Length; i++)
        {
            characters[i] = CreateCharacter(Characters[i], GroundPointAt(camera, CharacterScreenX[i], CharacterFootScreenY), camera, canvas, font);
        }

        CharacterSelectScreen.BossOption[] options = CreateBossOptions();
        Button[] tabs = CreateBossTabs(canvas, options, font);
        Button mobilize = CreateMobilizeButton(canvas, font);

        CharacterSelectScreen screen = new GameObject("CharacterSelectScreen").AddComponent<CharacterSelectScreen>();
        SerializedObject serialized = new SerializedObject(screen);
        SetArray(serialized.FindProperty("characters"), characters);
        SetArray(serialized.FindProperty("bossTabs"), tabs);
        SerializedProperty optionsProperty = serialized.FindProperty("bossOptions");
        optionsProperty.arraySize = options.Length;
        for (int i = 0; i < options.Length; i++)
        {
            SerializedProperty element = optionsProperty.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("label").stringValue = options[i].label;
            element.FindPropertyRelative("icon").objectReferenceValue = options[i].icon;
            element.FindPropertyRelative("loop").boolValue = options[i].loop;
            SetArray(element.FindPropertyRelative("bosses"), options[i].bosses);
        }

        serialized.FindProperty("selectedTabColor").colorValue = TabSelectedColor;
        serialized.FindProperty("normalTabColor").colorValue = TabNormalColor;
        serialized.FindProperty("selectedTabTextColor").colorValue = TabSelectedColor;
        serialized.FindProperty("normalTabTextColor").colorValue = Color.white;
        serialized.FindProperty("mobilizeButton").objectReferenceValue = mobilize;
        serialized.FindProperty("gameSelection").objectReferenceValue = selection;
        serialized.FindProperty("gameSceneName").stringValue = System.IO.Path.GetFileNameWithoutExtension(GameScenePath);
        serialized.FindProperty("sceneTransition").objectReferenceValue = SceneTransitionBuilder.Create();
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        UpdateBuildSettings();
        WireGameScene();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Debug.Log("[CharacterSelectBuilder] 已產生 " + ScenePath);
    }

    // 畫面上 (viewportX, viewportY) 對到的地面 (y = 0) 位置
    internal static Vector3 GroundPointAt(Camera camera, float viewportX, float viewportY)
    {
        Ray ray = camera.ViewportPointToRay(new Vector3(viewportX, viewportY, 0f));
        float distance = -ray.origin.y / ray.direction.y;
        return ray.origin + ray.direction * distance;
    }

    private static GameSelection GetOrCreateSelection()
    {
        GameSelection selection = AssetDatabase.LoadAssetAtPath<GameSelection>(SelectionPath);
        if (selection != null) return selection;

        selection = ScriptableObject.CreateInstance<GameSelection>();
        AssetDatabase.CreateAsset(selection, SelectionPath);
        return selection;
    }

    // 分頁圖：Raid 大廳的橫幅 (LobbyBG 背景 + Lobby Boss 本體)；無盡模式把三隻並排在同一張背景上
    private struct TabArt
    {
        public string background;
        public string[] bosses;
    }

    private static readonly TabArt[] TabArts =
    {
        new TabArt { background = "Boss_Portrait_Perorozilla_LobbyBG", bosses = new[] { "Boss_Portrait_Perorozilla_Lobby" } },
        new TabArt { background = "Boss_Portrait_Binah_LobbyBG", bosses = new[] { "Boss_Portrait_Binah_Lobby" } },
        new TabArt { background = "Boss_Portrait_RaidHoverCraft_LobbyBG", bosses = new[] { "Boss_Portrait_RaidHoverCraft_Lobby" } },
        new TabArt { background = "Boss_Portrait_Binah_LobbyBG_Street", bosses = new[] { "Boss_Portrait_Perorozilla_Lobby", "Boss_Portrait_Binah_Lobby", "Boss_Portrait_RaidHoverCraft_Lobby" } },
    };

    // 單一 Boss 各一隻；無盡模式依序 Perorozilla → Binah → HoverCraft (HoverCraft 還沒有 Profile，做好後加進來)
    private static CharacterSelectScreen.BossOption[] CreateBossOptions()
    {
        BossProfile perorozilla = AssetDatabase.LoadAssetAtPath<BossProfile>(ProfileFolder + "Boss_Perorozilla.asset");
        BossProfile binah = AssetDatabase.LoadAssetAtPath<BossProfile>(ProfileFolder + "Boss_Binah.asset");
        BossProfile hoverCraft = AssetDatabase.LoadAssetAtPath<BossProfile>(ProfileFolder + "Boss_HoverCraft.asset");
        return new[]
        {
            new CharacterSelectScreen.BossOption { label = "Perorozilla", icon = LoadSprite(RaidFolder + "Boss_Portrait_Perorozilla_Lobby.png"), bosses = new[] { perorozilla } },
            new CharacterSelectScreen.BossOption { label = "Binah", icon = LoadSprite(RaidFolder + "Boss_Portrait_Binah_Lobby.png"), bosses = new[] { binah } },
            new CharacterSelectScreen.BossOption { label = "HoverCraft", icon = LoadSprite(RaidFolder + "Boss_Portrait_RaidHoverCraft_Lobby.png"), bosses = new[] { hoverCraft } },
            new CharacterSelectScreen.BossOption { label = "無盡模式", icon = LoadSprite(RaidFolder + "Boss_Portrait_Binah_Insane_Lobby.png"), bosses = new[] { perorozilla, binah, hoverCraft }, loop = true },
        };
    }

    private static Camera CreateCamera()
    {
        GameObject go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        Camera camera = go.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = BackgroundColor;
        camera.fieldOfView = 30f;
        camera.aspect = 16f / 9f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;
        go.transform.position = CameraPosition;
        go.transform.rotation = Quaternion.LookRotation(CameraLookAt - CameraPosition, Vector3.up);
        go.AddComponent<PhysicsRaycaster>();
        return camera;
    }

    private static void CreateLight()
    {
        Light light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional;
        // 從相機這側斜上方打光 (角色正面亮)；材質改成非金屬後 (見 CreateNonMetalCopies)，
        // 主光 + 環境光約等於貼圖原色 (對照 Unlit 調出來的)，陰影面維持在原色的七八成，不會發灰也不會過曝
        light.intensity = KeyLightIntensity;
        light.color = new Color(1f, 0.98f, 0.95f);
        light.shadows = LightShadows.None;
        light.transform.rotation = Quaternion.Euler(30f, 170f, 0f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = AmbientColor;
    }

    private const float KeyLightIntensity = 0.8f;
    private static readonly Color AmbientColor = new Color(0.85f, 0.86f, 0.9f);

    // 背景圖：放在相機後方遠處的 Canvas (Screen Space - Camera)，角色畫在它前面
    private static void CreateBackground(Camera camera)
    {
        GameObject go = new GameObject("BackgroundCanvas", typeof(RectTransform));
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = camera.farClipPlane * 0.8f;
        canvas.sortingOrder = -10;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        RectTransform image = NewRect("Background", go.transform);
        image.anchorMin = image.anchorMax = new Vector2(0.5f, 0.5f);
        Image background = AddImage(image.gameObject, LoadSprite(BackgroundPath), Color.white);
        background.raycastTarget = false;
        AspectRatioFitter fitter = image.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = background.sprite != null ? background.sprite.rect.width / background.sprite.rect.height : 2f;
    }

    private static void CreateEventSystem()
    {
        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    private static RectTransform CreateCanvas()
    {
        GameObject go = new GameObject("Canvas", typeof(RectTransform));
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return (RectTransform)go.transform;
    }

    // 一個角色：模型 (Idle ⇄ Pickup)、可點的碰撞框、腳下名牌 (畫面上對齊模型腳下)
    private static SelectableCharacter CreateCharacter(CharacterSpec spec, Vector3 position, Camera camera, RectTransform canvas, Font font)
    {
        GameObject root = new GameObject("Character_" + spec.id);
        root.transform.position = position;
        root.transform.localScale = Vector3.one * CharacterScale;

        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(spec.modelPath);
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
        model.transform.SetParent(root.transform, false);
        if (spec.hiddenParts != null)
        {
            foreach (Transform part in model.GetComponentsInChildren<Transform>(true))
            {
                if (System.Array.IndexOf(spec.hiddenParts, part.name) >= 0) part.gameObject.SetActive(false);
            }
        }
        CharacterMouthBaker.ApplyTo(model, DefaultMouthCode, MouthFolder);
        // 材質改成選角場景專用的非金屬複本 (glb 預設全金屬會發灰)，原本 glb 的材質與戰鬥場景都不受影響
        NonMetalMaterials.ApplyTo(model, MaterialFolder);
        Animator animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.AddComponent<Animator>();
        animator.runtimeAnimatorController = CreateController(spec);

        Bounds bounds = RendererBounds(model);
        BoxCollider box = root.AddComponent<BoxCollider>();
        box.center = root.transform.InverseTransformPoint(bounds.center);
        box.size = new Vector3(Mathf.Min(bounds.size.x, CharacterSlotWidth), bounds.size.y, bounds.size.z) / CharacterScale;

        Vector3 viewport = camera.WorldToViewportPoint(position);
        RectTransform plate = NewRect("Nameplate_" + spec.id, canvas);
        plate.anchorMin = plate.anchorMax = new Vector2(viewport.x, viewport.y);
        plate.pivot = new Vector2(0.5f, 1f);
        plate.anchoredPosition = new Vector2(0f, -24f);
        plate.sizeDelta = new Vector2(260f, 80f);
        Image frame = AddImage(plate.gameObject, RoundedSprite(), Color.white);
        Button button = plate.gameObject.AddComponent<Button>();
        button.targetGraphic = frame;
        button.transition = Selectable.Transition.None;

        RectTransform inner = NewRect("Inner", plate);
        Stretch(inner, 6f);
        AddImage(inner.gameObject, RoundedSprite(), Color.white).raycastTarget = false;
        Text name = AddText(NewRect("Name", inner), font, spec.displayName, 40, Navy);
        Stretch((RectTransform)name.transform, 0f);

        SelectableCharacter character = root.AddComponent<SelectableCharacter>();
        SerializedObject serialized = new SerializedObject(character);
        serialized.FindProperty("characterId").stringValue = spec.id;
        serialized.FindProperty("displayName").stringValue = spec.displayName;
        serialized.FindProperty("animator").objectReferenceValue = animator;
        serialized.FindProperty("nameplateButton").objectReferenceValue = button;
        serialized.FindProperty("nameplateFrame").objectReferenceValue = frame;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return character;
    }

    // Idle (預設) ⇄ Pickup，由 bool 參數 Picked 切換；glb 匯入的動畫本身就會循環
    private static RuntimeAnimatorController CreateController(CharacterSpec spec)
    {
        string path = OutputFolder + "/" + spec.id + "_Select.controller";
        AssetDatabase.DeleteAsset(path);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Picked", AnimatorControllerParameterType.Bool);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        AnimatorState idle = machine.AddState("Idle");
        idle.motion = PerorozillaSummonBuilder.FindClip(spec.modelPath, spec.id + "_Formation_Idle");
        AnimatorState pickup = machine.AddState("Pickup");
        pickup.motion = PerorozillaSummonBuilder.FindClip(spec.modelPath, spec.id + "_Formation_Pickup");
        machine.defaultState = idle;

        AnimatorStateTransition toPickup = idle.AddTransition(pickup);
        toPickup.AddCondition(AnimatorConditionMode.If, 0f, "Picked");
        toPickup.hasExitTime = false;
        toPickup.duration = 0.15f;
        AnimatorStateTransition toIdle = pickup.AddTransition(idle);
        toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Picked");
        toIdle.hasExitTime = false;
        toIdle.duration = 0.2f;
        return controller;
    }

    // 左側 Boss 分頁 (參考圖的 Unit 1–4 位置)
    private static Button[] CreateBossTabs(RectTransform canvas, CharacterSelectScreen.BossOption[] options, Font font)
    {
        Button[] tabs = new Button[options.Length];
        for (int i = 0; i < options.Length; i++)
        {
            RectTransform tab = NewRect("BossTab_" + options[i].label, canvas);
            tab.anchorMin = tab.anchorMax = new Vector2(0f, 0.5f);
            tab.pivot = new Vector2(0f, 0.5f);
            tab.sizeDelta = new Vector2(420f, 134f);
            tab.anchoredPosition = new Vector2(24f, 240f - i * 156f);
            // 外框 (選中時變黃) 由 CharacterSelectScreen 上色
            Image frame = AddImage(tab.gameObject, RoundedSprite(), TabNormalColor);
            Button button = tab.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            button.transition = Selectable.Transition.None;

            RectTransform art = NewRect("Art", tab);
            Stretch(art, 5f);
            art.gameObject.AddComponent<RectMask2D>();
            TabArt tabArt = TabArts[i];
            RectTransform artBackground = NewRect("LobbyBG", art);
            Stretch(artBackground, 0f);
            AddImage(artBackground.gameObject, LoadSprite(RaidFolder + tabArt.background + ".png"), Color.white).raycastTarget = false;
            for (int b = 0; b < tabArt.bosses.Length; b++)
            {
                // 一隻就鋪滿；多隻時各佔一等分
                RectTransform boss = NewRect("Lobby", art);
                boss.anchorMin = new Vector2((float)b / tabArt.bosses.Length, 0f);
                boss.anchorMax = new Vector2((float)(b + 1) / tabArt.bosses.Length, 1f);
                boss.offsetMin = boss.offsetMax = Vector2.zero;
                Image bossImage = AddImage(boss.gameObject, LoadSprite(RaidFolder + tabArt.bosses[b] + ".png"), Color.white);
                bossImage.preserveAspect = tabArt.bosses.Length > 1;
                bossImage.raycastTarget = false;
            }

            Text label = AddText(NewRect("Label", art), font, options[i].label, 36, Color.white);
            RectTransform labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0.45f);
            labelRect.offsetMin = new Vector2(16f, 0f);
            labelRect.offsetMax = new Vector2(-12f, 0f);
            label.alignment = TextAnchor.MiddleLeft;
            Outline outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(Navy.r, Navy.g, Navy.b, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            tabs[i] = button;
        }

        return tabs;
    }

    private static Button CreateMobilizeButton(RectTransform canvas, Font font)
    {
        RectTransform rect = NewRect("MobilizeButton", canvas);
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = new Vector2(400f, 140f);
        rect.anchoredPosition = new Vector2(-40f, 40f);
        Image image = AddImage(rect.gameObject, RoundedSprite(), MobilizeYellow);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.disabledColor = new Color(0.62f, 0.62f, 0.62f, 0.8f);
        button.colors = colors;

        Text label = AddText(NewRect("Label", rect), font, "出擊", 64, Navy);
        Stretch((RectTransform)label.transform, 0f);
        return button;
    }

    // 順序：開始畫面 (有做的話) → 選角 → 戰鬥
    public static void UpdateBuildSettings()
    {
        System.Collections.Generic.List<EditorBuildSettingsScene> scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
        if (System.IO.File.Exists(TitleScenePath)) scenes.Add(new EditorBuildSettingsScene(TitleScenePath, true));
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        scenes.Add(new EditorBuildSettingsScene(GameScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // 戰鬥場景的 BossEncounter 讀同一份 GameSelection (戰鬥結束演出與結算面板由 BattleFlowBuilder 接)
    private static void WireGameScene()
    {
        Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        GameSelection selection = GetOrCreateSelection();
        BossEncounter encounter = Object.FindFirstObjectByType<BossEncounter>();
        if (encounter == null)
        {
            Debug.LogWarning("[CharacterSelectBuilder] 戰鬥場景中沒有 BossEncounter");
            return;
        }

        SerializedObject serialized = new SerializedObject(encounter);
        serialized.FindProperty("gameSelection").objectReferenceValue = selection;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    internal static Bounds RendererBounds(GameObject model)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(model.transform.position + Vector3.up * 0.5f, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    internal static Sprite RoundedSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }

    internal static Sprite LoadSprite(string path)
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null)
        {
            Debug.LogWarning("[CharacterSelectBuilder] 找不到圖片 " + path);
            return null;
        }

        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    internal static RectTransform NewRect(string name, Transform parent)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    internal static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    internal static Image AddImage(GameObject target, Sprite sprite, Color color)
    {
        Image image = target.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        if (sprite != null && sprite.border != Vector4.zero) image.type = Image.Type.Sliced;
        return image;
    }

    internal static Text AddText(RectTransform rect, Font font, string content, int size, Color color)
    {
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static void SetArray(SerializedProperty property, Object[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    internal static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;

        string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
    }
}
#endif
