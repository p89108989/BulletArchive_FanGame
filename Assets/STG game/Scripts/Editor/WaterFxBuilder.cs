#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 建立 Binah 用的水花與細緻煙霧特效 (Tools/STG/Build Water FX)，全部用內建 Particle System + 自己產生的柔邊雲團貼圖：
// FX_WaterSplash — Binah 從海中竄出時的水花：往上噴的水柱 + 往外濺的水冠 + 擴散的水霧 + 水面上擴散的泡沫圈。
// FX_FineSmoke   — Binah 退場時的細緻水霧 (取代 Perorozilla 的卡通煙塵)。
// 大小以 4 倍大的 Binah (竄出的身體約 40 m 寬、水柱要高過頭部) 為準；Prefab 的 localScale 可以整組放大縮小
public static class WaterFxBuilder
{
    private const string Folder = "Assets/STG game/resources/prefab/WaterFX";
    public const string SplashPrefabPath = Folder + "/FX_WaterSplash.prefab";
    // Binah 光炮機制開頭的「潑沙」：同樣的水花結構，換成沙色
    public const string SandSplashPrefabPath = Folder + "/FX_SandSplash.prefab";
    public const string FineSmokePrefabPath = Folder + "/FX_FineSmoke.prefab";
    private const string PuffTexturePath = Folder + "/FX_SoftPuff.asset";
    private const string DotTexturePath = Folder + "/FX_SoftDot.asset";
    private const int DotTextureSize = 64;
    private const string MistMaterialPath = Folder + "/FX_WaterMist.mat";
    private const string DropletMaterialPath = Folder + "/FX_WaterDroplet.mat";
    private const int PuffTextureSize = 256;
    private const int PuffSeed = 1;

    private static readonly Color DropletColor = new Color(0.92f, 0.97f, 1f, 1f);
    private static readonly Color MistColor = new Color(0.95f, 0.98f, 1f, 1f);
    private static readonly Color FoamColor = new Color(0.85f, 0.95f, 1f, 1f);
    private static readonly Color SandDropletColor = new Color(0.9f, 0.76f, 0.52f, 1f);
    private static readonly Color SandMistColor = new Color(0.86f, 0.74f, 0.55f, 1f);
    private static readonly Color SandFoamColor = new Color(0.8f, 0.66f, 0.44f, 1f);
    private static readonly Color FineSmokeColor = new Color(0.88f, 0.9f, 0.93f, 1f);

    // 整組特效存在多久後自動移除 (要比最長的粒子壽命長)
    private const float SplashLifetime = 5f;

    [MenuItem("Tools/STG/Build Water FX")]
    public static void Build()
    {
        BuildAll();
        Debug.Log("[WaterFxBuilder] 已建立 " + SplashPrefabPath + " 與 " + FineSmokePrefabPath);
    }

    public static void BuildAll()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/STG game/resources/prefab", "WaterFX");

        Texture2D puff = GetOrCreateTexture(PuffTexturePath, PuffTextureSize, SoftPuffTexture.Generate(PuffTextureSize, PuffSeed));
        Material mist = AreaStrikeExplosionBuilder.GetOrCreateParticleMaterial(MistMaterialPath, puff, 1f, false, 0);
        // 水霧貼到地面/模型時柔化交界，不會出現一條硬邊
        AreaStrikeExplosionBuilder.EnableSoftParticles(mist, 4f);
        Material droplet = AreaStrikeExplosionBuilder.GetOrCreateParticleMaterial(DropletMaterialPath, GetOrCreateTexture(DotTexturePath, DotTextureSize, SoftPuffTexture.GenerateDot(DotTextureSize)), 1f, false, 1);
        AssetDatabase.SaveAssets();

        BuildSplash(SplashPrefabPath, "FX_WaterSplash", mist, droplet, DropletColor, MistColor, FoamColor);
        BuildSplash(SandSplashPrefabPath, "FX_SandSplash", mist, droplet, SandDropletColor, SandMistColor, SandFoamColor);
        BuildFineSmoke(mist);
    }

    private static void BuildSplash(string path, string name, Material mist, Material droplet, Color dropletColor, Color mistColor, Color foamColor)
    {
        GameObject root = new GameObject(name);
        try
        {
            SetupLifetimeRoot(root);

            // 水柱：往上噴的水滴，拉長成水線，落回時淡出
            ParticleSystem column = CreateSystem(root.transform, "Column");
            SetupMain(column, 0f, new Vector2(1.6f, 2.4f), new Vector2(55f, 90f), new Vector2(3f, 6f), 5f, 200);
            SetBurst(column, 140);
            SetConeShape(column, 10f, 10f);
            SetFade(column, dropletColor, 1f, 0.75f);
            SetStretched(column, droplet, 0.12f);

            // 水冠：往外斜濺的一圈水花
            ParticleSystem crown = CreateSystem(root.transform, "Crown");
            SetupMain(crown, 0.05f, new Vector2(1.2f, 1.8f), new Vector2(30f, 50f), new Vector2(2f, 4f), 5f, 150);
            SetBurst(crown, 90);
            SetConeShape(crown, 55f, 14f);
            SetFade(crown, dropletColor, 1f, 0.7f);
            SetStretched(crown, droplet, 0.1f);

            // 水霧：水花散開後留下的白霧，慢慢膨脹、往上飄、淡出
            ParticleSystem spray = CreateSystem(root.transform, "Mist");
            SetupMain(spray, 0.1f, new Vector2(2.5f, 4f), new Vector2(8f, 20f), new Vector2(25f, 45f), -0.05f, 60);
            SetBurst(spray, 32);
            SetConeShape(spray, 50f, 14f);
            SetRandomRotation(spray);
            SetGrow(spray, 0.6f, 1.5f);
            SetFade(spray, mistColor, 0.45f, 0.15f);
            SetDamping(spray, 5f, 0.3f);
            SetBillboard(spray, mist);

            // 泡沫圈：貼在水面 (水平) 往外擴散的白色泡沫
            ParticleSystem foam = CreateSystem(root.transform, "Foam");
            SetupMain(foam, 0f, new Vector2(2f, 3f), Vector2.zero, new Vector2(40f, 60f), 0f, 12);
            SetBurst(foam, 8);
            SetCircleShape(foam, 8f);
            SetRandomRotation(foam);
            SetGrow(foam, 0.5f, 1.6f);
            SetFade(foam, foamColor, 0.7f, 0.15f);
            ParticleSystemRenderer foamRenderer = SetBillboard(foam, mist);
            foamRenderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // Binah 退場用：從水面冒出的柔和白霧 (BossDeathPerformance 會在下沉時陸續生成數團)
    private static void BuildFineSmoke(Material mist)
    {
        GameObject root = new GameObject("FX_FineSmoke");
        try
        {
            ParticleSystem smoke = root.AddComponent<ParticleSystem>();
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            SetupMain(smoke, 0f, new Vector2(3f, 4.5f), new Vector2(2f, 6f), new Vector2(10f, 20f), -0.08f, 40);
            ParticleSystem.MainModule main = smoke.main;
            main.playOnAwake = true;
            SetBurst(smoke, 14);
            SetConeShape(smoke, 40f, 6f);
            SetRandomRotation(smoke);
            SetGrow(smoke, 0.5f, 1.6f);
            SetFade(smoke, FineSmokeColor, 0.6f, 0.25f);
            SetDamping(smoke, 2f, 0.2f);
            SetBillboard(smoke, mist);

            PrefabUtility.SaveAsPrefabAsset(root, FineSmokePrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // 根物件也是一個不發射的粒子系統：時間到就把整組 (含子系統) 刪掉，生成的人不用另外處理回收
    private static void SetupLifetimeRoot(GameObject root)
    {
        ParticleSystem system = root.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = system.main;
        main.duration = SplashLifetime;
        main.loop = false;
        main.playOnAwake = true;
        main.stopAction = ParticleSystemStopAction.Destroy;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        system.GetComponent<ParticleSystemRenderer>().enabled = false;
    }

    private static ParticleSystem CreateSystem(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return system;
    }

    private static void SetupMain(ParticleSystem system, float delay, Vector2 lifetime, Vector2 speed, Vector2 size, float gravity, int maxParticles)
    {
        ParticleSystem.MainModule main = system.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = true;
        main.startDelay = delay;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.gravityModifier = gravity;
        main.maxParticles = maxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
    }

    private static void SetBurst(ParticleSystem system, short count)
    {
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
    }

    // 圓錐朝上 (預設朝 +Z，轉 -90 度朝 +Y)
    private static void SetConeShape(ParticleSystem system, float angle, float radius)
    {
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = angle;
        shape.radius = radius;
        shape.rotation = new Vector3(-90f, 0f, 0f);
    }

    // 水平的圓 (泡沫圈在水面上散開)
    private static void SetCircleShape(ParticleSystem system, float radius)
    {
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.rotation = new Vector3(90f, 0f, 0f);
    }

    private static void SetRandomRotation(ParticleSystem system)
    {
        ParticleSystem.MainModule main = system.main;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
    }

    // 慢慢膨脹 (start → end 倍)
    private static void SetGrow(ParticleSystem system, float start, float end)
    {
        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, start, 1f, end));
    }

    // 一出現就是 alpha 濃度，從 fadeStart (壽命比例) 開始淡到全透明
    private static void SetFade(ParticleSystem system, Color color, float alpha, float fadeStart)
    {
        ParticleSystem.MainModule main = system.main;
        main.startColor = color;
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, 0.05f), new GradientAlphaKey(alpha, fadeStart), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;
    }

    private static void SetDamping(ParticleSystem system, float limit, float dampen)
    {
        ParticleSystem.LimitVelocityOverLifetimeModule limitVelocity = system.limitVelocityOverLifetime;
        limitVelocity.enabled = true;
        limitVelocity.limit = limit;
        limitVelocity.dampen = dampen;
    }

    private static ParticleSystemRenderer SetBillboard(ParticleSystem system, Material material)
    {
        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    // 水滴依速度拉長成水線 (速度越快越長)
    private static void SetStretched(ParticleSystem system, Material material, float velocityScale)
    {
        ParticleSystemRenderer renderer = SetBillboard(system, material);
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = velocityScale;
        renderer.lengthScale = 1f;
    }

    private static Texture2D GetOrCreateTexture(string path, int size, Color32[] pixels)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        bool isNew = texture == null;
        if (isNew) texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels32(pixels);
        texture.Apply();
        if (isNew) AssetDatabase.CreateAsset(texture, path);
        else EditorUtility.SetDirty(texture);
        return texture;
    }
}
#endif
