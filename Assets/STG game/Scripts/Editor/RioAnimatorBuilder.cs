#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 一鍵建立 Rio 的 Animator Controller (BA 原始動畫，CH0331.glb)：
// Base 層：Idle / Run / Dash (由 PlayerMovement 驅動) + Death (IsDead，只播一次)。
// Attack 層：只蓋上半身與槍 (Avatar Mask)，雷射發射中播射擊循環，放開後播收槍再淡出，跑步時下半身照跑。
// glb 內的動畫都是循環且不能改設定，死亡動畫另存一份不循環的副本。可重複執行，會就地重建同一個 Controller
public static class RioAnimatorBuilder
{
    private const string ModelPath = "Assets/STG game/resources/model/CH0331_調月リオ/bundles/Assets/_MX/AddressableAsset/Character/CH0331/CH0331.glb";
    private const string OutputFolder = "Assets/STG game/resources/model/animation";
    private const string ControllerPath = OutputFolder + "/Rio_BA.controller";
    private const string DeathClipPath = OutputFolder + "/CH0331_Vital_Death_Once.anim";
    private const string MaskPath = OutputFolder + "/Rio_UpperBody.mask";

    // 上半身遮罩包含的骨骼：脊椎以上 (含雙手、頭) 與槍
    private static readonly string[] UpperBodyBones = { "Bip001 Spine", "Bip001_Weapon" };
    // 衝刺沒有專用動畫，用跑步加速播放
    private const float DashPlaybackSpeed = 1.8f;

    [MenuItem("Tools/STG/Build Rio Animator")]
    private static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[RioAnimatorBuilder] 請先離開 Play Mode 再執行。");
            return;
        }

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null)
        {
            Debug.LogError("[RioAnimatorBuilder] 找不到模型 " + ModelPath);
            return;
        }

        AnimationClip idle = FindClip("CH0331_Normal_Idle");
        AnimationClip move = FindClip("CH0331_Move_Ing");
        AnimationClip attack = FindClip("CH0331_Normal_Attack_Ing");
        AnimationClip attackEnd = FindClip("CH0331_Normal_Attack_End");
        AnimationClip death = CreateNonLoopingCopy(FindClip("CH0331_Vital_Death"), DeathClipPath);
        AvatarMask upperBody = CreateUpperBodyMask(model.transform);

        AnimatorController controller = GetOrCreateController();
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsDashing", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsAttacking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsDead", AnimatorControllerParameterType.Bool);

        BuildBaseLayer(controller.layers[0].stateMachine, idle, move, death);
        BuildAttackLayer(controller, upperBody, attack, attackEnd);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("[RioAnimatorBuilder] 已建立 " + ControllerPath);
    }

    private static void BuildBaseLayer(AnimatorStateMachine machine, AnimationClip idle, AnimationClip move, AnimationClip death)
    {
        AnimatorState idleState = machine.AddState("Idle");
        idleState.motion = idle;
        AnimatorState runState = machine.AddState("Run");
        runState.motion = move;
        AnimatorState dashState = machine.AddState("Dash");
        dashState.motion = move;
        dashState.speed = DashPlaybackSpeed;
        AnimatorState deathState = machine.AddState("Death");
        deathState.motion = death;
        machine.defaultState = idleState;

        AddTransition(idleState.AddTransition(runState), 0.1f).AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");
        AddTransition(runState.AddTransition(idleState), 0.1f).AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");

        AnimatorStateTransition toDash = AddTransition(machine.AddAnyStateTransition(dashState), 0.05f);
        toDash.AddCondition(AnimatorConditionMode.If, 0f, "IsDashing");
        toDash.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
        toDash.canTransitionToSelf = false;

        AnimatorStateTransition dashToRun = AddTransition(dashState.AddTransition(runState), 0.1f);
        dashToRun.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDashing");
        dashToRun.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");
        AnimatorStateTransition dashToIdle = AddTransition(dashState.AddTransition(idleState), 0.1f);
        dashToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDashing");
        dashToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");

        // 死亡從任何狀態切入且不再離開 (重新開始會重新載入場景)
        AnimatorStateTransition toDeath = AddTransition(machine.AddAnyStateTransition(deathState), 0.15f);
        toDeath.AddCondition(AnimatorConditionMode.If, 0f, "IsDead");
        toDeath.canTransitionToSelf = false;
    }

    // Empty (不覆蓋) → Attack 循環 → 放開後 AttackEnd 收槍 → Empty；死亡時立即淡出，讓死亡動畫完整顯示
    private static void BuildAttackLayer(AnimatorController controller, AvatarMask mask, AnimationClip attack, AnimationClip attackEnd)
    {
        AnimatorStateMachine machine = new AnimatorStateMachine { name = "Attack" };
        AssetDatabase.AddObjectToAsset(machine, controller);
        machine.hideFlags = HideFlags.HideInHierarchy;

        AnimatorState empty = machine.AddState("Empty");
        AnimatorState attackState = machine.AddState("Attack");
        attackState.motion = attack;
        AnimatorState endState = machine.AddState("AttackEnd");
        endState.motion = attackEnd;
        machine.defaultState = empty;

        AnimatorStateTransition startFiring = AddTransition(empty.AddTransition(attackState), 0.1f);
        startFiring.AddCondition(AnimatorConditionMode.If, 0f, "IsAttacking");
        startFiring.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
        AddTransition(attackState.AddTransition(endState), 0.1f).AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAttacking");
        AddTransition(endState.AddTransition(attackState), 0.1f).AddCondition(AnimatorConditionMode.If, 0f, "IsAttacking");

        AnimatorStateTransition endToEmpty = AddTransition(endState.AddTransition(empty), 0.2f);
        endToEmpty.hasExitTime = true;
        endToEmpty.exitTime = 0.8f;

        AnimatorStateTransition deathCancel = AddTransition(machine.AddAnyStateTransition(empty), 0.1f);
        deathCancel.AddCondition(AnimatorConditionMode.If, 0f, "IsDead");
        deathCancel.canTransitionToSelf = false;

        controller.AddLayer(new AnimatorControllerLayer
        {
            name = "Attack",
            stateMachine = machine,
            avatarMask = mask,
            defaultWeight = 1f,
            blendingMode = AnimatorLayerBlendingMode.Override,
        });
    }

    private static AnimatorStateTransition AddTransition(AnimatorStateTransition transition, float duration)
    {
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        return transition;
    }

    // 既有的 Controller 就地清空重建 (保留資產 GUID，場景上的引用不會斷)
    private static AnimatorController GetOrCreateController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) return AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        while (controller.layers.Length > 1) controller.RemoveLayer(controller.layers.Length - 1);
        while (controller.parameters.Length > 0) controller.RemoveParameter(0);

        AnimatorStateMachine baseMachine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState child in baseMachine.states) baseMachine.RemoveState(child.state);
        foreach (AnimatorStateTransition transition in baseMachine.anyStateTransitions) baseMachine.RemoveAnyStateTransition(transition);
        return controller;
    }

    // 遮罩的骨骼路徑要相對於 Animator 所在物件 (玩家根物件)，模型根節點 CH0331 掛在玩家底下，與 glb 匯入後的路徑一致
    private static AvatarMask CreateUpperBodyMask(Transform modelRoot)
    {
        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        if (mask == null)
        {
            mask = new AvatarMask();
            AssetDatabase.CreateAsset(mask, MaskPath);
        }

        mask.transformCount = 0;
        mask.AddTransformPath(modelRoot, true);
        for (int i = 0; i < mask.transformCount; i++)
        {
            mask.SetTransformActive(i, IsUpperBody(mask.GetTransformPath(i)));
        }

        EditorUtility.SetDirty(mask);
        return mask;
    }

    private static bool IsUpperBody(string path)
    {
        for (int i = 0; i < UpperBodyBones.Length; i++)
        {
            if (path.Contains("/" + UpperBodyBones[i])) return true;
        }
        return false;
    }

    private static AnimationClip CreateNonLoopingCopy(AnimationClip source, string path)
    {
        AnimationClip copy = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (copy == null)
        {
            copy = new AnimationClip();
            AssetDatabase.CreateAsset(copy, path);
        }

        EditorUtility.CopySerialized(source, copy);
        copy.name = System.IO.Path.GetFileNameWithoutExtension(path);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(copy);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(copy, settings);
        EditorUtility.SetDirty(copy);
        return copy;
    }

    // glb 內的動畫名稱帶有雜湊後綴 (例如 CH0331_Normal_Idle--79c8...)，以前綴比對
    private static AnimationClip FindClip(string name)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
        {
            if (asset is AnimationClip clip && clip.name.StartsWith(name + "--")) return clip;
        }

        Debug.LogError("[RioAnimatorBuilder] 找不到動畫 " + name);
        return null;
    }
}
#endif
