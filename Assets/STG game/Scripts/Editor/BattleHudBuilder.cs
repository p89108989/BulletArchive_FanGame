#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 戰鬥 HUD (蔚藍檔案風格)：用切好的蔚藍檔案 UI sprite (BaAtlasSlicer) 建立 uGUI 版面並存成 Prefab，放進目前場景。
// 尺寸取自蔚藍檔案的 UIBattle (設計解析度 2560×1440)，乘上 BaScale 換算成本專案的 1920×1080。
// 各區塊 (左上關卡名、右上計時器與暫停鍵…) 先留好錨點容器，之後的能量條、技能卡、對話框等再逐步加入。
// 可重複執行：Prefab 整個重建，場景中已有的實例會自動更新
public static class BattleHudBuilder
{
    private const string PrefabPath = "Assets/STG game/resources/prefab/UI/BattleHUD.prefab";
    private const string AtlasFolder = "Assets/STG game/resources/model/BattleUI/Atlas/";
    private const string FontPath = "Assets/STG game/resources/prefab/UI_Prefabs/ExportedProject/Assets/_MX/UIs/_MXCommon/Fonts/CCG_LINESeedKR_Bd.ttf";
    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
    // 蔚藍檔案 2560×1440 → 1920×1080
    private const float BaScale = 0.75f;

    // 蔚藍檔案 HUD 的深藍底 (關卡名、計時器) 與暫停符號的顏色
    private static readonly Color PanelNavy = new Color(0.0235f, 0.1765f, 0.3176f, 0.95f);
    private static readonly Color PauseMarkNavy = new Color(0.1765f, 0.2745f, 0.3922f, 1f);
    private static readonly Color ButtonWhite = new Color(1f, 1f, 1f, 0.9f);

    private const string DefaultStageName = "Stage 1";

    [MenuItem("Tools/STG/Build Battle HUD")]
    private static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[BattleHudBuilder] 請先離開 Play Mode 再執行。");
            return;
        }

        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
        {
            Debug.LogWarning("[BattleHudBuilder] 場景有未存的變更，請先存檔或還原後再執行 (本工具會存檔場景)");
            return;
        }

        Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        GameObject root = CreateRoot();
        try
        {
            RectTransform topLeft = CreateRegion(root.transform, "TopLeft", new Vector2(0f, 1f));
            RectTransform topRight = CreateRegion(root.transform, "TopRight", new Vector2(1f, 1f));
            RectTransform topCenter = CreateRegion(root.transform, "TopCenter", new Vector2(0.5f, 1f));
            RectTransform bottomLeft = CreateRegion(root.transform, "BottomLeft", new Vector2(0f, 0f));
            RectTransform bottomRight = CreateRegion(root.transform, "BottomRight", new Vector2(1f, 0f));
            BuildPlayerStatus(bottomLeft);

            BuildStageLabel(topLeft, font);
            BuildTimer(topRight, font);
            BuildPauseButton(topRight, root.GetComponent<PauseController>());
            BuildEnergyGauges(bottomRight, font);
            BuildSkillCards(bottomRight, font);
            BuildSkillDialog(topLeft);
            BuildBossHealthBar(topCenter, font);

            EnsureFolder(System.IO.Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PlaceInScene(prefab);
            Debug.Log("[BattleHudBuilder] 已建立 " + PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static GameObject CreateRoot()
    {
        GameObject root = new GameObject("BattleHUD", typeof(RectTransform));
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();
        root.AddComponent<PauseController>();
        return root;
    }

    // 貼在畫面某個角落/邊的零尺寸容器，子物件以它為原點擺放
    private static RectTransform CreateRegion(Transform parent, string name, Vector2 anchor)
    {
        RectTransform rect = NewRect(name, parent);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    // 左上關卡名：深藍底條從畫面左緣外延伸進來 (蔚藍檔案 Stage 236×70)，白字 46
    private static void BuildStageLabel(RectTransform topLeft, Font font)
    {
        RectTransform stage = NewRect("Stage", topLeft);
        SetRect(stage, new Vector2(0f, 1f), new Vector2(-20f, Ba(-44f)), new Vector2(Ba(244f) + 20f, Ba(70f)));
        AddImage(stage.gameObject, Sprite("Combat", "Combat_Gauge_Hp"), PanelNavy);

        Text label = AddText(NewRect("Label_Stage", stage), font, DefaultStageName, Ba(46f), TextAnchor.MiddleLeft);
        Stretch(label.rectTransform, new Vector2(20f + Ba(32f), 0f), Vector2.zero);
    }

    // 右上計時器：深藍底條 (255×70) + 時鐘圖示 + 往上數的時間 (52)，右邊接暫停鍵
    private static void BuildTimer(RectTransform topRight, Font font)
    {
        RectTransform timer = NewRect("Timer", topRight);
        SetRect(timer, new Vector2(1f, 1f), new Vector2(-Ba(67f + 133f + 6f), Ba(-44f)), new Vector2(Ba(255f), Ba(70f)));
        AddImage(timer.gameObject, Sprite("Combat", "Combat_Gauge_Hp"), PanelNavy);

        RectTransform icon = NewRect("Icon", timer);
        icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
        icon.pivot = new Vector2(0f, 0.5f);
        icon.anchoredPosition = new Vector2(Ba(15f), 0f);
        icon.sizeDelta = new Vector2(Ba(40f), Ba(44f));
        Image iconImage = AddImage(icon.gameObject, Sprite("Combat", "Combat_Time_Icon"), new Color(1f, 1f, 1f, 0.89f));
        iconImage.type = Image.Type.Simple;

        Text label = AddText(NewRect("Label_Time", timer), font, BattleClock.Format(0f), Ba(52f), TextAnchor.MiddleRight);
        Stretch(label.rectTransform, Vector2.zero, new Vector2(-Ba(21f), 0f));

        BattleTimerUI timerUI = timer.gameObject.AddComponent<BattleTimerUI>();
        SetReference(timerUI, "label", label);
    }

    // 右上暫停鍵：白色圓角按鈕 (133×125) + 兩條深藍直槓
    private static void BuildPauseButton(RectTransform topRight, PauseController pause)
    {
        RectTransform button = NewRect("BtnPause", topRight);
        SetRect(button, new Vector2(1f, 1f), new Vector2(-Ba(67f), Ba(-20f)), new Vector2(Ba(133f), Ba(125f)));
        Image background = AddImage(button.gameObject, Sprite("Common", "Common_Btn_BG"), ButtonWhite);
        background.raycastTarget = true;

        Sprite square = Sprite("Common", "Common_Square_Bg");
        for (int i = 0; i < 2; i++)
        {
            RectTransform bar = NewRect(i == 0 ? "Mark_L" : "Mark_R", button);
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0.5f);
            bar.pivot = new Vector2(0.5f, 0.5f);
            // 按鈕圖下方有陰影，圖示稍微往上擺才會在白色區域正中
            bar.anchoredPosition = new Vector2(i == 0 ? -Ba(12f) : Ba(12f), Ba(5f));
            bar.sizeDelta = new Vector2(Ba(12f), Ba(62f));
            AddImage(bar.gameObject, square, PauseMarkNavy);
        }

        Button buttonComponent = button.gameObject.AddComponent<Button>();
        buttonComponent.targetGraphic = background;
        UnityEventTools.AddPersistentListener(buttonComponent.onClick, pause.TogglePause);
    }

    // ── 左下：角色頭像卡 + 血條 ──────────────────────────────────────────────────
    // 頭像卡沿用蔚藍檔案技能卡的平行四邊形底 (Combat_CardBg_EX) 當遮罩，角色立繪只顯示在卡片形狀內；
    // 血條是蔚藍檔案的角色血條 (深藍底、白色殘影、萊姆綠血量、3 條分隔線 = 4 格)，下緣切齊頭像卡往右延伸
    private const string PortraitPath = "Assets/STG game/resources/model/BattleUI/02_Tactics/01_Character/Skill_Portrait_CH0331.png";
    public static readonly Vector2 CardSize = new Vector2(Ba(246f), Ba(203f));
    private const float BottomLeftMargin = 24f;
    // 平行四邊形卡片的下緣右端約在卡寬的 90% 處，血條從這裡接出去
    private const float CardBottomRightEdge = 0.9f;
    private static readonly Vector2 HealthBarSize = new Vector2(400f, 34f);
    // 血量填滿框內的深藍區、只留 1px：框圖 (Combat_Bg_Hp) 下方有陰影，可見的內框比圖片中心偏上，
    // 實測內框在左右各 6px、上 4px、下 10px 處 (左、下 / 右、上 為 offsetMin / offsetMax)
    private static readonly Vector2 HealthFillOffsetMin = new Vector2(7f, 10f);
    private static readonly Vector2 HealthFillOffsetMax = new Vector2(-7f, -5f);
    private const int HealthSegments = 4;
    private static readonly Color HealthFill = new Color(0.729f, 0.957f, 0.027f, 1f);
    private static readonly Color HealthTrail = new Color(1f, 1f, 1f, 0.76f);
    private static readonly Color HealthDivider = new Color(0f, 0f, 0f, 0.78f);

    private static void BuildPlayerStatus(RectTransform bottomLeft)
    {
        RectTransform status = NewRect("PlayerStatus", bottomLeft);
        SetRect(status, new Vector2(0f, 0f), new Vector2(BottomLeftMargin, BottomMargin), CardSize);

        // 頭像卡：卡片底當遮罩，立繪鋪滿卡片
        AddImage(status.gameObject, Sprite("Combat", "Combat_CardBg_EX"), Color.white);
        Mask mask = status.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;
        RectTransform portrait = NewRect("Portrait", status);
        Stretch(portrait, Vector2.zero, Vector2.zero);
        AddImage(portrait.gameObject, LoadSpriteFile(PortraitPath), Color.white);

        // 血條掛在頭像卡外 (不受遮罩裁切)，下緣切齊卡片
        RectTransform bar = NewRect("HealthBar", bottomLeft);
        bar.anchorMin = bar.anchorMax = Vector2.zero;
        bar.pivot = Vector2.zero;
        bar.anchoredPosition = new Vector2(BottomLeftMargin + CardSize.x * CardBottomRightEdge, BottomMargin);
        bar.sizeDelta = HealthBarSize;
        AddImage(bar.gameObject, Sprite("Combat", "Combat_Bg_Hp"), Color.white);

        RectTransform gauge = NewRect("Gauge", bar);
        Stretch(gauge, HealthFillOffsetMin, HealthFillOffsetMax);
        // 用全不透明的白色小圖：圖集裡的血條圖只有幾個像素大，Filled 模式不吃九宮格，
        // 拉長到 400px 時每個像素被放大近百倍，雙線性取樣會把旁邊的透明像素混進來，兩端糊成長長的漸層。
        // (不能不給 sprite：沒有 sprite 時 Image 的 Filled 模式不會生效，會畫成整條)
        Sprite gaugeSprite = SolidWhiteSprite();
        Image trail = AddFilledImage(NewRect("DamageTrail", gauge), gaugeSprite, HealthTrail);
        Image fill = AddFilledImage(NewRect("Fill", gauge), gaugeSprite, HealthFill);

        Sprite lineSprite = Sprite("Common", "Common_Line_Dot");
        for (int i = 1; i < HealthSegments; i++)
        {
            RectTransform line = NewRect("Divider_" + i, gauge);
            line.anchorMin = new Vector2((float)i / HealthSegments, 0f);
            line.anchorMax = new Vector2((float)i / HealthSegments, 1f);
            line.pivot = new Vector2(0.5f, 0.5f);
            line.anchoredPosition = Vector2.zero;
            line.sizeDelta = new Vector2(2f, 2f);
            Image lineImage = AddImage(line.gameObject, lineSprite, HealthDivider);
            lineImage.type = Image.Type.Simple;
        }

        PlayerHealthBarUI ui = bar.gameObject.AddComponent<PlayerHealthBarUI>();
        SerializedObject serialized = new SerializedObject(ui);
        serialized.FindProperty("fill").objectReferenceValue = fill;
        serialized.FindProperty("damageTrail").objectReferenceValue = trail;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Image AddFilledImage(RectTransform rect, Sprite sprite, Color color)
    {
        Stretch(rect, Vector2.zero, Vector2.zero);
        Image image = AddImage(rect.gameObject, sprite, color);
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        image.fillAmount = 1f;
        return image;
    }

    // 8×8 全白不透明的 sprite，給需要 Filled 模式又要邊緣俐落的長條使用
    private static Sprite SolidWhiteSprite()
    {
        string path = BadgeFolder + "/SolidWhite.png";
        if (!System.IO.File.Exists(path))
        {
            EnsureFolder(BadgeFolder);
            Texture2D texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
        }

        return LoadSpriteFile(path);
    }

    // 衝刺圖示：清掉去背殘留的雜點後另存，匯入時限制在 256 (原圖 1221×1288，其他圖示只有 128)
    private static Sprite CleanDashIcon()
    {
        string source = SkillIconFolder + "COMMON_SKILLICON_Dash.png";
        string target = BadgeFolder + "/SkillIcon_Dash_Clean.png";
        if (!System.IO.File.Exists(source))
        {
            Debug.LogWarning("[BattleHudBuilder] 找不到圖示 " + source);
            return null;
        }

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(System.IO.File.ReadAllBytes(source));
        texture.SetPixels32(IconCleanup.ClearFaintAlpha(texture.GetPixels32(), DashIconAlphaThreshold));
        EnsureFolder(BadgeFolder);
        System.IO.File.WriteAllBytes(target, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(target);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(target);
        importer.maxTextureSize = IconMaxSize;
        importer.SaveAndReimport();
        return LoadSpriteFile(target);
    }

    // 單張圖片 (例如角色立繪) 確保匯入成 Sprite 再載入
    // 圖示原圖四周有 15~25% 透明邊，直接照整張圖縮放時圖形看起來偏小；
    // 這裡以「不透明的部分」為準縮放並置中到 box 內，整張圖的 rect 可以超出 box (透明邊不影響畫面)
    private static void FitVisibleArea(RectTransform rect, Sprite sprite, Vector2 center, Vector2 box)
    {
        RectInt visible;
        if (sprite == null || !TryGetVisibleBounds(sprite, out visible))
        {
            PlaceAt(rect, center, new Vector2(box.y, box.y));
            return;
        }

        Rect full = sprite.rect;
        float scale = Mathf.Min(box.x / visible.width, box.y / visible.height);
        Vector2 visibleCenter = new Vector2(visible.x + visible.width * 0.5f, visible.y + visible.height * 0.5f);
        Vector2 offset = (new Vector2(full.width * 0.5f, full.height * 0.5f) - visibleCenter) * scale;
        PlaceAt(rect, center + offset, new Vector2(full.width, full.height) * scale);
    }

    // 從原始檔讀像素 (匯入設定不必開 Read/Write)，回傳 alpha > IconVisibleAlpha 的範圍 (以 sprite 的像素為單位，左下為原點)
    private static bool TryGetVisibleBounds(Sprite sprite, out RectInt bounds)
    {
        bounds = default;
        string path = AssetDatabase.GetAssetPath(sprite.texture);
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return false;

        Texture2D texture = new Texture2D(2, 2);
        try
        {
            if (!texture.LoadImage(System.IO.File.ReadAllBytes(path))) return false;

            Color32[] pixels = texture.GetPixels32();
            int width = texture.width;
            int height = texture.height;
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].a <= IconVisibleAlpha) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < 0) return false;

            // 原圖尺寸可能和匯入後的 sprite 不同 (maxTextureSize 縮小)，換算回 sprite 的像素
            float sx = sprite.rect.width / width;
            float sy = sprite.rect.height / height;
            bounds = new RectInt(Mathf.FloorToInt(minX * sx), Mathf.FloorToInt(minY * sy),
                Mathf.Max(1, Mathf.CeilToInt((maxX - minX + 1) * sx)), Mathf.Max(1, Mathf.CeilToInt((maxY - minY + 1) * sy)));
            return true;
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    private static Sprite LoadSpriteFile(string path)
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null)
        {
            Debug.LogWarning("[BattleHudBuilder] 找不到圖片 " + path);
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

    // ── 左上：放技能時的對話框 ─────────────────────────────────────────────────────
    // 蔚藍檔案左上的支援技能提示：方形頭像卡 (白底 + 深藍圓角框 + 立繪) + 右邊的對話泡泡。
    // 台詞用內建字型：蔚藍檔案的 LINE Seed 沒有中文字，內建字型會自動改用系統中文字型
    private static readonly Vector2 DialogPosition = new Vector2(16f, -Ba(44f + 70f) - 14f);
    private static readonly Vector2 DialogPortraitSize = new Vector2(Ba(154f), Ba(152f));
    private static readonly Vector2 DialogBubbleSize = new Vector2(330f, Ba(96f));
    private static readonly Color DialogOutline = new Color(0.212f, 0.275f, 0.341f, 1f);
    private static readonly Color DialogPortraitBack = new Color(1f, 0.675f, 0.514f, 1f);
    private static readonly Color DialogText = new Color(0.243f, 0.267f, 0.29f, 1f);

    private static void BuildSkillDialog(RectTransform topLeft)
    {
        RectTransform dialog = NewRect("SkillDialog", topLeft);
        SetRect(dialog, new Vector2(0f, 1f), DialogPosition, new Vector2(DialogPortraitSize.x + DialogBubbleSize.x, DialogPortraitSize.y));
        RectTransform panel = NewRect("Panel", dialog);
        Stretch(panel, Vector2.zero, Vector2.zero);

        // 頭像卡：白色圓角底 → 桃色底當遮罩 + 立繪 → 深藍圓角外框
        RectTransform card = NewRect("Portrait", panel);
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0f, 0.5f);
        card.anchoredPosition = Vector2.zero;
        card.sizeDelta = DialogPortraitSize;
        AddImage(card.gameObject, Sprite("Common", "Common_Tab_Normal_Btn"), Color.white);

        RectTransform inner = NewRect("Inner", card);
        Stretch(inner, new Vector2(Ba(9f), Ba(12f)), new Vector2(-Ba(9f), -Ba(12f)));
        AddImage(inner.gameObject, Sprite("Combat", "Combat_Textbg"), DialogPortraitBack);
        inner.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        RectTransform face = NewRect("Face", inner);
        Stretch(face, new Vector2(-12f, -6f), new Vector2(12f, 6f));
        Image faceImage = AddImage(face.gameObject, LoadSpriteFile(PortraitPath), Color.white);
        faceImage.preserveAspect = true;

        RectTransform outline = NewRect("Outline", card);
        Stretch(outline, new Vector2(Ba(6f), Ba(9f)), new Vector2(-Ba(6f), -Ba(9f)));
        AddImage(outline.gameObject, Sprite("Common", "Common_Line_Raius20px"), DialogOutline);

        // 對話泡泡：尾巴在左邊，接在頭像卡右側
        RectTransform bubble = NewRect("Bubble", panel);
        bubble.anchorMin = bubble.anchorMax = bubble.pivot = new Vector2(0f, 0.5f);
        bubble.anchoredPosition = new Vector2(DialogPortraitSize.x - 4f, 0f);
        bubble.sizeDelta = DialogBubbleSize;
        AddImage(bubble.gameObject, Sprite("Combat", "Combat_Balloon_Sup"), Color.white);

        Text line = AddText(NewRect("Label_Line", bubble), Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), "", 26f, TextAnchor.MiddleLeft);
        line.color = DialogText;
        line.fontStyle = FontStyle.Bold;
        Stretch(line.rectTransform, new Vector2(Ba(46f), 0f), new Vector2(-14f, 0f));

        SkillDialogUI ui = dialog.gameObject.AddComponent<SkillDialogUI>();
        SerializedObject serialized = new SerializedObject(ui);
        serialized.FindProperty("panel").objectReferenceValue = panel.gameObject;
        serialized.FindProperty("lineLabel").objectReferenceValue = line;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        panel.gameObject.SetActive(false);
    }

    // ── 上方中間：Boss 血條 (蔚藍檔案 Boss 戰樣式) ─────────────────────────────────
    // 左邊是 Boss 頭像框 (Ingame_Boss_Portrait + 頭像 + BOSS 字樣)，右邊是多層血條：
    // 深藍框 → 下一層顏色 → 白色殘影 → 目前這層 → 3 條分隔線，上方左邊名稱、右邊血量數字，尾端「x N」剩餘層數
    private const string BossPortraitPath = "Assets/STG game/resources/model/BattleUI/02_Tactics/06_Raid/Portrait_Raidboss_Perorozilla.png";
    private static readonly Vector2 BossBarSize = new Vector2(Ba(1100f), Ba(68f));
    private static readonly Vector2 BossPortraitFrameSize = new Vector2(Ba(150f), Ba(170f));
    private static readonly Vector2 BossPortraitSize = new Vector2(Ba(209f), Ba(213f));
    private static readonly Vector2 BossPortraitOffset = new Vector2(Ba(2.7f), Ba(23.5f));
    private const float BossBarTop = 40f;
    private static readonly Color BossNextLayerDim = new Color(1f, 1f, 1f, 1f);
    private static readonly Color BossTrail = new Color(1f, 1f, 1f, 0.76f);

    private static void BuildBossHealthBar(RectTransform topCenter, Font font)
    {
        RectTransform root = NewRect("BossHealthBar", topCenter);
        SetRect(root, new Vector2(0.5f, 1f), new Vector2(BossPortraitFrameSize.x * 0.5f, -BossBarTop), BossBarSize);
        RectTransform panel = NewRect("Panel", root);
        Stretch(panel, Vector2.zero, Vector2.zero);

        // 頭像框在血條左邊
        RectTransform frame = NewRect("Portrait", panel);
        frame.anchorMin = frame.anchorMax = new Vector2(0f, 0.5f);
        frame.pivot = new Vector2(1f, 0.5f);
        frame.anchoredPosition = new Vector2(-4f, -Ba(10f));
        frame.sizeDelta = BossPortraitFrameSize;
        Image frameImage = AddImage(frame.gameObject, Sprite("Combat", "Ingame_Boss_Portrait"), Color.white);
        frameImage.type = Image.Type.Simple;
        RectTransform face = NewRect("Face", frame);
        PlaceAt(face, BossPortraitFrameSize * 0.5f + BossPortraitOffset, BossPortraitSize);
        // 預設頭像；Boss 出場時 BossEncounter 依 BossProfile 換成該 Boss 的頭像
        Image faceImage = AddImage(face.gameObject, LoadSpriteFile(BossPortraitPath), Color.white);
        faceImage.preserveAspect = true;
        RectTransform bossFont = NewRect("BossFont", frame);
        PlaceAt(bossFont, BossPortraitFrameSize * 0.5f + new Vector2(0f, Ba(-65f)), new Vector2(Ba(94f), Ba(40f)));
        AddImage(bossFont.gameObject, Sprite("Combat", "Ingame_Boss_Imagefont"), Color.white).type = Image.Type.Simple;

        // 血條本體
        RectTransform bar = NewRect("Bar", panel);
        Stretch(bar, Vector2.zero, Vector2.zero);
        AddImage(bar.gameObject, Sprite("Combat", "Combat_Bg_Hp"), Color.white);
        RectTransform gauge = NewRect("Gauge", bar);
        Stretch(gauge, HealthFillOffsetMin, HealthFillOffsetMax);
        Sprite solid = SolidWhiteSprite();
        RectTransform nextRect = NewRect("NextLayer", gauge);
        Stretch(nextRect, Vector2.zero, Vector2.zero);
        Image next = AddImage(nextRect.gameObject, solid, BossNextLayerDim);
        Image trail = AddFilledImage(NewRect("DamageTrail", gauge), solid, BossTrail);
        Image current = AddFilledImage(NewRect("Current", gauge), solid, Color.red);
        Sprite lineSprite = Sprite("Common", "Common_Line_Dot");
        for (int i = 1; i < HealthSegments; i++)
        {
            RectTransform line = NewRect("Divider_" + i, gauge);
            line.anchorMin = new Vector2((float)i / HealthSegments, 0f);
            line.anchorMax = new Vector2((float)i / HealthSegments, 1f);
            line.pivot = new Vector2(0.5f, 0.5f);
            line.anchoredPosition = Vector2.zero;
            line.sizeDelta = new Vector2(2f, 2f);
            AddImage(line.gameObject, lineSprite, HealthDivider).type = Image.Type.Simple;
        }

        // 文字：名稱 (左上)、血量 (右上)、剩餘層數 (尾端)
        Text name = AddText(NewRect("Label_Name", panel), font, "Boss", 26f, TextAnchor.LowerLeft);
        SetTextAbove(name.rectTransform, new Vector2(0f, 1f), new Vector2(6f, 0f));
        AddOutline(name.gameObject, PanelNavy, 1.5f);
        Text value = AddText(NewRect("Label_HP", panel), font, "0 / 0", 20f, TextAnchor.LowerRight);
        SetTextAbove(value.rectTransform, new Vector2(1f, 1f), new Vector2(-6f, 0f));
        AddOutline(value.gameObject, PanelNavy, 1.5f);
        Text layers = AddText(NewRect("Label_Layers", panel), font, "x1", 34f, TextAnchor.MiddleLeft);
        layers.fontStyle = FontStyle.Italic;
        layers.rectTransform.anchorMin = layers.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        layers.rectTransform.pivot = new Vector2(0f, 0.5f);
        layers.rectTransform.anchoredPosition = new Vector2(8f, 0f);
        layers.rectTransform.sizeDelta = new Vector2(80f, BossBarSize.y);
        AddOutline(layers.gameObject, PanelNavy, 2f);

        EnemyHealthBarUI ui = root.gameObject.AddComponent<EnemyHealthBarUI>();
        SerializedObject serialized = new SerializedObject(ui);
        serialized.FindProperty("panel").objectReferenceValue = panel.gameObject;
        serialized.FindProperty("currentFill").objectReferenceValue = current;
        serialized.FindProperty("nextLayer").objectReferenceValue = next;
        serialized.FindProperty("damageTrail").objectReferenceValue = trail;
        serialized.FindProperty("valueText").objectReferenceValue = value;
        serialized.FindProperty("nameText").objectReferenceValue = name;
        serialized.FindProperty("layerCountText").objectReferenceValue = layers;
        serialized.FindProperty("portraitImage").objectReferenceValue = faceImage;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        panel.gameObject.SetActive(false);
    }

    // 文字放在血條上緣外側 (corner = 對齊的角，offset = 往內縮)
    private static void SetTextAbove(RectTransform rect, Vector2 corner, Vector2 offset)
    {
        rect.anchorMin = rect.anchorMax = corner;
        rect.pivot = new Vector2(corner.x, 0f);
        rect.anchoredPosition = offset + new Vector2(0f, 2f);
        rect.sizeDelta = new Vector2(400f, 34f);
    }

    // ── 右下：四張卡 (衝刺 / 護盾 / F 大招 / 特殊技能) ───────────────────────────
    // 蔚藍檔案 EX 技能卡：平行四邊形卡底當遮罩 + 技能圖示，左上角圓形標籤 (原作是費用，這裡寫按鍵)，下方技能名稱。
    // 四張的總寬維持原本三張 (SkillAreaWidth)，所以每張依比例縮小；卡底比欄位寬一點，相鄰的卡會稍微重疊 (原作也是)
    private const float CardsAboveGauges = 10f;
    // 技能區 (與能量格) 的總寬度 = 蔚藍檔案三張卡 (每張 219 寬、間距 12)；必須宣告在下面用到它的欄位之前 (靜態欄位依序初始化)
    public static readonly float SkillAreaWidth = Ba(219f) * 3f + Ba(12f) * 2f;
    private const int SkillSlotCount = 4;
    private static readonly float SkillSlotGap = Ba(12f);
    private static readonly float SkillSlotWidth = (SkillAreaWidth - SkillSlotGap * (SkillSlotCount - 1)) / SkillSlotCount;
    // 原作每張卡佔 219 寬時卡底是 CardSize；卡上所有東西 (標籤、名稱條、字) 都照這個比例縮
    private static readonly float SkillCardScale = SkillSlotWidth / Ba(219f);
    private static readonly Vector2 SkillCardSize = CardSize * SkillCardScale;
    private static readonly Vector2 KeyTagSize = new Vector2(Ba(72f), Ba(72f)) * SkillCardScale;
    // 原作費用標籤中心相對卡片中心的位置 (-60.8, 89.5)
    private static readonly Vector2 KeyTagOffset = new Vector2(Ba(-60.8f), Ba(89.5f)) * SkillCardScale;
    private static readonly float NameStripHeight = 26f * SkillCardScale;
    private static readonly Color NameStripColor = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.35f);
    private static readonly Color CooldownColor = new Color(0f, 0f, 0f, 0.6f);
    private static readonly Color ActiveGlowColor = new Color(0.3f, 1f, 0.35f, 0.95f);
    private const float ActiveGlowScale = 1.08f;

    private struct CardSpec
    {
        public SkillCardUI.SkillKind kind;
        // 護盾卡不是技能 (沒有冷卻/鎖頭)，只顯示目前的護盾顏色
        public bool isShield;
        public string name;
        public string key;
        public Sprite icon;
    }

    // 技能圖示 (白色圖形)：卡底改成深藍才看得清楚；圖示放在名稱條上方的中間
    private const string SkillIconFolder = "Assets/STG game/resources/model/BattleUI/Skillicon/";
    private static readonly Color IconCardColor = new Color(0.1f, 0.2f, 0.38f, 1f);
    // 圖示「看得到的圖形」要填滿的範圍 (名稱條上方)：卡片是平行四邊形，寬度只取中間不會被斜邊切到的部分
    private const float IconBoxWidthRatio = 0.78f;
    private const float IconBoxHeightRatio = 0.92f;
    private const byte IconVisibleAlpha = 20;
    // 衝刺圖示原圖去背不完全 (背景殘留 alpha 1~4 的雜點)，清理後另存一份使用，不改動原圖
    private const byte DashIconAlphaThreshold = 4;
    private const int IconMaxSize = 256;

    private static void BuildSkillCards(RectTransform bottomRight, Font font)
    {
        CardSpec[] cards =
        {
            new CardSpec { kind = SkillCardUI.SkillKind.Dash, name = "Dash", key = "Shift", icon = CleanDashIcon() },
            new CardSpec { isShield = true, name = "Shield", key = "Space", icon = LoadSpriteFile(SkillIconFolder + "COMMON_SKILLICON_SHIELD.png") },
            new CardSpec { kind = SkillCardUI.SkillKind.Burst, name = "Burst", key = "F", icon = LoadSpriteFile(SkillIconFolder + "COMMON_SKILLICON_EN0004_SCENARIOEXSKILL01.png") },
            new CardSpec { kind = SkillCardUI.SkillKind.Special, name = "Special", key = null, icon = LoadSpriteFile(SkillIconFolder + "COMMON_SKILLICON_CROSS.png") },
        };

        float gaugesHeight = BadgeSize.y * 2f + GaugeRowSpacing;
        RectTransform area = NewRect("SkillCards", bottomRight);
        SetRect(area, new Vector2(1f, 0f), new Vector2(-BottomRightMargin, BottomMargin + gaugesHeight + CardsAboveGauges), new Vector2(SkillAreaWidth, SkillCardSize.y));

        for (int i = 0; i < cards.Length; i++)
        {
            float centerX = SkillSlotWidth * 0.5f + i * (SkillSlotWidth + SkillSlotGap);
            BuildSkillCard(area, cards[i], new Vector2(centerX, SkillCardSize.y * 0.5f), font);
        }
    }

    private static void BuildSkillCard(RectTransform area, CardSpec spec, Vector2 center, Font font)
    {
        Sprite cardSprite = Sprite("Combat", "Combat_CardBg_EX");
        RectTransform slot = NewRect("Card_" + spec.name, area);
        PlaceAt(slot, center, SkillCardSize);

        // 發動中的綠色外框：比卡片大一點的卡底放在卡片後面 (護盾卡沒有發動狀態)
        RectTransform glow = null;
        if (!spec.isShield)
        {
            glow = NewRect("ActiveGlow", slot);
            PlaceAt(glow, SkillCardSize * 0.5f, SkillCardSize * ActiveGlowScale);
            AddImage(glow.gameObject, cardSprite, ActiveGlowColor);
        }

        // 卡片本體 (遮罩內：圖示、名稱、暗色、冷卻、秒數、鎖頭)
        RectTransform card = NewRect("Card", slot);
        Stretch(card, Vector2.zero, Vector2.zero);
        Image cardImage = AddImage(card.gameObject, cardSprite, IconCardColor);
        card.gameObject.AddComponent<Mask>().showMaskGraphic = true;

        RectTransform art = NewRect("Icon", card);
        Vector2 iconBox = new Vector2(SkillCardSize.x * IconBoxWidthRatio, (SkillCardSize.y - NameStripHeight) * IconBoxHeightRatio);
        Vector2 iconCenter = new Vector2(SkillCardSize.x * 0.5f, NameStripHeight + (SkillCardSize.y - NameStripHeight) * 0.5f);
        FitVisibleArea(art, spec.icon, iconCenter, iconBox);
        Image iconImage = AddImage(art.gameObject, spec.icon, Color.white);
        iconImage.preserveAspect = true;

        // 名稱底條、暗色、冷卻遮罩都用卡底圖本身 (同樣的平行四邊形)：圖集裡卡底四周的透明邊有幾乎看不見的雜點，
        // 用純色方塊時遮罩會把那些雜點當成卡片範圍，暗色從卡片兩側漏出一條條橫線
        RectTransform strip = NewRect("NameStrip", card);
        Stretch(strip, Vector2.zero, Vector2.zero);
        Image stripImage = AddImage(strip.gameObject, cardSprite, NameStripColor);
        stripImage.type = Image.Type.Filled;
        stripImage.fillMethod = Image.FillMethod.Vertical;
        stripImage.fillOrigin = (int)Image.OriginVertical.Bottom;
        stripImage.fillAmount = NameStripHeight / SkillCardSize.y;
        Text name = AddText(NewRect("Label_Name", card), font, spec.name, CardFont(18f), TextAnchor.MiddleCenter);
        name.rectTransform.anchorMin = Vector2.zero;
        name.rectTransform.anchorMax = new Vector2(1f, 0f);
        name.rectTransform.pivot = new Vector2(0.5f, 0f);
        name.rectTransform.anchoredPosition = Vector2.zero;
        name.rectTransform.sizeDelta = new Vector2(0f, NameStripHeight);

        BuildKeyTag(slot, spec.key, font);

        if (spec.isShield)
        {
            ShieldCardUI shield = slot.gameObject.AddComponent<ShieldCardUI>();
            SerializedObject shieldSerialized = new SerializedObject(shield);
            shieldSerialized.FindProperty("background").objectReferenceValue = cardImage;
            shieldSerialized.ApplyModifiedPropertiesWithoutUndo();
            return;
        }

        BuildSkillState(slot, card, cardSprite, glow, spec, font);
    }

    // 卡上的字跟著卡片比例縮小，但不小於 MinCardFont 才看得清楚
    private const float MinCardFont = 12f;

    private static float CardFont(float originalSize)
    {
        return Mathf.Max(MinCardFont, originalSize * SkillCardScale);
    }

    // 左上圓形標籤寫按鍵 (遮罩外，可以超出卡片邊緣)
    private static void BuildKeyTag(RectTransform slot, string keyName, Font font)
    {
        if (string.IsNullOrEmpty(keyName)) return;

        RectTransform tag = NewRect("KeyTag", slot);
        PlaceAt(tag, SkillCardSize * 0.5f + KeyTagOffset, KeyTagSize);
        Image tagImage = AddImage(tag.gameObject, Sprite("Combat", "Combat_CardBg_EX_CostTag"), Color.white);
        tagImage.type = Image.Type.Simple;
        Text key = AddText(NewRect("Label_Key", tag), font, keyName, keyName.Length > 1 ? CardFont(15f) : CardFont(30f), TextAnchor.MiddleCenter);
        Stretch(key.rectTransform, Vector2.zero, Vector2.zero);
        // 「Space」比標籤寬，讓字自動縮到放得下
        key.resizeTextForBestFit = true;
        key.resizeTextMinSize = 8;
        key.resizeTextMaxSize = key.fontSize;
    }

    // 技能卡的狀態外觀 (暗色、冷卻、秒數、鎖頭、發動外框) 與 SkillCardUI
    private static void BuildSkillState(RectTransform slot, RectTransform card, Sprite cardSprite, RectTransform glow, CardSpec spec, Font font)
    {
        RectTransform dim = NewRect("Dim", card);
        Stretch(dim, Vector2.zero, Vector2.zero);
        AddImage(dim.gameObject, cardSprite, DimColor);

        // 冷卻/發動剩餘量：圓形倒數，暗色從 12 點鐘方向像時鐘指針一樣順時針退掉
        // (fillAmount = 剩餘比例；逆時針填色 = 亮的部分順時針長回來)
        RectTransform cooldown = NewRect("Cooldown", card);
        Stretch(cooldown, Vector2.zero, Vector2.zero);
        Image cooldownImage = AddImage(cooldown.gameObject, cardSprite, CooldownColor);
        cooldownImage.type = Image.Type.Filled;
        cooldownImage.fillMethod = Image.FillMethod.Radial360;
        cooldownImage.fillOrigin = (int)Image.Origin360.Top;
        cooldownImage.fillClockwise = false;

        Text seconds = AddText(NewRect("Label_Seconds", card), font, "0", CardFont(54f), TextAnchor.MiddleCenter);
        Stretch(seconds.rectTransform, Vector2.zero, Vector2.zero);
        AddOutline(seconds.gameObject, new Color(0f, 0f, 0f, 0.8f), 2f);

        RectTransform lockRect = NewRect("Lock", card);
        PlaceAt(lockRect, SkillCardSize * 0.5f, new Vector2(49f, 62f) * 0.8f * SkillCardScale);
        Image lockImage = AddImage(lockRect.gameObject, Sprite("Common", "Common_Icon_Lock"), Color.white);
        lockImage.type = Image.Type.Simple;

        SkillCardUI ui = slot.gameObject.AddComponent<SkillCardUI>();
        SerializedObject serialized = new SerializedObject(ui);
        serialized.FindProperty("kind").enumValueIndex = (int)spec.kind;
        serialized.FindProperty("dimOverlay").objectReferenceValue = dim.gameObject;
        serialized.FindProperty("cooldownFill").objectReferenceValue = cooldownImage;
        serialized.FindProperty("secondsLabel").objectReferenceValue = seconds;
        serialized.FindProperty("lockIcon").objectReferenceValue = lockRect.gameObject;
        serialized.FindProperty("activeHighlight").objectReferenceValue = glow.gameObject;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // 預設狀態 (可用)：遮罩類先關掉，執行時由 SkillCardUI 依狀態開關
        dim.gameObject.SetActive(false);
        cooldown.gameObject.SetActive(false);
        seconds.gameObject.SetActive(false);
        lockRect.gameObject.SetActive(spec.kind == SkillCardUI.SkillKind.Special);
        glow.gameObject.SetActive(false);
    }

    // ── 右下：藍 (Pulse) / 黃 (Flame) 能量條 ──────────────────────────────────────
    // 蔚藍檔案 COST 條的樣式：前面是圓形數字牌 (上方小字 + 中間數字)，後面 10 格 (每格 10%)。
    // 10 格的總寬度與上方技能卡區 (SkillAreaWidth) 切齊，數字牌在格子左邊；整塊切齊畫面右下
    private const string BadgeFolder = "Assets/STG game/resources/prefab/UI";
    private const float BottomRightMargin = 24f;
    private const float BottomMargin = 18f;
    private static readonly Vector2 BadgeSize = new Vector2(50f, 50f * CostBadgeArt.Height / CostBadgeArt.Width);
    private const float GaugeRowSpacing = 6f;
    private const float BadgeToGaugeGap = 6f;
    private const int EnergySegments = 10;
    private const float SegmentGap = 3f;
    private const float SegmentHeight = 24f;
    // 填色比底框內縮一點，露出深色邊 (蔚藍檔案 70×40 底框、66×36 填色)
    private const float SegmentInset = 1.5f;
    private static readonly Color SegmentBackground = new Color(0.094f, 0.141f, 0.259f, 1f);
    private static readonly Color PulseFill = new Color(0f, 0.68f, 1f, 1f);
    private static readonly Color FlameFill = new Color(1f, 0.72f, 0.1f, 1f);
    private static readonly Color PulseOutline = new Color(0.02f, 0.2f, 0.5f, 1f);
    private static readonly Color FlameOutline = new Color(0.5f, 0.22f, 0f, 1f);

    private static void BuildEnergyGauges(RectTransform bottomRight, Font font)
    {
        Sprite pulseBadge = BakeBadge("CostBadge_Pulse", false);
        Sprite flameBadge = BakeBadge("CostBadge_Flame", true);

        // 只有 10 格與技能卡等寬切齊，數字牌放在格子左邊 (蔚藍檔案的 COST 數字也在格子左側外面)
        RectTransform area = NewRect("EnergyGauges", bottomRight);
        float rowHeight = BadgeSize.y;
        SetRect(area, new Vector2(1f, 0f), new Vector2(-BottomRightMargin, BottomMargin),
            new Vector2(SkillAreaWidth + BadgeSize.x + BadgeToGaugeGap, rowHeight * 2f + GaugeRowSpacing));

        // Flame 在下、Pulse 在上
        BuildGaugeRow(area, "Gauge_Flame", 0f, BulletPolarity.Yellow, "Flame", flameBadge, FlameFill, FlameOutline, font);
        BuildGaugeRow(area, "Gauge_Pulse", rowHeight + GaugeRowSpacing, BulletPolarity.Blue, "Pulse", pulseBadge, PulseFill, PulseOutline, font);
    }

    private static void BuildGaugeRow(RectTransform area, string name, float bottom, BulletPolarity polarity, string caption,
        Sprite badgeSprite, Color fillColor, Color outlineColor, Font font)
    {
        RectTransform row = NewRect(name, area);
        row.anchorMin = new Vector2(0f, 0f);
        row.anchorMax = new Vector2(1f, 0f);
        row.pivot = new Vector2(0.5f, 0f);
        row.anchoredPosition = new Vector2(0f, bottom);
        row.sizeDelta = new Vector2(0f, BadgeSize.y);

        // 數字牌：小字放在原本「COST」的位置，數字放在內圓正中
        RectTransform badge = NewRect("Badge", row);
        badge.anchorMin = badge.anchorMax = badge.pivot = new Vector2(0f, 0f);
        badge.anchoredPosition = Vector2.zero;
        badge.sizeDelta = BadgeSize;
        AddImage(badge.gameObject, badgeSprite, Color.white);

        Vector2 circleCenter = BadgePoint(63f, 68.5f);
        Text number = AddText(NewRect("Label_Num", badge), font, "0", BadgeSize.x * 0.5f, TextAnchor.MiddleCenter);
        PlaceAt(number.rectTransform, circleCenter, BadgeSize);
        AddOutline(number.gameObject, outlineColor, 1.5f);

        Text captionText = AddText(NewRect("Label_Caption", badge), font, caption, BadgeSize.x * 0.3f, TextAnchor.MiddleCenter);
        captionText.fontStyle = FontStyle.Italic;
        PlaceAt(captionText.rectTransform, BadgePoint(63f, 22f), new Vector2(BadgeSize.x * 1.4f, BadgeSize.y * 0.4f));
        AddOutline(captionText.gameObject, outlineColor, 1.5f);

        // 10 格：與內圓中心對齊
        float gaugeLeft = BadgeSize.x + BadgeToGaugeGap;
        float segmentWidth = (SkillAreaWidth - SegmentGap * (EnergySegments - 1)) / EnergySegments;
        Sprite backgroundSprite = Sprite("Common", "Common_Bg_Raius5px");
        Sprite fillSprite = Sprite("Combat", "Combat_Cost_Gauge");
        Image[] fills = new Image[EnergySegments];
        for (int i = 0; i < EnergySegments; i++)
        {
            RectTransform segment = NewRect("Segment_" + i, row);
            segment.anchorMin = segment.anchorMax = new Vector2(0f, 0f);
            segment.pivot = new Vector2(0f, 0.5f);
            segment.anchoredPosition = new Vector2(gaugeLeft + i * (segmentWidth + SegmentGap), circleCenter.y);
            segment.sizeDelta = new Vector2(segmentWidth, SegmentHeight);
            AddImage(segment.gameObject, backgroundSprite, SegmentBackground);

            RectTransform fill = NewRect("Fill", segment);
            Stretch(fill, new Vector2(SegmentInset, SegmentInset), new Vector2(-SegmentInset, -SegmentInset));
            Image fillImage = AddImage(fill.gameObject, fillSprite, fillColor);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 0f;
            fills[i] = fillImage;
        }

        EnergyGaugeUI gauge = row.gameObject.AddComponent<EnergyGaugeUI>();
        SerializedObject serialized = new SerializedObject(gauge);
        serialized.FindProperty("polarity").enumValueIndex = (int)polarity;
        serialized.FindProperty("numberLabel").objectReferenceValue = number;
        SerializedProperty segments = serialized.FindProperty("segmentFills");
        segments.arraySize = fills.Length;
        for (int i = 0; i < fills.Length; i++) segments.GetArrayElementAtIndex(i).objectReferenceValue = fills[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // 數字牌原圖 (117×123，原點左上) 上的一點，換算成數字牌 RectTransform 內 (原點左下) 的位置
    private static Vector2 BadgePoint(float x, float y)
    {
        return new Vector2(x / CostBadgeArt.Width * BadgeSize.x, (1f - y / CostBadgeArt.Height) * BadgeSize.y);
    }

    private static void PlaceAt(RectTransform rect, Vector2 center, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = center;
        rect.sizeDelta = size;
    }

    private static void AddOutline(GameObject target, Color color, float distance)
    {
        Outline outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(distance, -distance);
    }

    // 從 Combat 圖集切出 COST 數字牌，去掉「COST」字樣 (flame 時再轉成暖色)，存成獨立的 sprite
    private static Sprite BakeBadge(string fileName, bool flame)
    {
        string definitionPath = "Assets/STG game/resources/prefab/UI_Prefabs/ExportedProject/Assets/_MX/UIs/Atlas/Combat.asset";
        NguiAtlas.Entry entry = default;
        foreach (NguiAtlas.Entry candidate in NguiAtlas.Parse(System.IO.File.ReadAllText(definitionPath)))
        {
            if (candidate.name == "Combat_CostBg") entry = candidate;
        }

        Texture2D atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        atlas.LoadImage(System.IO.File.ReadAllBytes(AtlasFolder + "Combat.png"));
        Rect rect = NguiAtlas.ToUnityRect(entry.rect, atlas.height);
        Color32[] pixels = CostBadgeArt.RemoveCaption(ToColor32(atlas.GetPixels((int)rect.x, (int)rect.y, CostBadgeArt.Width, CostBadgeArt.Height)));
        Object.DestroyImmediate(atlas);
        if (flame)
        {
            for (int i = 0; i < pixels.Length; i++) pixels[i] = CostBadgeArt.ToFlame(pixels[i]);
        }

        Texture2D badge = new Texture2D(CostBadgeArt.Width, CostBadgeArt.Height, TextureFormat.RGBA32, false);
        badge.SetPixels32(pixels);
        EnsureFolder(BadgeFolder);
        string path = BadgeFolder + "/" + fileName + ".png";
        System.IO.File.WriteAllBytes(path, badge.EncodeToPNG());
        Object.DestroyImmediate(badge);

        AssetDatabase.ImportAsset(path);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Color32[] ToColor32(Color[] colors)
    {
        Color32[] result = new Color32[colors.Length];
        for (int i = 0; i < colors.Length; i++) result[i] = colors[i];
        return result;
    }

    // 每次重建都換掉場景中的 HUD：Prefab 是整個重新產生的，舊實例與 Prefab 的連結會斷掉，留著會變成兩份
    private static void PlaceInScene(GameObject prefab)
    {
        UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            if (rootObject.name == prefab.name) Object.DestroyImmediate(rootObject);
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);

        // Boss 出場時要接上的血條改成新 HUD 裡的這一條 (HUD 每次重建都是新實例，舊的參照會斷)
        BossEncounter encounter = Object.FindFirstObjectByType<BossEncounter>();
        if (encounter != null)
        {
            SerializedObject serialized = new SerializedObject(encounter);
            serialized.FindProperty("bossHealthBar").objectReferenceValue = instance.GetComponentInChildren<EnemyHealthBarUI>(true);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static float Ba(float value) => value * BaScale;

    private static RectTransform NewRect(string name, Transform parent)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    // 以左上角 (pivot) 為準擺放：position 為 pivot 相對區塊原點的位置
    private static void SetRect(RectTransform rect, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.parent is RectTransform parent ? parent.pivot : pivot;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    // 有九宮格邊界的 sprite 用 Sliced，拉長不變形
    private static Image AddImage(GameObject target, Sprite sprite, Color color)
    {
        Image image = target.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.raycastTarget = false;
        return image;
    }

    private static Text AddText(RectTransform rect, Font font, string content, float size, TextAnchor alignment)
    {
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = Mathf.RoundToInt(size);
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static Sprite Sprite(string atlas, string name)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(AtlasFolder + atlas + ".png"))
        {
            if (asset is Sprite sprite && sprite.name == name) return sprite;
        }

        Debug.LogWarning("[BattleHudBuilder] 找不到 sprite " + atlas + "/" + name + "，請先執行 Tools/STG/Slice BA UI Atlases");
        return null;
    }

    private static void SetReference(Object target, string field, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;

        string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
    }
}
#endif
