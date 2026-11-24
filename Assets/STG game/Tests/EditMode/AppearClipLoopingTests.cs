using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// glb 內的動畫一律是循環的：出場 (從空中掉下來) 動畫若循環，播完切回待機的過渡期間會繞回第一格 (又在空中)，
// 看起來像再落地一次。出場動畫必須用不循環的版本，播完停在最後一格
public class AppearClipLoopingTests
{
    [TestCase("Assets/STG game/resources/prefab/Tank.controller")]
    [TestCase("Assets/STG game/resources/prefab/Peroro_Minion.controller")]
    public void AppearState_UsesNonLoopingClip(string controllerPath)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        Assert.IsNotNull(controller, controllerPath);

        AnimationClip appear = null;
        foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
        {
            if (child.state.name == "Appear") appear = child.state.motion as AnimationClip;
        }

        Assert.IsNotNull(appear, "Appear state clip");
        Assert.IsFalse(appear.isLooping, appear.name + " must not loop");
    }
}
