using UnityEngine;

// 警示區的呼吸閃爍：透明度在 minAlpha ~ maxAlpha 之間來回變化 (例如 Binah 光炮蓄力時整個場地的紅色警示)。
// 用 MaterialPropertyBlock 改顏色，不會動到共用的材質資產
[RequireComponent(typeof(Renderer))]
public class PulseAlpha : MonoBehaviour
{
    [SerializeField, Tooltip("警示顏色 (透明度由下面兩個值決定)")] private Color color = new Color(1f, 0.1f, 0.1f, 1f);
    [SerializeField, Range(0f, 1f), Tooltip("最淡時的透明度")] private float minAlpha = 0.12f;
    [SerializeField, Range(0f, 1f), Tooltip("最濃時的透明度")] private float maxAlpha = 0.35f;
    [SerializeField, Min(0.05f), Tooltip("一次閃爍 (淡 → 濃 → 淡) 的秒數")] private float period = 1f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private Renderer target;
    private MaterialPropertyBlock block;
    private float phase;

    // 閃爍週期 (秒)；執行中可以改 (例如蓄力快完成時閃得越來越快)，相位累加所以改週期時不會跳動
    public float Period
    {
        get => period;
        set => period = Mathf.Max(0.05f, value);
    }

    private void Awake()
    {
        target = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
    }

    // 每次出現都從最淡開始，閃爍節奏一致
    private void OnEnable()
    {
        phase = 0f;
        Apply(minAlpha);
    }

    private void Update()
    {
        phase = Mathf.Repeat(phase + Time.deltaTime / period, 1f);
        float t = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);
        Apply(Mathf.Lerp(minAlpha, maxAlpha, t));
    }

    private void Apply(float alpha)
    {
        target.GetPropertyBlock(block);
        block.SetColor(BaseColorId, new Color(color.r, color.g, color.b, alpha));
        target.SetPropertyBlock(block);
    }
}
