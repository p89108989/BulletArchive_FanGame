using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// 戰鬥 HUD：右上的計時器 (往上數、遊戲暫停時停住) 與暫停鍵
public class BattleHudTests
{
    private GameObject hud;

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        Object.Destroy(hud);
    }

    private BattleTimerUI CreateTimer(out Text label)
    {
        hud = new GameObject("Timer");
        hud.SetActive(false);
        label = new GameObject("Label").AddComponent<Text>();
        label.transform.SetParent(hud.transform);
        BattleTimerUI timer = hud.AddComponent<BattleTimerUI>();
        TestUtil.SetField(timer, "label", label);
        hud.SetActive(true);
        return timer;
    }

    [UnityTest]
    public IEnumerator Timer_StartsAtZeroAndCountsUp()
    {
        BattleTimerUI timer = CreateTimer(out Text label);
        yield return null;
        Assert.AreEqual("00:00", label.text);

        yield return new WaitForSeconds(1.2f);

        Assert.AreEqual("00:01", label.text);
        Assert.Greater(timer.Elapsed, 1f);
    }

    [UnityTest]
    public IEnumerator Timer_StopsWhileGameIsPaused()
    {
        BattleTimerUI timer = CreateTimer(out _);
        yield return new WaitForSeconds(0.2f);
        float before = timer.Elapsed;

        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(0.3f);

        Assert.AreEqual(before, timer.Elapsed, 0.0001f);
    }

    [UnityTest]
    public IEnumerator Pause_TogglesGameTime()
    {
        hud = new GameObject("Pause");
        PauseController pause = hud.AddComponent<PauseController>();
        yield return null;

        pause.TogglePause();
        Assert.IsTrue(pause.IsPaused);
        Assert.AreEqual(0f, Time.timeScale);

        pause.TogglePause();
        Assert.IsFalse(pause.IsPaused);
        Assert.AreEqual(1f, Time.timeScale);
    }

    // 暫停前若時間流速不是 1 (例如慢動作)，恢復時要回到原本的值
    [UnityTest]
    public IEnumerator Pause_RestoresPreviousTimeScale()
    {
        hud = new GameObject("Pause");
        PauseController pause = hud.AddComponent<PauseController>();
        yield return null;
        Time.timeScale = 0.5f;

        pause.TogglePause();
        pause.TogglePause();

        Assert.AreEqual(0.5f, Time.timeScale);
    }

    // 暫停中元件被停用 (例如切換場景) 時要把時間還原，否則下一個場景會卡在暫停
    [UnityTest]
    public IEnumerator Pause_DisabledWhilePaused_RestoresTime()
    {
        hud = new GameObject("Pause");
        PauseController pause = hud.AddComponent<PauseController>();
        yield return null;
        pause.TogglePause();

        pause.enabled = false;

        Assert.AreEqual(1f, Time.timeScale);
    }
}
