using UnityEngine;

// 一個角色的語音與專屬音效 (每個可選角色一份資產，要換聲音只要改這裡)：
// 戰鬥開始、衝刺、移動、勝利結算、選角時被點到、落敗、受傷，以及一般射擊的槍聲
[CreateAssetMenu(menuName = "STG/Character Voice Set", fileName = "CharacterVoiceSet")]
public class CharacterVoiceSet : ScriptableObject
{
    [SerializeField, Tooltip("角色 ID (與 SelectableCharacter / GameSelection 相同，例如 CH0331)")] private string characterId;

    [Header("Voice")]
    [SerializeField, Tooltip("戰鬥開始 (START)")] private SoundEffect battleStart = new SoundEffect();
    [SerializeField, Tooltip("衝刺")] private SoundEffect dash = new SoundEffect();
    [SerializeField, Tooltip("移動中 (每隔一段時間有移動就講)")] private SoundEffect move = new SoundEffect();
    [SerializeField, Tooltip("勝利結算面板")] private SoundEffect victory = new SoundEffect();
    [SerializeField, Tooltip("選角畫面點到這個角色")] private SoundEffect select = new SoundEffect();
    [SerializeField, Tooltip("戰鬥失敗")] private SoundEffect defeat = new SoundEffect();
    [SerializeField, Tooltip("受到傷害")] private SoundEffect damage = new SoundEffect();

    [Header("Sound")]
    [SerializeField, Tooltip("一般射擊每一發的聲音")] private SoundEffect basicAttack = new SoundEffect();

    public string CharacterId => characterId;
    public SoundEffect BattleStart => battleStart;
    public SoundEffect Dash => dash;
    public SoundEffect Move => move;
    public SoundEffect Victory => victory;
    public SoundEffect Select => select;
    public SoundEffect Defeat => defeat;
    public SoundEffect Damage => damage;
    public SoundEffect BasicAttack => basicAttack;

    // 找角色 ID 對應的語音；沒有對應 (例如直接開戰鬥場景、沒選過角色) 時用第一組
    public static CharacterVoiceSet Find(CharacterVoiceSet[] sets, string id)
    {
        if (sets == null) return null;

        CharacterVoiceSet first = null;
        for (int i = 0; i < sets.Length; i++)
        {
            if (sets[i] == null) continue;
            if (first == null) first = sets[i];
            if (!string.IsNullOrEmpty(id) && sets[i].characterId == id) return sets[i];
        }

        return first;
    }
}
