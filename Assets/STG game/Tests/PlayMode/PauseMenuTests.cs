using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// 暫停選單：按暫停鍵顯示選單並停住時間；繼續 = 關掉選單恢復；重新開始 / 回大廳 = 恢復時間後換場景。
// 遊戲已經被別的東西停住 (開場 START、結算) 時不能暫停
public class PauseMenuTests
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

    private Button NewButton(string name, Transform parent)
    {
        Button button = new GameObject(name, typeof(RectTransform)).AddComponent<Image>().gameObject.AddComponent<Button>();
        button.transform.SetParent(parent, false);
        return button;
    }

    private PauseMenu CreateMenu(out PauseController controller, out GameObject panel, out Button continueButton, out Button restartButton, out Button lobbyButton)
    {
        GameObject root = new GameObject("Pause", typeof(RectTransform));
        spawned.Add(root);
        root.SetActive(false);
        panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        continueButton = NewButton("Continue", panel.transform);
        restartButton = NewButton("Restart", panel.transform);
        lobbyButton = NewButton("Lobby", panel.transform);

        controller = root.AddComponent<PauseController>();
        TestUtil.SetField(controller, "menu", panel);
        PauseMenu menu = root.AddComponent<PauseMenu>();
        TestUtil.SetField(menu, "pauseController", controller);
        TestUtil.SetField(menu, "continueButton", continueButton);
        TestUtil.SetField(menu, "restartButton", restartButton);
        TestUtil.SetField(menu, "lobbyButton", lobbyButton);
        TestUtil.SetField(menu, "restartSceneName", "");
        TestUtil.SetField(menu, "lobbySceneName", "");
        root.SetActive(true);
        return menu;
    }

    [UnityTest]
    public IEnumerator Pause_ShowsTheMenu_ContinueHidesIt()
    {
        CreateMenu(out PauseController controller, out GameObject panel, out Button continueButton, out _, out _);
        yield return null;
        Assert.IsFalse(panel.activeSelf, "hidden while playing");

        controller.TogglePause();
        Assert.IsTrue(panel.activeSelf);
        Assert.AreEqual(0f, Time.timeScale);

        continueButton.onClick.Invoke();
        Assert.IsFalse(panel.activeSelf);
        Assert.AreEqual(1f, Time.timeScale);
    }

    [UnityTest]
    public IEnumerator Restart_And_Lobby_UnpauseAndRequestTheirScene()
    {
        PauseMenu menu = CreateMenu(out PauseController controller, out _, out _, out Button restartButton, out Button lobbyButton);
        int restarts = 0;
        int lobbies = 0;
        menu.OnRestartRequested += () => restarts++;
        menu.OnLobbyRequested += () => lobbies++;
        yield return null;

        controller.TogglePause();
        restartButton.onClick.Invoke();
        Assert.AreEqual(1, restarts);
        Assert.AreEqual(1f, Time.timeScale, "time runs again for the next scene");

        controller.TogglePause();
        lobbyButton.onClick.Invoke();
        Assert.AreEqual(1, lobbies);
        Assert.IsFalse(controller.IsPaused);
    }

    // 開場 START 或結算面板已經把時間停住時，按暫停鍵不做事 (不然恢復時會跟它們搶時間)
    [UnityTest]
    public IEnumerator Pause_IsIgnored_WhenSomethingElseStoppedTime()
    {
        CreateMenu(out PauseController controller, out GameObject panel, out _, out _, out _);
        yield return null;
        Time.timeScale = 0f;

        controller.TogglePause();

        Assert.IsFalse(controller.IsPaused);
        Assert.IsFalse(panel.activeSelf);
    }
}
