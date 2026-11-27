using UnityEngine;
using UnityEngine.UI;

// 場景裡所有按鈕 (含一開始隱藏的暫停選單、結算面板) 按下時播放點擊音效。
// 場景開始時找一次並登記，不必每顆按鈕各掛一個元件；之後才生成的按鈕不會有聲音
public class UiClickSounds : MonoBehaviour
{
    [SerializeField, Tooltip("按鈕點擊音效")] private AudioClip clickSound;

    private Button[] buttons = System.Array.Empty<Button>();

    private void Awake()
    {
        buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    private void OnEnable()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null) buttons[i].onClick.AddListener(PlayClick);
        }
    }

    private void OnDisable()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null) buttons[i].onClick.RemoveListener(PlayClick);
        }
    }

    private void PlayClick()
    {
        GameAudio.PlaySfx(clickSound);
    }
}
