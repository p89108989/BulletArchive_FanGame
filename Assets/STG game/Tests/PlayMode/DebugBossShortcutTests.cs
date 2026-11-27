using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// Debug 用快捷鍵：按 B 直接進入 Boss 戰 (不用等 bossStartDelay)；已經在 Boss 戰時再按不會重新觸發
public class DebugBossShortcutTests : InputTestFixture
{
    private GameObject root;
    private CombatPhaseController controller;
    private Keyboard keyboard;
    private int bossEntries;

    public override void Setup()
    {
        base.Setup();
        keyboard = InputSystem.AddDevice<Keyboard>();
        root = new GameObject("Phase");
        root.SetActive(false);
        controller = root.AddComponent<CombatPhaseController>();
        TestUtil.SetField(controller, "bossStartDelay", 999f);
        DebugBossShortcut shortcut = root.AddComponent<DebugBossShortcut>();
        TestUtil.SetField(shortcut, "phaseController", controller);
        root.SetActive(true);
        bossEntries = 0;
        controller.OnPhaseChanged += phase => { if (phase == CombatPhase.Boss) bossEntries++; };
    }

    public override void TearDown()
    {
        Object.Destroy(root);
        base.TearDown();
    }

    [UnityTest]
    public IEnumerator PressingB_EntersBossPhase()
    {
        yield return null;
        Assert.AreEqual(CombatPhase.Normal, controller.CurrentPhase);

        Press(keyboard.bKey);
        yield return null;

        Assert.AreEqual(CombatPhase.Boss, controller.CurrentPhase);
    }

    [UnityTest]
    public IEnumerator PressingBAgainInBossPhase_DoesNotReenter()
    {
        yield return null;
        Press(keyboard.bKey);
        yield return null;
        Release(keyboard.bKey);
        yield return null;

        Press(keyboard.bKey);
        yield return null;

        Assert.AreEqual(1, bossEntries);
    }
}
