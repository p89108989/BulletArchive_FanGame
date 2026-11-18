#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 一鍵建立直升機飛彈 prefab：紅/藍/黃三種 RMB_16 外觀 (依攻擊屬性切換) + 尾焰 + 煙霧尾跡。
// 外觀統一縮放成長度 1、彈頭朝 +Z，實際長度由 StrikeMissile 發射時縮放根物件決定。
// 尾焰/煙霧沿用 AreaStrike 爆炸特效的材質，請先執行 Build Area Strike。
public static class StrikeMissileBuilder
{
    private const string PrefabPath = "Assets/STG game/resources/prefab/StrikeMissile.prefab";
    private const string MissileFolder = "Assets/STG game/resources/shooting item/BTM_Rockets_Missiles_Bombs/Prefabs/";
    private const string MissileName = "/RMB_16.prefab";
    private const string SmokeMaterialPath = "Assets/STG game/resources/prefab/AreaStrike/Explosion_Smoke.mat";
    private const string FlameMaterialPath = "Assets/STG game/resources/prefab/AreaStrike/Explosion_Spark.mat";

    [MenuItem("Tools/STG/Build Strike Missile")]
    private static void Build()
    {
        Material smokeMaterial = AssetDatabase.LoadAssetAtPath<Material>(SmokeMaterialPath);
        Material flameMaterial = AssetDatabase.LoadAssetAtPath<Material>(FlameMaterialPath);
        if (smokeMaterial == null || flameMaterial == null)
        {
            Debug.LogError("[StrikeMissileBuilder] 找不到爆炸特效材質，請先執行 Tools/STG/Build Area Strike。");
            return;
        }

        GameObject root = new GameObject("StrikeMissile");
        try
        {
            GameObject red = CreateVisual(root.transform, "Red");
            GameObject blue = CreateVisual(root.transform, "Blue");
            GameObject yellow = CreateVisual(root.transform, "Yellow");
            ParticleSystem trail = CreateTrail(root.transform, smokeMaterial, flameMaterial);

            StrikeMissile missile = root.AddComponent<StrikeMissile>();
            SerializedObject serialized = new SerializedObject(missile);
            SerializedProperty visuals = serialized.FindProperty("visuals");
            visuals.arraySize = 3;
            WriteVisual(visuals.GetArrayElementAtIndex(0), BulletPolarity.None, red);
            WriteVisual(visuals.GetArrayElementAtIndex(1), BulletPolarity.Blue, blue);
            WriteVisual(visuals.GetArrayElementAtIndex(2), BulletPolarity.Yellow, yellow);
            serialized.FindProperty("trailParticles").objectReferenceValue = trail;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        Debug.Log("[StrikeMissileBuilder] 已更新 " + PrefabPath);
    }

    // RMB_16 原始模型彈頭朝 +Y、長約 13.6：轉成彈頭朝 +Z、縮放成長度 1、中心對齊原點
    private static GameObject CreateVisual(Transform parent, string color)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(MissileFolder + color + MissileName);
        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
        visual.name = "Visual_" + color;

        Bounds meshBounds = visual.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
        float scale = 1f / meshBounds.size.y;
        visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        visual.transform.localScale = Vector3.one * scale;
        visual.transform.localPosition = -(visual.transform.localRotation * meshBounds.center) * scale;

        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = ShadowCastingMode.Off;
        return visual;
    }

    private static void WriteVisual(SerializedProperty element, BulletPolarity polarity, GameObject visual)
    {
        element.FindPropertyRelative("polarity").enumValueIndex = (int)polarity;
        element.FindPropertyRelative("visual").objectReferenceValue = visual;
    }

    // 煙霧尾跡 (世界空間，依飛行距離噴出) + 跟著彈尾的尾焰 (自身空間)；大小以飛彈長度 1 為基準
    private static ParticleSystem CreateTrail(Transform parent, Material smokeMaterial, Material flameMaterial)
    {
        GameObject smokeObject = new GameObject("Trail");
        smokeObject.transform.SetParent(parent, false);
        smokeObject.transform.localPosition = new Vector3(0f, 0f, -0.5f);
        ParticleSystem smoke = smokeObject.AddComponent<ParticleSystem>();
        smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule smokeMain = SetupMain(smoke, new Vector2(0.4f, 0.7f), 0.4f, ParticleSystemSimulationSpace.World, 120);
        smokeMain.startColor = new Color(0.75f, 0.75f, 0.75f, 0.6f);
        ParticleSystem.EmissionModule smokeEmission = smoke.emission;
        smokeEmission.rateOverTime = 0f;
        smokeEmission.rateOverDistance = 1.5f;
        ParticleSystem.SizeOverLifetimeModule smokeSize = smoke.sizeOverLifetime;
        smokeSize.enabled = true;
        smokeSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 2.5f));
        ParticleSystem.ColorOverLifetimeModule smokeColor = smoke.colorOverLifetime;
        smokeColor.enabled = true;
        smokeColor.color = FadeOut();
        SetupRenderer(smoke, smokeMaterial);

        GameObject flameObject = new GameObject("Flame");
        flameObject.transform.SetParent(smokeObject.transform, false);
        ParticleSystem flame = flameObject.AddComponent<ParticleSystem>();
        flame.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule flameMain = SetupMain(flame, new Vector2(0.06f, 0.1f), 0.35f, ParticleSystemSimulationSpace.Local, 20);
        flameMain.startColor = new Color(1f, 0.6f, 0.2f, 1f);
        ParticleSystem.EmissionModule flameEmission = flame.emission;
        flameEmission.rateOverTime = 40f;
        SetupRenderer(flame, flameMaterial);

        return smoke;
    }

    private static ParticleSystem.MainModule SetupMain(ParticleSystem system, Vector2 lifetime, float size, ParticleSystemSimulationSpace space, int maxParticles)
    {
        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
        main.startSpeed = 0f;
        main.startSize = size;
        main.simulationSpace = space;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = maxParticles;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;
        return main;
    }

    private static Gradient FadeOut()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        return gradient;
    }

    private static void SetupRenderer(ParticleSystem system, Material material)
    {
        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }
}
#endif
