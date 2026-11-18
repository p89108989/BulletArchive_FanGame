using System;
using UnityEngine;

// 依子彈屬性切換外觀：彈體發光材質、粒子光暈顏色與 (可選的) 點光源顏色
public class BulletPolarityVisual : MonoBehaviour
{
    [Serializable]
    private struct PolarityStyle
    {
        [Tooltip("對應的屬性")] public BulletPolarity polarity;
        [Tooltip("彈體使用的發光材質")] public Material bodyMaterial;
        [Tooltip("粒子光暈的顏色 (亮度由粒子材質的 HDR Base Color 決定)")] public Color glowColor;
        [Tooltip("點光源顏色，只有在 glowLight 啟用時才看得到")] public Color lightColor;
    }

    [Header("References")]
    [SerializeField, Tooltip("彈體模型的 Renderer")] private Renderer bodyRenderer;
    [SerializeField, Tooltip("跟隨子彈的光暈粒子")] private ParticleSystem glowParticles;
    [SerializeField, Tooltip("可選的即時點光源；大量子彈同時存在時很吃效能，預設關閉")] private Light glowLight;

    [Header("Styles")]
    [SerializeField, Tooltip("每種屬性對應的外觀設定")] private PolarityStyle[] styles;

    // 套用指定屬性的外觀；子彈從物件池取出後每次都會呼叫
    public void Apply(BulletPolarity polarity)
    {
        if (!TryGetStyle(polarity, out PolarityStyle style)) return;

        if (bodyRenderer != null && style.bodyMaterial != null)
        {
            bodyRenderer.sharedMaterial = style.bodyMaterial;
        }

        if (glowParticles != null)
        {
            ParticleSystem.MainModule main = glowParticles.main;
            main.startColor = style.glowColor;
            // 清掉上一次使用時殘留的舊顏色粒子，避免重用時閃一下別的顏色
            glowParticles.Clear();
            glowParticles.Play();
        }

        if (glowLight != null)
        {
            glowLight.color = style.lightColor;
        }
    }

    private bool TryGetStyle(BulletPolarity polarity, out PolarityStyle style)
    {
        for (int i = 0; i < styles.Length; i++)
        {
            if (styles[i].polarity == polarity)
            {
                style = styles[i];
                return true;
            }
        }

        style = default;
        return false;
    }
}
