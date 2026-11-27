using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 結算面板：左邊 BATTLE COMPLETE + 關卡名稱 + 擊殺敵方單位數 + 使用時間，右邊選到的角色全身立繪；
// 顯示時暫停遊戲，按「確認」以稜鏡轉場回開始畫面
public class ResultPanel : MonoBehaviour
{
    [Serializable]
    public struct CharacterArt
    {
        [Tooltip("角色 ID (例如 CH0331)")] public string characterId;
        [Tooltip("全身立繪")] public Sprite art;
    }

    [Header("UI")]
    [SerializeField, Tooltip("面板本體 (預設關閉)")] private GameObject panel;
    [SerializeField, Tooltip("關卡名稱")] private Text stageText;
    [SerializeField, Tooltip("擊殺敵方單位數")] private Text killsText;
    [SerializeField, Tooltip("使用時間")] private Text timeText;
    [SerializeField, Tooltip("右邊的角色立繪")] private Image characterImage;
    [SerializeField, Tooltip("確認按鈕")] private Button confirmButton;

    [Header("Character")]
    [SerializeField, Tooltip("各角色的全身立繪")] private CharacterArt[] characterArts = Array.Empty<CharacterArt>();
    [SerializeField, Tooltip("沒有選角 (直接從戰鬥場景開始) 時用的立繪")] private Sprite defaultArt;
    [SerializeField, Tooltip("選角場景寫入的選擇，可留空")] private GameSelection gameSelection;

    [Header("Exit")]
    [SerializeField, Tooltip("確認後的轉場，可留空 (留空直接換場景)")] private SceneTransition sceneTransition;
    [SerializeField, Tooltip("確認後回到的場景；留空則只發出事件")] private string titleSceneName = "Title";

    public bool IsShowing { get; private set; }
    public event Action OnConfirmed;

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void OnEnable()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
    }

    // 停用或場景卸載時，若是本面板暫停了遊戲就把時間流速還原
    private void OnDisable()
    {
        if (confirmButton != null) confirmButton.onClick.RemoveListener(Confirm);
        if (IsShowing) Time.timeScale = 1f;
    }

    public void Show(string stageName, int kills, float seconds)
    {
        if (stageText != null) stageText.text = stageName;
        if (killsText != null) killsText.text = kills.ToString();
        if (timeText != null) timeText.text = BattleClock.Format(seconds);
        if (characterImage != null) characterImage.sprite = PickArt();

        IsShowing = true;
        if (panel != null) panel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void Confirm()
    {
        if (!IsShowing) return;

        IsShowing = false;
        Time.timeScale = 1f;
        OnConfirmed?.Invoke();
        if (string.IsNullOrEmpty(titleSceneName)) return;

        if (sceneTransition != null) sceneTransition.LoadScene(titleSceneName, false);
        else SceneManager.LoadScene(titleSceneName);
    }

    private Sprite PickArt()
    {
        string id = gameSelection != null && gameSelection.HasSelection ? gameSelection.CharacterId : null;
        for (int i = 0; i < characterArts.Length; i++)
        {
            if (characterArts[i].characterId == id) return characterArts[i].art;
        }

        return defaultArt;
    }
}
