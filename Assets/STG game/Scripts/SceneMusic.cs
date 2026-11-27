using UnityEngine;

// 場景開始時播放這個場景的背景音樂 (開始畫面、選角畫面)；同一首已經在播就接著播
public class SceneMusic : MonoBehaviour
{
    [SerializeField, Tooltip("這個場景的背景音樂")] private AudioClip music;

    private void Start()
    {
        GameAudio.PlayMusic(music);
    }
}
