using System;
using UnityEngine;

// 全遊戲共用的聲音播放器 (第一次使用時自動建立，跨場景保留)：
// 背景音樂只有一首在播，換場景時同一首不會重播 (例如出擊時就開始的戰鬥 BGM 進場後接著播)；
// 換曲先淡出再淡入，用不受 timeScale 影響的時間 (開場、勝利時遊戲是暫停的)。
// 音效用另一個 AudioSource 疊播，也跨場景保留：按鈕按下後立刻換場景，點擊聲不會被切掉
public class GameAudio : MonoBehaviour
{
    [SerializeField, Range(0f, 1f), Tooltip("背景音樂音量")] private float musicVolume = 0.5f;
    [SerializeField, Range(0f, 1f), Tooltip("音效音量")] private float sfxVolume = 1f;
    [SerializeField, Range(0f, 1f), Tooltip("角色語音音量")] private float voiceVolume = 1f;
    [SerializeField, Min(0f), Tooltip("換曲時淡出 / 淡入各花的秒數")] private float fadeDuration = 0.4f;

    private static GameAudio instance;

    private AudioSource musicSource;
    private AudioSource sfxSource;
    private AudioSource voiceSource;
    private readonly System.Collections.Generic.Dictionary<AudioClip, float> lastPlayed = new System.Collections.Generic.Dictionary<AudioClip, float>();
    private AudioClip targetMusic;
    // 0 ~ 1，乘上 musicVolume
    private float fade = 1f;

    // 目前 (或即將) 播放的背景音樂；沒有則為 null
    public static AudioClip CurrentMusic => instance != null ? instance.targetMusic : null;

    // 每次播放音效時發出 (除錯 / 測試用)
    public static event Action<AudioClip> OnSfxPlayed;

    // 關閉 Domain Reload 時靜態欄位不會自動清空
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        OnSfxPlayed = null;
    }

    public static void PlayMusic(AudioClip clip)
    {
        if (clip == null) return;

        Instance().SetMusic(clip);
    }

    public static void StopMusic()
    {
        if (instance != null) instance.SetMusic(null);
    }

    // minInterval > 0：同一個音檔距離上次播放不到這麼久就略過 (以真實時間計算)
    public static void PlaySfx(AudioClip clip, float volume = 1f, float minInterval = 0f)
    {
        if (clip == null) return;

        GameAudio audio = Instance();
        float now = Time.unscaledTime;
        if (minInterval > 0f && audio.lastPlayed.TryGetValue(clip, out float last) && now - last < minInterval) return;

        audio.lastPlayed[clip] = now;
        audio.sfxSource.PlayOneShot(clip, volume);
        OnSfxPlayed?.Invoke(clip);
    }

    // 角色語音：同時只講一句，新的蓋掉還在講的
    public static void PlayVoice(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;

        GameAudio audio = Instance();
        audio.voiceSource.Stop();
        audio.voiceSource.clip = clip;
        audio.voiceSource.volume = volume * audio.voiceVolume;
        audio.voiceSource.Play();
        OnSfxPlayed?.Invoke(clip);
    }

    // 目前 (最後一次) 播放的角色語音
    public static AudioClip CurrentVoice => instance != null ? instance.voiceSource.clip : null;

    private static GameAudio Instance()
    {
        if (instance == null) new GameObject("GameAudio").AddComponent<GameAudio>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.volume = sfxVolume;
        voiceSource = gameObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void SetMusic(AudioClip clip)
    {
        if (clip == targetMusic) return;

        targetMusic = clip;
        // 現在沒在播：直接開始，不用等淡出
        if (musicSource.clip == null || !musicSource.isPlaying) SwitchToTarget();
    }

    private void Update()
    {
        float step = fadeDuration > 0f ? Time.unscaledDeltaTime / fadeDuration : 1f;
        bool switching = musicSource.clip != targetMusic;
        fade = Mathf.Clamp01(fade + (switching ? -step : step));
        if (switching && fade <= 0f) SwitchToTarget();
        musicSource.volume = musicVolume * fade;
    }

    private void SwitchToTarget()
    {
        musicSource.Stop();
        musicSource.clip = targetMusic;
        fade = 0f;
        musicSource.volume = 0f;
        if (targetMusic != null) musicSource.Play();
    }
}
