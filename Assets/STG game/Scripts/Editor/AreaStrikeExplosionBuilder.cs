#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 產生範圍攻擊的爆炸粒子：火球 (加算發光) + 火花 (拉長亮點) + 煙霧 (半透明灰煙)。
// 所有數值以半徑 10 的範圍設計，AreaStrike 會依實際半徑等比例縮放整組特效。
public static class AreaStrikeExplosionBuilder
{
    private const int NoiseTextureSize = 128;
    private const int DotTextureSize = 64;

    // 火球與火花的 HDR 亮度倍數 (粒子頂點色上限 1，亮度放在材質上才會觸發 Bloom)；
    // 火球亮度不能太高，否則在亮色地板上會被洗成白色看不出火的顏色
    private const float FireBrightness = 1.6f;
    private const float SparkBrightness = 4f;
    // 整組特效離地高度，避免煙霧貼圖切進地板出現一條硬邊
    private const float HeightAboveGround = 2f;
    // Soft Particles：粒子在離地面這個距離內逐漸淡出，消除貼圖與地板交界的硬邊 (需 URP 開啟 Depth Texture)；
    // 火球貼近地面，淡出距離太大會整團變透明，所以火球用較短的距離
    private const float SmokeSoftFadeDistance = 4f;
    private const float FireSoftFadeDistance = 1f;

    public static ParticleSystem Build(Transform parent, string folder)
    {
        // 煙霧用高對比雜訊 (一團團輪廓)，火球用較飽滿的雜訊 (整團火光不破碎)
        Texture2D puffTexture = GetOrCreatePuffTexture(folder + "/Explosion_Puff_Tex.asset", 0.3f, 0.75f);
        Texture2D fireTexture = GetOrCreatePuffTexture(folder + "/Explosion_Fire_Tex.asset", 0f, 0.55f);
        Texture2D dotTexture = GetOrCreateDotTexture(folder + "/Explosion_Dot_Tex.asset");

        // 火球用 Alpha 混合：加算混合疊在亮色地板上會變白，Alpha 混合才保得住橘紅色
        Material fireMaterial = GetOrCreateParticleMaterial(folder + "/Explosion_Fire.mat", fireTexture, FireBrightness, false, 10);
        EnableSoftParticles(fireMaterial, FireSoftFadeDistance);
        Material sparkMaterial = GetOrCreateParticleMaterial(folder + "/Explosion_Spark.mat", dotTexture, SparkBrightness, true, 11);
        Material smokeMaterial = GetOrCreateParticleMaterial(folder + "/Explosion_Smoke.mat", puffTexture, 1f, false, 5);
        EnableSoftParticles(smokeMaterial, SmokeSoftFadeDistance);

        ParticleSystem fire = CreateSystem(parent, "Explosion");
        fire.transform.localPosition = new Vector3(0f, HeightAboveGround, 0f);
        SetupFire(fire, fireMaterial);
        SetupSparks(CreateSystem(fire.transform, "Sparks"), sparkMaterial);
        SetupSmoke(CreateSystem(fire.transform, "Smoke"), smokeMaterial);
        return fire;
    }

    // 火球：瞬間爆開後快速減速，顏色白黃 → 橘 → 暗紅並淡出
    private static void SetupFire(ParticleSystem system, Material material)
    {
        ParticleSystem.MainModule main = SetupMain(system, 0f, new Vector2(0.4f, 0.8f), new Vector2(12f, 28f), new Vector2(7f, 14f), -0.3f, 80);
        SetBurst(system, 45);
        SetHemisphereShape(system, 4f);
        SetDamping(system, 5f, 0.25f);
        SetSizeCurve(system, 0.6f, 1.4f);
        SetColorGradient(system,
            new[]
            {
                new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0f),
                new GradientColorKey(new Color(1f, 0.5f, 0.08f), 0.2f),
                new GradientColorKey(new Color(0.75f, 0.15f, 0.03f), 0.55f),
                new GradientColorKey(new Color(0.15f, 0.05f, 0.03f), 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.95f, 0.45f), new GradientAlphaKey(0f, 1f) });
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        SetupRenderer(system, material, ParticleSystemRenderMode.Billboard);
    }

    // 火花：高速往外噴濺並受重力落下，沿速度方向拉長
    private static void SetupSparks(ParticleSystem system, Material material)
    {
        SetupMain(system, 0f, new Vector2(0.4f, 0.9f), new Vector2(20f, 40f), new Vector2(0.3f, 0.6f), 2f, 60);
        SetBurst(system, 40);
        SetHemisphereShape(system, 2f);
        SetColorGradient(system,
            new[] { new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.45f, 0.1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });

        ParticleSystemRenderer renderer = SetupRenderer(system, material, ParticleSystemRenderMode.Stretch);
        renderer.velocityScale = 0.08f;
        renderer.lengthScale = 1f;
    }

    // 煙霧：比火光稍晚出現，緩慢上升、旋轉、擴散後淡出
    private static void SetupSmoke(ParticleSystem system, Material material)
    {
        ParticleSystem.MainModule main = SetupMain(system, 0.1f, new Vector2(1.8f, 2.8f), new Vector2(4f, 10f), new Vector2(10f, 16f), -0.15f, 50);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        SetBurst(system, 24);
        SetHemisphereShape(system, 6f);
        SetDamping(system, 2.5f, 0.2f);
        SetSizeCurve(system, 0.6f, 1.6f);
        SetColorGradient(system,
            new[] { new GradientColorKey(new Color(0.42f, 0.37f, 0.34f), 0f), new GradientColorKey(new Color(0.62f, 0.62f, 0.64f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.15f), new GradientAlphaKey(0.5f, 0.6f), new GradientAlphaKey(0f, 1f) });

        ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-30f * Mathf.Deg2Rad, 30f * Mathf.Deg2Rad);

        SetupRenderer(system, material, ParticleSystemRenderMode.Billboard);
    }

    private static ParticleSystem CreateSystem(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return system;
    }

    private static ParticleSystem.MainModule SetupMain(ParticleSystem system, float delay, Vector2 lifetime, Vector2 speed, Vector2 size, float gravity, int maxParticles)
    {
        ParticleSystem.MainModule main = system.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startDelay = delay;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.gravityModifier = gravity;
        main.maxParticles = maxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        // 跟著父物件縮放，AreaStrike 依半徑調整 Explosion 的 localScale 即可放大整組特效
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        return main;
    }

    private static void SetBurst(ParticleSystem system, short count)
    {
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
    }

    // 半球朝上發射 (半球預設朝 +Z，轉 -90 度讓它朝 +Y)
    private static void SetHemisphereShape(ParticleSystem system, float radius)
    {
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = radius;
        shape.rotation = new Vector3(-90f, 0f, 0f);
    }

    // 速度上限 + 阻尼：爆開瞬間很快、之後迅速慢下來，看起來比較有爆炸的衝擊感
    private static void SetDamping(ParticleSystem system, float limit, float dampen)
    {
        ParticleSystem.LimitVelocityOverLifetimeModule limitVelocity = system.limitVelocityOverLifetime;
        limitVelocity.enabled = true;
        limitVelocity.limit = limit;
        limitVelocity.dampen = dampen;
    }

    private static void SetSizeCurve(ParticleSystem system, float start, float end)
    {
        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, start, 1f, end));
    }

    private static void SetColorGradient(ParticleSystem system, GradientColorKey[] colorKeys, GradientAlphaKey[] alphaKeys)
    {
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(colorKeys, alphaKeys);
        colorOverLifetime.color = gradient;
    }

    private static ParticleSystemRenderer SetupRenderer(ParticleSystem system, Material material, ParticleSystemRenderMode mode)
    {
        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = mode;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    // URP Particles/Unlit 材質；透過腳本設定時不會經過 ShaderGUI，所以混合狀態要手動寫齊
    private static Material GetOrCreateParticleMaterial(string path, Texture2D texture, float brightness, bool additive, int queueOffset)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", new Color(brightness, brightness, brightness, 1f));
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", additive ? 2f : 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        // 煙霧先畫，火光與火花疊在上面
        material.renderQueue = (int)RenderQueue.Transparent + queueOffset;
        EditorUtility.SetDirty(material);
        return material;
    }

    // URP 粒子材質的 Soft Particles 參數；透過腳本設定時 ShaderGUI 不會幫忙換算 _SoftParticleFadeParams，所以手動寫入
    private static void EnableSoftParticles(Material material, float fadeDistance)
    {
        material.SetFloat("_SoftParticlesEnabled", 1f);
        material.SetFloat("_SoftParticlesNearFadeDistance", 0f);
        material.SetFloat("_SoftParticlesFarFadeDistance", fadeDistance);
        material.SetVector("_SoftParticleFadeParams", new Vector4(0f, 1f / fadeDistance, 0f, 0f));
        material.EnableKeyword("_SOFTPARTICLES_ON");
        EditorUtility.SetDirty(material);
    }

    // 雲團貼圖：多層 Perlin 雜訊乘上圓形衰減；densityMin/Max 決定雜訊對比 (區間越窄、輪廓越分明)
    private static Texture2D GetOrCreatePuffTexture(string path, float densityMin, float densityMax)
    {
        Texture2D texture = GetOrCreateTextureAsset(path, NoiseTextureSize, out bool isNew);
        float half = (NoiseTextureSize - 1) * 0.5f;
        for (int y = 0; y < NoiseTextureSize; y++)
        {
            for (int x = 0; x < NoiseTextureSize; x++)
            {
                float distance = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                float falloff = Mathf.Pow(Mathf.Clamp01(1f - distance), 1.5f);
                float noise = FractalNoise(x / (float)NoiseTextureSize, y / (float)NoiseTextureSize);
                float shade = Mathf.Lerp(0.55f, 1f, noise);
                float density = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(densityMin, densityMax, noise));
                texture.SetPixel(x, y, new Color(shade, shade, shade, falloff * density));
            }
        }
        return SaveTexture(texture, path, isNew);
    }

    private static Texture2D GetOrCreateDotTexture(string path)
    {
        Texture2D texture = GetOrCreateTextureAsset(path, DotTextureSize, out bool isNew);
        float half = (DotTextureSize - 1) * 0.5f;
        for (int y = 0; y < DotTextureSize; y++)
        {
            for (int x = 0; x < DotTextureSize; x++)
            {
                float distance = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - distance), 2f)));
            }
        }
        return SaveTexture(texture, path, isNew);
    }

    private static float FractalNoise(float u, float v)
    {
        float sum = 0f;
        float amplitude = 0.5f;
        float frequency = 4f;
        for (int octave = 0; octave < 4; octave++)
        {
            sum += amplitude * Mathf.PerlinNoise(u * frequency + 13.7f * octave, v * frequency + 7.3f * octave);
            amplitude *= 0.5f;
            frequency *= 2f;
        }
        return Mathf.Clamp01(sum / 0.9375f);
    }

    private static Texture2D GetOrCreateTextureAsset(string path, int size, out bool isNew)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        isNew = texture == null;
        if (isNew) texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
        texture.wrapMode = TextureWrapMode.Clamp;
        return texture;
    }

    private static Texture2D SaveTexture(Texture2D texture, string path, bool isNew)
    {
        texture.Apply();
        if (isNew) AssetDatabase.CreateAsset(texture, path);
        else EditorUtility.SetDirty(texture);
        return texture;
    }
}
#endif
