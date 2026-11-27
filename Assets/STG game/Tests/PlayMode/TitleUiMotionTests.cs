using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

// 開始畫面 UI 的動態：按鈕滑過放大、按下縮小 (不能按的按鈕不動)；進場時各元件依序從左滑入並淡入
public class TitleUiMotionTests
{
    private readonly List<Object> spawned = new List<Object>();

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

    private UiButtonFeedback CreateButton(out Button button)
    {
        GameObject go = Track(new GameObject("Button", typeof(RectTransform)));
        go.AddComponent<Image>();
        button = go.AddComponent<Button>();
        return go.AddComponent<UiButtonFeedback>();
    }

    private static PointerEventData Pointer()
    {
        return new PointerEventData(EventSystem.current);
    }

    [UnityTest]
    public IEnumerator Hover_GrowsSlightly_Press_Shrinks()
    {
        UiButtonFeedback feedback = CreateButton(out _);
        yield return null;

        feedback.OnPointerEnter(Pointer());
        yield return new WaitForSecondsRealtime(0.3f);
        Assert.AreEqual(1.04f, feedback.transform.localScale.x, 0.005f, "hover");

        feedback.OnPointerDown(Pointer());
        yield return new WaitForSecondsRealtime(0.3f);
        Assert.AreEqual(0.96f, feedback.transform.localScale.x, 0.005f, "pressed");

        feedback.OnPointerUp(Pointer());
        feedback.OnPointerExit(Pointer());
        yield return new WaitForSecondsRealtime(0.3f);
        Assert.AreEqual(1f, feedback.transform.localScale.x, 0.005f, "back to normal");
    }

    [UnityTest]
    public IEnumerator DisabledButton_DoesNotReact()
    {
        UiButtonFeedback feedback = CreateButton(out Button button);
        button.interactable = false;
        yield return null;

        feedback.OnPointerEnter(Pointer());
        yield return new WaitForSecondsRealtime(0.3f);

        Assert.AreEqual(1f, feedback.transform.localScale.x, 0.001f);
    }

    // 主畫面右側慢動作播放時，左邊 UI 晚一點才開始淡入
    [UnityTest]
    public IEnumerator Intro_WaitsForTheStartDelay()
    {
        GameObject root = Track(new GameObject("Root", typeof(RectTransform)));
        root.SetActive(false);
        RectTransform item = new GameObject("Item", typeof(RectTransform)).GetComponent<RectTransform>();
        item.SetParent(root.transform, false);
        UiSequentialIntro intro = root.AddComponent<UiSequentialIntro>();
        TestUtil.SetField(intro, "items", new[] { item });
        TestUtil.SetField(intro, "startDelay", 0.4f);
        TestUtil.SetField(intro, "duration", 0.2f);
        root.SetActive(true);

        yield return new WaitForSecondsRealtime(0.25f);
        Assert.AreEqual(0f, item.GetComponent<CanvasGroup>().alpha, 0.001f, "still hidden during the delay");

        CanvasGroup group = item.GetComponent<CanvasGroup>();
        yield return WaitUntil(() => group.alpha >= 1f);
        Assert.AreEqual(1f, group.alpha, 0.001f);
    }

    [UnityTest]
    public IEnumerator Intro_SlidesItemsInOneAfterAnother()
    {
        GameObject root = Track(new GameObject("Root", typeof(RectTransform)));
        root.SetActive(false);
        RectTransform first = new GameObject("First", typeof(RectTransform)).GetComponent<RectTransform>();
        RectTransform second = new GameObject("Second", typeof(RectTransform)).GetComponent<RectTransform>();
        first.SetParent(root.transform, false);
        second.SetParent(root.transform, false);
        first.anchoredPosition = new Vector2(100f, 0f);
        second.anchoredPosition = new Vector2(200f, -50f);
        UiSequentialIntro intro = root.AddComponent<UiSequentialIntro>();
        TestUtil.SetField(intro, "items", new[] { first, second });
        TestUtil.SetField(intro, "delayBetween", 0.2f);
        TestUtil.SetField(intro, "duration", 0.2f);
        root.SetActive(true);

        yield return null;
        Assert.Less(first.anchoredPosition.x, 100f, "starts further left");
        Assert.Less(second.GetComponent<CanvasGroup>().alpha, 0.5f, "starts transparent");

        // 動畫每幀最多前進 1/30 秒 (AnimationClock)，畫面更新慢時用等到為止的方式，不用固定秒數
        CanvasGroup firstGroup = first.GetComponent<CanvasGroup>();
        CanvasGroup secondGroup = second.GetComponent<CanvasGroup>();
        yield return WaitUntil(() => firstGroup.alpha >= 1f);
        Assert.AreEqual(100f, first.anchoredPosition.x, 0.01f, "first item has arrived");
        Assert.Less(secondGroup.alpha, 1f, "second still on its way");

        yield return WaitUntil(() => secondGroup.alpha >= 1f);
        Assert.AreEqual(200f, second.anchoredPosition.x, 0.01f);
        Assert.AreEqual(-50f, second.anchoredPosition.y, 0.01f);
    }

    private static IEnumerator WaitUntil(System.Func<bool> condition)
    {
        float timeout = Time.realtimeSinceStartup + 3f;
        while (!condition() && Time.realtimeSinceStartup < timeout) yield return null;
    }
}
