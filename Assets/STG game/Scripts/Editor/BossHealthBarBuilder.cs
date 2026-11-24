#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 在目前場景的 Canvas 上建立 Boss 血條 (畫面上方置中)：沿用玩家血條 (HealthBarPanel) 的外觀加寬，
// 血條正中間寫 Boss 名稱、最右邊寫「目前血量 / 最大血量」，並接到 BossEncounter。可重複執行，舊的 Boss 血條會先移除
public static class BossHealthBarBuilder
{
    private const string PlayerBarName = "HealthBarPanel";
    private const string BossBarName = "BossHealthBar";
    private static readonly Vector2 BarSize = new Vector2(900f, 40f);
    private const float TopMargin = 24f;
    private const int NameFontSize = 24;
    private const int ValueFontSize = 22;
    // 血量數字離右緣的距離
    private const float ValuePadding = 12f;

    [MenuItem("Tools/STG/Build Boss Health Bar")]
    private static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[BossHealthBarBuilder] 請先離開 Play Mode 再執行。");
            return;
        }

        GameObject playerBar = GameObject.Find(PlayerBarName);
        if (playerBar == null)
        {
            Debug.LogError("[BossHealthBarBuilder] 場景中找不到 " + PlayerBarName + "，無法沿用血條外觀");
            return;
        }

        if (playerBar.scene.isDirty)
        {
            Debug.LogWarning("[BossHealthBarBuilder] 場景有未存的變更，請先存檔或還原後再執行 (本工具會存檔場景)");
            return;
        }

        Transform canvas = playerBar.transform.parent;
        Transform old = canvas.Find(BossBarName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // 常駐的根物件 (掛 EnemyHealthBarUI)，只開關底下的 Panel
        RectTransform root = new GameObject(BossBarName, typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(canvas, false);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        root.anchoredPosition = new Vector2(0f, -TopMargin);
        root.sizeDelta = BarSize;

        GameObject panel = Object.Instantiate(playerBar, root);
        panel.name = "Panel";
        foreach (HealthBarUI playerUi in panel.GetComponentsInChildren<HealthBarUI>(true)) Object.DestroyImmediate(playerUi);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(0f, BarSize.y);

        Slider slider = panel.GetComponentInChildren<Slider>(true);
        Text valueText = panel.GetComponentInChildren<Text>(true);
        StyleValueText(valueText);
        Text nameText = CreateNameText(panel.transform, valueText);

        EnemyHealthBarUI ui = root.gameObject.AddComponent<EnemyHealthBarUI>();
        SerializedObject serialized = new SerializedObject(ui);
        serialized.FindProperty("panel").objectReferenceValue = panel;
        serialized.FindProperty("healthSlider").objectReferenceValue = slider;
        serialized.FindProperty("valueText").objectReferenceValue = valueText;
        serialized.FindProperty("nameText").objectReferenceValue = nameText;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        panel.SetActive(false);

        BossEncounter encounter = Object.FindFirstObjectByType<BossEncounter>();
        if (encounter != null)
        {
            SerializedObject encounterSerialized = new SerializedObject(encounter);
            encounterSerialized.FindProperty("bossHealthBar").objectReferenceValue = ui;
            encounterSerialized.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning("[BossHealthBarBuilder] 場景中沒有 BossEncounter，血條不會自動接上 Boss");
        }

        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        EditorSceneManager.SaveScene(root.gameObject.scene);
        Debug.Log("[BossHealthBarBuilder] 已建立 Boss 血條");
    }

    // 血量數字 (沿用玩家血條的文字) 改成貼血條右緣
    private static void StyleValueText(Text valueText)
    {
        if (valueText == null) return;

        RectTransform rect = valueText.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = new Vector2(-ValuePadding, 0f);
        valueText.alignment = TextAnchor.MiddleRight;
        valueText.fontSize = ValueFontSize;
        if (valueText.GetComponent<Shadow>() == null) valueText.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1.5f, -1.5f);
    }

    // 名稱寫在血條正中間，字型沿用血量數字
    private static Text CreateNameText(Transform panel, Text styleSource)
    {
        RectTransform rect = new GameObject("NameText", typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(panel, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Text text = rect.gameObject.AddComponent<Text>();
        text.font = styleSource != null ? styleSource.font : null;
        text.fontSize = NameFontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        // 疊在紅色血條上也看得清楚
        rect.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
        return text;
    }
}
#endif
