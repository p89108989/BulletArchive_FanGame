using System;
using UnityEngine;

// 一個可在 Inspector 調整的音效：音檔、音量、最短間隔 (同一個聲音在間隔內再觸發就略過，
// 例如連射時的受擊聲、直升機一波三顆飛彈的爆炸只響一次)
[Serializable]
public class SoundEffect
{
    [SerializeField, Tooltip("音檔，留空則不播放")] private AudioClip clip;
    [SerializeField, Range(0f, 1f), Tooltip("音量")] private float volume = 1f;
    [SerializeField, Min(0f), Tooltip("同一個聲音至少隔幾秒才會再播 (0 = 不限制)")] private float minInterval;

    public SoundEffect()
    {
    }

    public SoundEffect(AudioClip clip, float volume, float minInterval = 0f)
    {
        this.clip = clip;
        this.volume = volume;
        this.minInterval = minInterval;
    }

    public AudioClip Clip => clip;
    public float Volume => volume;

    public void Play()
    {
        GameAudio.PlaySfx(clip, volume, minInterval);
    }

    // 角色語音：同時只講一句
    public void PlayAsVoice()
    {
        GameAudio.PlayVoice(clip, volume);
    }
}
