using UnityEngine;

// 一隻 Boss 的出場設定：哪個 Prefab、血條名稱、生成位置、Boss 戰的相機位置。
// 每隻 Boss 一個資產檔，BossEncounter 指定哪一個就打哪一隻 (之後選關畫面也只要換這個)
[CreateAssetMenu(menuName = "STG/Boss Profile", fileName = "BossProfile")]
public class BossProfile : ScriptableObject
{
    [Header("Boss")]
    [SerializeField, Tooltip("Boss Prefab")] private GameObject bossPrefab;
    [SerializeField, Tooltip("血條上顯示的 Boss 名稱")] private string displayName;
    [SerializeField, Tooltip("血條左側的 Boss 頭像 (BattleUI/02_Tactics/06_Raid/Portrait_Raidboss_*)，留空則不更換")] private Sprite portrait;
    [SerializeField, Tooltip("Boss 生成位置")] private Vector3 spawnPosition = new Vector3(-50f, -100f, -65f);
    [SerializeField, Tooltip("Boss 生成時的旋轉 (尤拉角)")] private Vector3 spawnRotation;

    [Header("Camera")]
    [SerializeField, Tooltip("Boss 戰時的相機位置")] private Vector3 cameraPosition = new Vector3(-50f, 45f, 85f);
    [SerializeField, Tooltip("Boss 戰時的相機旋轉 (尤拉角)；水平朝向 (Y) 要與一般視角相同，玩家的 WASD 方向才不會改變")]
    private Vector3 cameraRotation = new Vector3(20f, 180f, 0f);

    [Header("Player Area")]
    [SerializeField, Tooltip("Boss 戰時限制玩家的活動範圍 (相機只看得到場地一部分的 Boss 要開，避免玩家跑出畫面)")] private bool limitPlayerArea;
    [SerializeField, Tooltip("可活動範圍的最小 X / Z")] private Vector2 playerAreaMin = new Vector2(-104f, -4f);
    [SerializeField, Tooltip("可活動範圍的最大 X / Z")] private Vector2 playerAreaMax = new Vector2(3f, 47f);

    [Header("Sky")]
    [SerializeField, Min(-1), Tooltip("Boss 戰的時段 (SkyEnvironment presets 的索引：0 白天、1 黃昏、2 黑夜)；-1 = 用 SkyEnvironment 的預設 Boss 時段")]
    private int skyPreset = -1;

    public GameObject BossPrefab => bossPrefab;
    // Boss 戰的時段索引，-1 = 沒有指定
    public int SkyPreset => skyPreset;
    public string DisplayName => displayName;
    public Sprite Portrait => portrait;
    public Vector3 SpawnPosition => spawnPosition;
    public Quaternion SpawnRotation => Quaternion.Euler(spawnRotation);
    public Vector3 CameraPosition => cameraPosition;
    public Quaternion CameraRotation => Quaternion.Euler(cameraRotation);
    public bool LimitsPlayerArea => limitPlayerArea;
    public Vector2 PlayerAreaMin => playerAreaMin;
    public Vector2 PlayerAreaMax => playerAreaMax;
}
