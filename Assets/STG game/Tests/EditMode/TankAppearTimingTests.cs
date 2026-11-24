using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// 戰車的落地煙塵與傷害要在 Appear 動畫「第一次觸地」的瞬間觸發；
// 若設定的落地時間比動畫晚，畫面上會先看到戰車落地、過一下才揚起煙塵，像是落地了兩次
public class TankAppearTimingTests
{
    private const string TankPrefabPath = "Assets/STG game/resources/prefab/Tank.prefab";
    private const string ModelPath = "Assets/STG game/resources/model/Enemy_HelmetTank_Cannon/bundles/Assets/_MX/AddressableAsset/Character/HeavyTank01_Helmet_Cannon/HeavyTank01_Helmet_Cannon.glb";
    // 車體離地高度 (模型本地座標) 低於此值視為觸地
    private const float GroundTolerance = 0.05f;
    // 允許的誤差 (秒)，約 3 幀
    private const float TimingTolerance = 0.05f;

    [Test]
    public void AppearLandTime_MatchesClipTouchdown()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TankPrefabPath);
        float configured = new SerializedObject(prefab.GetComponent<TankCharge>()).FindProperty("appearLandTime").floatValue;

        Assert.AreEqual(FindClipTouchdown(), configured, TimingTolerance);
    }

    private static float FindClipTouchdown()
    {
        AnimationClip clip = null;
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
        {
            if (asset is AnimationClip candidate && candidate.name.StartsWith("HeavyTank01_Helmet_Cannon_Appear--")) clip = candidate;
        }
        Assert.IsNotNull(clip, "Appear clip");

        GameObject model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
        try
        {
            Transform mover = null;
            foreach (Transform child in model.GetComponentsInChildren<Transform>()) if (child.name == "Tank_Move") mover = child;
            Assert.IsNotNull(mover, "Tank_Move bone");

            for (float time = 0f; time <= clip.length; time += 0.01f)
            {
                clip.SampleAnimation(model, time);
                if (mover.localPosition.y < GroundTolerance) return time;
            }

            Assert.Fail("Appear clip never touches the ground");
            return 0f;
        }
        finally
        {
            Object.DestroyImmediate(model);
        }
    }
}
