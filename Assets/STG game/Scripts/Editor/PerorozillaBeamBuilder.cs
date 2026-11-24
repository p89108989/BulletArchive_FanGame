#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 建立 Boss 雷射的外觀 Prefab (Boss_Beam)，由 PerorozillaPrefabBuilder 一起呼叫：
// 兩條互相纏繞的扭轉光帶 (BossBeamRibbon，像拓樸學的紙帶) + 細亮芯與淡藍光暈 (BossBeamWave，一波波往前鼓起) +
// 覆蓋整顆眼球的白色激光球 + 發射點光團粒子 + 落點地面波紋 (平貼地面往外擴散的光環) 與火花；整體為藍白配色
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
    private const string NoiseTexturePath = "Assets/STG game/resources/model/Boss_Perorozilla/bundles/Assets/_MX/NPCs/Perorozilla_Original/Perorozilla_Original/Effect/Texture/FX_TEX_Noise_02.png";
    private const string DotTexturePath = OutputFolder + "/AreaStrike/Explosion_Dot_Tex.asset";
    private const string RingTexturePath = OutputFolder + "/AreaStrike/AreaStrike_Ring_Tex.asset";

    // 圓管網格解析度：沿光束方向要夠密，波形鼓起才會平滑
    private const int TubeSegmentsAround = 24;
    private const int TubeSegmentsAlong = 200;

    private const string RibbonMeshPath = BeamFolder + "/BossBeam_Ribbon.asset";
    private const int RibbonSegmentsAlong = 240;

    // 藍白配色：帶子中央接近白色、邊緣藍色；亮芯白中帶藍；外層淡藍光暈
    private static readonly Color RibbonCenterColor = new Color(2.6f, 2.9f, 3.4f, 1f);
    private static readonly Color RibbonEdgeColor = new Color(0.25f, 1f, 3.6f, 1f);
    private static readonly Color GlowColor = new Color(0.3f, 0.9f, 2.6f, 0.45f);
    private static readonly Color GlowCrestColor = new Color(0.8f, 1.6f, 3.2f, 0.6f);
    private static readonly Color CoreColor = new Color(2.8f, 3.1f, 3.6f, 1f);
    private static readonly Color CoreCrestColor = new Color(4f, 4.3f, 4.8f, 1f);
    private static readonly Color EffectColor = new Color(0.55f, 0.85f, 1f, 1f);
    // 發射期間覆蓋整顆眼球的白色激光球
    // 光束起點在眼球處鼓成球形光團 (直徑約等於眼球)，光束看起來是從整顆眼睛噴出來，而不是另外貼一顆白球
    private const float BeamWidth = 9f;
    private const float EyeFlareDiameter = 24f;
    private static readonly Color CoreFlareColor = new Color(0.2f, 0.5f, 1.3f, 0.4f);
    // 眼球層：加算光疊在白色眼球上只會更白、也蓋不住瞳孔，所以另用一層一般混合的藍白光團 (只在起點光團出現) 把眼睛蓋掉
    private static readonly Color EyeEdgeColor = new Color(0.08f, 0.45f, 1.5f, 0f);
    private static readonly Color EyeCenterColor = new Color(0.6f, 1.05f, 1.9f, 0.97f);
    private static readonly Color GlowFlareColor = new Color(0.05f, 0.3f, 1.1f, 0.3f);

    public static BossBeamVisual BuildBeam()
    {
        if (!AssetDatabase.IsValidFolder(BeamFolder)) AssetDatabase.CreateFolder(OutputFolder, "BossBeam");

        Mesh tube = GetOrCreateTubeMesh();
        Mesh ribbon = GetOrCreateRibbonMesh();
        Texture2D noise = LoadRepeatTexture(NoiseTexturePath);
        // 兩條扭轉光帶錯開 90 度，互相纏繞成帶狀光束；中間一條細亮芯，外面一層淡藍光暈
        Material ribbonA = GetOrCreateRibbonMaterial("BossBeam_RibbonA", noise, 0f);
        Material ribbonB = GetOrCreateRibbonMaterial("BossBeam_RibbonB", noise, 90f);
        Material glow = GetOrCreateWaveMaterial("BossBeam_Glow", noise, GlowColor, GlowCrestColor, 0.35f, 14f, 50f, 2.5f, 1.2f);
        Material core = GetOrCreateWaveMaterial("BossBeam_Core", noise, CoreColor, CoreCrestColor, 0.15f, 14f, 50f, 5f, 2.5f);
        Material dot = GetOrCreateParticleMaterial("BossBeam_Dot", AssetDatabase.LoadAssetAtPath<Texture2D>(DotTexturePath));
        Material ring = GetOrCreateParticleMaterial("BossBeam_Ring", AssetDatabase.LoadAssetAtPath<Texture2D>(RingTexturePath));
        Material eye = GetOrCreateWaveMaterial("BossBeam_Eye", noise, EyeEdgeColor, EyeEdgeColor, 0f, 14f, 50f, 1f, 1f);
        eye.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        eye.renderQueue = (int)RenderQueue.Transparent + 19;
        float[] layerWidths = { 1.15f, 1f, 1f, 0.32f, 1f };
        SetFlare(eye, layerWidths[4], EyeCenterColor);
        SetFlare(glow, layerWidths[0], GlowFlareColor);
        SetFlare(ribbonA, layerWidths[1], Color.white);
        SetFlare(ribbonB, layerWidths[2], Color.white);
        SetFlare(core, layerWidths[3], CoreFlareColor);

        GameObject root = new GameObject("Boss_Beam");
        try
        {
            Renderer glowTube = CreateTube(root.transform, "Glow", tube, glow);
            Renderer ribbonATube = CreateTube(root.transform, "RibbonA", ribbon, ribbonA);
            Renderer ribbonBTube = CreateTube(root.transform, "RibbonB", ribbon, ribbonB);
            Renderer coreTube = CreateTube(root.transform, "Core", tube, core);
            Renderer eyeTube = CreateTube(root.transform, "EyeFlare", tube, eye);

            Transform muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root.transform, false);
            ParticleSystem muzzleGlow = CreateMuzzleGlow(muzzle, dot);

            Transform impact = new GameObject("Impact").transform;
            impact.SetParent(root.transform, false);
            ParticleSystem ripples = CreateGroundRipples(impact, ring);
            CreateImpactSparks(ripples.transform, dot);

            BossBeamVisual visual = root.AddComponent<BossBeamVisual>();
            SerializedObject serialized = new SerializedObject(visual);
            SerializedProperty tubes = serialized.FindProperty("beamTubes");
            Renderer[] layers = { glowTube, ribbonATube, ribbonBTube, coreTube, eyeTube };
            tubes.arraySize = layers.Length;
            SerializedProperty scales = serialized.FindProperty("tubeWidthScales");
            scales.arraySize = layers.Length;
            for (int i = 0; i < layers.Length; i++)
            {
                tubes.GetArrayElementAtIndex(i).objectReferenceValue = layers[i];
                scales.GetArrayElementAtIndex(i).floatValue = layerWidths[i];
            }
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

    // 光團寬度以「管寬的倍數」設定，所以每層依自己的寬度換算，讓各層光團都約等於眼球大小
    private static void SetFlare(Material material, float layerWidthScale, Color flareColor)
    {
        material.SetFloat("_FlareScale", Mathf.Max(1f, EyeFlareDiameter / (BeamWidth * layerWidthScale)));
        material.SetFloat("_FlareLength", EyeFlareDiameter);
        if (material.HasProperty("_FlareColor")) material.SetColor("_FlareColor", flareColor);
        EditorUtility.SetDirty(material);
    }

    private static Mesh GetOrCreateRibbonMesh()
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(RibbonMeshPath);
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh { name = "BossBeam_Ribbon" };
        mesh.Clear();

        int rows = RibbonSegmentsAlong + 1;
        Vector3[] vertices = new Vector3[rows * 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        for (int row = 0; row < rows; row++)
        {
            float along = (float)row / RibbonSegmentsAlong;
            vertices[row * 2] = new Vector3(-0.5f, 0f, along);
            vertices[row * 2 + 1] = new Vector3(0.5f, 0f, along);
            uvs[row * 2] = new Vector2(0f, along);
            uvs[row * 2 + 1] = new Vector2(1f, along);
        }

        int[] triangles = new int[RibbonSegmentsAlong * 6];
        for (int row = 0, t = 0; row < RibbonSegmentsAlong; row++)
        {
            int a = row * 2;
            triangles[t++] = a; triangles[t++] = a + 2; triangles[t++] = a + 1;
            triangles[t++] = a + 1; triangles[t++] = a + 2; triangles[t++] = a + 3;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        // 扭轉後帶子會轉到任何方向，外框涵蓋整個圓柱範圍
        mesh.bounds = new Bounds(new Vector3(0f, 0f, 0.5f), new Vector3(2f, 2f, 1f));

        if (isNew) AssetDatabase.CreateAsset(mesh, RibbonMeshPath);
        else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static Material GetOrCreateRibbonMaterial(string name, Texture2D noise, float phaseOffset)
    {
        string path = BeamFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("STG/BossBeamRibbon"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_CenterColor", RibbonCenterColor);
        material.SetColor("_EdgeColor", RibbonEdgeColor);
        material.SetTexture("_NoiseTex", noise);
        material.SetFloat("_PhaseOffset", phaseOffset);
        EditorUtility.SetDirty(material);
        return material;
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

    // 開口圓管：半徑 0.5、沿 Z 從 0 到 1；uv.x 繞一圈、uv.y 沿光束 (shader 依 uv.y 計算波形)
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
                triangles[t++] = a; triangles[t++] = b; triangles[t++] = a + 1;
                triangles[t++] = a + 1; triangles[t++] = b; triangles[t++] = b + 1;
            }
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        // 波形會讓頂點往外鼓，外框放大一點避免被提早裁掉
        mesh.bounds = new Bounds(new Vector3(0f, 0f, 0.5f), new Vector3(2f, 2f, 1f));

        if (isNew) AssetDatabase.CreateAsset(mesh, TubeMeshPath);
        else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static Material GetOrCreateWaveMaterial(string name, Texture2D noise, Color color, Color crest, float amplitude, float waveLength, float waveSpeed, float scrollSpeed, float edgePower)
    {
        string path = BeamFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("STG/BossBeamWave"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_Color", color);
        material.SetColor("_CrestColor", crest);
        material.SetTexture("_NoiseTex", noise);
        material.SetFloat("_WaveAmplitude", amplitude);
        material.SetFloat("_WaveLength", waveLength);
        material.SetFloat("_WaveSpeed", waveSpeed);
        material.SetFloat("_ScrollSpeed", scrollSpeed);
        material.SetFloat("_EdgePower", edgePower);
        EditorUtility.SetDirty(material);
        return material;
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

    // 雜訊貼圖要沿光束重複捲動，匯入設定必須是 Repeat
    private static Texture2D LoadRepeatTexture(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.wrapMode != TextureWrapMode.Repeat)
        {
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
#endif
