using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// 開始畫面：教學還沒做所以按鈕不能按；按 start 要求進入下一個場景、按離開遊戲要求關閉 (測試時不真的換場景/關閉)
public class TitleScreenTests
{
    private GameObject root;
    private TitleScreen screen;
    private Button start;
    private Button tutorial;
    private Button quit;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Title");
        root.SetActive(false);
        start = new GameObject("Start").AddComponent<Button>();
        tutorial = new GameObject("Tutorial").AddComponent<Button>();
        quit = new GameObject("Quit").AddComponent<Button>();
        start.transform.SetParent(root.transform);
        tutorial.transform.SetParent(root.transform);
        quit.transform.SetParent(root.transform);

        screen = root.AddComponent<TitleScreen>();
        TestUtil.SetField(screen, "startButton", start);
        TestUtil.SetField(screen, "tutorialButton", tutorial);
        TestUtil.SetField(screen, "quitButton", quit);
        TestUtil.SetField(screen, "nextSceneName", "");
        TestUtil.SetField(screen, "quitApplication", false);
        root.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(root);
    }

    [UnityTest]
    public IEnumerator TutorialButton_IsNotInteractable()
    {
        yield return null;

        Assert.IsFalse(tutorial.interactable);
        Assert.IsTrue(start.interactable);
        Assert.IsTrue(quit.interactable);
    }

    [UnityTest]
    public IEnumerator ClickingStart_RequestsStart()
    {
        int requests = 0;
        screen.OnStartRequested += () => requests++;
        yield return null;

        start.onClick.Invoke();

        Assert.AreEqual(1, requests);
    }

    [UnityTest]
    public IEnumerator ClickingQuit_RequestsQuit()
    {
        int requests = 0;
        screen.OnQuitRequested += () => requests++;
        yield return null;

        quit.onClick.Invoke();

        Assert.AreEqual(1, requests);
    }

    // 停用後不再回應按鈕 (OnDisable 解除註冊)
    [UnityTest]
    public IEnumerator WhenDisabled_StartIsIgnored()
    {
        int requests = 0;
        screen.OnStartRequested += () => requests++;
        yield return null;

        screen.enabled = false;
        start.onClick.Invoke();

        Assert.AreEqual(0, requests);
    }
}
