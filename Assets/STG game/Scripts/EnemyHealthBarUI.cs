using UnityEngine;
using UnityEngine.UI;

// 敵方 (Boss) 的畫面血條，蔚藍檔案 Boss 戰樣式：總血量分成好幾層 (hpPerLayer)，條上只畫目前這一層，
// 下面露出下一層的顏色，尾端「x N」= 還剩幾層；白色殘影顯示剛扣掉的血 (同一層內才有殘影)。
// 平常隱藏，Bind 到某個 EnemyHealth 後顯示，目標死亡時隱藏。
// 本元件掛在常駐的物件上，只開關 panel，事件才能照 OnEnable/OnDisable 註冊與解除
public class EnemyHealthBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("整條血條的外框 (顯示/隱藏用)")] private GameObject panel;
    [SerializeField, Tooltip("目前這一層的血量 (Filled 類型)")] private Image currentFill;
    [SerializeField, Tooltip("下一層 (畫在目前這層下面，整條)，最後一層時隱藏")] private Image nextLayer;
    [SerializeField, Tooltip("剛扣掉的血 (Filled 類型，畫在目前這層後面)，可留空")] private Image damageTrail;
    [SerializeField, Tooltip("血量數字文字，可留空")] private Text valueText;
    [SerializeField, Tooltip("名稱文字，可留空")] private Text nameText;
    [SerializeField, Tooltip("剩餘層數「x N」，可留空")] private Text layerCountText;
    [SerializeField, Tooltip("血條左側的頭像，Bind 時換成該 Boss 的頭像，可留空")] private Image portraitImage;

    [Header("Layers")]
    [SerializeField, Min(0), Tooltip("每一層的血量 (0 = 整條只有一層)")] private int hpPerLayer = 250;
    [SerializeField, Tooltip("各層的顏色：第 0 個是最後一層 (最底層)，往上依序循環")]
    private Color[] layerColors =
    {
        new Color(0.84f, 0.21f, 0.21f, 1f),
        new Color(0.84f, 0.42f, 0.21f, 1f),
        new Color(0.86f, 0.68f, 0.16f, 1f),
        new Color(0.42f, 0.74f, 0.22f, 1f),
    };

    [Header("Damage Trail")]
    [SerializeField, Min(0f), Tooltip("受傷後白色殘影停留多久才開始縮短 (秒)")] private float trailDelay = 0.4f;
    [SerializeField, Min(0.01f), Tooltip("白色殘影縮短的速度 (每秒一層的比例)")] private float trailSpeed = 1.5f;

    // 層數只顯示整數，預先做好字串
    private static readonly string[] LayerTexts = BuildLayerTexts(100);

    private EnemyHealth health;
    private BossHpLayers shown;
    private float trailHoldUntil;

    private void Awake()
    {
        if (RequiredReference.IsMissing(panel, nameof(panel), this)) return;
        if (RequiredReference.IsMissing(currentFill, nameof(currentFill), this)) return;
        if (health == null) panel.SetActive(false);
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    // 只有白色殘影還沒追上時才需要每幀更新
    private void Update()
    {
        if (damageTrail == null || damageTrail.fillAmount <= shown.fill || Time.time < trailHoldUntil) return;

        damageTrail.fillAmount = Mathf.MoveTowards(damageTrail.fillAmount, shown.fill, trailSpeed * Time.deltaTime);
    }

    // 同上，並把左側頭像換成這隻 Boss 的頭像 (portrait 為 null 時保留原本的頭像)
    public void Bind(EnemyHealth target, string displayName, Sprite portrait)
    {
        if (portraitImage != null && portrait != null) portraitImage.sprite = portrait;
        Bind(target, displayName);
    }

    // 開始顯示某個敵人的血量 (例如 Boss 出場時)；再次呼叫會換成新的目標
    public void Bind(EnemyHealth target, string displayName)
    {
        Unsubscribe();
        health = target;
        if (isActiveAndEnabled) Subscribe();

        if (nameText != null) nameText.text = displayName;
        if (health == null)
        {
            panel.SetActive(false);
            return;
        }

        panel.SetActive(!health.IsDead);
        shown = BossHpLayers.Compute(health.CurrentHealth, health.MaxHealth, hpPerLayer);
        if (damageTrail != null) damageTrail.fillAmount = shown.fill;
        Show(health.CurrentHealth, health.MaxHealth, shown);
    }

    private void Subscribe()
    {
        if (health == null) return;

        health.OnHealthChanged += HandleHealthChanged;
        health.OnDied += HandleDied;
    }

    private void Unsubscribe()
    {
        if (health == null) return;

        health.OnHealthChanged -= HandleHealthChanged;
        health.OnDied -= HandleDied;
    }

    private void HandleHealthChanged(int current, int max)
    {
        BossHpLayers layers = BossHpLayers.Compute(current, max, hpPerLayer);

        // 同一層內受傷：殘影留在原處一下；換到下一層 (或回血)：殘影直接跟上
        if (damageTrail != null)
        {
            if (layers.remaining == shown.remaining && layers.fill < damageTrail.fillAmount) trailHoldUntil = Time.time + trailDelay;
            else damageTrail.fillAmount = layers.fill;
        }

        shown = layers;
        Show(current, max, layers);
    }

    private void Show(int current, int max, BossHpLayers layers)
    {
        currentFill.fillAmount = layers.fill;
        currentFill.color = LayerColor(layers.remaining);
        if (nextLayer != null)
        {
            bool hasNext = layers.remaining > 1;
            if (nextLayer.gameObject.activeSelf != hasNext) nextLayer.gameObject.SetActive(hasNext);
            if (hasNext) nextLayer.color = LayerColor(layers.remaining - 1);
        }

        if (layerCountText != null) layerCountText.text = LayerTexts[Mathf.Clamp(layers.remaining, 0, LayerTexts.Length - 1)];
        if (valueText != null) valueText.text = current + " / " + max;
    }

    // 第 1 層 (最後一層) 用 layerColors[0]，往上循環
    private Color LayerColor(int layer)
    {
        if (layerColors == null || layerColors.Length == 0) return Color.red;

        return layerColors[Mathf.Max(0, layer - 1) % layerColors.Length];
    }

    private void HandleDied()
    {
        panel.SetActive(false);
    }

    private static string[] BuildLayerTexts(int count)
    {
        string[] texts = new string[count];
        for (int i = 0; i < count; i++) texts[i] = "x" + i;
        return texts;
    }
}
