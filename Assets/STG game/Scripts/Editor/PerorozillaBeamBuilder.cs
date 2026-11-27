#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 建立 Boss 雷射的外觀 Prefab (Boss_Beam)，由 PerorozillaPrefabBuilder 一起呼叫：
// 卡通風格 (STG/ToonBeam：亮芯 / 主色 / 邊緣三段純色 + 描邊，與開始畫面相同)：一根藍色光管 + 蓋住整顆眼球的卡通光球 +
// 發射點光團粒子 + 落點地面波紋 (平貼地面往外擴散的光環) 與火花
public static class PerorozillaBeamBuilder
{
    private const string OutputFolder = "Assets/STG game/resources/prefab";
    private const string BeamFolder = OutputFolder + "/BossBeam";
    private const string BeamPrefabPath = OutputFolder + "/Boss_Beam.prefab";
    private const string WarningPrefabPath = OutputFolder + "/Boss_LaserWarning.prefab";
    // 紅圈警示沿用範圍攻擊 (AreaStrike) 的紅色外圈與填充材質，外觀與其他紅色攻擊一致
    private const string WarningRingMaterialPath = OutputFolder + "/AreaStrike/AreaStrike_Ring.mat";
    private const string WarningFillMaterialPath = OutputFolder + "/AreaStrike/AreaStrike_Fill.mat";
    private const string TubeMeshPath = BeamFolder + "/BossBeam_Tube.asset";
    private const string DotTexturePath = OutputFolder + "/AreaStrike/Explosion_Dot_Tex.asset";
    private const string RingTexturePath = OutputFolder + "/AreaStrike/AreaStrike_Ring_Tex.asset";

    // 圓管網格解析度：沿光束方向要夠密，卡通 shader 的波動鼓起才會平滑
    private const int TubeSegmentsAround = 24;
    private const int TubeSegmentsAlong = 200;

    // 光管粗細 (BossBeam_Tube 網格直徑 1，BossBeamVisual 依強度縮放)；不透明的卡通管看起來比原本半透明的光暈粗，所以比例調小
    private const float BeamWidth = 9f;
    private const float BeamWidthScale = 0.7f;
    // 發射期間蓋住整顆眼球的卡通光球 (直徑)；光管起點往後退一半，從球裡面射出來
    private const float EyeFlareDiameter = 24f;
    // 發射點光團、地面波紋、火花粒子的藍白色
    private static readonly Color EffectColor = new Color(0.55f, 0.85f, 1f, 1f);

    public static BossBeamVisual BuildBeam()
    {
        if (!AssetDatabase.IsValidFolder(BeamFolder)) AssetDatabase.CreateFolder(OutputFolder, "BossBeam");

        Mesh tube = GetOrCreateTubeMesh();
        Material beam = ToonBeamMaterials.GetOrCreate("Battle_Laser", ToonBeamMaterials.Laser, ToonBeamMaterials.AxisZ, wave: ToonBeamMaterials.BattleLaserWave);
        Material eyeBall = ToonBeamMaterials.GetOrCreate("Battle_LaserBall", ToonBeamMaterials.Laser, ToonBeamMaterials.NoAxis);
        Material dot = GetOrCreateParticleMaterial("BossBeam_Dot", AssetDatabase.LoadAssetAtPath<Texture2D>(DotTexturePath));
        Material ring = GetOrCreateParticleMaterial("BossBeam_Ring", AssetDatabase.LoadAssetAtPath<Texture2D>(RingTexturePath));

        GameObject root = new GameObject("Boss_Beam");
        try
        {
            Renderer beamTube = CreateTube(root.transform, "Beam", tube, beam);

            // Muzzle 由 BossBeamVisual 依強度縮放，球跟著一起長大/收起
            Transform muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root.transform, false);
            ParticleSystem muzzleGlow = CreateMuzzleGlow(muzzle, dot);
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "EyeBall";
            Object.DestroyImmediate(ball.GetComponent<Collider>());
            ball.transform.SetParent(muzzle, false);
            ball.transform.localScale = Vector3.one * EyeFlareDiameter;
            Renderer ballRenderer = ball.GetComponent<Renderer>();
            ballRenderer.sharedMaterial = eyeBall;
            ballRenderer.shadowCastingMode = ShadowCastingMode.Off;
            ballRenderer.receiveShadows = false;

            Transform impact = new GameObject("Impact").transform;
            impact.SetParent(root.transform, false);
            ParticleSystem ripples = CreateGroundRipples(impact, ring);
            CreateImpactSparks(ripples.transform, dot);

            BossBeamVisual visual = root.AddComponent<BossBeamVisual>();
            SerializedObject serialized = new SerializedObject(visual);
            SerializedProperty tubes = serialized.FindProperty("beamTubes");
            tubes.arraySize = 1;
            tubes.GetArrayElementAtIndex(0).objectReferenceValue = beamTube;
            SerializedProperty scales = serialized.FindProperty("tubeWidthScales");
            scales.arraySize = 1;
            scales.GetArrayElementAtIndex(0).floatValue = BeamWidthScale;
            serialized.FindProperty("width").floatValue = BeamWidth;
            serialized.FindProperty("startBackOffset").floatValue = EyeFlareDiameter * 0.5f;
            serialized.FindProperty("muzzle").objectReferenceValue = muzzle;
            serialized.FindProperty("impact").objectReferenceValue = impact;
            serialized.FindProperty("muzzleParticles").objectReferenceValue = muzzleGlow;
            serialized.FindProperty("impactParticles").objectReferenceValue = ripples;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(root, BeamPrefabPath).GetComponent<BossBeamVisual>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // 雷射的地面紅圈警示 (可移動、由呼叫端控制倒數)；需先跑過 Build Area Strike 產生紅圈材質
    public static GroundWarningCircle BuildWarning()
    {
        Material ringMaterial = AssetDatabase.LoadAssetAtPath<Material>(WarningRingMaterialPath);
        Material fillMaterial = AssetDatabase.LoadAssetAtPath<Material>(WarningFillMaterialPath);
        if (ringMaterial == null || fillMaterial == null)
        {
            Debug.LogError("[PerorozillaBeamBuilder] 找不到紅圈材質，請先執行 Tools/STG/Build Area Strike");
            return null;
        }

        GameObject root = new GameObject("Boss_LaserWarning");
        try
        {
            Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            Transform ring = CreateFlatQuad(root.transform, "Ring", quad, ringMaterial, 0.02f);
            Transform fill = CreateFlatQuad(root.transform, "Fill", quad, fillMaterial, 0.01f);

            GroundWarningCircle warning = root.AddComponent<GroundWarningCircle>();
            SerializedObject serialized = new SerializedObject(warning);
            serialized.FindProperty("ring").objectReferenceValue = ring;
            serialized.FindProperty("fill").objectReferenceValue = fill;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(root, WarningPrefabPath).GetComponent<GroundWarningCircle>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static Transform CreateFlatQuad(Transform parent, string name, Mesh mesh, Material material, float height)
    {
        GameObject quad = new GameObject(name);
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = new Vector3(0f, height, 0f);
        // Quad 預設面向 -Z，繞 X 轉 90 度後朝上平貼地面
        quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        quad.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = quad.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return quad.transform;
    }

    private static Renderer CreateTube(Transform parent, string name, Mesh mesh, Material material)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    // 發射點：眼睛前一團持續閃動的光
    private static ParticleSystem CreateMuzzleGlow(Transform parent, Material material)
    {
        ParticleSystem system = CreateSystem(parent, "MuzzleGlow", material);
        ParticleSystem.MainModule main = system.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(14f, 22f);
        main.startColor = EffectColor;
        main.maxParticles = 30;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 40f;
        SetFade(system);
        return system;
    }

    // 落點：平貼地面的光環一圈圈往外擴散 (地面上的波紋)，跟光束的波動節奏呼應
    private static ParticleSystem CreateGroundRipples(Transform parent, Material material)
    {
        ParticleSystem system = CreateSystem(parent, "GroundRipples", material);
        system.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        ParticleSystem.MainModule main = system.main;
        main.startLifetime = 0.7f;
        main.startSpeed = 0f;
        main.startSize = 6f;
        main.startColor = EffectColor;
        main.maxParticles = 20;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 6f;
        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 5f));
        SetFade(system);
        system.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        return system;
    }

    // 落點：往上噴濺的火花
    private static void CreateImpactSparks(Transform parent, Material material)
    {
        ParticleSystem system = CreateSystem(parent, "Sparks", material);
        ParticleSystem.MainModule main = system.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(15f, 30f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        main.startColor = EffectColor;
        main.gravityModifier = 2f;
        main.maxParticles = 80;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 60f;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 2f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        SetFade(system);
        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.06f;
    }

    private static ParticleSystem CreateSystem(Transform parent, string name, Material material)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return system;
    }

    private static void SetFade(ParticleSystem system)
    {
        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
    }

    // 開口圓管：半徑 0.5、沿 Z 從 0 到 1；三角形朝外 (卡通 shader 正面畫色塊、背面畫描邊)
    private static Mesh GetOrCreateTubeMesh()
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(TubeMeshPath);
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh { name = "BossBeam_Tube" };
        mesh.Clear();

        int columns = TubeSegmentsAround + 1;
        int rows = TubeSegmentsAlong + 1;
        Vector3[] vertices = new Vector3[columns * rows];
        Vector3[] normals = new Vector3[vertices.Length];
        Vector2[] uvs = new Vector2[vertices.Length];
        for (int row = 0; row < rows; row++)
        {
            float along = (float)row / TubeSegmentsAlong;
            for (int column = 0; column < columns; column++)
            {
                float around = (float)column / TubeSegmentsAround;
                float angle = around * Mathf.PI * 2f;
                Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                int index = row * columns + column;
                vertices[index] = radial * 0.5f + new Vector3(0f, 0f, along);
                normals[index] = radial;
                uvs[index] = new Vector2(around, along);
            }
        }

        int[] triangles = new int[TubeSegmentsAround * TubeSegmentsAlong * 6];
        int t = 0;
        for (int row = 0; row < TubeSegmentsAlong; row++)
        {
            for (int column = 0; column < TubeSegmentsAround; column++)
            {
                int a = row * columns + column;
                int b = a + columns;
                triangles[t++] = a; triangles[t++] = a + 1; triangles[t++] = b;
                triangles[t++] = a + 1; triangles[t++] = b + 1; triangles[t++] = b;
            }
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        if (isNew) AssetDatabase.CreateAsset(mesh, TubeMeshPath);
        else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    // URP Particles/Unlit 加算混合；透過腳本設定時不會經過 ShaderGUI，所以混合狀態要手動寫齊
    private static Material GetOrCreateParticleMaterial(string name, Texture2D texture)
    {
        string path = BeamFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", new Color(2f, 2f, 2f, 1f));
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 2f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.One);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent + 25;
        EditorUtility.SetDirty(material);
        return material;
    }

}
#endif
