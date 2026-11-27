#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 一鍵建立 Boss_Perorozilla Prefab：Perorozilla_Original 模型放大 10 倍 + Idle 動畫 + 血量 + 雷射可命中的碰撞體。
// glb 匯入時根物件已有 Animator，動畫路徑以它為基準，所以 Controller 直接掛在模型根物件上。可重複執行，會覆寫既有的 Prefab
public static class PerorozillaPrefabBuilder
{
    private const string ModelPath = "Assets/STG game/resources/model/Boss_Perorozilla/bundles/Assets/_MX/AddressableAsset/Character/Perorozilla_Original/Perorozilla_Original.glb";
    private const string OutputFolder = "Assets/STG game/resources/prefab";
    private const string PrefabPath = OutputFolder + "/Boss_Perorozilla.prefab";
    private const string ControllerPath = OutputFolder + "/Boss_Perorozilla.controller";
    // glb 的動畫都會循環；死亡動畫要播一次停在最後一格，另存不循環的副本
    private const string DeathClipCopyPath = OutputFolder + "/Boss_Perorozilla_Death_Once.anim";
    private const string DeathClipName = "Perorozilla_Original_Vital_Death";
    // Boss 下沉消失時的煙霧：沿用碎石落地的卡通煙塵 (貼圖與材質在 AreaStrike 資料夾)
    private const string VanishSmokePrefabPath = OutputFolder + "/Boss_VanishSmoke.prefab";
    private const string AreaStrikeFolder = OutputFolder + "/AreaStrike";
    private const float VanishSmokeSimulationSpeed = 0.5f;
    private static readonly Color VanishSmokeFadeColor = new Color(0.9f, 0.92f, 0.95f);

    private const float ModelScale = 10f;
    private const float NormalAttackSpeed = 1.35f;
    private const int BossMaxHealth = 1000;
    // 聚光燈：位於場地上空 (相對 Boss 根物件)，打向臉部
    private static readonly Vector3 SpotlightLocalPosition = new Vector3(0f, 170f, 105f);
    private static readonly Vector3 SpotlightLocalAim = new Vector3(0f, 140f, 40f);
    private const float SpotlightIntensity = 4000f;
    // 召喚膠囊的落點警示與落地傷害沿用圈形範圍攻擊
    private const string LandingStrikePrefabPath = OutputFolder + "/AreaStrike.prefab";
    // 碎石落地只有煙塵、沒有火光
    private const string DebrisStrikePrefabPath = OutputFolder + "/AreaStrike_Dust.prefab";
    // 雙眼的特效掛點骨骼，雷射從這裡射出
    private static readonly string[] EyeBoneNames = { "FX_Eye_L_DM", "FX_Eye_R_DM" };
    // 嘴巴前端的發射點骨骼，召喚膠囊從這裡吐出
    private const string MouthBoneName = "fire_01";
    // 身體碰撞體 (模型本地座標，未乘 ModelScale)。不能用 Renderer 外框：蒙皮網格的外框很鬆 (長約 30)，
    // 放大後會蓋住整個場地。深度刻意收窄，讓碰撞體停在場地後方，玩家不會站在碰撞體裡 (雷射從內部射不到)
    private static readonly Vector3 BodyColliderCenter = new Vector3(0f, 11.5f, 0f);
    private static readonly Vector3 BodyColliderSize = new Vector3(15f, 23f, 10f);

    [MenuItem("Tools/STG/Build Boss Perorozilla")]
    private static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[PerorozillaPrefabBuilder] 請先離開 Play Mode 再執行。");
            return;
        }

        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelAsset == null)
        {
            Debug.LogError("[PerorozillaPrefabBuilder] 找不到模型 " + ModelPath);
            return;
        }

        GameObject capsulePrefab = PerorozillaSummonBuilder.BuildCapsule();
        GameObject minionPrefab = PerorozillaSummonBuilder.BuildMinion();
        GameObject[] debrisPrefabs = PerorozillaAttackBuilder.BuildDebris();
        BossBeamVisual beamPrefab = PerorozillaBeamBuilder.BuildBeam();
        GroundWarningCircle laserWarningPrefab = PerorozillaBeamBuilder.BuildWarning();
        AnimatorController controller = BuildController();
        GameObject root = new GameObject("Boss_Perorozilla");
        try
        {
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            model.name = "Model";
            model.transform.SetParent(root.transform, false);
            model.transform.localScale = Vector3.one * ModelScale;
            model.layer = LayerMask.NameToLayer("Enemy");
            PerorozillaSummonBuilder.ApplyMatteMaterials(model, "Boss_Perorozilla");
            CreateSpotlight(root.transform);

            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            // 模型很大，部分骨骼離開畫面時也要繼續動畫，避免被裁切後姿勢凍結
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Trigger：只給雷射判定用，不推擠玩家
            BoxCollider collider = model.AddComponent<BoxCollider>();
            collider.center = BodyColliderCenter;
            collider.size = BodyColliderSize;
            collider.isTrigger = true;

            EnemyHealth health = root.AddComponent<EnemyHealth>();
            root.AddComponent<EnemyHitFlash>();
            SerializedObject serialized = new SerializedObject(health);
            serialized.FindProperty("maxHealth").intValue = BossMaxHealth;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            BossSummonAttack summon = root.AddComponent<BossSummonAttack>();
            SerializedObject summonSerialized = new SerializedObject(summon);
            summonSerialized.FindProperty("animator").objectReferenceValue = animator;
            summonSerialized.FindProperty("mouth").objectReferenceValue = FindChild(model.transform, MouthBoneName);
            summonSerialized.FindProperty("capsulePrefab").objectReferenceValue = capsulePrefab;
            summonSerialized.FindProperty("minionPrefab").objectReferenceValue = minionPrefab;
            summonSerialized.FindProperty("landingStrikePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(LandingStrikePrefabPath);
            summonSerialized.ApplyModifiedPropertiesWithoutUndo();

            BossDebrisAttack debris = root.AddComponent<BossDebrisAttack>();
            SerializedObject debrisSerialized = new SerializedObject(debris);
            debrisSerialized.FindProperty("animator").objectReferenceValue = animator;
            debrisSerialized.FindProperty("mouth").objectReferenceValue = FindChild(model.transform, MouthBoneName);
            debrisSerialized.FindProperty("strikePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(DebrisStrikePrefabPath);
            SerializedProperty debrisList = debrisSerialized.FindProperty("debrisPrefabs");
            debrisList.arraySize = debrisPrefabs.Length;
            for (int i = 0; i < debrisPrefabs.Length; i++) debrisList.GetArrayElementAtIndex(i).objectReferenceValue = debrisPrefabs[i];
            debrisSerialized.ApplyModifiedPropertiesWithoutUndo();

            BossLaserAttack laser = root.AddComponent<BossLaserAttack>();
            SerializedObject laserSerialized = new SerializedObject(laser);
            laserSerialized.FindProperty("animator").objectReferenceValue = animator;
            laserSerialized.FindProperty("beamPrefab").objectReferenceValue = beamPrefab;
            laserSerialized.FindProperty("warningPrefab").objectReferenceValue = laserWarningPrefab;
            SerializedProperty origins = laserSerialized.FindProperty("beamOrigins");
            origins.arraySize = EyeBoneNames.Length;
            for (int i = 0; i < EyeBoneNames.Length; i++) origins.GetArrayElementAtIndex(i).objectReferenceValue = FindChild(model.transform, EyeBoneNames[i]);
            laserSerialized.ApplyModifiedPropertiesWithoutUndo();

            BossDeathPerformance death = root.AddComponent<BossDeathPerformance>();
            SerializedObject deathSerialized = new SerializedObject(death);
            deathSerialized.FindProperty("animator").objectReferenceValue = animator;
            AnimationClip deathClip = FindClip(DeathClipName);
            if (deathClip != null) deathSerialized.FindProperty("deathAnimationDuration").floatValue = deathClip.length;
            deathSerialized.FindProperty("vanishSmokePrefab").objectReferenceValue = BuildVanishSmoke();
            deathSerialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[PerorozillaPrefabBuilder] 已建立 " + PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // Idle → 攻擊 (各自的觸發器，播完回 Idle)：
    // Summon = Exs03 召喚；Attack = 普通攻擊 Start → Ing → End；Laser = Exs01 雷射；
    // Death = Vital_Death，任何狀態都能直接切過去 (攻擊到一半被擊倒也會倒下)，播完停在最後一格
    private static AnimatorController BuildController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState child in machine.states) machine.RemoveState(child.state);
        while (controller.parameters.Length > 0) controller.RemoveParameter(0);
        controller.AddParameter("Summon", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Laser", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);
        foreach (AnimatorStateTransition anyTransition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(anyTransition);

        AnimatorState idle = AddState(machine, "Idle", "Perorozilla_Original_Normal_Idle");
        machine.defaultState = idle;

        AnimatorState summon = AddState(machine, "Summon", "Perorozilla_Original_Exs03");
        AddTriggerTransition(idle, summon, "Summon");
        AddExitTransition(summon, idle, 0.25f);

        AnimatorState attackStart = AddState(machine, "AttackStart", "Perorozilla_Original_Normal_Attack_Start");
        AnimatorState attackIng = AddState(machine, "AttackIng", "Perorozilla_Original_Normal_Attack_Ing");
        AnimatorState attackEnd = AddState(machine, "AttackEnd", "Perorozilla_Original_Normal_Attack_End");
        // 普通攻擊每 3 秒一次：原本整段約 4 秒，加快到約 3 秒 (BossDebrisAttack 的吐出時間與長度已依此設定)
        attackStart.speed = NormalAttackSpeed;
        attackIng.speed = NormalAttackSpeed;
        attackEnd.speed = NormalAttackSpeed;
        AddTriggerTransition(idle, attackStart, "Attack");
        AddExitTransition(attackStart, attackIng, 0.1f);
        AddExitTransition(attackIng, attackEnd, 0.1f);
        AddExitTransition(attackEnd, idle, 0.2f);

        AnimatorState laserState = AddState(machine, "Laser", "Perorozilla_Original_Exs01");
        AddTriggerTransition(idle, laserState, "Laser");
        AddExitTransition(laserState, idle, 0.25f);

        AnimationClip deathSource = FindClip(DeathClipName);
        if (deathSource != null)
        {
            AnimatorState death = machine.AddState("Death");
            death.motion = AnimationClipCopies.CreateNonLoopingCopy(deathSource, DeathClipCopyPath);
            AnimatorStateTransition toDeath = machine.AddAnyStateTransition(death);
            toDeath.hasExitTime = false;
            toDeath.hasFixedDuration = true;
            toDeath.duration = 0.2f;
            toDeath.canTransitionToSelf = false;
            toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Death");
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static AnimatorState AddState(AnimatorStateMachine machine, string name, string clipName)
    {
        AnimatorState state = machine.AddState(name);
        state.motion = FindClip(clipName);
        return state;
    }

    private static void AddTriggerTransition(AnimatorState from, AnimatorState to, string trigger)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0.2f;
        transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
    }

    // glb 內的動畫都是循環的，用離開時間在播完一次時接下一段
    private static void AddExitTransition(AnimatorState from, AnimatorState to, float duration)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = true;
        transition.exitTime = 0.97f;
        transition.hasFixedDuration = true;
        transition.duration = duration;
    }

    // 從場地上方打向 Boss 臉部的聚光燈 (跟著 Boss 一起生成/消失，只在 Boss 戰出現)。
    // 位置以 Boss 根物件為基準；URP 點光源亮度隨距離平方衰減，約 70 m 外打光需要數千的強度
    private static void CreateSpotlight(Transform root)
    {
        GameObject lightObject = new GameObject("Spotlight");
        lightObject.transform.SetParent(root, false);
        lightObject.transform.localPosition = SpotlightLocalPosition;
        lightObject.transform.localRotation = Quaternion.LookRotation(SpotlightLocalAim - SpotlightLocalPosition, Vector3.up);

        Light spot = lightObject.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = new Color(1f, 0.97f, 0.92f);
        spot.intensity = SpotlightIntensity;
        spot.range = 200f;
        spot.spotAngle = 70f;
        spot.innerSpotAngle = 40f;
        spot.shadows = LightShadows.None;
    }

    private static GameObject BuildVanishSmoke()
    {
        GameObject root = new GameObject("Boss_VanishSmoke");
        try
        {
            AreaStrikeExplosionBuilder.BuildDust(root.transform, AreaStrikeFolder);
            // 落地煙塵一下就散；Boss 消失要遮住整隻慢慢沉下去，放慢播放讓煙霧停留久一點
            foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = system.main;
                main.simulationSpeed = VanishSmokeSimulationSpeed;
                // 改成白色煙霧 (原本是米色煙塵)；貼圖內建的卡通描邊與陰影保留
                ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
                colorOverLifetime.enabled = true;
                Gradient white = new Gradient();
                white.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(VanishSmokeFadeColor, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                colorOverLifetime.color = new ParticleSystem.MinMaxGradient(white);
            }
            return PrefabUtility.SaveAsPrefabAsset(root, VanishSmokePrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static AnimationClip FindClip(string name)
    {
        return PerorozillaSummonBuilder.FindClip(ModelPath, name);
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name) return child;
        }

        Debug.LogError("[PerorozillaPrefabBuilder] 找不到骨骼 " + name);
        return null;
    }
}
#endif
