using UnityEngine;

// 物件出現 (Start) 時播一次聲音，例如 Boss 生成時的登場聲
public class PlaySoundOnStart : MonoBehaviour
{
    [SerializeField, Tooltip("出現時播放的聲音")] private SoundEffect sound = new SoundEffect();

    private void Start()
    {
        sound.Play();
    }
}
