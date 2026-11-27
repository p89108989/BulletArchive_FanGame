using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 選角場景：選一個角色 + 左側一個 Boss 選項 (單一 Boss 或無盡模式) 後按出擊，
// 把選擇寫進 GameSelection 並載入戰鬥場景。一開始沒有角色被選 (全部 Idle)，Boss 預設選第一個分頁
public class CharacterSelectScreen : MonoBehaviour
{
    [Serializable]
    public struct BossOption
    {
        [Tooltip("分頁上的名稱")] public string label;
        [Tooltip("分頁上的圖示 (BattleUI/02_Tactics/06_Raid/Boss_Portrait_*_Lobby)")] public Sprite icon;
        [Tooltip("要打的 Boss 依序排列；還沒做完的 Boss 留空 (會略過)")] public BossProfile[] bosses;
        [Tooltip("打完最後一隻從頭再來 (無盡模式)")] public bool loop;
        [Tooltip("這個關卡的戰鬥 BGM (按出擊、轉場開始時就播)，可留空")] public AudioClip battleMusic;
    }

    [Header("Characters")]
    [SerializeField, Tooltip("場上可以選的角色")] private SelectableCharacter[] characters = Array.Empty<SelectableCharacter>();

    [Header("Boss")]
    [SerializeField, Tooltip("左側的 Boss 選項")] private BossOption[] bossOptions = Array.Empty<BossOption>();
    [SerializeField, Tooltip("每個 Boss 選項對應的分頁按鈕 (順序與上面相同)，可留空")] private Button[] bossTabs = Array.Empty<Button>();
    [SerializeField, Tooltip("選中的分頁底色")] private Color selectedTabColor = new Color(0.17f, 0.24f, 0.36f, 1f);
    [SerializeField, Tooltip("沒選中的分頁底色")] private Color normalTabColor = new Color(0.96f, 0.97f, 0.98f, 1f);
    [SerializeField, Tooltip("選中的分頁文字顏色")] private Color selectedTabTextColor = new Color(1f, 0.85f, 0.3f, 1f);
    [SerializeField, Tooltip("沒選中的分頁文字顏色")] private Color normalTabTextColor = new Color(0.17f, 0.24f, 0.36f, 1f);

    [Header("Mobilize")]
    [SerializeField, Tooltip("出擊按鈕 (沒選角色或 Boss 選項沒有可打的 Boss 時不能按)")] private Button mobilizeButton;
    [SerializeField, Tooltip("寫入選擇的資產 (戰鬥場景的 BossEncounter 也要指定同一個)")] private GameSelection gameSelection;
    [SerializeField, Tooltip("出擊後載入的場景名稱 (要在 Build Settings 裡)；留空則只寫入選擇")] private string gameSceneName = "mian game";
    [SerializeField, Tooltip("進入戰鬥的轉場 (先壓黑再稜鏡)，可留空 (留空直接換場景)")] private SceneTransition sceneTransition;

    [Header("Voice")]
    [SerializeField, Tooltip("各角色的語音 (點到角色時講選角語音)")] private CharacterVoiceSet[] voiceSets = Array.Empty<CharacterVoiceSet>();

    public SelectableCharacter SelectedCharacter { get; private set; }
    public int SelectedBossOption { get; private set; }

    public bool CanMobilize => SelectedCharacter != null && SelectedOptionHasBoss();

    private void OnEnable()
    {
        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i] != null) characters[i].OnClicked += PickCharacter;
        }

        if (mobilizeButton != null) mobilizeButton.onClick.AddListener(Mobilize);
        for (int i = 0; i < bossTabs.Length; i++)
        {
            int index = i;
            if (bossTabs[i] != null) bossTabs[i].onClick.AddListener(() => PickBossOption(index));
        }

        Refresh();
    }

    private void OnDisable()
    {
        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i] != null) characters[i].OnClicked -= PickCharacter;
        }

        if (mobilizeButton != null) mobilizeButton.onClick.RemoveListener(Mobilize);
        for (int i = 0; i < bossTabs.Length; i++)
        {
            if (bossTabs[i] != null) bossTabs[i].onClick.RemoveAllListeners();
        }
    }

    // 選這個角色 (它播 Pickup，其他回到 Idle)
    public void PickCharacter(SelectableCharacter character)
    {
        SelectedCharacter = character;
        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i] != null) characters[i].SetPicked(characters[i] == character);
        }

        // 只講點到的那個角色自己的語音 (找不到就不講，不用預設那組)
        CharacterVoiceSet voices = character != null ? CharacterVoiceSet.Find(voiceSets, character.CharacterId) : null;
        if (voices != null && voices.CharacterId == character.CharacterId) voices.Select.PlayAsVoice();
        Refresh();
    }

    public void PickBossOption(int index)
    {
        if (index < 0 || index >= bossOptions.Length) return;

        SelectedBossOption = index;
        Refresh();
    }

    // 寫入選擇並進入戰鬥
    public void Mobilize()
    {
        if (!CanMobilize) return;

        BossOption option = bossOptions[SelectedBossOption];
        if (gameSelection != null) gameSelection.Select(SelectedCharacter.CharacterId, option.bosses, option.loop, option.battleMusic);
        GameAudio.PlayMusic(option.battleMusic);
        if (string.IsNullOrEmpty(gameSceneName)) return;

        if (sceneTransition != null) sceneTransition.LoadScene(gameSceneName, true);
        else SceneManager.LoadScene(gameSceneName);
    }

    private bool SelectedOptionHasBoss()
    {
        if (SelectedBossOption >= bossOptions.Length) return false;

        BossProfile[] bosses = bossOptions[SelectedBossOption].bosses;
        if (bosses == null) return false;
        for (int i = 0; i < bosses.Length; i++)
        {
            if (bosses[i] != null) return true;
        }

        return false;
    }

    private void Refresh()
    {
        if (mobilizeButton != null) mobilizeButton.interactable = CanMobilize;
        for (int i = 0; i < bossTabs.Length; i++)
        {
            if (bossTabs[i] == null) continue;

            bool selected = i == SelectedBossOption;
            if (bossTabs[i].targetGraphic != null) bossTabs[i].targetGraphic.color = selected ? selectedTabColor : normalTabColor;
            Text label = bossTabs[i].GetComponentInChildren<Text>();
            if (label != null) label.color = selected ? selectedTabTextColor : normalTabTextColor;
        }
    }
}
