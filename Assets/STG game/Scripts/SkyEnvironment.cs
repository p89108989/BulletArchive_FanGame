using System;
using UnityEngine;
using UnityEngine.Rendering;

// 場景時段 (白天/黃昏/黑夜)：天空球的貼圖、太陽光與環境光一起切換。
// 進入 Boss 戰時漸變到 Boss 指定的時段，回到一般戰鬥時變回一般時段；漸變時間配合鏡頭轉場。
// 天空用 STG/SkyBlend 在兩張貼圖間交叉淡化，參數走 MaterialPropertyBlock，不會改到材質資產
public class SkyEnvironment : MonoBehaviour, ICombatPhaseListener
{
    [Serializable]
    public struct SkyPreset
    {
        [Tooltip("時段名稱 (只用來辨識)")] public string name;
        [Tooltip("天空球貼圖")] public Texture skyTexture;
        [Tooltip("太陽光 (Directional Light) 顏色")] public Color sunColor;
        [Min(0f), Tooltip("太陽光強度")] public float sunIntensity;
        [Range(0f, 2f), Tooltip("環境光強度 (場景原本的環境光乘上這個倍數)")] public float ambientIntensity;
        [Tooltip("額外疊加的環境光顏色 (例如黃昏偏橘、黑夜偏藍)")] public Color ambientTint;
        [Min(0f), Tooltip("角色補光 (characterLights) 的強度，0 = 關閉；暗的時段讓深色角色仍看得清楚")] public float characterLightIntensity;
    }

    [Header("References")]
    [SerializeField, Tooltip("天空球的 Renderer (材質需使用 STG/SkyBlend)")] private Renderer skyRenderer;
    [SerializeField, Tooltip("場景的太陽光 (Directional Light)")] private Light sun;
    [SerializeField, Tooltip("跟著角色的補光 (例如 Rio 身上的點光源)，強度依時段設定")] private Light[] characterLights = Array.Empty<Light>();

    [Header("Presets")]
    [SerializeField, Tooltip("可用的時段，例如 0 白天、1 黃昏、2 黑夜")] private SkyPreset[] presets = Array.Empty<SkyPreset>();
    [SerializeField, Min(0), Tooltip("一般戰鬥使用的時段 (presets 的索引)")] private int normalPreset;
    [SerializeField, Min(0), Tooltip("Boss 戰使用的時段 (presets 的索引)；Perorozilla 為黑夜")] private int bossPreset = 2;
    [SerializeField, Min(0f), Tooltip("切換時段的漸變時間 (秒)，建議與鏡頭轉場時間相同")] private float transitionDuration = 2f;

    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int BlendTexId = Shader.PropertyToID("_BlendTex");
    private static readonly int BlendId = Shader.PropertyToID("_Blend");

    private MaterialPropertyBlock propertyBlock;
    private SphericalHarmonicsL2 sceneAmbient;
    private Texture currentTexture;
    private Color fromSunColor;
    private float fromSunIntensity;
    private float fromAmbientIntensity;
    private float fromCharacterLight;
    private float currentCharacterLight;
    private Color fromAmbientTint;
    private float currentAmbientIntensity = 1f;
    private Color currentAmbientTint = Color.black;
    private int targetPreset = -1;
    private float elapsed;
    private bool transitioning;

    // 給測試與建置工具建立時段設定用
    public static SkyPreset CreatePreset(string name, Texture texture, Color sunColor, float sunIntensity, float ambientIntensity, Color ambientTint, float characterLightIntensity)
    {
        return new SkyPreset
        {
            name = name,
            skyTexture = texture,
            sunColor = sunColor,
            sunIntensity = sunIntensity,
            ambientIntensity = ambientIntensity,
            ambientTint = ambientTint,
            characterLightIntensity = characterLightIntensity,
        };
    }

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        // 場景原本 (烘焙/天空盒) 的環境光作為基準，各時段只做倍數與疊色
        sceneAmbient = RenderSettings.ambientProbe;
        if (RequiredReference.IsMissing(skyRenderer, nameof(skyRenderer), this)) return;
        BeginTransition(normalPreset, 0f);
    }

    public void OnCombatPhaseChanged(CombatPhase phase)
    {
        BeginTransition(phase == CombatPhase.Boss ? bossPreset : normalPreset, transitionDuration);
    }

    private void Update()
    {
        if (!transitioning) return;

        elapsed += Time.deltaTime;
        float t = transitionDuration > 0f ? Mathf.Clamp01(elapsed / transitionDuration) : 1f;
        Apply(Mathf.SmoothStep(0f, 1f, t));
        if (t >= 1f) transitioning = false;
    }

    // 從目前的狀態漸變到指定時段；漸變途中再切換時從當下的光線接續 (天空從上一個目標貼圖開始淡入)
    private void BeginTransition(int presetIndex, float duration)
    {
        if (presetIndex < 0 || presetIndex >= presets.Length || !enabled) return;

        targetPreset = presetIndex;
        Texture fromTexture = currentTexture != null ? currentTexture : presets[presetIndex].skyTexture;
        currentTexture = presets[presetIndex].skyTexture;

        fromSunColor = sun != null ? sun.color : Color.white;
        fromSunIntensity = sun != null ? sun.intensity : 0f;
        fromAmbientIntensity = currentAmbientIntensity;
        fromAmbientTint = currentAmbientTint;
        fromCharacterLight = currentCharacterLight;

        if (skyRenderer != null)
        {
            if (fromTexture != null) propertyBlock.SetTexture(MainTexId, fromTexture);
            if (currentTexture != null) propertyBlock.SetTexture(BlendTexId, currentTexture);
        }

        elapsed = 0f;
        transitioning = duration > 0f;
        Apply(transitioning ? 0f : 1f);
    }

    private void Apply(float blend)
    {
        SkyPreset target = presets[targetPreset];

        if (skyRenderer != null)
        {
            propertyBlock.SetFloat(BlendId, blend);
            skyRenderer.SetPropertyBlock(propertyBlock);
        }

        if (sun != null)
        {
            sun.color = Color.Lerp(fromSunColor, target.sunColor, blend);
            sun.intensity = Mathf.Lerp(fromSunIntensity, target.sunIntensity, blend);
        }

        currentCharacterLight = Mathf.Lerp(fromCharacterLight, target.characterLightIntensity, blend);
        for (int i = 0; i < characterLights.Length; i++)
        {
            if (characterLights[i] == null) continue;

            // 強度為 0 時直接關掉，白天不多算一盞燈
            characterLights[i].intensity = currentCharacterLight;
            characterLights[i].enabled = currentCharacterLight > 0.001f;
        }

        currentAmbientIntensity = Mathf.Lerp(fromAmbientIntensity, target.ambientIntensity, blend);
        currentAmbientTint = Color.Lerp(fromAmbientTint, target.ambientTint, blend);
        SphericalHarmonicsL2 ambient = sceneAmbient * currentAmbientIntensity;
        ambient.AddAmbientLight(currentAmbientTint);
        RenderSettings.ambientProbe = ambient;
    }
}
