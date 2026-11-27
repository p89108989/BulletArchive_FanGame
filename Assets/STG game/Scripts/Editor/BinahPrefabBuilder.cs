#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 建立第二隻 Boss「Binah」的 Prefab (Tools/STG/Build Boss Binah)：模型、動畫、血量、受擊閃白、死亡演出，
// 以及例行換位潑沙 (BinahSplashAttack，每 15 秒)、光炮特殊機制 (BinahCannonMechanic，血量每 25% 一次)、
// 共用的換位 (BinahReposition：Move_Start 潛入 / Move_End 竄出) 與沙堆 (BinahDuneField，最多 3 個)、程式頭部轉向 (BinahHeadAim)。
// Binah 的模型只有頭與一段身體 (從地面竄出的造型)，根物件放在地面高度，身體從原點往上長
public static class BinahPrefabBuilder
{
    private const string ModelPath = "Assets/STG game/resources/model/Boss_Binah/bundles/Assets/_MX/AddressableAsset/Character/Binah_Original/Binah_Original.glb";
    private const string OutputFolder = "Assets/STG game/resources/prefab";
    public const string PrefabPath = OutputFolder + "/Boss_Binah.prefab";
    private const string ControllerPath = OutputFolder + "/Boss_Binah.controller";
    private const string DeathClipCopyPath = OutputFolder + "/Boss_Binah_Death_Once.anim";

    private const string DeathClipName = "Binah_Original_Vital_Death";

    // 原始模型約 22 m 高；5 倍約 110 m，竄出時在場地外圍看得出巨大
    public const float ModelScale = 5f;
    private const int BossMaxHealth = 1000;

    // 受擊判定 (未縮放的模型座標)：頭與竄出地面的身體
    private static readonly Vector3 BodyColliderCenter = new Vector3(0f, 11f, 0f);
    private static readonly Vector3 BodyColliderSize = new Vector3(7f, 22f, 10f);

    // 退場：根物件在水面高度，整隻沉下去；用細緻水霧 (不用 Perorozilla 的卡通煙塵)，水霧本身已是 Binah 的大小
    private const float SinkDistance = 150f;
    private static readonly Vector3 SmokeCenterOffset = new Vector3(0f, 5f, 0f);
    private static readonly Vector3 SmokeAreaExtents = new Vector3(15f, 3f, 15f);
    private const float SmokeScale = 1f;

    // 光炮特殊機制：回到的中間位置 (= BossProfile 的生成位置) 與用到的骨骼
    private static readonly Vector3 CenterPosition = new Vector3(-50f, -5f, -15f);
    private const string NeckBone = "Bone_Neck";
    private const string HeadBone = "Bone_Head";
    private const string MuzzleBone = "Fire_01";
    // 張嘴：上顎 (帶眼睛與角的 Head_cap_01) 往上掀；下顎那兩根 JAW 骨骼轉了沒有明顯效果
    private static readonly string[] JawBones = { "Head_cap_01" };
    private static readonly float[] JawOpenAngles = { -35f };
    private static readonly Vector3 JawLocalAxis = new Vector3(0f, 0f, 1f);

    // 換位動畫的播放速度與對應的等待時間：
    // Move_Start 5.33 秒 ×1.5 倍速 → 約 3.6 秒整隻潛入；Move_End 4.67 秒 ×1.2 倍速 → 約 0.5 秒探出地面、再 3.2 秒回到 Idle
    private const float MoveStartSpeed = 1.5f;
    private const float MoveEndSpeed = 1.2f;
    private const float DiveDuration = 3.6f;
    private const float EmergeDelay = 0.5f;
    private const float SettleDuration = 3.2f;

    // 例行換位潑沙：可以換到的三個位置 (場地遠側中間、左側、右側，地板範圍 X -109 ~ 7.3、Z -9 ~ 88.5)，都面向場地
    private static readonly Vector3[] SpotPositions =
    {
        new Vector3(-50f, -5f, -15f),
        new Vector3(-128f, -5f, 40f),
        new Vector3(25f, -5f, 40f),
    };
    private static readonly float[] SpotYaws = { 0f, 90f, -90f };
    private const float SplashAttackInterval = 15f;

    // 普通攻擊 (吐能量球，沿用 Perorozilla 的 BossDebrisAttack)：Normal_Attack 4.67 秒 ×1.6 倍速 ≈ 2.9 秒，每 3 秒一次
    private const float NormalAttackSpeed = 1.6f;
    private const float NormalAttackInterval = 3f;
    private const float NormalAttackSpitDelay = 1f;
    private const float NormalAttackDuration = 2.9f;
    private const float NormalAttackFirstDelay = 4f;
    private const string EnergyBallStrikePrefabPath = "Assets/STG game/resources/prefab/AreaStrike.prefab";

    [MenuItem("Tools/STG/Build Boss Binah")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[BinahPrefabBuilder] 請先離開 Play Mode 再執行。");
            return;
        }

        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelAsset == null)
        {
            Debug.LogError("[BinahPrefabBuilder] 找不到模型 " + ModelPath);
            return;
        }

        WaterFxBuilder.BuildAll();
        GameObject dunePrefab = BinahCannonFxBuilder.BuildDunePrefab();
        AnimatorController controller = BuildController();
        GameObject root = new GameObject("Boss_Binah");
        try
        {
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            model.name = "Model";
            model.transform.SetParent(root.transform, false);
            model.transform.localScale = Vector3.one * ModelScale;
            model.layer = LayerMask.NameToLayer("Enemy");
            PerorozillaSummonBuilder.ApplyMatteMaterials(model, "Boss_Binah");

            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            // 模型很大，部分骨骼離開畫面時也要繼續動畫，避免被裁切後姿勢凍結
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Trigger：只給玩家攻擊判定用，不推擠玩家
            BoxCollider collider = model.AddComponent<BoxCollider>();
            collider.center = BodyColliderCenter;
            collider.size = BodyColliderSize;
            collider.isTrigger = true;

            EnemyHealth health = root.AddComponent<EnemyHealth>();
            root.AddComponent<EnemyHitFlash>();
            SerializedObject serialized = new SerializedObject(health);
            serialized.FindProperty("maxHealth").intValue = BossMaxHealth;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            BossDeathPerformance death = root.AddComponent<BossDeathPerformance>();
            SerializedObject deathSerialized = new SerializedObject(death);
            deathSerialized.FindProperty("animator").objectReferenceValue = animator;
            AnimationClip deathClip = FindClip(DeathClipName);
            if (deathClip != null) deathSerialized.FindProperty("deathAnimationDuration").floatValue = deathClip.length;
            deathSerialized.FindProperty("sinkDistance").floatValue = SinkDistance;
            deathSerialized.FindProperty("vanishSmokePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(WaterFxBuilder.FineSmokePrefabPath);
            deathSerialized.FindProperty("smokeCenterOffset").vector3Value = SmokeCenterOffset;
            deathSerialized.FindProperty("smokeAreaExtents").vector3Value = SmokeAreaExtents;
            deathSerialized.FindProperty("smokeScale").floatValue = SmokeScale;
            deathSerialized.ApplyModifiedPropertiesWithoutUndo();

            AddCannonMechanic(root, model.transform, animator, dunePrefab);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[BinahPrefabBuilder] 已建立 " + PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void AddCannonMechanic(GameObject root, Transform model, Animator animator, GameObject dunePrefab)
    {
        Transform muzzle = FindChild(model, MuzzleBone);

        BinahHeadAim headAim = root.AddComponent<BinahHeadAim>();
        SerializedObject aimSerialized = new SerializedObject(headAim);
        aimSerialized.FindProperty("neck").objectReferenceValue = FindChild(model, NeckBone);
        aimSerialized.FindProperty("head").objectReferenceValue = FindChild(model, HeadBone);
        aimSerialized.FindProperty("muzzle").objectReferenceValue = muzzle;
        SerializedProperty jaws = aimSerialized.FindProperty("jaws");
        jaws.arraySize = JawBones.Length;
        for (int i = 0; i < JawBones.Length; i++) jaws.GetArrayElementAtIndex(i).objectReferenceValue = FindChild(model, JawBones[i]);
        SerializedProperty jawAngles = aimSerialized.FindProperty("jawOpenAngles");
        jawAngles.arraySize = JawOpenAngles.Length;
        for (int i = 0; i < JawOpenAngles.Length; i++) jawAngles.GetArrayElementAtIndex(i).floatValue = JawOpenAngles[i];
        aimSerialized.FindProperty("jawLocalAxis").vector3Value = JawLocalAxis;
        aimSerialized.ApplyModifiedPropertiesWithoutUndo();

        BinahReposition reposition = root.AddComponent<BinahReposition>();
        SerializedObject repositionSerialized = new SerializedObject(reposition);
        repositionSerialized.FindProperty("animator").objectReferenceValue = animator;
        repositionSerialized.FindProperty("diveDuration").floatValue = DiveDuration;
        repositionSerialized.FindProperty("emergeDelay").floatValue = EmergeDelay;
        repositionSerialized.FindProperty("settleDuration").floatValue = SettleDuration;
        repositionSerialized.FindProperty("splashPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(WaterFxBuilder.SandSplashPrefabPath);
        repositionSerialized.ApplyModifiedPropertiesWithoutUndo();

        BinahDuneField duneField = root.AddComponent<BinahDuneField>();
        SerializedObject duneSerialized = new SerializedObject(duneField);
        duneSerialized.FindProperty("dunePrefab").objectReferenceValue = dunePrefab;
        duneSerialized.ApplyModifiedPropertiesWithoutUndo();

        BinahCannonMechanic mechanic = root.AddComponent<BinahCannonMechanic>();
        SerializedObject serialized = new SerializedObject(mechanic);
        serialized.FindProperty("animator").objectReferenceValue = animator;
        serialized.FindProperty("muzzle").objectReferenceValue = muzzle;
        serialized.FindProperty("headAim").objectReferenceValue = headAim;
        serialized.FindProperty("centerPosition").vector3Value = CenterPosition;
        serialized.FindProperty("reposition").objectReferenceValue = reposition;
        serialized.FindProperty("duneField").objectReferenceValue = duneField;
        serialized.FindProperty("quicksand").objectReferenceValue = BinahCannonFxBuilder.CreateQuicksand(root.transform, CenterPosition);
        serialized.FindProperty("chargeWarning").objectReferenceValue = BinahCannonFxBuilder.CreateWarning(root.transform, CenterPosition);
        serialized.FindProperty("beamEffect").objectReferenceValue = BinahCannonFxBuilder.CreateBeam(root.transform);
        serialized.FindProperty("chargeFill").objectReferenceValue = BinahCannonFxBuilder.CreateChargeFill(root.transform, CenterPosition);
        if (muzzle != null) serialized.FindProperty("chargeEffect").objectReferenceValue = BinahCannonFxBuilder.CreateChargeOrb(muzzle);
        serialized.FindProperty("chargeGlowScale").vector2Value = new Vector2(0.4f, 3f);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        BinahSplashAttack splashAttack = root.AddComponent<BinahSplashAttack>();
        SerializedObject attackSerialized = new SerializedObject(splashAttack);
        attackSerialized.FindProperty("reposition").objectReferenceValue = reposition;
        attackSerialized.FindProperty("mechanic").objectReferenceValue = mechanic;
        attackSerialized.FindProperty("duneField").objectReferenceValue = duneField;
        attackSerialized.FindProperty("interval").floatValue = SplashAttackInterval;
        SerializedProperty spots = attackSerialized.FindProperty("spots");
        spots.arraySize = SpotPositions.Length;
        for (int i = 0; i < SpotPositions.Length; i++)
        {
            SerializedProperty spot = spots.GetArrayElementAtIndex(i);
            spot.FindPropertyRelative("position").vector3Value = SpotPositions[i];
            spot.FindPropertyRelative("yaw").floatValue = SpotYaws[i];
        }

        attackSerialized.ApplyModifiedPropertiesWithoutUndo();

        AddNormalAttacks(root, muzzle, animator, headAim);
    }

    // 普通攻擊 (能量球) 與長條光炮；兩者與換位、光炮機制共用攻擊使用權 (BossAttackGate)，同一時間只有一個
    private static void AddNormalAttacks(GameObject root, Transform muzzle, Animator animator, BinahHeadAim headAim)
    {
        BossDebrisAttack energyBall = root.AddComponent<BossDebrisAttack>();
        SerializedObject ballSerialized = new SerializedObject(energyBall);
        ballSerialized.FindProperty("animator").objectReferenceValue = animator;
        ballSerialized.FindProperty("mouth").objectReferenceValue = muzzle;
        SerializedProperty projectiles = ballSerialized.FindProperty("debrisPrefabs");
        projectiles.arraySize = 1;
        projectiles.GetArrayElementAtIndex(0).objectReferenceValue = BinahAttackFxBuilder.BuildEnergyBall();
        ballSerialized.FindProperty("strikePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(EnergyBallStrikePrefabPath);
        ballSerialized.FindProperty("firstAttackDelay").floatValue = NormalAttackFirstDelay;
        ballSerialized.FindProperty("attackInterval").floatValue = NormalAttackInterval;
        ballSerialized.FindProperty("spitDelay").floatValue = NormalAttackSpitDelay;
        ballSerialized.FindProperty("attackDuration").floatValue = NormalAttackDuration;
        ballSerialized.ApplyModifiedPropertiesWithoutUndo();

        BinahLaneBeamAttack lane = root.AddComponent<BinahLaneBeamAttack>();
        SerializedObject laneSerialized = new SerializedObject(lane);
        laneSerialized.FindProperty("headAim").objectReferenceValue = headAim;
        laneSerialized.FindProperty("muzzle").objectReferenceValue = muzzle;
        laneSerialized.FindProperty("laneWarningFill").objectReferenceValue = BinahAttackFxBuilder.CreateLaneWarning(root.transform, out Transform laneWarning);
        laneSerialized.FindProperty("laneWarning").objectReferenceValue = laneWarning;
        laneSerialized.FindProperty("beamEffect").objectReferenceValue = BinahAttackFxBuilder.CreateLaneBeam(root.transform);
        laneSerialized.FindProperty("fireZonePrefab").objectReferenceValue = BinahAttackFxBuilder.BuildFireZone();
        laneSerialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name) return child;
        }

        Debug.LogError("[BinahPrefabBuilder] 找不到骨骼 " + name);
        return null;
    }

    // 目前只接 Idle 與死亡；其他動畫先放成狀態 (由觸發器切入、播完回 Idle)，之後設計攻擊時直接使用：
    // MoveStart / MoveIng / MoveEnd = 竄出 → 移動 → 鑽回 (推測)，Attack = 普通攻擊，Groggy = 暈眩
    private static AnimatorController BuildController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState child in machine.states) machine.RemoveState(child.state);
        while (controller.parameters.Length > 0) controller.RemoveParameter(0);
        foreach (AnimatorStateTransition anyTransition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(anyTransition);
        foreach (string trigger in new[] { "Attack", "MoveStart", "MoveEnd", "Groggy", "Death" })
        {
            controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
        }

        AnimatorState idle = AddState(machine, "Idle", "Binah_Original_Idle");
        machine.defaultState = idle;

        AnimatorState attack = AddState(machine, "Attack", "Binah_Original_Normal_Attack");
        attack.speed = NormalAttackSpeed;
        AddTriggerTransition(idle, attack, "Attack");
        AddExitTransition(attack, idle, 0.25f);

        AnimatorState moveStart = AddState(machine, "MoveStart", "Binah_Original_Move_Start");
        AnimatorState moveIng = AddState(machine, "MoveIng", "Binah_Original_Move_Ing");
        AnimatorState moveEnd = AddState(machine, "MoveEnd", "Binah_Original_Move_End");
        // 換位：原速太慢 (潛入 5.3 秒 + 竄出 4.7 秒)，加快播放；BinahReposition 的等待時間依這兩個速度設定
        moveStart.speed = MoveStartSpeed;
        moveEnd.speed = MoveEndSpeed;
        AddTriggerTransition(idle, moveStart, "MoveStart");
        AddExitTransition(moveStart, moveIng, 0.1f);
        AddTriggerTransition(moveIng, moveEnd, "MoveEnd");
        // 潛入動作還沒播完就要竄出時 (等待時間比動畫短) 也能直接接上
        AddTriggerTransition(moveStart, moveEnd, "MoveEnd");
        AddExitTransition(moveEnd, idle, 0.2f);

        AnimatorState groggy = AddState(machine, "Groggy", "Binah_Original_Vital_Groggy");
        AddTriggerTransition(idle, groggy, "Groggy");
        AddExitTransition(groggy, idle, 0.25f);

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

    private static AnimationClip FindClip(string name)
    {
        return PerorozillaSummonBuilder.FindClip(ModelPath, name);
    }
}
#endif
