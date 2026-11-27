using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// 戰鬥演出：場景轉場 (稜鏡蓋滿才換場景、結束後不再忙碌)、字卡 (遊戲暫停中也會播完)、
// 結算面板 (關卡、擊殺數、時間、依選到的角色換立繪、確認回開始畫面)、
// 戰鬥流程 (開場暫停到 START 結束、Boss 戰放 WARNING、玩家倒下 → DEFEAT → 結算)
public class BattlePresentationTests
{
    private readonly List<Object> spawned = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        foreach (Object o in spawned)
        {
            if (o != null) Object.Destroy(o);
        }

        spawned.Clear();
    }

    private T Track<T>(T o) where T : Object
    {
        spawned.Add(o);
        return o;
    }

    private GameObject NewInactive(string name)
    {
        GameObject go = Track(new GameObject(name, typeof(RectTransform)));
        go.SetActive(false);
        return go;
    }

    private Image NewImage(string name, Transform parent)
    {
        Image image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        image.transform.SetParent(parent, false);
        return image;
    }

    private Text NewText(string name, Transform parent)
    {
        Text text = new GameObject(name, typeof(RectTransform)).AddComponent<Text>();
        text.transform.SetParent(parent, false);
        return text;
    }

    private SceneTransition CreateTransition()
    {
        GameObject go = NewInactive("Transition");
        SceneTransition transition = go.AddComponent<SceneTransition>();
        TestUtil.SetField(transition, "prism", NewImage("Prism", go.transform));
        TestUtil.SetField(transition, "blackout", NewImage("Black", go.transform));
        TestUtil.SetField(transition, "blackDuration", 0.05f);
        TestUtil.SetField(transition, "coverDuration", 0.1f);
        TestUtil.SetField(transition, "revealDuration", 0.1f);
        TestUtil.SetField(transition, "shatterDuration", 0.1f);
        go.SetActive(true);
        return transition;
    }

    // 轉場播到一半物件被刪掉 (例如換場景)，不能讓「轉場中」一直卡著 (開場 START、主畫面進場都在等它)
    [UnityTest]
    public IEnumerator Transition_DestroyedMidway_IsNoLongerBusy()
    {
        SceneTransition transition = CreateTransition();
        transition.Play(null, true, false);
        yield return null;
        Assert.IsTrue(SceneTransition.IsBusy);

        Object.Destroy(transition.gameObject);
        yield return null;

        Assert.IsFalse(SceneTransition.IsBusy);
    }

    private BattleBanner CreateBanner()
    {
        GameObject go = NewInactive("Banner");
        BattleBanner banner = go.AddComponent<BattleBanner>();
        TestUtil.SetField(banner, "darkOverlay", NewImage("Dark", go.transform));
        TestUtil.SetField(banner, "word", NewImage("Word", go.transform));
        TestUtil.SetField(banner, "stampHold", 0.2f);
        go.SetActive(true);
        return banner;
    }

    private ResultPanel CreateResultPanel(out Text stage, out Text kills, out Text time, out Image art, out Button confirm)
    {
        GameObject go = NewInactive("Result");
        GameObject panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(go.transform, false);
        stage = NewText("Stage", panel.transform);
        kills = NewText("Kills", panel.transform);
        time = NewText("Time", panel.transform);
        art = NewImage("Art", panel.transform);
        confirm = NewImage("Confirm", panel.transform).gameObject.AddComponent<Button>();
        ResultPanel result = go.AddComponent<ResultPanel>();
        TestUtil.SetField(result, "panel", panel);
        TestUtil.SetField(result, "stageText", stage);
        TestUtil.SetField(result, "killsText", kills);
        TestUtil.SetField(result, "timeText", time);
        TestUtil.SetField(result, "characterImage", art);
        TestUtil.SetField(result, "confirmButton", confirm);
        TestUtil.SetField(result, "titleSceneName", "");
        go.SetActive(true);
        return result;
    }

    private static Sprite NewSprite()
    {
        return Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), Vector2.one * 0.5f);
    }

    [UnityTest]
    public IEnumerator Transition_RunsTheMiddleStepOnlyWhenFullyCovered()
    {
        SceneTransition transition = CreateTransition();
        float progressWhenCovered = -1f;

        transition.Play(() => progressWhenCovered = transition.Progress, true, false);
        Assert.IsTrue(SceneTransition.IsBusy);

        yield return WaitUntil(() => !SceneTransition.IsBusy);

        Assert.AreEqual(0.5f, progressWhenCovered, 0.001f, "screen fully covered when the scene switches");
        Assert.IsFalse(SceneTransition.IsBusy);
        Assert.AreEqual(1f, transition.Progress, 0.001f);
    }

    [UnityTest]
    public IEnumerator Banner_PlaysToTheEnd_EvenWhilePaused()
    {
        BattleBanner banner = CreateBanner();
        int finished = 0;
        Time.timeScale = 0f;

        banner.PlayStart(() => finished++);
        Assert.IsTrue(banner.IsPlaying);
        yield return WaitUntil(() => finished > 0);

        Assert.AreEqual(1, finished);
        Assert.IsFalse(banner.IsPlaying);
    }

    [UnityTest]
    public IEnumerator Result_ShowsStatsAndTheSelectedCharacter()
    {
        ResultPanel result = CreateResultPanel(out Text stage, out Text kills, out Text time, out Image art, out _);
        Sprite toki = NewSprite();
        GameSelection selection = Track(ScriptableObject.CreateInstance<GameSelection>());
        selection.Select("CH0187", new[] { Track(ScriptableObject.CreateInstance<BossProfile>()) }, false);
        TestUtil.SetField(result, "gameSelection", selection);
        TestUtil.SetField(result, "characterArts", new[]
        {
            new ResultPanel.CharacterArt { characterId = "CH0331", art = NewSprite() },
            new ResultPanel.CharacterArt { characterId = "CH0187", art = toki },
        });
        yield return null;

        result.Show("Perorozilla", 12, 65.4f);

        Assert.IsTrue(result.IsShowing);
        Assert.AreEqual("Perorozilla", stage.text);
        Assert.AreEqual("12", kills.text);
        Assert.AreEqual("01:05", time.text);
        Assert.AreEqual(toki, art.sprite);
    }

    [UnityTest]
    public IEnumerator Result_Confirm_LeavesTheResultsAndUnpauses()
    {
        ResultPanel result = CreateResultPanel(out _, out _, out _, out _, out Button confirm);
        int confirmed = 0;
        result.OnConfirmed += () => confirmed++;
        yield return null;
        result.Show("Binah", 0, 0f);
        Assert.AreEqual(0f, Time.timeScale, "results pause the game");

        confirm.onClick.Invoke();

        Assert.AreEqual(1, confirmed);
        Assert.AreEqual(1f, Time.timeScale);
    }

    private BattleFlow CreateFlow(out BattleBanner banner, out ResultPanel result, out PlayerHealth player, out BattleStats stats)
    {
        banner = CreateBanner();
        result = CreateResultPanel(out _, out _, out _, out _, out _);
        GameObject playerObject = Track(new GameObject("Player"));
        player = playerObject.AddComponent<PlayerHealth>();
        stats = Track(new GameObject("Stats")).AddComponent<BattleStats>();

        GameObject go = NewInactive("Flow");
        BattleFlow flow = go.AddComponent<BattleFlow>();
        TestUtil.SetField(flow, "banner", banner);
        TestUtil.SetField(flow, "stats", stats);
        TestUtil.SetField(flow, "resultPanel", result);
        TestUtil.SetField(flow, "playerHealth", player);
        TestUtil.SetField(flow, "warningWord", NewSprite());
        TestUtil.SetField(flow, "victoryWord", NewSprite());
        TestUtil.SetField(flow, "defeatWord", NewSprite());
        go.SetActive(true);
        return flow;
    }

    // 戰鬥 HUD 左上角的關卡名稱 = 選到的 Boss 名稱 (無盡模式顯示「無盡模式」)
    [UnityTest]
    public IEnumerator Flow_ShowsTheBossNameOnTheHud()
    {
        Text label = NewText("Stage", Track(new GameObject("Hud", typeof(RectTransform))).transform);
        label.text = "Stage 1";
        BossProfile profile = Track(ScriptableObject.CreateInstance<BossProfile>());
        TestUtil.SetField(profile, "displayName", "Binah");
        TestUtil.SetField(profile, "bossPrefab", Track(new GameObject("BossTemplate")));
        GameObject encounterObject = NewInactive("Encounter");
        BossEncounter encounter = encounterObject.AddComponent<BossEncounter>();
        TestUtil.SetField(encounter, "bossProfile", profile);
        encounterObject.SetActive(true);

        GameObject go = NewInactive("Flow");
        BattleFlow flow = go.AddComponent<BattleFlow>();
        TestUtil.SetField(flow, "bossEncounter", encounter);
        TestUtil.SetField(flow, "stageLabel", label);
        go.SetActive(true);
        yield return null;

        Assert.AreEqual("Binah", label.text);
    }

    [UnityTest]
    public IEnumerator Flow_Endless_ShowsEndlessOnTheHud()
    {
        Text label = NewText("Stage", Track(new GameObject("Hud", typeof(RectTransform))).transform);
        GameSelection selection = Track(ScriptableObject.CreateInstance<GameSelection>());
        selection.Select("CH0331", new[] { Track(ScriptableObject.CreateInstance<BossProfile>()) }, true);
        GameObject go = NewInactive("Flow");
        BattleFlow flow = go.AddComponent<BattleFlow>();
        TestUtil.SetField(flow, "gameSelection", selection);
        TestUtil.SetField(flow, "stageLabel", label);
        go.SetActive(true);
        yield return null;

        Assert.AreEqual("無盡模式", label.text);
    }

    [UnityTest]
    public IEnumerator Flow_PausesUntilStartFinishes()
    {
        CreateFlow(out _, out _, out _, out _);
        yield return null;
        Assert.AreEqual(0f, Time.timeScale, "paused during START");

        yield return WaitUntil(() => Time.timeScale > 0f);

        Assert.AreEqual(1f, Time.timeScale, "battle starts after START");
    }

    [UnityTest]
    public IEnumerator Flow_BossPhase_PlaysWarning()
    {
        BattleFlow flow = CreateFlow(out BattleBanner banner, out _, out _, out _);
        yield return WaitUntil(() => flow.IsBattleRunning);

        flow.OnCombatPhaseChanged(CombatPhase.Boss);

        Assert.IsTrue(banner.IsPlaying);
        Assert.AreEqual(1f, Time.timeScale, "warning does not pause");
    }

    [UnityTest]
    public IEnumerator Flow_PlayerDown_ShowsDefeatThenResults()
    {
        BattleFlow flow = CreateFlow(out BattleBanner banner, out ResultPanel result, out PlayerHealth player, out _);
        yield return WaitUntil(() => flow.IsBattleRunning);

        player.TakeDamage(player.MaxHealth);
        Assert.IsTrue(banner.IsPlaying, "DEFEAT banner");
        Assert.IsFalse(result.IsShowing);

        yield return WaitUntil(() => result.IsShowing);

        Assert.IsTrue(result.IsShowing);
        Assert.AreEqual(0f, Time.timeScale);
    }

    [UnityTest]
    public IEnumerator Flow_Victory_ShowsResults()
    {
        BattleFlow flow = CreateFlow(out _, out ResultPanel result, out _, out _);
        yield return WaitUntil(() => flow.IsBattleRunning);

        flow.ShowVictory();
        yield return WaitUntil(() => result.IsShowing);

        Assert.IsTrue(result.IsShowing);
    }

    // 演出動畫每幀最多前進 1/30 秒 (AnimationClock)，畫面更新慢時固定秒數不一定夠，改成等到條件成立 (最多 6 秒)
    private static IEnumerator WaitUntil(System.Func<bool> condition)
    {
        float timeout = Time.realtimeSinceStartup + 6f;
        while (!condition() && Time.realtimeSinceStartup < timeout) yield return null;
    }
}
