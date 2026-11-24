#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// glb 匯入的動畫一律是循環的且無法修改匯入設定：需要「只播一次、停在最後一格」的動畫 (出場、死亡) 時，
// 另存一份不循環的副本。可重複呼叫，已存在的副本會以來源動畫重新覆寫
public static class AnimationClipCopies
{
    public static AnimationClip CreateNonLoopingCopy(AnimationClip source, string path)
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
}
#endif
