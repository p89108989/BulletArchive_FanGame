#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Binah 一般攻擊的外觀 (由 BinahPrefabBuilder 呼叫)：
// 能量球 (普通攻擊吐出的投擲物)、長條光炮的警示與光炮本體、光炮打過後地面殘留的火焰
public static class BinahAttackFxBuilder
{
    private const string Folder = "Assets/STG game/resources/prefab/BinahFX";
    private const string EnergyBallPrefabPath = Folder + "/Binah_EnergyBall.prefab";
    private const string FireZonePrefabPath = Folder + "/Binah_FireZone.prefab";
    private const string EnergyBallGlowMaterialPath = Folder + "/Binah_EnergyBallGlow.mat";
    private const string LaneWarningMaterialPath = Folder + "/Binah_LaneWarning.mat";
    private const string LaneFillMaterialPath = Folder + "/Binah_LaneWarningFill.mat";
    private const string LaneBeamMaterialPath = Folder + "/Binah_LaneBeam.mat";
    private const string FireGlowMaterialPath = Folder + "/Binah_FireGlow.mat";
    private const string FireFlameMaterialPath = Folder + "/Binah_FireFlame.mat";
    private const string DotTexturePath = "Assets/STG game/resources/prefab/WaterFX/FX_SoftDot.asset";
    private const string PuffTexturePath = "Assets/STG game/resources/prefab/WaterFX/FX_SoftPuff.asset";

    // 能量球：直徑約 7 m 的發光球 + 外圈光暈 (與光炮蓄力的能量球同色系)
    private const float EnergyBallSize = 9.45f;
    private static readonly Color EnergyBallHaloColor = new Color(1f, 0.55f, 0.2f, 1f);

    // 長條警示：淡紅底 + 從 Binah 這端漸漸塗滿的紅色
    private static readonly Color LaneWarningColor = new Color(1f, 0.1f, 0.1f, 0.15f);
    private static readonly Color LaneFillColor = new Color(1f, 0.08f, 0.08f, 0.4f);
    // 長條光炮：卡通圓柱 (半徑倍數相對光炮粗細；不透明的卡通管看起來比原本半透明的火焰粗，比原本最外層略小)
    internal const float LaneBeamToonScale = 1.2f;
    // 外圍黑色雷電的圓管比光柱粗多少 (雷電纏在光柱外面)；材質數值 (條數、閃動速度…) 可直接在材質上調
    internal const float LaneLightningScale = 1.6f;
    private const string LaneLightningMaterialPath = ToonBeamMaterials.Folder + "/Battle_CannonDarkLightning.mat";
    // 火焰三層材質 (帶黑的暗紅火焰 → 橘黃火焰 → 近白核心)：能量球與蓄力能量球使用
    private const string LaneBeamDarkMaterialPath = Folder + "/Binah_LaneBeamDark.mat";
    private const string LaneBeamFlameMaterialPath = Folder + "/Binah_LaneBeamFlame.mat";
    private static readonly Color LaneBeamDarkColor = new Color(0.05f, 0.005f, 0.005f, 0.95f);
    private static readonly Color LaneBeamFlameColor = new Color(1f, 0.45f, 0.1f, 1f);
    private static readonly Color LaneBeamCoreColor = new Color(1f, 0.92f, 0.7f, 1f);
    // 光球 (能量球、蓄力能量球) 也用同樣的三層：黑紅外殼 → 橘色火焰 → 近白核心 (相對光球直徑)
    private const float OrbDarkScale = 1.3f;
    private const float OrbFlameScale = 1f;
    private const float OrbCoreScale = 0.55f;
    private static readonly Vector2 OrbTiling = new Vector2(2f, 2f);
    // 地面火焰底下的焦黑痕跡
    private const string ScorchMaterialPath = Folder + "/Binah_Scorch.mat";
    private static readonly Color ScorchColor = new Color(0.08f, 0.03f, 0.02f, 0.6f);

    // 地面火焰：沿長條冒出的火焰粒子 + 貼地的橘色光帶
    private static readonly Color FireGlowColor = new Color(1f, 0.4f, 0.08f, 0.55f);
    private static readonly Color FireFlameColor = new Color(1f, 0.55f, 0.15f, 1f);
    private const float FireRate = 260f;

    public static GameObject BuildEnergyBall()
    {
        GameObject root = new GameObject("Binah_EnergyBall");
        try
        {
            CreateFireOrbLayers(root.transform, EnergyBallSize);

            Material glow = AreaStrikeExplosionBuilder.GetOrCreateParticleMaterial(EnergyBallGlowMaterialPath, AssetDatabase.LoadAssetAtPath<Texture2D>(DotTexturePath), 1f, true, 2);
            ParticleSystem halo = BinahCannonFxBuilder.CreateLoopingSystem(root.transform, "Halo", glow);
            ParticleSystem.MainModule main = halo.main;
            main.startLifetime = 0.4f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(EnergyBallSize * 2f, EnergyBallSize * 2.8f);
            main.startColor = EnergyBallHaloColor;
            ParticleSystem.EmissionModule emission = halo.emission;
            emission.rateOverTime = 20f;
            ParticleSystem.ShapeModule shape = halo.shape;
            shape.enabled = false;
            BinahCannonFxBuilder.FadeInOut(halo, 0.8f);

            ArcProjectile projectile = root.AddComponent<ArcProjectile>();
            SerializedObject serialized = new SerializedObject(projectile);
            // 球體翻滾看不出來，不轉
            serialized.FindProperty("spinSpeed").floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(root, EnergyBallPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // 長條警示：根物件放在起點、朝長條方向並依 (寬, 1, 長) 縮放；子物件以大小 1 製作。回傳 (根物件, 塗滿的部分)
    public static WarningFill CreateLaneWarning(Transform root, out Transform laneWarning)
    {
        GameObject warning = new GameObject("LaneWarning");
        warning.transform.SetParent(root, false);
        laneWarning = warning.transform;

        CreateUnitQuad(warning.transform, "Base", 0.02f, BinahCannonFxBuilder.GetOrCreateColorMaterial(LaneWarningMaterialPath, LaneWarningColor, false));

        GameObject pivot = new GameObject("FillPivot");
        pivot.transform.SetParent(warning.transform, false);
        pivot.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        Material fill = AreaStrikeExplosionBuilder.GetOrCreateParticleMaterial(LaneFillMaterialPath, null, 1f, false, 1);
        fill.SetColor("_BaseColor", LaneFillColor);
        EditorUtility.SetDirty(fill);
        CreateUnitQuad(pivot.transform, "Fill", 0f, fill);
        WarningFill warningFill = pivot.AddComponent<WarningFill>();
        warningFill.SetProgress(0f);

        warning.SetActive(false);
        return warningFill;
    }

    // 長條光炮本體 (Y 軸為長度)，根物件的位置與長度由 BinahLaneBeamAttack 每幀設定
    public static GameObject CreateLaneBeam(Transform root)
    {
        GameObject beam = new GameObject("LaneBeam");
        beam.transform.SetParent(root, false);
        AddToonLaneBeam(beam.transform);
        beam.SetActive(false);
        return beam;
    }

    // 卡通風格的光炮 (參考蔚藍檔案 Binah 的吐息)：中間一根實心的卡通光柱 (STG/ToonBeam 橘黃配色，不波動)，
    // 外面套一層稍粗的透明圓管畫黑色雷電環繞 (STG/BeamDarkLightning)；重做既有 prefab 時也呼叫這裡
    public static void AddToonLaneBeam(Transform beam)
    {
        Material core = ToonBeamMaterials.GetOrCreate("Battle_Cannon", ToonBeamMaterials.Fire, ToonBeamMaterials.AxisY);
        AddCylinderLayer(beam, "Beam", LaneBeamToonScale, core);
        AddCylinderLayer(beam, "DarkLightning", LaneBeamToonScale * LaneLightningScale, GetOrCreateLightningMaterial());
    }

    private static void AddCylinderLayer(Transform parent, string name, float width, Material material)
    {
        GameObject layer = new GameObject(name);
        layer.transform.SetParent(parent, false);
        layer.transform.localScale = new Vector3(width, 1f, width);
        layer.AddComponent<MeshFilter>().sharedMesh = ToonBeamMeshes.GetOrCreateCylinder();
        MeshRenderer renderer = layer.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    internal static Material GetOrCreateLightningMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(LaneLightningMaterialPath);
        if (material != null) return material;

        material = new Material(Shader.Find("STG/BeamDarkLightning"));
        AssetDatabase.CreateAsset(material, LaneLightningMaterialPath);
        return material;
    }

    // 三層火焰光球 (黑紅外殼 → 橘色火焰 → 近白核心)，直徑 size；能量球與光炮蓄力的能量球共用
    internal static void CreateFireOrbLayers(Transform parent, float size)
    {
        CreateLayer(parent, PrimitiveType.Sphere, "Dark", Vector3.one * (size * OrbDarkScale), DarkFlameMaterial(), OrbTiling, new Vector2(0.3f, -0.8f));
        CreateLayer(parent, PrimitiveType.Sphere, "Flame", Vector3.one * (size * OrbFlameScale), FlameMaterial(), OrbTiling, new Vector2(-0.5f, -1.2f));
        CreateLayer(parent, PrimitiveType.Sphere, "Core", Vector3.one * (size * OrbCoreScale), HotCoreMaterial(), OrbTiling, Vector2.zero);
    }

    // 外層：偏黑的暗紅火焰 (半透明，畫在最前面)
    private static Material DarkFlameMaterial()
    {
        Material dark = AreaStrikeExplosionBuilder.GetOrCreateParticleMaterial(LaneBeamDarkMaterialPath, AssetDatabase.LoadAssetAtPath<Texture2D>(PuffTexturePath), 1f, false, 0);
        dark.SetColor("_BaseColor", LaneBeamDarkColor);
        EditorUtility.SetDirty(dark);
        return dark;
    }

    // 中層：橘黃火焰 (加亮)
    private static Material FlameMaterial()
    {
        Material flame = AreaStrikeExplosionBuilder.GetOrCreateParticleMaterial(LaneBeamFlameMaterialPath, AssetDatabase.LoadAssetAtPath<Texture2D>(PuffTexturePath), 1f, true, 1);
        flame.SetColor("_BaseColor", LaneBeamFlameColor);
        EditorUtility.SetDirty(flame);
        return flame;
    }

    // 中心：近白的高熱核心 (加亮，最後畫)
    private static Material HotCoreMaterial()
    {
        Material core = BinahCannonFxBuilder.GetOrCreateColorMaterial(LaneBeamMaterialPath, LaneBeamCoreColor, true);
        core.renderQueue = (int)RenderQueue.Transparent + 2;
        return core;
    }

    // 一層圓柱或球；speed 不為零時貼圖持續捲動 (火焰流動)
    private static void CreateLayer(Transform parent, PrimitiveType shape, string name, Vector3 scale, Material material, Vector2 tiling, Vector2 speed)
    {
        GameObject layer = GameObject.CreatePrimitive(shape);
        layer.name = name;
        Object.DestroyImmediate(layer.GetComponent<Collider>());
        layer.transform.SetParent(parent, false);
        layer.transform.localScale = scale;
        Renderer renderer = layer.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        if (speed == Vector2.zero) return;

        ScrollTexture scroll = layer.AddComponent<ScrollTexture>();
        SerializedObject serialized = new SerializedObject(scroll);
        serialized.FindProperty("tiling").vector2Value = tiling;
        serialized.FindProperty("speed").vector2Value = speed;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // 地面火焰 Prefab：Visual 依長條的 (寬, 1, 長) 縮放；火焰粒子只縮放發射範圍 (粒子大小不變)
    public static GroundFireZone BuildFireZone()
    {
        GameObject root = new GameObject("Binah_FireZone");
        try
        {
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            // 焦黑痕跡在下、橘色光帶疊在上 (長條中心在原點，所以兩片都放回中心)
            CreateUnitQuad(visual.transform, "Scorch", 0.03f, BinahCannonFxBuilder.GetOrCreateColorMaterial(ScorchMaterialPath, ScorchColor, false));
            visual.transform.GetChild(0).localPosition = new Vector3(0f, 0.03f, 0f);
            CreateUnitQuad(visual.transform, "Glow", 0.05f, BinahCannonFxBuilder.GetOrCreateColorMaterial(FireGlowMaterialPath, FireGlowColor, true));
            visual.transform.GetChild(1).localPosition = new Vector3(0f, 0.05f, 0f);

            Material flameMaterial = AreaStrikeExplosionBuilder.GetOrCreateParticleMaterial(FireFlameMaterialPath, AssetDatabase.LoadAssetAtPath<Texture2D>(PuffTexturePath), 1f, true, 2);
            ParticleSystem flames = BinahCannonFxBuilder.CreateLoopingSystem(visual.transform, "Flames", flameMaterial);
            ParticleSystem.MainModule main = flames.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startColor = FireFlameColor;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 600;
            main.prewarm = false;
            ParticleSystem.EmissionModule emission = flames.emission;
            emission.rateOverTime = FireRate;
            ParticleSystem.ShapeModule shape = flames.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            // 朝上噴 (Box 預設往 +Z 發射，繞 X 轉 -90 度往 +Y)；轉完後本地 Y 變成長條方向、本地 Z 變成高度，所以薄的是 Z
            shape.scale = new Vector3(1f, 1f, 0.01f);
            shape.rotation = new Vector3(-90f, 0f, 0f);
            ParticleSystem.SizeOverLifetimeModule size = flames.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.2f));
            BinahCannonFxBuilder.FadeInOut(flames, 0.85f);

            GroundFireZone zone = root.AddComponent<GroundFireZone>();
            SerializedObject serialized = new SerializedObject(zone);
            serialized.FindProperty("visual").objectReferenceValue = visual.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(false);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, FireZonePrefabPath);
            return prefab.GetComponent<GroundFireZone>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // 平放在地上、大小 1 的面 (中心在本地 Z = 0.5，起點在 Z = 0)
    private static void CreateUnitQuad(Transform parent, string name, float height, Material material)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = new Vector3(0f, height, 0.5f);
        quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Renderer renderer = quad.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }
}
#endif
