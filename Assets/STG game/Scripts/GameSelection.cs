using System;
using System.Collections.Generic;
using UnityEngine;

// 選角場景與戰鬥場景之間的橋：選角場景寫入選到的角色與 Boss 清單，戰鬥場景 (BossEncounter) 讀取。
// 內容只存在這次遊玩 (欄位不序列化、載入時清空)：Editor 玩完不會改到資產檔，
// 直接開戰鬥場景測試時也不會沿用上次的選擇
[CreateAssetMenu(menuName = "STG/Game Selection", fileName = "GameSelection")]
public class GameSelection : ScriptableObject
{
    [NonSerialized] private string characterId;
    [NonSerialized] private bool loop;
    [NonSerialized] private bool hasSelection;
    [NonSerialized] private readonly List<BossProfile> bosses = new List<BossProfile>();
    [NonSerialized] private AudioClip battleMusic;

    public bool HasSelection => hasSelection;
    // 選到的關卡的戰鬥 BGM (出擊時就開始播，戰鬥場景接著播)；沒有指定為 null
    public AudioClip BattleMusic => battleMusic;
    // 選到的角色 (CH0331 等)；角色還沒實裝，目前只記錄
    public string CharacterId => characterId;
    // 打完最後一隻是否從頭再來 (無盡模式)
    public bool Loop => loop;
    public int BossCount => bosses.Count;

    private void OnEnable()
    {
        Clear();
    }

    // 記下這次的選擇；沒有 Profile 的 Boss (還沒做完) 直接略過
    public void Select(string selectedCharacterId, BossProfile[] bossList, bool loopBosses, AudioClip music = null)
    {
        battleMusic = music;
        bosses.Clear();
        if (bossList != null)
        {
            for (int i = 0; i < bossList.Length; i++)
            {
                if (bossList[i] != null) bosses.Add(bossList[i]);
            }
        }

        characterId = selectedCharacterId;
        loop = loopBosses;
        hasSelection = true;
    }

    // 第 index 隻要打的 Boss；循環時繞回開頭，不循環且打完了回傳 null
    public BossProfile GetBoss(int index)
    {
        if (index < 0 || bosses.Count == 0) return null;
        if (loop) return bosses[index % bosses.Count];
        return index < bosses.Count ? bosses[index] : null;
    }

    public void Clear()
    {
        bosses.Clear();
        characterId = null;
        battleMusic = null;
        loop = false;
        hasSelection = false;
    }
}
