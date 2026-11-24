#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 在目前場景建立 Boss 結算面板 (沿用 Game Over 畫面的半透明黑底與字型)，
// 並把 CombatPhaseController 接到 BossEncounter (Boss 被擊倒後回到一般戰鬥用)。
// 結算面板目前先不接：要用時在 BossEncounter 指定 resultScreen，並把 defeatOutcome 改成 ShowResults。
// 可重複執行，舊的結算面板會先移除；場景有未存的變更時不執行，避免連同使用者的改動一起存檔
public static class BossResultScreenBuilder
{
    private const string GameOverName = "GameOverScreen";
    private const string ResultName = "BossResultScreen";
    private const string Title = "BOSS DEFEATED";

    [MenuItem("Tools/STG/Build Boss Result Screen")]
    private static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[BossResultScreenBuilder] 請先離開 Play Mode 再執行。");
            return;
        }

        GameObject gameOver = GameObject.Find(GameOverName);
        if (gameOver == null)
        {
            Debug.LogError("[BossResultScreenBuilder] 場景中找不到 " + GameOverName + "，無法沿用面板外觀");
            return;
        }

        if (gameOver.scene.isDirty)
        {
            Debug.LogWarning("[BossResultScreenBuilder] 場景有未存的變更，請先存檔或還原後再執行");
            return;
        }

        Transform canvas = gameOver.transform.parent;
        Transform old = canvas.Find(ResultName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        GameObject result = Object.Instantiate(gameOver, canvas);
        result.name = ResultName;
        Object.DestroyImmediate(result.GetComponent<GameOverScreen>());
        Transform panel = result.transform.Find("Panel");
        Text title = panel.GetComponentInChildren<Text>(true);
        title.text = Title;
        title.color = new Color(1f, 0.85f, 0.35f);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 0.55f);
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;
        title.alignment = TextAnchor.LowerCenter;

        Text detail = Object.Instantiate(title, panel);
        detail.name = "Detail";
        detail.color = Color.white;
        detail.fontSize = 40;
        detail.alignment = TextAnchor.UpperCenter;
        RectTransform detailRect = detail.rectTransform;
        detailRect.anchorMin = Vector2.zero;
        detailRect.anchorMax = new Vector2(1f, 0.5f);
        detailRect.offsetMin = Vector2.zero;
        detailRect.offsetMax = Vector2.zero;
        detail.text = string.Empty;

        BossResultScreen screen = result.AddComponent<BossResultScreen>();
        SerializedObject serialized = new SerializedObject(screen);
        serialized.FindProperty("panel").objectReferenceValue = panel.gameObject;
        serialized.FindProperty("detailText").objectReferenceValue = detail;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        panel.gameObject.SetActive(false);

        BossEncounter encounter = Object.FindFirstObjectByType<BossEncounter>();
        if (encounter != null)
        {
            SerializedObject encounterSerialized = new SerializedObject(encounter);
            encounterSerialized.FindProperty("phaseController").objectReferenceValue = Object.FindFirstObjectByType<CombatPhaseController>();
            encounterSerialized.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning("[BossResultScreenBuilder] 場景中沒有 BossEncounter，Boss 被擊倒後不會回到一般戰鬥");
        }

        EditorSceneManager.MarkSceneDirty(result.scene);
        EditorSceneManager.SaveScene(result.scene);
        Debug.Log("[BossResultScreenBuilder] 已建立 Boss 結算面板");
    }
}
#endif
