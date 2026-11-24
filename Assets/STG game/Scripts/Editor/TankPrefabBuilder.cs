#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

// 一鍵建立戰車衝撞攻擊用的 Prefab：
// LaneWarning (長方形警示區) 與 Tank (模型 + 碰撞體 + 落地煙塵 + TankCharge)。
// 煙塵沿用範圍攻擊爆炸的煙霧材質，只在這裡調整發射方式與顏色。可重複執行，會覆寫既有的 Prefab
public static class TankPrefabBuilder
{
    private const string OutputFolder = "Assets/STG game/resources/prefab/Tank";
    private const string WarningPrefabPath = "Assets/STG game/resources/prefab/LaneWarning.prefab";
    private const string TankPrefabPath = "Assets/STG game/resources/prefab/Tank.prefab";
    private const string ModelPath = "Assets/STG game/resources/model/Enemy_HelmetTank_Cannon/bundles/Assets/_MX/AddressableAsset/Character/HeavyTank01_Helmet_Cannon/HeavyTank01_Helmet_Cannon.glb";
    private const string ControllerPath = "Assets/STG game/resources/prefab/Tank.controller";
    private const string AppearClipPath = "Assets/STG game/resources/prefab/Tank_Appear_Once.anim";
    private const string ClipPrefix = "HeavyTank01_Helmet_Cannon_";
    private const string SmokeMaterialPath = "Assets/STG game/resources/prefab/AreaStrike/Explosion_Smoke.mat";
    private const string DeathEffectPath = "Assets/STG game/resources/prefab/EnemyExplosion.prefab";

    // 模型原始長約 3.2，放大後約 19 m (與舊戰車相同大小)；砲管已朝 +Z (前進方向)
    private const float ModelScale = 6f;
    private const float ModelYaw = 0f;

    // 煙塵貼地擴散，Soft Particles 淡出距離要比爆炸煙霧短，否則整圈煙會被淡到幾乎看不見
    private const float DustSoftFadeDistance = 1.2f;

    // 卡通風格的煙塵：明亮的米色雲團 (暗面與描邊來自共用的卡通雲團貼圖)，碎土用深咖啡色
    private static readonly Color DustColor = new Color(0.92f, 0.84f, 0.7f);
    private static readonly Color DebrisColor = new Color(0.42f, 0.32f, 0.24f);

    [MenuItem("Tools/STG/Build Tank")]
    private static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[TankPrefabBuilder] 請先離開 Play Mode 再執行。");
            return;
        }

        EnsureFolder(OutputFolder);
        LaneWarning warning = BuildWarningPrefab();
        BuildTankPrefab(warning);
        AssetDatabase.SaveAssets();
        Debug.Log("[TankPrefabBuilder] 已建立 " + WarningPrefabPath + " 與 " + TankPrefabPath);
    }

    private static LaneWarning BuildWarningPrefab()
    {
        Material material = GetOrCreateSolidMaterial(OutputFolder + "/LaneWarning.mat");
        GameObject root = new GameObject("LaneWarning");
        try
        {
            Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            Renderer area = CreateFlatQuad(root.transform, "Area", quad, material);
            Renderer fill = CreateFlatQuad(root.transform, "Fill", quad, material);
            Renderer[] borders =
            {
                CreateFlatQuad(root.transform, "BorderLeft", quad, material),
                CreateFlatQuad(root.transform, "BorderRight", quad, material),
                CreateFlatQuad(root.transform, "BorderBack", quad, material),
                CreateFlatQuad(root.transform, "BorderFront", quad, material),
            };

            LaneWarning warning = root.AddComponent<LaneWarning>();
            SerializedObject serialized = new SerializedObject(warning);
            serialized.FindProperty("areaRenderer").objectReferenceValue = area;
            serialized.FindProperty("fillRenderer").objectReferenceValue = fill;
            SerializedProperty borderProperty = serialized.FindProperty("borderRenderers");
            borderProperty.arraySize = borders.Length;
            for (int i = 0; i < borders.Length; i++) borderProperty.GetArrayElementAtIndex(i).objectReferenceValue = borders[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(root, WarningPrefabPath).GetComponent<LaneWarning>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void BuildTankPrefab(LaneWarning warningPrefab)
    {
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelAsset == null)
        {
            Debug.LogError("[TankPrefabBuilder] 找不到模型 " + ModelPath);
            return;
        }

        GameObject root = new GameObject("Tank");
        try
        {
            GameObject modelRoot = new GameObject("Model");
            modelRoot.transform.SetParent(root.transform, false);
            modelRoot.layer = LayerMask.NameToLayer("Enemy");

            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            model.transform.SetParent(modelRoot.transform, false);
            model.transform.localRotation = Quaternion.Euler(0f, ModelYaw, 0f) * model.transform.localRotation;
            model.transform.localScale = Vector3.one * ModelScale;

            // 讓車底貼齊根物件原點 (= 地面)，並以模型實際外框建立碰撞體
            Bounds bounds = GetRendererBounds(model);
            model.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            bounds = GetRendererBounds(model);
            BoxCollider collider = modelRoot.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = bounds.size;
            // Trigger：戰車用 transform 移動，實體碰撞體會把玩家的剛體硬推出去；雷射與落地壓毀判定都會偵測 Trigger
            collider.isTrigger = true;

            ParticleSystem dust = BuildLandingDust(root.transform);

            root.AddComponent<EnemyHealth>();
            root.AddComponent<EnemyHitFlash>();
            TankCharge tank = root.AddComponent<TankCharge>();
            SerializedObject serialized = new SerializedObject(tank);
            serialized.FindProperty("modelRoot").objectReferenceValue = modelRoot;
            serialized.FindProperty("animator").objectReferenceValue = SetupAnimator(model);
            serialized.FindProperty("appearLandTime").floatValue = FindAppearTouchdown(modelAsset);
            serialized.FindProperty("warningPrefab").objectReferenceValue = warningPrefab;
            serialized.FindProperty("landingDust").objectReferenceValue = dust;
            serialized.FindProperty("deathEffectPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(DeathEffectPath);
            serialized.FindProperty("bodyWidth").floatValue = bounds.size.x;
            serialized.FindProperty("bodyLength").floatValue = bounds.size.z;
            serialized.FindProperty("bodyHeight").floatValue = bounds.size.y;
            // 只偵測玩家，地板與牆壁不會佔用判定緩衝區
            serialized.FindProperty("hitLayers").intValue = LayerMask.GetMask("Player");
            serialized.FindProperty("wallLayers").intValue = LayerMask.GetMask("ArenaWall");
            serialized.FindProperty("crushLayers").intValue = LayerMask.GetMask("Enemy");
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, TankPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // glb 匯入時根物件已有 Animator，動畫路徑以它為基準：Appear (從空中掉落) → Idle ↔ Move (IsMoving)
    private static Animator SetupAnimator(GameObject model)
    {
        Animator animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.AddComponent<Animator>();
        animator.runtimeAnimatorController = BuildController();
        animator.applyRootMotion = false;
        return animator;
    }

    private static AnimatorController BuildController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState child in machine.states) machine.RemoveState(child.state);
        while (controller.parameters.Length > 0) controller.RemoveParameter(0);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);

        AnimatorState idle = machine.AddState("Idle");
        idle.motion = FindClip("Normal_Idle");
        // 出場動畫播完要停在最後一格 (地面)；循環版在切到 Idle 的過渡期間會繞回第一格 (空中)，看起來像再落地一次
        AnimatorState appear = machine.AddState("Appear");
        appear.motion = AnimationClipCopies.CreateNonLoopingCopy(FindClip("Appear"), AppearClipPath);
        AnimatorState move = machine.AddState("Move");
        move.motion = FindClip("Move_Ing");
        machine.defaultState = idle;

        // Appear 在 glb 內是循環動畫，用離開時間在播完一次時接 Idle
        AnimatorStateTransition appearDone = appear.AddTransition(idle);
        appearDone.hasExitTime = true;
        appearDone.exitTime = 0.95f;
        appearDone.hasFixedDuration = true;
        appearDone.duration = 0.2f;
        AddBoolTransition(appear, move, true);
        AddBoolTransition(idle, move, true);
        AddBoolTransition(move, idle, false);

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void AddBoolTransition(AnimatorState from, AnimatorState to, bool moving)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0.15f;
        transition.AddCondition(moving ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, "IsMoving");
    }

    // 取樣 Appear 動畫，找出車體 (Tank_Move 骨骼) 第一次觸地的時間，煙塵與落地傷害就在這一刻觸發；
    // 動畫觸地後還有一下小彈跳，若時間設得比觸地晚，看起來會像落地兩次
    // 取樣會改動整個骨架的姿勢，所以用暫時的複本取樣，不動到要存成 prefab 的模型
    private static float FindAppearTouchdown(GameObject modelAsset)
    {
        AnimationClip clip = FindClip("Appear");
        GameObject sample = Object.Instantiate(modelAsset);
        try
        {
            Transform mover = null;
            foreach (Transform child in sample.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Tank_Move") mover = child;
            }
            if (clip == null || mover == null) return 0.6f;

            for (float time = 0f; time <= clip.length; time += 0.01f)
            {
                clip.SampleAnimation(sample, time);
                if (mover.localPosition.y < 0.05f) return time;
            }
            return clip.length;
        }
        finally
        {
            Object.DestroyImmediate(sample);
        }
    }

    // glb 內的動畫名稱帶有雜湊後綴 (例如 HeavyTank01_Helmet_Cannon_Appear--xxxx)，以前綴比對
    private static AnimationClip FindClip(string name)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
        {
            if (asset is AnimationClip clip && clip.name.StartsWith(ClipPrefix + name + "--")) return clip;
        }

        Debug.LogError("[TankPrefabBuilder] 找不到動畫 " + ClipPrefix + name);
        return null;
    }

    // 落地煙塵：貼地往四周快速擴散的煙環 + 往上翻滾的煙柱 + 被震飛的碎土
    private static ParticleSystem BuildLandingDust(Transform parent)
    {
        Material smoke = GetOrCreateDustMaterial(OutputFolder + "/LandingDust.mat");

        ParticleSystem ring = CreateSystem(parent, "LandingDust", smoke);
        ring.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        SetupMain(ring, new Vector2(1.8f, 3f), new Vector2(30f, 50f), new Vector2(8f, 14f), 0f, DustColor);
        SetBurst(ring, 15);
        ParticleSystem.ShapeModule shape = ring.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 6f;
        shape.radiusThickness = 0f;
        // Circle 預設在 XY 平面，轉成貼地的 XZ 平面往外擴散
        shape.rotation = new Vector3(90f, 0f, 0f);
        SetDamping(ring, 3f, 0.35f);
        SetPopSizeCurve(ring, 0.6f, 2f);

        ParticleSystem plume = CreateSystem(ring.transform, "Plume", smoke);
        SetupMain(plume, new Vector2(2f, 3.2f), new Vector2(6f, 16f), new Vector2(10f, 18f), -0.05f, DustColor * 0.9f);
        SetBurst(plume, 15);
        ParticleSystem.ShapeModule plumeShape = plume.shape;
        plumeShape.enabled = true;
        plumeShape.shapeType = ParticleSystemShapeType.Hemisphere;
        plumeShape.radius = 6f;
        plumeShape.rotation = new Vector3(-90f, 0f, 0f);
        SetDamping(plume, 3f, 0.2f);
        SetPopSizeCurve(plume, 0.5f, 1.6f);

        ParticleSystem debris = CreateSystem(ring.transform, "Debris", smoke);
        SetupMain(debris, new Vector2(0.8f, 1.4f), new Vector2(15f, 30f), new Vector2(0.8f, 1.6f), 4f, DebrisColor);
        SetBurst(debris, 30);
        ParticleSystem.ShapeModule debrisShape = debris.shape;
        debrisShape.enabled = true;
        debrisShape.shapeType = ParticleSystemShapeType.Cone;
        debrisShape.angle = 50f;
        debrisShape.radius = 5f;
        debrisShape.rotation = new Vector3(-90f, 0f, 0f);
        SetPopSizeCurve(debris, 1f, 1.2f);

        return ring;
    }

    private static ParticleSystem CreateSystem(Transform parent, string name, Material material)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return system;
    }

    private static ParticleSystem.MainModule SetupMain(ParticleSystem system, Vector2 lifetime, Vector2 speed, Vector2 size, float gravity, Color color)
    {
        ParticleSystem.MainModule main = system.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = color;
        main.gravityModifier = gravity;
        main.maxParticles = 64;
        // 世界座標：戰車落地後立刻轉向，煙塵不能跟著車身一起轉
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        return main;
    }

    private static void SetBurst(ParticleSystem system, short count)
    {
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
    }

    // 落地瞬間衝很快、之後迅速停下並在原地擴散，看起來像被衝擊波推開的煙塵
    private static void SetDamping(ParticleSystem system, float limit, float dampen)
    {
        ParticleSystem.LimitVelocityOverLifetimeModule limitVelocity = system.limitVelocityOverLifetime;
        limitVelocity.enabled = true;
        limitVelocity.limit = limit;
        limitVelocity.dampen = dampen;
    }

    // 卡通式消失：先膨脹到 peak 再縮小到 0，取代半透明淡出
    private static void SetPopSizeCurve(ParticleSystem system, float start, float peak)
    {
        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, start, 0f, 0f),
            new Keyframe(0.3f, peak, 0f, 0f),
            new Keyframe(1f, 0f, -1.5f, 0f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);
    }

    private static Renderer CreateFlatQuad(Transform parent, string name, Mesh mesh, Material material)
    {
        GameObject quad = new GameObject(name);
        quad.transform.SetParent(parent, false);
        // Quad 預設面向 -Z，繞 X 轉 90 度後朝上平貼地面
        quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        quad.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = quad.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private static Bounds GetRendererBounds(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    // 從爆炸煙霧材質複製一份 (同樣的雲團貼圖與混合設定)，只把 Soft Particles 淡出距離縮短
    private static Material GetOrCreateDustMaterial(string path)
    {
        Material source = AssetDatabase.LoadAssetAtPath<Material>(SmokeMaterialPath);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(source);
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.CopyPropertiesFromMaterial(source);
        }

        material.SetFloat("_SoftParticlesFarFadeDistance", DustSoftFadeDistance);
        material.SetVector("_SoftParticleFadeParams", new Vector4(0f, 1f / DustSoftFadeDistance, 0f, 0f));
        EditorUtility.SetDirty(material);
        return material;
    }

    // URP Unlit 半透明純色材質；顏色由 LaneWarning 依屬性以 MaterialPropertyBlock 覆寫
    private static Material GetOrCreateSolidMaterial(string path)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", new Color(2f, 0.15f, 0.1f, 0.5f));
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
    }
}
#endif
