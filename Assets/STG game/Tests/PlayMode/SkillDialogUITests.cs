using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// 放技能時跳出的對話框 (蔚藍檔案左上的頭像 + 台詞)：平常隱藏，Show 後顯示台詞，幾秒後自動收起
public class SkillDialogUITests
{
    private GameObject dialog;

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(dialog);
    }

    private SkillDialogUI CreateDialog(float duration, out GameObject panel, out Text line)
    {
        dialog = new GameObject("Dialog");
        dialog.SetActive(false);
        panel = new GameObject("Panel");
        panel.transform.SetParent(dialog.transform);
        line = new GameObject("Line").AddComponent<Text>();
        line.transform.SetParent(panel.transform);
        SkillDialogUI ui = dialog.AddComponent<SkillDialogUI>();
        TestUtil.SetField(ui, "panel", panel);
        TestUtil.SetField(ui, "lineLabel", line);
        TestUtil.SetField(ui, "showDuration", duration);
        dialog.SetActive(true);
        return ui;
    }

    [UnityTest]
    public IEnumerator HiddenUntilASkillIsUsed()
    {
        CreateDialog(1f, out GameObject panel, out _);
        yield return null;

        Assert.IsFalse(panel.activeSelf);
    }

    [UnityTest]
    public IEnumerator Show_DisplaysLineThenHides()
    {
        SkillDialogUI ui = CreateDialog(0.3f, out GameObject panel, out Text line);
        yield return null;

        ui.Show("Test line");
        Assert.IsTrue(panel.activeSelf);
        Assert.AreEqual("Test line", line.text);

        yield return new WaitForSeconds(0.5f);
        Assert.IsFalse(panel.activeSelf);
    }

    // 連續放技能時以最後一次為準重新計時，不會被前一次的計時提早收掉
    [UnityTest]
    public IEnumerator Show_AgainRestartsTimer()
    {
        SkillDialogUI ui = CreateDialog(0.4f, out GameObject panel, out _);
        yield return null;
        ui.Show("First");
        yield return new WaitForSeconds(0.3f);

        ui.Show("Second");
        yield return new WaitForSeconds(0.25f);

        Assert.IsTrue(panel.activeSelf);
    }
}
