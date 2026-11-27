#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 產生範圍攻擊的爆炸粒子 (卡通風格)：火球 (分段色階的實心圓團) + 火花 (拉長亮點) + 煙霧 (帶描邊的白灰雲團)。
// 卡通感來自硬邊圓團貼圖 + 明暗兩階 + 分段變色，並以「先膨脹再縮小消失」取代半透明淡出。
// 煙霧/火球貼圖也被飛彈尾煙、戰車落地煙塵共用，改這裡會一起變成卡通風格。
// 所有數值以半徑 10 的範圍設計，AreaStrike 會依實際半徑等比例縮放整組特效。
public static class AreaStrikeExplosionBuilder
{
    private const int NoiseTextureSize = 128;
    private const int DotTextureSize = 64;

    // 火球與火花的 HDR 亮度倍數 (粒子頂點色上限 1，亮度放在材質上才會觸發 Bloom)；
    // 火球亮度不能太高，否則在亮色地板上會被洗成白色看不出火的顏色
    private const float FireBrightness = 1.25f;
    private const float SparkBrightness = 4f;
    // 整組特效離地高度，避免煙霧貼圖切進地板出現一條硬邊
    private const float HeightAboveGround = 2f;
    // Soft Particles：粒子在離地面這個距離內逐漸淡出，消除貼圖與地板交界的硬邊 (需 URP 開啟 Depth Texture)；
    // 火球貼近地面，淡出距離太大會整團變透明，所以火球用較短的距離
    private const float SmokeSoftFadeDistance = 1.5f;
    private const float FireSoftFadeDistance = 0.5f;

    // 落地煙塵的米色 (與戰車落地煙塵同色系)
    private static readonly Color DustColor = new Color(0.9f, 0.82f, 0.68f);

    // 卡通圓團貼圖：描邊佔半徑比例、暗面亮度、描邊亮度
    private const float ToonOutlineWidth = 0.09f;
    private const float ToonShadowShade = 0.68f;
    private const float ToonOutlineShade = 0.28f;

    public static ParticleSystem Build(Transform parent, string folder)
    {
        // 煙霧帶深色描邊 (漫畫雲團)，火球不描邊，靠分段色階表現
        Texture2D puffTexture = GetOrCreateToonPuffTexture(folder + "/Explosion_Puff_Tex.asset", true);
        Texture2D fireTexture = GetOrCreateToonPuffTexture(folder + "/Explosion_Fire_Tex.asset", false);
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

    // 只有煙塵、沒有火光的落地衝擊 (例如 Boss 吐出的碎石落地)：米色卡通雲團貼地往外滾開 + 少量往上翻起
    public static ParticleSystem BuildDust(Transform parent, string folder)
    {
        Texture2D puffTexture = GetOrCreateToonPuffTexture(folder + "/Explosion_Puff_Tex.asset", true);
        Material smokeMaterial = GetOrCreateParticleMaterial(folder + "/Explosion_Smoke.mat", puffTexture, 1f, false, 5);
        EnableSoftParticles(smokeMaterial, SmokeSoftFadeDistance);

        ParticleSystem ring = CreateSystem(parent, "Explosion");
        ring.transform.localPosition = new Vector3(0f, HeightAboveGround, 0f);
        ParticleSystem.MainModule ringMain = SetupMain(ring, 0f, new Vector2(1f, 1.6f), new Vector2(18f, 30f), new Vector2(5f, 9f), 0f, 40);
        ringMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        SetBurst(ring, 16);
        ParticleSystem.ShapeModule ringShape = ring.shape;
        ringShape.enabled = true;
        ringShape.shapeType = ParticleSystemShapeType.Circle;
        ringShape.radius = 3f;
        ringShape.radiusThickness = 0f;
        // Circle 預設在 XY 平面，轉成貼地的 XZ 平面往外滾開
        ringShape.rotation = new Vector3(90f, 0f, 0f);
        SetDamping(ring, 3f, 0.3f);
        SetPopSizeCurve(ring, 0.5f, 1.5f, 0.3f);
        SetColorGradient(ring,
            new[] { new GradientColorKey(DustColor, 0f), new GradientColorKey(DustColor * 1.08f, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        SetupRenderer(ring, smokeMaterial, ParticleSystemRenderMode.Billboard);

        ParticleSystem plume = CreateSystem(ring.transform, "Plume");
        ParticleSystem.MainModule plumeMain = SetupMain(plume, 0.05f, new Vector2(1.2f, 1.8f), new Vector2(4f, 9f), new Vector2(6f, 10f), -0.1f, 20);
        plumeMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        SetBurst(plume, 8);
        SetHemisphereShape(plume, 4f);
        SetDamping(plume, 2.5f, 0.2f);
        SetPopSizeCurve(plume, 0.4f, 1.3f, 0.35f);
        SetColorGradient(plume,
            new[] { new GradientColorKey(DustColor * 0.92f, 0f), new GradientColorKey(DustColor, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        SetupRenderer(plume, smokeMaterial, ParticleSystemRenderMode.Billboard);
        return ring;
    }

    // 火球：瞬間爆開後快速減速，顏色分段跳變 亮黃 → 橘 → 紅 → 深灰 (燒成黑煙)，最後縮小消失
    private static void SetupFire(ParticleSystem system, Material material)
    {
        ParticleSystem.MainModule main = SetupMain(system, 0f, new Vector2(0.5f, 0.9f), new Vector2(12f, 26f), new Vector2(7f, 13f), -0.4f, 80);
        SetBurst(system, 40);
        SetHemisphereShape(system, 4f);
        SetDamping(system, 5f, 0.25f);
        SetPopSizeCurve(system, 0.5f, 1.3f, 0.3f);
        SetColorGradient(system,
            new[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.45f), 0.18f),
                new GradientColorKey(new Color(1f, 0.6f, 0.08f), 0.4f),
                new GradientColorKey(new Color(0.95f, 0.22f, 0.05f), 0.65f),
                new GradientColorKey(new Color(0.25f, 0.2f, 0.22f), 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) },
            GradientMode.Fixed);
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

    // 煙霧：比火光稍晚出現，白灰色雲團緩慢上升、旋轉、膨脹後縮小消失
    private static void SetupSmoke(ParticleSystem system, Material material)
    {
        ParticleSystem.MainModule main = SetupMain(system, 0.12f, new Vector2(1.4f, 2.2f), new Vector2(4f, 10f), new Vector2(9f, 14f), -0.2f, 50);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        SetBurst(system, 15);
        SetHemisphereShape(system, 6f);
        SetDamping(system, 2.5f, 0.2f);
        SetPopSizeCurve(system, 0.4f, 1.4f, 0.35f);
        SetColorGradient(system,
            new[] { new GradientColorKey(new Color(0.55f, 0.52f, 0.55f), 0f), new GradientColorKey(new Color(0.95f, 0.95f, 0.97f), 0.35f), new GradientColorKey(new Color(0.85f, 0.85f, 0.9f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });

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

    // 卡通式消失：先膨脹到 peak (在 peakTime 時)，再縮小到 0，取代半透明淡出
    private static void SetPopSizeCurve(ParticleSystem system, float start, float peak, float peakTime)
    {
        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, start, 0f, 0f),
            new Keyframe(peakTime, peak, 0f, 0f),
            new Keyframe(1f, 0f, -1.5f, 0f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);
    }

    // Fixed 模式的漸層會在色鍵之間直接跳色，做出卡通的分段色階
    private static void SetColorGradient(ParticleSystem system, GradientColorKey[] colorKeys, GradientAlphaKey[] alphaKeys, GradientMode mode = GradientMode.Blend)
    {
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.mode = mode;
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
    internal static Material GetOrCreateParticleMaterial(string path, Texture2D texture, float brightness, bool additive, int queueOffset)
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
    internal static void EnableSoftParticles(Material material, float fadeDistance)
    {
        material.SetFloat("_SoftParticlesEnabled", 1f);
        material.SetFloat("_SoftParticlesNearFadeDistance", 0f);
        material.SetFloat("_SoftParticlesFarFadeDistance", fadeDistance);
        material.SetVector("_SoftParticleFadeParams", new Vector4(0f, 1f / fadeDistance, 0f, 0f));
        material.EnableKeyword("_SOFTPARTICLES_ON");
        EditorUtility.SetDirty(material);
    }

    // 卡通圓團貼圖：外緣略帶起伏的硬邊圓 + 左上亮右下暗的兩階明暗 (+ 可選的深色描邊)。
    // 只在邊緣留 1~2 像素的反鋸齒，避免放大後鋸齒明顯
    private static Texture2D GetOrCreateToonPuffTexture(string path, bool outline)
    {
        Texture2D texture = GetOrCreateTextureAsset(path, NoiseTextureSize, out bool isNew);
        float half = (NoiseTextureSize - 1) * 0.5f;
        float pixel = 1f / half;
        for (int y = 0; y < NoiseTextureSize; y++)
        {
            for (int x = 0; x < NoiseTextureSize; x++)
            {
                float u = (x - half) / half;
                float v = (y - half) / half;
                float angle = Mathf.Atan2(v, u);
                // 外緣做成幾個圓弧鼓起，看起來像一團雲而不是正圓
                float edge = 0.9f + 0.06f * Mathf.Sin(angle * 5f) + 0.03f * Mathf.Sin(angle * 9f + 1.3f);
                float distance = Mathf.Sqrt(u * u + v * v) / edge;
                float alpha = Mathf.Clamp01((1f - distance) / (pixel * 1.5f));

                // 往左上偏移的圓之外就是暗面 (月牙形陰影)
                float lit = Mathf.Sqrt((u + 0.22f) * (u + 0.22f) + (v - 0.22f) * (v - 0.22f)) / edge;
                float shade = Mathf.Lerp(1f, ToonShadowShade, Mathf.Clamp01((lit - 0.95f) / (pixel * 2f)));
                if (outline) shade = Mathf.Lerp(shade, ToonOutlineShade, Mathf.Clamp01((distance - (1f - ToonOutlineWidth)) / (pixel * 1.5f)));

                texture.SetPixel(x, y, new Color(shade, shade, shade, alpha));
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
