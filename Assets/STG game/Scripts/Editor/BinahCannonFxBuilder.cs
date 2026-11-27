#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Binah 光炮特殊機制用到的外觀 (由 BinahPrefabBuilder 呼叫)：
// 沙丘 Prefab、鋪滿整個場地往下流的流沙、整個場地的紅色警示、嘴裡的蓄力光、光炮本體 (從嘴到整個場地的光錐)
public static class BinahCannonFxBuilder
{
    private const string Folder = "Assets/STG game/resources/prefab/BinahFX";
    public const string DunePrefabPath = Folder + "/Binah_Dune.prefab";
    // 沙丘模型 (掃描的沙堆，頂點色是灰階明暗，顏色來自材質的 diffuseFactor)
    private const string SandHillModelPath = "Assets/STG game/resources/model/sand_hill.glb";
    private const string SandHillMaterialPrefix = Folder + "/Binah_SandHill_";
    // 減面後的沙丘網格 (原模型 4 塊共約 22 萬頂點 → 96 x 96 網格重新取樣，約 9 千頂點)
    private const string SandHillLowMeshPath = Folder + "/Binah_SandHill_Low.asset";
    private const int SandHillResolution = 96;
    private const string WarningMaterialPath = Folder + "/Binah_CannonWarning.mat";
    private const string BeamMaterialPath = Folder + "/Binah_CannonBeam.mat";
    private const string GlowMaterialPath = Folder + "/Binah_CannonGlow.mat";
    private const string GlowTexturePath = "Assets/STG game/resources/prefab/WaterFX/FX_SoftDot.asset";
    private const string PuffTexturePath = "Assets/STG game/resources/prefab/WaterFX/FX_SoftPuff.asset";
    private const string SandFlowTexturePath = Folder + "/Binah_SandFlow.asset";
    private const string SandFlowMaterialPath = Folder + "/Binah_SandFlow.mat";
    private const string SandDustMaterialPath = Folder + "/Binah_SandDust.mat";
    private const int SandFlowTextureSize = 128;
    private const string FillMaterialPath = Folder + "/Binah_CannonWarningFill.mat";

    // 蓄力能量球 (Fire_01 的本地單位，模型 5 倍縮放：1 = 5 m)：嘴前 8 m，核心直徑 5 m (再依蓄力進度縮放)
    private static readonly Vector3 OrbLocalOffset = new Vector3(0f, 0f, 1.6f);
    private const float OrbCoreSize = 1f;
    private const float OrbSparkRadius = 2.5f;
    private static readonly Color OrbHaloColor = new Color(1f, 0.55f, 0.2f, 1f);
    private static readonly Color OrbSparkColor = new Color(1f, 0.95f, 0.7f, 1f);
    // 地板警示：塗滿的部分比底下閃爍的整片警示濃
    private static readonly Color FillColor = new Color(1f, 0.08f, 0.08f, 0.38f);

    // 沙丘：寬 22 m、深 9 m 的低矮土堆 (半顆壓扁的球)，Collider 擋住玩家移動
    // 原始模型約 2.2 m 寬、0.6 m 高；以模型根物件為原點時，土堆中心與底面在 (0.63, -1.22, 0.05)
    // (模型根物件本身帶有位移，放進沙丘時會被覆蓋)。放大 10 倍約 22 m 寬、6 m 高，並把中心與底面對到原點
    private const float SandHillScale = 10f;
    private static readonly Vector3 SandHillCenter = new Vector3(0.63f, -1.22f, 0.05f);
    // 碰撞範圍比外觀略小 (圓形土堆的邊緣很薄，不該擋住玩家)
    private static readonly Vector3 DuneColliderSize = new Vector3(19f, 5f, 20f);
    // 沙色：與流沙、沙塵一致的米黃 (原模型偏橘)；霧面
    private static readonly Color SandHillColor = new Color(0.95f, 0.86f, 0.66f, 1f);
    private const float SandHillGlossiness = 0.1f;
    private const int DuneHealth = 30;
    private const float DuneCoverLength = 25f;
    private const float DuneTipWidthRatio = 0.4f;

    // 整個場地 (四塊地板合起來 X -109 ~ 7.3、Z -9 ~ 88.5)，警示與流沙都要整片蓋住
    private static readonly Vector3 ArenaCenter = new Vector3(-50.85f, 0.45f, 39.75f);
    private static readonly Vector2 ArenaSize = new Vector2(116.3f, 97.5f);
    // 流沙：沙紋重複次數與往下流 (往 +Z、畫面下方) 的速度
    private static readonly Vector2 SandFlowTiling = new Vector2(3f, 2.5f);
    private static readonly Vector2 SandFlowSpeed = new Vector2(0f, -0.9f);
    private static readonly Color SandDustColor = new Color(0.88f, 0.75f, 0.53f, 1f);
    private const float SandDustSpeed = 45f;
    private static readonly Color WarningColor = new Color(1f, 0.1f, 0.1f, 0.18f);
    private static readonly Color BeamColor = new Color(1f, 0.55f, 0.55f, 1f);
    private static readonly Color GlowColor = new Color(1f, 0.6f, 0.5f, 1f);

    public static GameObject BuildDunePrefab()
    {
        EnsureFolder();
        GameObject root = new GameObject("Binah_Dune");
        try
        {
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SandHillModelPath);
            if (modelAsset == null)
            {
                Debug.LogError("[BinahCannonFxBuilder] 找不到沙丘模型 " + SandHillModelPath);
                return null;
            }

            GameObject mound = new GameObject("Mound");
            mound.transform.SetParent(root.transform, false);
            mound.AddComponent<MeshFilter>().sharedMesh = GetOrCreateLowPolySandHill(modelAsset);
            MeshRenderer moundRenderer = mound.AddComponent<MeshRenderer>();
            moundRenderer.sharedMaterial = GetOrCreateSandHillMaterial(modelAsset);
            moundRenderer.shadowCastingMode = ShadowCastingMode.On;

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, DuneColliderSize.y * 0.5f, 0f);
            collider.size = DuneColliderSize;

            BinahDune dune = root.AddComponent<BinahDune>();
            SerializedObject serialized = new SerializedObject(dune);
            serialized.FindProperty("maxHealth").intValue = DuneHealth;
            serialized.FindProperty("coverLength").floatValue = DuneCoverLength;
            serialized.FindProperty("tipWidthRatio").floatValue = DuneTipWidthRatio;
            // 機制結束時沙丘破掉：原地揚起一陣沙 (沿用潑沙特效，縮小)
            serialized.FindProperty("breakEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(WaterFxBuilder.SandSplashPrefabPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(root, DunePrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // 整個場地的紅色半透明警示 (世界座標放在場地上；root 為 Binah 根物件，機制開始時 Binah 會先回到中間)
    public static GameObject CreateWarning(Transform root, Vector3 rootWorldPosition)
    {
        GameObject warning = GameObject.CreatePrimitive(PrimitiveType.Quad);
        warning.name = "CannonWarning";
        Object.DestroyImmediate(warning.GetComponent<Collider>());
        warning.transform.SetParent(root, false);
        warning.transform.localPosition = ArenaCenter - rootWorldPosition;
        warning.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        warning.transform.localScale = new Vector3(ArenaSize.x, ArenaSize.y, 1f);
        Renderer renderer = warning.GetComponent<Renderer>();
        renderer.sharedMaterial = GetOrCreateColorMaterial(WarningMaterialPath, WarningColor, false);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        PulseAlpha pulse = warning.AddComponent<PulseAlpha>();
        SerializedObject pulseSerialized = new SerializedObject(pulse);
        pulseSerialized.FindProperty("minAlpha").floatValue = 0.06f;
        pulseSerialized.FindProperty("maxAlpha").floatValue = 0.2f;
        pulseSerialized.ApplyModifiedPropertiesWithoutUndo();
        warning.SetActive(false);
        return warning;
    }

    // 光炮本體：空的 MeshFilter，BinahCannonMechanic 發射時建立「從嘴到整個場地四角」的光錐網格
    public static GameObject CreateBeam(Transform root)
    {
        GameObject beam = new GameObject("CannonBeam");
        beam.transform.SetParent(root, false);
        beam.AddComponent<MeshFilter>();
        MeshRenderer renderer = beam.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = GetOrCreateColorMaterial(BeamMaterialPath, BeamColor, true);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        beam.SetActive(false);
        return beam;
    }

    // 流沙：鋪滿整個場地、沙紋往畫面下方捲動的半透明層 + 往下流的沙塵 (把玩家沖到最下方時顯示)
    public static GameObject CreateQuicksand(Transform root, Vector3 rootWorldPosition)
    {
        GameObject quicksand = new GameObject("Quicksand");
        quicksand.transform.SetParent(root, false);
        quicksand.transform.localPosition = ArenaCenter + Vector3.up * 0.05f - rootWorldPosition;

        GameObject layer = GameObject.CreatePrimitive(PrimitiveType.Quad);
        layer.name = "SandLayer";
        Object.DestroyImmediate(layer.GetComponent<Collider>());
        layer.transform.SetParent(quicksand.transform, false);
        layer.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        layer.transform.localScale = new Vector3(ArenaSize.x, ArenaSize.y, 1f);
        Renderer layerRenderer = layer.GetComponent<Renderer>();
        layerRenderer.sharedMaterial = AreaStrikeExplosionBuilder.GetOrCreateParticleMaterial(SandFlowMaterialPath, GetOrCreateSandFlowTexture(), 1f, false, -1);
        layerRenderer.shadowCastingMode = ShadowCastingMode.Off;
        ScrollTexture scroll = layer.AddComponent<ScrollTexture>();
        SerializedObject scrollSerialized = new SerializedObject(scroll);
        scrollSerialized.FindProperty("tiling").vector2Value = SandFlowTiling;
        scrollSerialized.FindProperty("speed").vector2Value = SandFlowSpeed;
        scrollSerialized.ApplyModifiedPropertiesWithoutUndo();

        CreateSandDust(quicksand.transform);
        quicksand.SetActive(false);
        return quicksand;
    }

    // 往畫面下方 (+Z) 流的沙塵：整個場地範圍內持續冒出
    private static void CreateSandDust(Transform parent)
    {
        GameObject dust = new GameObject("SandDust");
        dust.transform.SetParent(parent, false);
        ParticleSystem system = dust.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.playOnAwake = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(6f, 14f);
        main.startColor = SandDustColor;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles = 300;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 120f;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(ArenaSize.x, 1f, ArenaSize.y);
        shape.position = new Vector3(0f, 1.5f, 0f);
        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-3f, 3f);
        velocity.y = new ParticleSystem.MinMaxCurve(0f, 2f);
        velocity.z = new ParticleSystem.MinMaxCurve(SandDustSpeed * 0.7f, SandDustSpeed);
        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.45f, 0.2f), new GradientAlphaKey(0.45f, 0.7f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;
        ParticleSystemRenderer renderer = dust.GetComponent<ParticleSystemRenderer>();
        Texture2D puff = AssetDatabase.LoadAssetAtPath<Texture2D>(PuffTexturePath);
        renderer.sharedMaterial = AreaStrikeExplosionBuilder.GetOrCreateParticleMaterial(SandDustMaterialPath, puff, 1f, false, 1);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    private static Texture2D GetOrCreateSandFlowTexture()
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SandFlowTexturePath);
        bool isNew = texture == null;
        if (isNew) texture = new Texture2D(SandFlowTextureSize, SandFlowTextureSize, TextureFormat.RGBA32, true);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.SetPixels32(SandFlowTexture.Generate(SandFlowTextureSize, 7));
        texture.Apply();
        if (isNew) AssetDatabase.CreateAsset(texture, SandFlowTexturePath);
        else EditorUtility.SetDirty(texture);
        return texture;
    }

    // 蓄力能量球：掛在嘴裡的 Fire_01 前方 (沿嘴的方向往外推，不會被頭擋住)，由發光核心 + 外圈光暈 + 往內聚集的火花組成；
    // BinahCannonMechanic 依蓄力進度放大整顆球。Fire_01 在 5 倍縮放的模型底下，本地 1 單位 = 世界 5 m
    public static GameObject CreateChargeOrb(Transform muzzle)
    {
        GameObject orb = new GameObject("ChargeOrb");
        orb.transform.SetParent(muzzle, false);
        orb.transform.localPosition = OrbLocalOffset;

        // 與長條光炮、能量球同樣的三層配色 (黑紅外殼 → 橘色火焰 → 近白核心)
        BinahAttackFxBuilder.CreateFireOrbLayers(orb.transform, OrbCoreSize);

        Texture2D dot = AssetDatabase.LoadAssetAtPath<Texture2D>(GlowTexturePath);
        Material glowMaterial = AreaStrikeExplosionBuilder.GetOrCreateParticleMaterial(GlowMaterialPath, dot, 1f, true, 2);

        // 外圈光暈：大顆、柔和、持續脈動
        ParticleSystem halo = CreateLoopingSystem(orb.transform, "Halo", glowMaterial);
        ParticleSystem.MainModule haloMain = halo.main;
        haloMain.startLifetime = 0.5f;
        haloMain.startSpeed = 0f;
        haloMain.startSize = new ParticleSystem.MinMaxCurve(OrbCoreSize * 2.2f, OrbCoreSize * 3f);
        haloMain.startColor = OrbHaloColor;
        ParticleSystem.EmissionModule haloEmission = halo.emission;
        haloEmission.rateOverTime = 16f;
        ParticleSystem.ShapeModule haloShape = halo.shape;
        haloShape.enabled = false;
        FadeInOut(halo, 0.8f);

        // 往內聚集的火花：從外圍的球面往中心吸
        ParticleSystem sparks = CreateLoopingSystem(orb.transform, "Sparks", glowMaterial);
        ParticleSystem.MainModule sparkMain = sparks.main;
        sparkMain.startLifetime = 0.35f;
        sparkMain.startSpeed = -OrbSparkRadius / 0.35f;
        sparkMain.startSize = new ParticleSystem.MinMaxCurve(OrbCoreSize * 0.12f, OrbCoreSize * 0.25f);
        sparkMain.startColor = OrbSparkColor;
        ParticleSystem.EmissionModule sparkEmission = sparks.emission;
        sparkEmission.rateOverTime = 50f;
        ParticleSystem.ShapeModule sparkShape = sparks.shape;
        sparkShape.enabled = true;
        sparkShape.shapeType = ParticleSystemShapeType.Sphere;
        sparkShape.radius = OrbSparkRadius;
        sparkShape.radiusThickness = 0f;
        FadeInOut(sparks, 1f);
        ParticleSystemRenderer sparkRenderer = sparks.GetComponent<ParticleSystemRenderer>();
        sparkRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        sparkRenderer.velocityScale = 0.04f;
        sparkRenderer.lengthScale = 1f;

        orb.SetActive(false);
        return orb;
    }

    // 地板警示漸漸塗滿：從場地遠側邊緣 (Binah 那側) 往畫面下方長；WarningFill 依蓄力進度縮放
    public static WarningFill CreateChargeFill(Transform root, Vector3 rootWorldPosition)
    {
        GameObject pivot = new GameObject("CannonWarningFill");
        pivot.transform.SetParent(root, false);
        Vector3 startEdge = new Vector3(ArenaCenter.x, ArenaCenter.y + 0.03f, ArenaCenter.z - ArenaSize.y * 0.5f);
        pivot.transform.localPosition = startEdge - rootWorldPosition;

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Fill";
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.transform.SetParent(pivot.transform, false);
        quad.transform.localPosition = new Vector3(0f, 0f, ArenaSize.y * 0.5f);
        quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        quad.transform.localScale = new Vector3(ArenaSize.x, ArenaSize.y, 1f);
        Renderer renderer = quad.GetComponent<Renderer>();
        Material material = AreaStrikeExplosionBuilder.GetOrCreateParticleMaterial(FillMaterialPath, null, 1f, false, 1);
        material.SetColor("_BaseColor", FillColor);
        EditorUtility.SetDirty(material);
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;

        WarningFill fill = pivot.AddComponent<WarningFill>();
        fill.SetProgress(0f);
        pivot.SetActive(false);
        return fill;
    }

    internal static ParticleSystem CreateLoopingSystem(Transform parent, string name, Material material)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        // 跟著能量球一起放大 (蓄力進度) 與模型的 5 倍縮放
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return system;
    }

    internal static void FadeInOut(ParticleSystem system, float alpha)
    {
        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, 0.3f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
    }

    // 減面：把原模型的 4 塊網格轉到沙丘座標 (放大、旋轉、置中與底面對齊)，合起來重新取樣成規則網格。
    // 資產已存在時只更新內容，保留 GUID (Prefab 的參照不會斷)
    private static Mesh GetOrCreateLowPolySandHill(GameObject modelAsset)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Color> colors = new List<Color>();
        Matrix4x4 toDune = Matrix4x4.TRS(-SandHillCenter * SandHillScale, modelAsset.transform.localRotation, Vector3.one * SandHillScale);
        Matrix4x4 rootInverse = modelAsset.transform.worldToLocalMatrix;
        foreach (MeshFilter filter in modelAsset.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh source = filter.sharedMesh;
            if (source == null) continue;
            Matrix4x4 matrix = toDune * rootInverse * filter.transform.localToWorldMatrix;
            int offset = vertices.Count;
            Vector3[] sourceVertices = source.vertices;
            for (int i = 0; i < sourceVertices.Length; i++) vertices.Add(matrix.MultiplyPoint3x4(sourceVertices[i]));
            Color[] sourceColors = source.colors;
            for (int i = 0; i < sourceVertices.Length; i++) colors.Add(sourceColors.Length == sourceVertices.Length ? sourceColors[i] : Color.white);
            int[] sourceTriangles = source.triangles;
            for (int i = 0; i < sourceTriangles.Length; i++) triangles.Add(sourceTriangles[i] + offset);
        }

        Mesh low = HeightfieldResampler.Resample(vertices.ToArray(), triangles.ToArray(), colors.ToArray(), SandHillResolution);
        low.name = "Binah_SandHill_Low";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(SandHillLowMeshPath);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(low, SandHillLowMeshPath);
            return low;
        }

        existing.Clear();
        existing.indexFormat = low.indexFormat;
        existing.SetVertices(low.vertices);
        existing.SetColors(low.colors);
        existing.SetNormals(low.normals);
        existing.SetTriangles(low.triangles, 0);
        existing.RecalculateBounds();
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(low);
        return existing;
    }

    // 沙色的材質複本 (沿用原 glTF shader，頂點色的明暗細節保留；只改顏色與光澤)
    private static Material GetOrCreateSandHillMaterial(GameObject modelAsset)
    {
        Renderer sourceRenderer = modelAsset.GetComponentInChildren<Renderer>(true);
        Material source = sourceRenderer != null ? sourceRenderer.sharedMaterial : null;
        string path = SandHillMaterialPrefix + "Low.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            if (source == null) return null;
            material = new Material(source);
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("diffuseFactor", SandHillColor);
        material.SetFloat("glossinessFactor", SandHillGlossiness);
        EditorUtility.SetDirty(material);
        return material;
    }

    // 純色半透明 (或加亮) 材質，沿用粒子材質的混合設定
    internal static Material GetOrCreateColorMaterial(string path, Color color, bool additive)
    {
        Material material = AreaStrikeExplosionBuilder.GetOrCreateParticleMaterial(path, null, 1f, additive, 0);
        material.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/STG game/resources/prefab", "BinahFX");
    }
}
#endif
