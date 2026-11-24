#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 一鍵建立：
// 1. 玩家雷射外觀 prefab：VolumetricLines 光劍光束 + 槍口光暈 + 命中火花 (顏色由 PlayerLaser 依極性設定)
// 2. 敵人爆炸 prefab：複製 AreaStrike 的爆炸粒子並改成生成即播放，供砲台等敵人被擊毀時使用
// 3. 玩家一般射擊的光束子彈 prefab：一條 LineRenderer，用光暈材質拉長成一小段光 (顏色由 PlayerLaser 依極性設定)
// 粒子材質沿用 AreaStrike 與極性子彈建置工具產生的材質，請先執行那兩個工具。
public static class PlayerLaserBuilder
{
    private const string BeamPrefabPath = "Assets/STG game/resources/prefab/PlayerLaserBeam.prefab";
    private const string ExplosionPrefabPath = "Assets/STG game/resources/prefab/EnemyExplosion.prefab";
    private const string AreaStrikePrefabPath = "Assets/STG game/resources/prefab/AreaStrike.prefab";
    private const string LineSourcePath = "Assets/STG game/resources/model/shooting item/VolumetricLines/BuiltInRenderPipeline/Prefabs/SingleLine-LightSaber.prefab";
    private const string SparkMaterialPath = "Assets/STG game/resources/prefab/AreaStrike/Explosion_Spark.mat";
    private const string GlowMaterialPath = "Assets/STG game/resources/prefab/BulletPolarity/Bullet_Glow.mat";
    private const string ShotPrefabPath = "Assets/STG game/resources/prefab/PlayerShot.prefab";
    private const string ShotMaterialPath = "Assets/STG game/resources/prefab/PlayerShot.mat";
    private const float ShotWidth = 1.6f;
    // 場地地板很亮，子彈要夠亮 (HDR，觸發 Bloom) 才看得清楚
    private static readonly Color ShotBrightness = new Color(4f, 4f, 4f, 1f);

    [MenuItem("Tools/STG/Build Player Laser")]
    private static void Build()
    {
        Material sparkMaterial = AssetDatabase.LoadAssetAtPath<Material>(SparkMaterialPath);
        Material glowMaterial = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterialPath);
        if (sparkMaterial == null || glowMaterial == null)
        {
            Debug.LogError("[PlayerLaserBuilder] 找不到粒子材質，請先執行 Build Area Strike 與 Build Polarity Bullet。");
            return;
        }

        BuildBeam(sparkMaterial, glowMaterial);
        BuildExplosion();
        BuildShot(glowMaterial);
        AssetDatabase.SaveAssets();
        Debug.Log("[PlayerLaserBuilder] 已更新 " + BeamPrefabPath + "、" + ExplosionPrefabPath + " 與 " + ShotPrefabPath);
    }

    // 光暈貼圖沿線段拉長 (Stretch)：中間亮、頭尾與兩側淡出，看起來就是一小段光束
    private static void BuildShot(Material glowMaterial)
    {
        GameObject root = new GameObject("PlayerShot");
        try
        {
            LineRenderer line = root.AddComponent<LineRenderer>();
            line.sharedMaterial = GetOrCreateShotMaterial(glowMaterial);
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = ShotWidth;
            line.numCapVertices = 4;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            PrefabUtility.SaveAsPrefabAsset(root, ShotPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // 複製光暈材質再調亮，不影響共用同一材質的敵方子彈
    private static Material GetOrCreateShotMaterial(Material glowMaterial)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ShotMaterialPath);
        if (material == null)
        {
            material = new Material(glowMaterial);
            AssetDatabase.CreateAsset(material, ShotMaterialPath);
        }
        else
        {
            material.CopyPropertiesFromMaterial(glowMaterial);
        }

        material.SetColor("_BaseColor", ShotBrightness);
        // 加算混合：藍色子彈疊在淺藍地板上用一般混合幾乎看不到，加算後會發亮浮出來。
        // 透過腳本設定不會經過 ShaderGUI，混合狀態要手動寫齊
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 2f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.One);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void BuildBeam(Material sparkMaterial, Material glowMaterial)
    {
        GameObject root = new GameObject("PlayerLaserBeam");
        try
        {
            GameObject line = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LineSourcePath), root.transform);
            line.name = "Line";

            // 槍口光暈：起點一團持續的光
            ParticleSystem muzzle = CreateSystem(root.transform, "Muzzle", glowMaterial, ParticleSystemRenderMode.Billboard);
            SetupMain(muzzle, new Vector2(0.06f, 0.1f), Vector2.zero, new Vector2(3f, 4f), 0f, 20);
            SetRate(muzzle, 40f);

            // 命中點：往射手方向噴濺的火花 + 一團光暈
            ParticleSystem impact = CreateSystem(root.transform, "Impact", sparkMaterial, ParticleSystemRenderMode.Stretch);
            SetupMain(impact, new Vector2(0.15f, 0.35f), new Vector2(10f, 25f), new Vector2(0.3f, 0.7f), 1f, 80);
            SetRate(impact, 60f);
            ParticleSystem.ShapeModule shape = impact.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.5f;
            ParticleSystemRenderer impactRenderer = impact.GetComponent<ParticleSystemRenderer>();
            impactRenderer.velocityScale = 0.05f;
            impactRenderer.lengthScale = 1f;

            ParticleSystem impactGlow = CreateSystem(impact.transform, "ImpactGlow", glowMaterial, ParticleSystemRenderMode.Billboard);
            SetupMain(impactGlow, new Vector2(0.06f, 0.1f), Vector2.zero, new Vector2(4.5f, 6f), 0f, 20);
            SetRate(impactGlow, 40f);

            LaserBeamVisual visual = root.AddComponent<LaserBeamVisual>();
            SerializedObject serialized = new SerializedObject(visual);
            serialized.FindProperty("line").objectReferenceValue = line.GetComponent<VolumetricLines.VolumetricLineBehavior>();
            serialized.FindProperty("muzzleParticles").objectReferenceValue = muzzle;
            serialized.FindProperty("impactParticles").objectReferenceValue = impact;
            SerializedProperty tinted = serialized.FindProperty("tintedParticles");
            tinted.arraySize = 3;
            tinted.GetArrayElementAtIndex(0).objectReferenceValue = muzzle;
            tinted.GetArrayElementAtIndex(1).objectReferenceValue = impact;
            tinted.GetArrayElementAtIndex(2).objectReferenceValue = impactGlow;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, BeamPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // 從 AreaStrike prefab 複製一份爆炸粒子，改成生成時自動播放
    private static void BuildExplosion()
    {
        GameObject areaStrike = PrefabUtility.LoadPrefabContents(AreaStrikePrefabPath);
        try
        {
            Transform source = areaStrike.transform.Find("Explosion");
            GameObject explosion = Object.Instantiate(source.gameObject);
            explosion.name = "EnemyExplosion";
            explosion.transform.localPosition = Vector3.zero;
            explosion.transform.localScale = Vector3.one;
            foreach (ParticleSystem system in explosion.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = system.main;
                main.playOnAwake = true;
            }

            PrefabUtility.SaveAsPrefabAsset(explosion, ExplosionPrefabPath);
            Object.DestroyImmediate(explosion);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(areaStrike);
        }
    }

    private static ParticleSystem CreateSystem(Transform parent, string name, Material material, ParticleSystemRenderMode renderMode)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = renderMode;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return system;
    }

    private static void SetupMain(ParticleSystem system, Vector2 lifetime, Vector2 speed, Vector2 size, float gravity, int maxParticles)
    {
        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.gravityModifier = gravity;
        main.maxParticles = maxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = fade;
    }

    private static void SetRate(ParticleSystem system, float rate)
    {
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = rate;
    }
}
#endif
