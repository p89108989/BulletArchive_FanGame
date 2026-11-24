#if UNITY_EDITOR
using UnityEngine;

// 把白天天空貼圖 (skybox_skydays_3) 的像素調成黃昏或黑夜。
// 原圖的亮度大致代表「離地平線多近」(天頂深藍、地平線附近接近白)，雲也比天空亮，
// 所以用亮度沿著一條漸層換色，雲與漸層的細節都能保留；
// 海是青色 (綠/藍高、紅很少)，跟天空分開調色；貼圖外的黑底維持黑色
public static class SkyColorGrading
{
    // 天空亮度範圍：天頂約 0.33、地平線約 0.85
    private const float SkyDarkLuma = 0.33f;
    private const float SkyBrightLuma = 0.85f;
    // 海的亮度範圍
    private const float SeaDarkLuma = 0.4f;
    private const float SeaBrightLuma = 0.68f;
    // 只有天空 (不是雲、不是地平線) 才放星星
    private const float StarThreshold = 0.997f;
    private const float StarBrightness = 0.9f;

    private static readonly Color DuskZenith = new Color(0.2f, 0.14f, 0.38f);
    private static readonly Color DuskMiddle = new Color(0.85f, 0.38f, 0.42f);
    private static readonly Color DuskHorizon = new Color(1f, 0.72f, 0.42f);
    private static readonly Color DuskSeaDark = new Color(0.14f, 0.12f, 0.3f);
    private static readonly Color DuskSeaBright = new Color(0.8f, 0.45f, 0.4f);

    private static readonly Color NightZenith = new Color(0.015f, 0.025f, 0.08f);
    private static readonly Color NightMiddle = new Color(0.05f, 0.08f, 0.2f);
    private static readonly Color NightHorizon = new Color(0.28f, 0.33f, 0.48f);
    private static readonly Color NightSeaDark = new Color(0.01f, 0.025f, 0.06f);
    private static readonly Color NightSeaBright = new Color(0.04f, 0.1f, 0.18f);

    public static Color Dusk(Color day)
    {
        return Grade(day, DuskZenith, DuskMiddle, DuskHorizon, DuskSeaDark, DuskSeaBright, 0f);
    }

    // starNoise：每個像素一個 0~1 的隨機值，超過門檻的開闊天空像素變成星星
    public static Color Night(Color day, float starNoise)
    {
        return Grade(day, NightZenith, NightMiddle, NightHorizon, NightSeaDark, NightSeaBright, starNoise);
    }

    private static Color Grade(Color day, Color zenith, Color middle, Color horizon, Color seaDark, Color seaBright, float starNoise)
    {
        float luma = 0.2126f * day.r + 0.7152f * day.g + 0.0722f * day.b;
        float blue = Mathf.Max(day.b, 0.001f);
        float sea = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 0.7f, day.g / blue))
            * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.3f, day.r / blue)));

        float skyHeight = Mathf.InverseLerp(SkyDarkLuma, SkyBrightLuma, luma);
        Color skyColor = skyHeight < 0.5f
            ? Color.Lerp(zenith, middle, skyHeight * 2f)
            : Color.Lerp(middle, horizon, (skyHeight - 0.5f) * 2f);
        Color seaColor = Color.Lerp(seaDark, seaBright, Mathf.InverseLerp(SeaDarkLuma, SeaBrightLuma, luma));
        Color graded = Color.Lerp(skyColor, seaColor, sea);

        float openSky = (1f - sea) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.45f, skyHeight)));
        if (starNoise > StarThreshold)
        {
            float star = (starNoise - StarThreshold) / (1f - StarThreshold) * StarBrightness * openSky;
            graded += new Color(star, star, star * 0.9f, 0f);
        }

        // 貼圖外的黑底 (以及與它相鄰的邊緣) 維持黑色，避免 mipmap 把顏色滲到接縫
        graded *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.08f, luma));
        graded.a = day.a;
        return graded;
    }
}
#endif
