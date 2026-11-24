#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

// 建立 Boss 召喚用的兩個 Prefab，由 PerorozillaPrefabBuilder 一起呼叫：
// SummonCapsule (扭蛋膠囊：上半殼 + 白色下半殼，取自 Perorozilla_Original 特效的球體網格) 與
// Peroro_Minion (Perorozilla_Peroro_MiddleSize：出場、巡邏、攻擊、死亡動畫 + PeroroMinion 行為 + 雷射子彈 Peroro_LaserBolt)
public static class PerorozillaSummonBuilder
{
    private const string MeshFolder = "Assets/STG game/resources/model/Boss_Perorozilla/bundles/Assets/_MX/NPCs/Perorozilla_Original/Perorozilla_Original/Effect/Mesh/";
    private const string MinionModelPath = "Assets/STG game/resources/model/Boss_Perorozilla/bundles/Assets/_MX/AddressableAsset/Character/Perorozilla_Peroro_MiddleSize/Perorozilla_Peroro_MiddleSize.glb";
    private const string SourceBulletPrefabPath = "Assets/STG game/resources/prefab/EnemyBullet_SM05.prefab";
    private const string BlueBodyMaterialPath = "Assets/STG game/resources/prefab/BulletPolarity/Bullet_Body_Blue.mat";
    private const string RedBodyMaterialPath = "Assets/STG game/resources/prefab/BulletPolarity/Bullet_Body_Red.mat";
    private const string LaserBoltPrefabPath = "Assets/STG game/resources/prefab/Peroro_LaserBolt.prefab";
    private const string MinionDeathClipPath = "Assets/STG game/resources/prefab/Peroro_Minion_Death_Once.anim";
    private const string MinionAppearClipPath = "Assets/STG game/resources/prefab/Peroro_Minion_Appear_Once.anim";
    // 雙眼的發射點骨骼
    private static readonly string[] EyeBoneNames = { "Shot_bone_01", "Shot_bone_002" };
    private static readonly Color RedColor = new Color(1f, 0.12f, 0.08f);
    // 砲台子彈根物件縮放為 20 (等比)，沿飛行方向 (Z) 拉長成光束狀
    private static readonly Vector3 LaserBoltScale = new Vector3(14f, 14f, 50f);
    private const string OutputFolder = "Assets/STG game/resources/prefab";
    private const string CapsulePrefabPath = OutputFolder + "/SummonCapsule.prefab";
    private const string MatteMaterialFolder = OutputFolder + "/Materials";
    private const string GltfMetallicProperty = "metallicFactor";
    private const string MinionPrefabPath = OutputFolder + "/Peroro_Minion.prefab";
    private const string MinionControllerPath = OutputFolder + "/Peroro_Minion.controller";
    private const string ShellMaterialPath = OutputFolder + "/SummonCapsule_Shell.mat";

    // 膠囊網格直徑約 1.1，放大後約 9 m；小怪原始高度約 2.6，放大後約 15 m (Rio 約 10 m)
    private const float CapsuleScale = 8f;
    // 半殼網格從殼頂到開口的深度 (= 球半徑)
    private const float ShellDepth = 0.56f;
    private const float MinionScale = 6f;
    private const int MinionMaxHealth = 30;

    public static GameObject BuildCapsule()
    {
        Material shell = GetOrCreateShellMaterial();
        GameObject root = new GameObject("SummonCapsule");
        try
        {
            root.transform.localScale = Vector3.one * CapsuleScale;
            // 半殼網格的原點在殼頂 (極點)、開口邊緣離原點 ShellDepth：上半殼殼頂往上放、下半殼殼頂往下放，開口在中間對齊成一顆球
            Renderer top = CreatePart(root.transform, "TopShell", FindMesh("FX_MESH_Ball_Red_Up"), shell, ShellDepth);
            CreatePart(root.transform, "BottomShell", FindMesh("FX_MESH_Ball_White_Down"), shell, -ShellDepth);

            SummonCapsule capsule = root.AddComponent<SummonCapsule>();
            SerializedObject serialized = new SerializedObject(capsule);
            serialized.FindProperty("topShell").objectReferenceValue = top.transform;
            serialized.FindProperty("topRenderer").objectReferenceValue = top;
            serialized.FindProperty("landingHeight").floatValue = ShellDepth * CapsuleScale;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(root, CapsulePrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private const float MinionHealthBarHeight = 17.5f;
    private const float MinionHealthBarWidth = 8f;
    private const float MinionHealthBarThickness = 0.8f;

    public static GameObject BuildMinion()
    {
        GameObject laserBoltPrefab = BuildLaserBolt();
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(MinionModelPath);
        if (modelAsset == null)
        {
            Debug.LogError("[PerorozillaSummonBuilder] 找不到小怪模型 " + MinionModelPath);
            return null;
        }

        GameObject root = new GameObject("Peroro_Minion");
        try
        {
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            model.name = "Model";
            model.transform.SetParent(root.transform, false);
            model.transform.localScale = Vector3.one * MinionScale;
            model.layer = LayerMask.NameToLayer("Enemy");
            ApplyMatteMaterials(model, "Peroro_Minion");

            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = BuildMinionController();
            animator.applyRootMotion = false;

            // Trigger：給雷射判定，不推擠玩家
            BoxCollider collider = model.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 1.28f, -0.15f);
            collider.size = new Vector3(2.3f, 2.5f, 1.9f);
            collider.isTrigger = true;

            EnemyHealth health = root.AddComponent<EnemyHealth>();
            root.AddComponent<EnemyHitFlash>();
            SerializedObject healthSerialized = new SerializedObject(health);
            healthSerialized.FindProperty("maxHealth").intValue = MinionMaxHealth;
            healthSerialized.ApplyModifiedPropertiesWithoutUndo();

            // 頭上血條：模型最高約 15.4 m，放在頭頂上方一點
            WorldHealthBarBuilder.Create(root.transform, new Vector3(0f, MinionHealthBarHeight, 0f), MinionHealthBarWidth, MinionHealthBarThickness);

            PeroroMinion minion = root.AddComponent<PeroroMinion>();
            SerializedObject minionSerialized = new SerializedObject(minion);
            minionSerialized.FindProperty("animator").objectReferenceValue = animator;
            minionSerialized.FindProperty("boltPrefab").objectReferenceValue = laserBoltPrefab;
            SerializedProperty muzzles = minionSerialized.FindProperty("muzzles");
            muzzles.arraySize = EyeBoneNames.Length;
            for (int i = 0; i < EyeBoneNames.Length; i++) muzzles.GetArrayElementAtIndex(i).objectReferenceValue = FindChild(model.transform, EyeBoneNames[i]);
            minionSerialized.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(root, MinionPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // 小 Peroro 的雷射子彈：沿用砲台子彈 (EnemyBullet_SM05) 拉長成光束狀，並多一種紅色 (None，護盾無法吸收)
    private static GameObject BuildLaserBolt()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(LaserBoltPrefabPath) == null) AssetDatabase.CopyAsset(SourceBulletPrefabPath, LaserBoltPrefabPath);

        GameObject root = PrefabUtility.LoadPrefabContents(LaserBoltPrefabPath);
        try
        {
            root.name = "Peroro_LaserBolt";
            root.transform.localScale = LaserBoltScale;

            Material redBody = GetOrCreateRedBodyMaterial();
            SerializedObject visual = new SerializedObject(root.GetComponent<BulletPolarityVisual>());
            SerializedProperty styles = visual.FindProperty("styles");
            int redIndex = -1;
            for (int i = 0; i < styles.arraySize; i++)
            {
                if (styles.GetArrayElementAtIndex(i).FindPropertyRelative("polarity").enumValueIndex == (int)BulletPolarity.None) redIndex = i;
            }
            if (redIndex < 0)
            {
                redIndex = styles.arraySize;
                styles.arraySize++;
            }

            SerializedProperty red = styles.GetArrayElementAtIndex(redIndex);
            red.FindPropertyRelative("polarity").enumValueIndex = (int)BulletPolarity.None;
            red.FindPropertyRelative("bodyMaterial").objectReferenceValue = redBody;
            red.FindPropertyRelative("glowColor").colorValue = RedColor;
            red.FindPropertyRelative("lightColor").colorValue = RedColor;
            visual.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(root, LaserBoltPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // 從藍色子彈材質複製一份改成紅色 (保留同樣的發光設定)
    private static Material GetOrCreateRedBodyMaterial()
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(RedBodyMaterialPath) == null) AssetDatabase.CopyAsset(BlueBodyMaterialPath, RedBodyMaterialPath);

        Material material = AssetDatabase.LoadAssetAtPath<Material>(RedBodyMaterialPath);
        material.SetColor("_BaseColor", RedColor);
        material.SetColor("_EmissionColor", RedColor * 2f);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        material.EnableKeyword("_EMISSION");
        EditorUtility.SetDirty(material);
        return material;
    }

    // Appear (從膠囊跳出來) → Idle ↔ Walk (Exs01 原地踏步，實際移動由程式控制)；
    // 任何狀態 → 攻擊三段 (Start → Ing → End → Idle)；任何狀態 → Death (只播一次)
    private static AnimatorController BuildMinionController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(MinionControllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(MinionControllerPath);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState child in machine.states) machine.RemoveState(child.state);
        foreach (AnimatorStateTransition transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);
        while (controller.parameters.Length > 0) controller.RemoveParameter(0);
        controller.AddParameter("IsWalking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("IsDead", AnimatorControllerParameterType.Bool);

        // 出場動畫播完要停在最後一格；循環版在切到 Idle 的過渡期間會繞回第一格
        AnimatorState appear = machine.AddState("Appear");
        appear.motion = AnimationClipCopies.CreateNonLoopingCopy(FindClip(MinionModelPath, "Perorozilla_Peroro_MiddleSize_Appear"), MinionAppearClipPath);
        AnimatorState idle = AddState(machine, "Idle", "Perorozilla_Peroro_MiddleSize_Normal_Idle");
        AnimatorState walk = AddState(machine, "Walk", "Perorozilla_Peroro_MiddleSize_Exs01");
        AnimatorState attackStart = AddState(machine, "AttackStart", "Perorozilla_Peroro_MiddleSize_Normal_Attack_Start");
        AnimatorState attackIng = AddState(machine, "AttackIng", "Perorozilla_Peroro_MiddleSize_Normal_Attack_Ing");
        AnimatorState attackEnd = AddState(machine, "AttackEnd", "Perorozilla_Peroro_MiddleSize_Normal_Attack_End");
        AnimatorState death = machine.AddState("Death");
        death.motion = AnimationClipCopies.CreateNonLoopingCopy(FindClip(MinionModelPath, "Perorozilla_Peroro_MiddleSize_Vital_Death"), MinionDeathClipPath);
        machine.defaultState = appear;

        AddExitTransition(appear, idle);
        AddTransition(idle.AddTransition(walk), 0.15f).AddCondition(AnimatorConditionMode.If, 0f, "IsWalking");
        AddTransition(walk.AddTransition(idle), 0.15f).AddCondition(AnimatorConditionMode.IfNot, 0f, "IsWalking");

        AnimatorStateTransition toAttack = AddTransition(machine.AddAnyStateTransition(attackStart), 0.1f);
        toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
        toAttack.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
        toAttack.canTransitionToSelf = false;
        AddExitTransition(attackStart, attackIng);
        AddExitTransition(attackIng, attackEnd);
        AddExitTransition(attackEnd, idle);

        AnimatorStateTransition toDeath = AddTransition(machine.AddAnyStateTransition(death), 0.1f);
        toDeath.AddCondition(AnimatorConditionMode.If, 0f, "IsDead");
        toDeath.canTransitionToSelf = false;

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static AnimatorState AddState(AnimatorStateMachine machine, string name, string clipName)
    {
        AnimatorState state = machine.AddState(name);
        state.motion = FindClip(MinionModelPath, clipName);
        return state;
    }

    private static AnimatorStateTransition AddTransition(AnimatorStateTransition transition, float duration)
    {
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        return transition;
    }

    // glb 內的動畫都是循環的，用離開時間在播完一次時接下一段
    private static void AddExitTransition(AnimatorState from, AnimatorState to)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = true;
        transition.exitTime = 0.95f;
        transition.hasFixedDuration = true;
        transition.duration = 0.1f;
    }


    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name) return child;
        }

        Debug.LogError("[PerorozillaSummonBuilder] 找不到骨骼 " + name);
        return null;
    }

    // 網格沿 Z 軸：上半殼在 -Z (殼頂 z = 0、開口 z = -ShellDepth)，下半殼在 +Z (開口 z = +ShellDepth)；
    // 繞 X 轉 -90 度讓網格的 Z 對應到 Y，再依 poleHeight 把殼頂移到球的上/下端
    private static Renderer CreatePart(Transform parent, string name, Mesh mesh, Material material, float poleHeight)
    {
        GameObject part = new GameObject(name);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = new Vector3(0f, poleHeight, 0f);
        part.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        return renderer;
    }

    private static Mesh FindMesh(string fileName)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(MeshFolder + fileName + ".glb"))
        {
            if (asset is Mesh mesh) return mesh;
        }

        Debug.LogError("[PerorozillaSummonBuilder] 找不到網格 " + fileName);
        return null;
    }

    // 白色塑膠質感；上半殼的顏色由 SummonCapsule 以 MaterialPropertyBlock 隨機覆寫
    private static Material GetOrCreateShellMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ShellMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, ShellMaterialPath);
        }

        material.SetColor("_BaseColor", new Color(0.97f, 0.97f, 0.97f));
        material.SetFloat("_Smoothness", 0.75f);
        material.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(material);
        return material;
    }

    // BA 的 glb 匯出時材質都是 metallicFactor = 1：全金屬材質幾乎不吃漫射光，整隻偏暗、燈光也打不亮。
    // glb 內的材質不能直接修改，所以複製一份 (金屬度 0) 存到 prefab 資料夾，再換到模型上
    public static void ApplyMatteMaterials(GameObject model, string prefix)
    {
        if (!AssetDatabase.IsValidFolder(MatteMaterialFolder)) AssetDatabase.CreateFolder(OutputFolder, "Materials");

        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material source = materials[i];
                if (source == null || !source.HasProperty(GltfMetallicProperty)) continue;
                materials[i] = GetOrCreateMatteCopy(source, MatteMaterialFolder + "/" + prefix + "_" + source.name + ".mat");
            }
            renderer.sharedMaterials = materials;
        }
    }

    private static Material GetOrCreateMatteCopy(Material source, string path)
    {
        // 已經是複製出來的材質 (重複執行時) 就直接沿用
        if (AssetDatabase.GetAssetPath(source) == path) return source;

        Material copy = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (copy == null)
        {
            copy = new Material(source);
            AssetDatabase.CreateAsset(copy, path);
        }
        else
        {
            copy.CopyPropertiesFromMaterial(source);
        }

        copy.SetFloat(GltfMetallicProperty, 0f);
        EditorUtility.SetDirty(copy);
        return copy;
    }

    // glb 內的動畫名稱帶有雜湊後綴，以前綴比對
    public static AnimationClip FindClip(string modelPath, string name)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
        {
            if (asset is AnimationClip clip && clip.name.StartsWith(name + "--")) return clip;
        }

        Debug.LogError("[PerorozillaSummonBuilder] 找不到動畫 " + name);
        return null;
    }
}
#endif
