using UnityEngine;

// 持續循環的音效 (直升機旋翼、戰車履帶)：擁有者呼叫 SetPlaying 開關；
// 遊戲暫停 (timeScale = 0：暫停選單、結算) 時跟著暫停，恢復後接著播
public class LoopingSound : MonoBehaviour
{
    [SerializeField, Tooltip("循環播放的音檔")] private AudioClip clip;
    [SerializeField, Range(0f, 1f), Tooltip("音量")] private float volume = 0.5f;

    private AudioSource source;
    private bool wantPlaying;
    private bool paused;

    public bool IsPlaying => wantPlaying;

    private void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
    }

    private void OnDisable()
    {
        wantPlaying = false;
        paused = false;
        if (source != null) source.Stop();
    }

    public void SetPlaying(bool playing)
    {
        wantPlaying = playing && clip != null;
        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (source == null) return;

        if (!wantPlaying)
        {
            if (source.isPlaying || paused) source.Stop();
            paused = false;
            return;
        }

        if (Time.timeScale <= 0f)
        {
            if (!source.isPlaying) return;

            source.Pause();
            paused = true;
            return;
        }

        if (source.isPlaying) return;

        source.volume = volume;
        if (paused) source.UnPause();
        else
        {
            source.clip = clip;
            source.Play();
        }

        paused = false;
    }
}
