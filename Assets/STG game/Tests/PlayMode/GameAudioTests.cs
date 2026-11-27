using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// 音樂與 UI 音效：
// 背景音樂跨場景不中斷 (同一首再要求一次不會重播)，換曲時淡出淡入 (遊戲暫停中也要能換)；
// 各場景開場播自己的 BGM；出擊時就換成選到的關卡 BGM (轉場一開始就聽到)；
// 戰鬥：START / WARNING 字卡伴隨音效、勝利換勝利 BGM；任何按鈕點擊有音效
public class GameAudioTests
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
        foreach (GameAudio audio in Object.FindObjectsByType<GameAudio>(FindObjectsSortMode.None)) Object.Destroy(audio.gameObject);
    }

    private T Track<T>(T o) where T : Object
    {
        spawned.Add(o);
        return o;
    }

    private AudioClip Clip(string name)
    {
        AudioClip clip = Track(AudioClip.Create(name, 4410, 1, 44100, false));
        return clip;
    }

    private GameObject NewInactive(string name)
    {
        GameObject go = Track(new GameObject(name, typeof(RectTransform)));
        go.SetActive(false);
        return go;
    }

    [Test]
    public void PlayMusic_SameClipTwice_KeepsPlaying()
    {
        AudioClip theme = Clip("Theme");
        GameAudio.PlayMusic(theme);
        GameAudio.PlayMusic(theme);

        Assert.AreSame(theme, GameAudio.CurrentMusic);
        Assert.AreEqual(1, Object.FindObjectsByType<GameAudio>(FindObjectsSortMode.None).Length, "one shared player");
    }

    // 勝利、開場時遊戲是暫停的 (timeScale = 0)，換曲還是要完成
    [UnityTest]
    public IEnumerator PlayMusic_SwitchesClip_EvenWhilePaused()
    {
        AudioClip first = Clip("First");
        AudioClip second = Clip("Second");
        GameAudio.PlayMusic(first);
        yield return null;
        Time.timeScale = 0f;

        GameAudio.PlayMusic(second);
        Assert.AreSame(second, GameAudio.CurrentMusic);
        float end = Time.realtimeSinceStartup + 3f;
        AudioSource source = Object.FindFirstObjectByType<GameAudio>().GetComponent<AudioSource>();
        while (source.clip != second && Time.realtimeSinceStartup < end) yield return null;

        Assert.AreSame(second, source.clip);
    }

    [Test]
    public void StopMusic_ClearsCurrentMusic()
    {
        GameAudio.PlayMusic(Clip("Theme"));
        GameAudio.StopMusic();

        Assert.IsNull(GameAudio.CurrentMusic);
    }

    [UnityTest]
    public IEnumerator SceneMusic_PlaysItsClipOnStart()
    {
        AudioClip theme = Clip("Title");
        GameObject go = NewInactive("Music");
        TestUtil.SetField(go.AddComponent<SceneMusic>(), "music", theme);
        go.SetActive(true);
        yield return null;

        Assert.AreSame(theme, GameAudio.CurrentMusic);
    }

    [UnityTest]
    public IEnumerator ClickSounds_AnyButtonInTheScene_PlaysTheClick()
    {
        AudioClip click = Clip("Click");
        Button button = Track(new GameObject("Button")).AddComponent<Button>();
        GameObject go = NewInactive("Clicks");
        TestUtil.SetField(go.AddComponent<UiClickSounds>(), "clickSound", click);
        go.SetActive(true);
        AudioClip played = null;
        GameAudio.OnSfxPlayed += Record;
        yield return null;

        button.onClick.Invoke();
        GameAudio.OnSfxPlayed -= Record;
        Assert.AreSame(click, played);

        void Record(AudioClip clip) => played = clip;
    }

    // 出擊時 (稜鏡轉場開始) 就換成選到的關卡 BGM
    [UnityTest]
    public IEnumerator Mobilize_PlaysTheChosenBattleMusic()
    {
        AudioClip binahTheme = Clip("Binah");
        GameSelection selection = Track(ScriptableObject.CreateInstance<GameSelection>());
        BossProfile binah = Track(ScriptableObject.CreateInstance<BossProfile>());
        SelectableCharacter rio = Track(new GameObject("Rio")).AddComponent<SelectableCharacter>();
        GameObject go = NewInactive("Screen");
        CharacterSelectScreen screen = go.AddComponent<CharacterSelectScreen>();
        TestUtil.SetField(screen, "characters", new[] { rio });
        TestUtil.SetField(screen, "bossOptions", new[]
        {
            new CharacterSelectScreen.BossOption { bosses = new[] { binah }, battleMusic = binahTheme },
        });
        TestUtil.SetField(screen, "gameSelection", selection);
        TestUtil.SetField(screen, "gameSceneName", "");
        go.SetActive(true);
        yield return null;

        screen.PickCharacter(rio);
        screen.Mobilize();

        Assert.AreSame(binahTheme, GameAudio.CurrentMusic);
        Assert.AreSame(binahTheme, selection.BattleMusic, "battle scene reads it back");
    }

    private BattleFlow CreateFlow(GameSelection selection, AudioClip fallback, AudioClip victory, AudioClip start, AudioClip warning)
    {
        GameObject go = NewInactive("Flow");
        BattleFlow flow = go.AddComponent<BattleFlow>();
        TestUtil.SetField(flow, "gameSelection", selection);
        TestUtil.SetField(flow, "defaultBattleMusic", fallback);
        TestUtil.SetField(flow, "victoryMusic", victory);
        TestUtil.SetField(flow, "startSound", start);
        TestUtil.SetField(flow, "warningSound", warning);
        go.SetActive(true);
        return flow;
    }

    [UnityTest]
    public IEnumerator Battle_PlaysTheSelectedMusic_AndStartSound()
    {
        AudioClip endless = Clip("Endless");
        AudioClip start = Clip("Start");
        GameSelection selection = Track(ScriptableObject.CreateInstance<GameSelection>());
        selection.Select("CH0331", new[] { Track(ScriptableObject.CreateInstance<BossProfile>()) }, true, endless);
        var played = new List<AudioClip>();
        GameAudio.OnSfxPlayed += played.Add;
        CreateFlow(selection, Clip("Fallback"), null, start, null);
        yield return null;
        yield return null;
        GameAudio.OnSfxPlayed -= played.Add;

        Assert.AreSame(endless, GameAudio.CurrentMusic);
        CollectionAssert.Contains(played, start);
    }

    // 直接開戰鬥場景 (沒有經過選角) 時播預設的戰鬥 BGM
    [UnityTest]
    public IEnumerator Battle_WithoutSelection_PlaysTheDefaultMusic()
    {
        AudioClip fallback = Clip("Fallback");
        CreateFlow(null, fallback, null, null, null);
        yield return null;

        Assert.AreSame(fallback, GameAudio.CurrentMusic);
    }

    [UnityTest]
    public IEnumerator Battle_Warning_PlaysAlarm_Victory_SwitchesMusic()
    {
        AudioClip warning = Clip("Alarm");
        AudioClip victory = Clip("Victory");
        BattleFlow flow = CreateFlow(null, Clip("Fallback"), victory, null, warning);
        var played = new List<AudioClip>();
        GameAudio.OnSfxPlayed += played.Add;
        yield return null;

        flow.OnCombatPhaseChanged(CombatPhase.Boss);
        CollectionAssert.Contains(played, warning);

        flow.ShowVictory();
        GameAudio.OnSfxPlayed -= played.Add;
        Assert.AreSame(victory, GameAudio.CurrentMusic);
    }
}
