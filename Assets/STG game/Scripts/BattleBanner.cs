using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 戰鬥字卡 (畫面壓暗 + 大字)：START 從左飛入、減速、從右飛出 (BannerTimeline)；
// WARNING / VICTORY / DEFEAT 是「蓋章」：放大縮回、停一下、淡出 (WARNING 另外顯示上下兩條橫向捲動的警示條紋)。
// 用不受暫停影響的時間，戰鬥暫停中也能播
public class BattleBanner : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField, Tooltip("全螢幕壓暗")] private Image darkOverlay;
    [SerializeField, Tooltip("字卡的字 (START / WARNING / VICTORY / DEFEAT)")] private Image word;
    [SerializeField, Tooltip("START 的字圖")] private Sprite startWord;
    [SerializeField, Tooltip("START 旁邊的閃光，可留空")] private Image startLight;
    [SerializeField, Tooltip("WARNING 上下的警示條紋 (RawImage，貼圖橫向重複捲動)，可留空")] private RawImage[] warningStripes = Array.Empty<RawImage>();
    [SerializeField, Tooltip("WARNING 字的顏色 (字圖是白色遮罩，靠這個上色)")] private Color warningTint = new Color(1f, 0.84f, 0.3f, 1f);

    [Header("Stamp")]
    [SerializeField, Min(0f), Tooltip("蓋章字卡停留的秒數")] private float stampHold = 1.2f;
    [SerializeField, Min(0.01f), Tooltip("蓋章字卡淡出的秒數")] private float stampFadeOut = 0.15f;
    [SerializeField, Range(0f, 1f), Tooltip("蓋章字卡的壓暗程度")] private float stampDarkness = 0.6f;
    [SerializeField, Tooltip("警示條紋每秒捲動的量 (貼圖寬度的倍數)")] private float stripeScrollSpeed = 0.4f;

    public bool IsPlaying { get; private set; }

    private void Awake()
    {
        Hide();
    }

    // START：播完呼叫 onFinished
    public void PlayStart(Action onFinished)
    {
        Begin(PlayStartRoutine(onFinished));
    }

    // 蓋章字卡；showStripes = WARNING 的警示條紋
    public void PlayStamp(Sprite stampWord, bool showStripes, Action onFinished)
    {
        Begin(PlayStampRoutine(stampWord, showStripes, onFinished));
    }

    private void Begin(IEnumerator routine)
    {
        StopAllCoroutines();
        IsPlaying = true;
        StartCoroutine(routine);
    }

    private IEnumerator PlayStartRoutine(Action onFinished)
    {
        Show(startWord, false);
        RectTransform rect = word.rectTransform;
        float t = 0f;
        while (!BannerTimeline.StartFinished(t))
        {
            rect.anchoredPosition = new Vector2(BannerTimeline.StartX(t), 0f);
            SetAlpha(darkOverlay, BannerTimeline.StartDarkness(t));
            SetAlpha(startLight, BannerTimeline.StartLight(t));
            yield return null;
            t += AnimationClock.UnscaledDelta;
        }

        Finish(onFinished);
    }

    private IEnumerator PlayStampRoutine(Sprite stampWord, bool showStripes, Action onFinished)
    {
        Show(stampWord, showStripes);
        RectTransform rect = word.rectTransform;
        rect.anchoredPosition = Vector2.zero;
        float t = 0f;
        float fadeStart = stampHold + 0.15f;
        float end = fadeStart + stampFadeOut;
        while (t < end)
        {
            float fade = t < fadeStart ? 1f : 1f - (t - fadeStart) / stampFadeOut;
            rect.localScale = Vector3.one * BannerTimeline.StampScale(t);
            SetAlpha(word, BannerTimeline.StampAlpha(t) * fade);
            SetAlpha(darkOverlay, stampDarkness * Mathf.Clamp01(t / 0.15f) * fade);
            ScrollStripes(t, fade);
            yield return null;
            t += AnimationClock.UnscaledDelta;
        }

        Finish(onFinished);
    }

    private void Finish(Action onFinished)
    {
        Hide();
        IsPlaying = false;
        onFinished?.Invoke();
    }

    private void Show(Sprite sprite, bool showStripes)
    {
        gameObject.SetActive(true);
        if (sprite != null) word.sprite = sprite;
        word.enabled = true;
        word.preserveAspect = true;
        word.rectTransform.localScale = Vector3.one;
        word.color = showStripes ? warningTint : Color.white;
        if (darkOverlay != null) darkOverlay.enabled = true;
        if (startLight != null) startLight.enabled = !showStripes && sprite == startWord;
        foreach (RawImage stripe in warningStripes)
        {
            if (stripe != null) stripe.enabled = showStripes;
        }
    }

    private void Hide()
    {
        if (word != null) word.enabled = false;
        if (darkOverlay != null) darkOverlay.enabled = false;
        if (startLight != null) startLight.enabled = false;
        foreach (RawImage stripe in warningStripes)
        {
            if (stripe != null) stripe.enabled = false;
        }
    }

    // 上下兩條往相反方向捲動
    private void ScrollStripes(float t, float alpha)
    {
        for (int i = 0; i < warningStripes.Length; i++)
        {
            RawImage stripe = warningStripes[i];
            if (stripe == null || !stripe.enabled) continue;

            Rect uv = stripe.uvRect;
            uv.x = (i % 2 == 0 ? 1f : -1f) * t * stripeScrollSpeed;
            stripe.uvRect = uv;
            SetAlpha(stripe, alpha);
        }
    }

    private static void SetAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null) return;

        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }
}
