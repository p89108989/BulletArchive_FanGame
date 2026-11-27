using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 稜鏡轉場 (參考蔚藍檔案的換場)，兩種：
// - fromBlack (進入戰鬥)：畫面壓黑 → 換場景 → 瞬間出現藍白頁面 → 頁面碎成半透明玻璃三角形往外飛散 (材質 _Mode 0)
// - 一般：三角形從左上往右下掃過、整面蓋滿 → 換場景 → 三角形變透明並依同方向掃開 (材質 _Mode 1，波浪)
// 進度 Progress：0 ~ 0.5 = 蓋上 (碎開模式這段是黑幕)、0.5 = 完全蓋住、0.5 ~ 1 = 打開 (材質的 _Progress)。
// 換場景時本物件跨場景保留，播完後自毀 (新場景裡有自己的 SceneTransition 供下一次使用)。
// 用不受暫停影響的時間，戰鬥暫停中也能播
public class SceneTransition : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField, Tooltip("全螢幕的稜鏡圖 (材質用 STG/PrismTransition)")] private Graphic prism;
    [SerializeField, Tooltip("全螢幕黑幕 (進入戰鬥時先壓黑)")] private Image blackout;

    [Header("Timing (秒)")]
    [SerializeField, Min(0f), Tooltip("壓黑的時間 (進入戰鬥)")] private float blackDuration = 0.35f;
    [SerializeField, Min(0.01f), Tooltip("藍白頁面碎開飛散的時間 (進入戰鬥)")] private float shatterDuration = 0.6f;
    [SerializeField, Min(0.01f), Tooltip("波浪：三角形掃過蓋滿畫面的時間")] private float coverDuration = 0.4f;
    [SerializeField, Min(0.01f), Tooltip("波浪：三角形掃開的時間")] private float revealDuration = 0.5f;

    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int ModeId = Shader.PropertyToID("_Mode");
    private static int busyCount;

    private Material material;
    // 本物件正在轉場 (算在 busyCount 裡)；播到一半被刪掉時由 OnDisable 還回去，不會一直卡在「轉場中」
    private bool isRunning;

    // 有任何轉場正在播放 (例如戰鬥開場要等轉場播完才放 START)
    public static bool IsBusy => busyCount > 0;
    public float Progress { get; private set; }

    // 關掉 Domain Reload 時靜態欄位不會重置，進 Play 時手動歸零
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        busyCount = 0;
    }

    private void Awake()
    {
        if (prism != null)
        {
            // 每個轉場用自己的材質複本，改進度不會動到資產
            material = new Material(prism.material);
            prism.material = material;
        }

        SetVisible(false);
    }

    // 停用或刪除時協程會直接中止 (不會跑 finally)，在這裡結束轉場狀態
    private void OnDisable()
    {
        SetRunning(false);
    }

    private void SetRunning(bool running)
    {
        if (running == isRunning) return;

        isRunning = running;
        busyCount = Mathf.Max(0, busyCount + (running ? 1 : -1));
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }

    // 轉場到指定場景 (要在 Build Settings 裡)
    public void LoadScene(string sceneName, bool fromBlack)
    {
        Play(() => SceneManager.LoadScene(sceneName), fromBlack, true);
    }

    // whileCovered 在畫面完全被蓋住時執行一次 (例如換場景)；persistAcrossScenes = 換場景後繼續播完
    public void Play(Action whileCovered, bool fromBlack, bool persistAcrossScenes)
    {
        if (persistAcrossScenes)
        {
            transform.SetParent(null, false);
            DontDestroyOnLoad(gameObject);
        }

        StopAllCoroutines();
        StartCoroutine(Run(whileCovered, fromBlack, persistAcrossScenes));
    }

    private IEnumerator Run(Action whileCovered, bool fromBlack, bool destroyAfter)
    {
        SetRunning(true);
        try
        {
            if (material != null) material.SetFloat(ModeId, fromBlack ? 0f : 1f);
            SetVisible(true);
            SetProgress(0f);
            SetBlack(0f);
            if (fromBlack)
            {
                // 壓黑 (稜鏡在 0.5 之前不畫)，全黑時藍白頁面瞬間蓋上
                yield return Animate(blackDuration, SetBlack);
                SetProgress(0.5f);
            }
            else
            {
                yield return Animate(coverDuration, t => SetProgress(t * 0.5f));
            }

            whileCovered?.Invoke();
            // 等新場景載入完成 (LoadScene 在下一幀生效)
            yield return null;
            SetBlack(0f);
            yield return Animate(fromBlack ? shatterDuration : revealDuration, t => SetProgress(0.5f + t * 0.5f));
            SetVisible(false);
        }
        finally
        {
            SetRunning(false);
        }

        if (destroyAfter) Destroy(gameObject);
    }

    private static IEnumerator Animate(float duration, Action<float> apply)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            apply(elapsed / duration);
            yield return null;
            elapsed += AnimationClock.UnscaledDelta;
        }

        apply(1f);
    }

    private void SetProgress(float progress)
    {
        Progress = progress;
        if (material != null) material.SetFloat(ProgressId, progress);
    }

    private void SetBlack(float alpha)
    {
        if (blackout == null) return;

        Color color = blackout.color;
        color.a = alpha;
        blackout.color = color;
    }

    private void SetVisible(bool visible)
    {
        if (prism != null) prism.enabled = visible;
        if (blackout != null) blackout.enabled = visible;
    }
}
