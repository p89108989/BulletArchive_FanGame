using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// 測試用工具：設定元件上的 private [SerializeField] 欄位 (Inspector 才會填的引用)
internal static class TestUtil
{
    public static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = null;
        for (System.Type type = target.GetType(); type != null && field == null; type = type.BaseType)
        {
            field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        }

        Assert.IsNotNull(field, target.GetType().Name + " has no field " + fieldName);
        field.SetValue(target, value);
    }

    // 戰鬥結束流程 (沒有字卡，直接顯示結算) + 結算面板，都放在 root 上；panel 是結算面板本體
    public static BattleFlow CreateBattleFlowWithResults(GameObject root, PlayerHealth player, out GameObject panel, out UnityEngine.UI.Text timeText)
    {
        root.SetActive(false);
        panel = new GameObject("Panel");
        panel.transform.SetParent(root.transform);
        timeText = new GameObject("Time").AddComponent<UnityEngine.UI.Text>();
        timeText.transform.SetParent(panel.transform);
        ResultPanel results = root.AddComponent<ResultPanel>();
        SetField(results, "panel", panel);
        SetField(results, "timeText", timeText);
        SetField(results, "titleSceneName", "");
        BattleFlow flow = root.AddComponent<BattleFlow>();
        SetField(flow, "resultPanel", results);
        SetField(flow, "playerHealth", player);
        root.SetActive(true);
        return flow;
    }
}
