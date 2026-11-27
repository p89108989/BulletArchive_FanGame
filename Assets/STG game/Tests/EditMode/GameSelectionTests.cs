using NUnit.Framework;
using UnityEngine;

// 選角場景寫入、戰鬥場景讀取的選擇：Boss 清單略過沒有 Profile 的項目，無盡模式循環
public class GameSelectionTests
{
    private GameSelection selection;
    private BossProfile a;
    private BossProfile b;

    [SetUp]
    public void SetUp()
    {
        selection = ScriptableObject.CreateInstance<GameSelection>();
        a = ScriptableObject.CreateInstance<BossProfile>();
        b = ScriptableObject.CreateInstance<BossProfile>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(selection);
        Object.DestroyImmediate(a);
        Object.DestroyImmediate(b);
    }

    [Test]
    public void NewInstance_HasNoSelection()
    {
        Assert.IsFalse(selection.HasSelection);
        Assert.AreEqual(0, selection.BossCount);
        Assert.IsNull(selection.GetBoss(0));
    }

    [Test]
    public void Select_SkipsMissingProfiles()
    {
        selection.Select("CH0331", new[] { a, null, b }, false);

        Assert.IsTrue(selection.HasSelection);
        Assert.AreEqual("CH0331", selection.CharacterId);
        Assert.AreEqual(2, selection.BossCount);
        Assert.AreSame(a, selection.GetBoss(0));
        Assert.AreSame(b, selection.GetBoss(1));
    }

    [Test]
    public void Loop_WrapsAround()
    {
        selection.Select("CH0331", new[] { a, b }, true);

        Assert.IsTrue(selection.Loop);
        Assert.AreSame(a, selection.GetBoss(2));
        Assert.AreSame(b, selection.GetBoss(3));
    }

    [Test]
    public void NoLoop_PastTheEndIsNull()
    {
        selection.Select("CH0331", new[] { a }, false);

        Assert.IsNull(selection.GetBoss(1));
    }

    [Test]
    public void Clear_RemovesSelection()
    {
        selection.Select("CH0331", new[] { a }, true);
        selection.Clear();

        Assert.IsFalse(selection.HasSelection);
        Assert.AreEqual(0, selection.BossCount);
        Assert.IsNull(selection.CharacterId);
    }
}
