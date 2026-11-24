using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

// 場景天色：進入 Boss 戰時天空、太陽光與環境光一起漸變到 Boss 指定的時段 (Perorozilla = 黑夜)，回到一般戰鬥時變回白天
public class SkyEnvironmentTests
{
    private GameObject root;
    private SkyEnvironment environment;
    private Renderer sky;
    private Light sun;
    private Light characterLight;
    private Texture2D dayTexture;
    private Texture2D nightTexture;
    private SphericalHarmonicsL2 originalProbe;

    [SetUp]
    public void SetUp()
    {
        originalProbe = RenderSettings.ambientProbe;
        SphericalHarmonicsL2 white = new SphericalHarmonicsL2();
        white.AddAmbientLight(Color.white);
        RenderSettings.ambientProbe = white;

        dayTexture = new Texture2D(2, 2);
        nightTexture = new Texture2D(2, 2);

        root = new GameObject("Sky");
        root.SetActive(false);
        sky = GameObject.CreatePrimitive(PrimitiveType.Cube).GetComponent<Renderer>();
        sky.transform.SetParent(root.transform, false);
        sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.SetParent(root.transform, false);

        characterLight = new GameObject("CharacterLight").AddComponent<Light>();
        characterLight.type = LightType.Point;
        characterLight.transform.SetParent(root.transform, false);

        environment = root.AddComponent<SkyEnvironment>();
        TestUtil.SetField(environment, "characterLights", new[] { characterLight });
        TestUtil.SetField(environment, "skyRenderer", sky);
        TestUtil.SetField(environment, "sun", sun);
        TestUtil.SetField(environment, "transitionDuration", 0.4f);
        TestUtil.SetField(environment, "normalPreset", 0);
        TestUtil.SetField(environment, "bossPreset", 1);
        TestUtil.SetField(environment, "presets", new[]
        {
            SkyEnvironment.CreatePreset("Day", dayTexture, Color.white, 1.2f, 1f, Color.black, 0f),
            SkyEnvironment.CreatePreset("Night", nightTexture, new Color(0.5f, 0.6f, 1f), 0.3f, 0.3f, Color.black, 8f),
        });
        root.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(root);
        Object.Destroy(dayTexture);
        Object.Destroy(nightTexture);
        RenderSettings.ambientProbe = originalProbe;
    }

    private float Blend()
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        sky.GetPropertyBlock(block);
        return block.GetFloat("_Blend");
    }

    private Texture TargetTexture()
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        sky.GetPropertyBlock(block);
        return block.GetTexture("_BlendTex");
    }

    [UnityTest]
    public IEnumerator StartsWithNormalPreset()
    {
        yield return null;

        Assert.AreEqual(1.2f, sun.intensity, 0.01f);
        Assert.AreEqual(dayTexture, TargetTexture());
        Assert.AreEqual(1f, Blend(), 0.01f);
        Assert.IsFalse(characterLight.enabled, "no character light in daytime");
    }

    // 黑夜時角色身上的補光亮起，讓深色的角色在暗場景裡仍看得清楚
    [UnityTest]
    public IEnumerator BossPhase_TurnsOnCharacterLight()
    {
        yield return null;
        environment.OnCombatPhaseChanged(CombatPhase.Boss);
        yield return new WaitForSeconds(0.55f);

        Assert.IsTrue(characterLight.enabled);
        Assert.AreEqual(8f, characterLight.intensity, 0.01f);
    }

    [UnityTest]
    public IEnumerator BossPhase_FadesToBossPreset()
    {
        yield return null;
        environment.OnCombatPhaseChanged(CombatPhase.Boss);
        yield return new WaitForSeconds(0.2f);

        Assert.That(sun.intensity, Is.InRange(0.31f, 1.19f), "the light changes gradually");
        Assert.That(Blend(), Is.InRange(0.01f, 0.99f), "the sky crossfades");

        yield return new WaitForSeconds(0.35f);

        Assert.AreEqual(0.3f, sun.intensity, 0.01f);
        Assert.AreEqual(nightTexture, TargetTexture());
        Assert.AreEqual(1f, Blend(), 0.01f);
        Assert.Less(RenderSettings.ambientProbe[0, 0], originalWhiteDc() * 0.5f, "ambient light dims at night");
    }

    [UnityTest]
    public IEnumerator NormalPhase_ReturnsToDay()
    {
        yield return null;
        environment.OnCombatPhaseChanged(CombatPhase.Boss);
        yield return new WaitForSeconds(0.5f);
        environment.OnCombatPhaseChanged(CombatPhase.Normal);
        yield return new WaitForSeconds(0.5f);

        Assert.AreEqual(1.2f, sun.intensity, 0.01f);
        Assert.AreEqual(dayTexture, TargetTexture());
        Assert.AreEqual(originalWhiteDc(), RenderSettings.ambientProbe[0, 0], 0.01f);
    }

    private static float originalWhiteDc()
    {
        SphericalHarmonicsL2 white = new SphericalHarmonicsL2();
        white.AddAmbientLight(Color.white);
        return white[0, 0];
    }
}
