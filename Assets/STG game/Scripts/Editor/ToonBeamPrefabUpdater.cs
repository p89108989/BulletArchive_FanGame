#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// 把戰鬥中的 Boss 光束換成卡通風格，但不整個重建 Boss prefab (重建會蓋掉其他調整)：
// Perorozilla 雷射的外觀本來就是獨立的 Boss_Beam prefab，直接重建它；
// Binah 長條光炮在 Boss_Binah prefab 裡面，只把 LaneBeam 底下的圓柱換掉 (LaneBeam 物件本身保留，攻擊腳本的引用不會斷)
public static class ToonBeamPrefabUpdater
{
    private const string LaneBeamName = "LaneBeam";

    [MenuItem("Tools/STG/Apply Toon Beams To Boss Prefabs")]
    public static void Apply()
    {
        PerorozillaBeamBuilder.BuildBeam();

        GameObject binah = PrefabUtility.LoadPrefabContents(BinahPrefabBuilder.PrefabPath);
        try
        {
            Transform lane = FindChild(binah.transform, LaneBeamName);
            if (lane == null)
            {
                Debug.LogError("[ToonBeamPrefabUpdater] Boss_Binah 裡找不到 " + LaneBeamName);
                return;
            }

            for (int i = lane.childCount - 1; i >= 0; i--) Object.DestroyImmediate(lane.GetChild(i).gameObject);
            BinahAttackFxBuilder.AddToonLaneBeam(lane);
            PrefabUtility.SaveAsPrefabAsset(binah, BinahPrefabBuilder.PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(binah);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[ToonBeamPrefabUpdater] 已把 Boss_Beam 與 Boss_Binah 的光炮換成卡通風格");
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name) return child;
        }

        return null;
    }
}
#endif
