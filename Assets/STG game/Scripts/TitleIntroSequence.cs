using UnityEngine;

// 開始畫面的開場慢動作總控：所有演出元件共用同一條「越來越慢」的進度 (IntroTimeline)，
// 一起從第一格動到最後一格後定格，之後不再更新 (關掉自己，沒有每幀開銷)
public class TitleIntroSequence : MonoBehaviour
{
    [SerializeField, Min(0f), Tooltip("從開場到完全定格的秒數")] private float duration = 4f;
    [SerializeField, Min(1f), Tooltip("放慢的程度：越大越早變慢")] private float slowDownPower = 3f;
    [SerializeField, Tooltip("演出元件 (要實作 ITitleIntroPart)")] private MonoBehaviour[] parts = System.Array.Empty<MonoBehaviour>();

    private float elapsed;

    public bool IsFinished { get; private set; }

    private void OnEnable()
    {
        elapsed = 0f;
        IsFinished = false;
        Apply(0f);
    }

    private void Update()
    {
        // 轉場進來時，等稜鏡散開才開始慢動作 (不然前半段被轉場蓋住)
        if (SceneTransition.IsBusy) return;

        elapsed += AnimationClock.ScaledDelta;
        float progress = IntroTimeline.Progress(elapsed, duration, slowDownPower);
        Apply(progress);
        if (progress < 1f) return;

        IsFinished = true;
        enabled = false;
    }

    private void Apply(float progress)
    {
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] is ITitleIntroPart part) part.SetProgress(progress);
        }
    }
}
