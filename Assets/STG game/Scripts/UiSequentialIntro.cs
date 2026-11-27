using System.Collections;
using UnityEngine;

// 畫面進場：UI 元件依序從左邊滑入並淡入 (每個晚 delayBetween 秒開始)，不受暫停影響；
// 等場景轉場播完、再過 startDelay 秒才開始。
// 每個元件會自動加上 CanvasGroup 控制透明度；結束位置就是元件原本擺放的位置
public class UiSequentialIntro : MonoBehaviour
{
    [SerializeField, Tooltip("依序進場的元件")] private RectTransform[] items = System.Array.Empty<RectTransform>();
    [SerializeField, Tooltip("從左邊多遠滑進來 (UI 像素)")] private float slideDistance = 120f;
    [SerializeField, Min(0.01f), Tooltip("每個元件滑入的時間 (秒)")] private float duration = 0.35f;
    [SerializeField, Min(0f), Tooltip("下一個元件晚多久開始 (秒)")] private float delayBetween = 0.08f;
    [SerializeField, Min(0f), Tooltip("轉場結束後，再等多久才開始進場 (秒)")] private float startDelay;

    private Vector2[] targets;
    private CanvasGroup[] groups;

    private void Awake()
    {
        targets = new Vector2[items.Length];
        groups = new CanvasGroup[items.Length];
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null) continue;

            targets[i] = items[i].anchoredPosition;
            groups[i] = items[i].GetComponent<CanvasGroup>();
            if (groups[i] == null) groups[i] = items[i].gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void OnEnable()
    {
        Apply(0f);
        StartCoroutine(Play());
    }

    private IEnumerator Play()
    {
        // 從別的場景轉場進來時，等稜鏡散開後才開始 (不然進場被轉場蓋住看不到)
        while (SceneTransition.IsBusy) yield return null;
        if (startDelay > 0f) yield return new WaitForSecondsRealtime(startDelay);

        float total = duration + delayBetween * Mathf.Max(0, items.Length - 1);
        float elapsed = 0f;
        while (elapsed < total)
        {
            Apply(elapsed);
            yield return null;
            elapsed += AnimationClock.UnscaledDelta;
        }

        Apply(total);
    }

    private void Apply(float elapsed)
    {
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null) continue;

            float t = Mathf.Clamp01((elapsed - i * delayBetween) / duration);
            // 減速滑入
            float eased = 1f - (1f - t) * (1f - t);
            items[i].anchoredPosition = targets[i] + Vector2.left * (slideDistance * (1f - eased));
            groups[i].alpha = t;
        }
    }
}
