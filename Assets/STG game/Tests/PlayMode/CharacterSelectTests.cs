using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// 選角畫面：一開始沒有角色被選 (全部 Idle)、不能出擊；選一個角色只有它播 Pickup；
// 目前 Boss 選項沒有任何 Profile 時不能出擊；出擊把選擇寫進 GameSelection
public class CharacterSelectTests
{
    private readonly List<Object> spawned = new List<Object>();
    private CharacterSelectScreen screen;
    private SelectableCharacter rio;
    private SelectableCharacter toki;
    private GameSelection selection;
    private BossProfile perorozilla;
    private BossProfile binah;
    private Button mobilize;

    [SetUp]
    public void SetUp()
    {
        selection = Track(ScriptableObject.CreateInstance<GameSelection>());
        perorozilla = Track(ScriptableObject.CreateInstance<BossProfile>());
        binah = Track(ScriptableObject.CreateInstance<BossProfile>());
        rio = CreateCharacter("CH0331");
        toki = CreateCharacter("CH0187");
        mobilize = Track(new GameObject("Mobilize")).AddComponent<Button>();

        GameObject screenObject = Track(new GameObject("Screen"));
        screenObject.SetActive(false);
        screen = screenObject.AddComponent<CharacterSelectScreen>();
        TestUtil.SetField(screen, "characters", new[] { rio, toki });
        TestUtil.SetField(screen, "bossOptions", new[]
        {
            Option(perorozilla),
            Option(),
            new CharacterSelectScreen.BossOption { bosses = new[] { perorozilla, null, binah }, loop = true },
        });
        TestUtil.SetField(screen, "mobilizeButton", mobilize);
        TestUtil.SetField(screen, "gameSelection", selection);
        TestUtil.SetField(screen, "gameSceneName", "");
        screenObject.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (Object o in spawned) Object.Destroy(o);
        spawned.Clear();
    }

    private T Track<T>(T o) where T : Object
    {
        spawned.Add(o);
        return o;
    }

    private SelectableCharacter CreateCharacter(string id)
    {
        GameObject go = Track(new GameObject(id));
        go.SetActive(false);
        SelectableCharacter character = go.AddComponent<SelectableCharacter>();
        TestUtil.SetField(character, "characterId", id);
        go.SetActive(true);
        return character;
    }

    private static CharacterSelectScreen.BossOption Option(params BossProfile[] bosses)
    {
        return new CharacterSelectScreen.BossOption { bosses = bosses, loop = false };
    }

    [UnityTest]
    public IEnumerator AtStart_NothingPicked_CannotMobilize()
    {
        yield return null;

        Assert.IsNull(screen.SelectedCharacter);
        Assert.IsFalse(rio.IsPicked);
        Assert.IsFalse(toki.IsPicked);
        Assert.AreEqual(0, screen.SelectedBossOption, "the first boss tab is selected by default");
        Assert.IsFalse(screen.CanMobilize);
        Assert.IsFalse(mobilize.interactable);
    }

    [UnityTest]
    public IEnumerator PickingACharacter_PicksOnlyIt()
    {
        yield return null;
        screen.PickCharacter(rio);

        Assert.AreSame(rio, screen.SelectedCharacter);
        Assert.IsTrue(rio.IsPicked);
        Assert.IsFalse(toki.IsPicked);
        Assert.IsTrue(screen.CanMobilize);
        Assert.IsTrue(mobilize.interactable);
    }

    [UnityTest]
    public IEnumerator ClickingACharacter_PicksIt()
    {
        yield return null;
        toki.Click();

        Assert.AreSame(toki, screen.SelectedCharacter);
    }

    [UnityTest]
    public IEnumerator PickingAnother_UnpicksThePrevious()
    {
        yield return null;
        screen.PickCharacter(rio);
        screen.PickCharacter(toki);

        Assert.IsFalse(rio.IsPicked, "back to idle");
        Assert.IsTrue(toki.IsPicked);
    }

    [UnityTest]
    public IEnumerator BossOptionWithoutProfiles_CannotMobilize()
    {
        yield return null;
        screen.PickCharacter(rio);
        screen.PickBossOption(1);

        Assert.AreEqual(1, screen.SelectedBossOption);
        Assert.IsFalse(screen.CanMobilize);
        Assert.IsFalse(mobilize.interactable);
    }

    [UnityTest]
    public IEnumerator Mobilize_WritesTheSelection()
    {
        yield return null;
        screen.PickCharacter(toki);
        screen.PickBossOption(2);
        screen.Mobilize();

        Assert.IsTrue(selection.HasSelection);
        Assert.AreEqual("CH0187", selection.CharacterId);
        Assert.AreEqual(2, selection.BossCount, "missing profiles are skipped");
        Assert.IsTrue(selection.Loop);
        Assert.AreSame(binah, selection.GetBoss(1));
    }

    [UnityTest]
    public IEnumerator Mobilize_WhenNotAllowed_DoesNothing()
    {
        yield return null;
        screen.Mobilize();

        Assert.IsFalse(selection.HasSelection);
    }
}
