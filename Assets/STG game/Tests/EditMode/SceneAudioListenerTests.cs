using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 每個遊戲場景都要正好有一個啟用中的 AudioListener：沒有就完全聽不到聲音，多個 Unity 會一直警告
public class SceneAudioListenerTests
{
    [TestCase("Assets/STG game/Scenes/Title.unity")]
    [TestCase("Assets/STG game/Scenes/CharacterSelect.unity")]
    [TestCase("Assets/STG game/Scenes/mian game.unity")]
    public void Scene_HasExactlyOneActiveAudioListener(string path)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        bool wasOpen = scene.IsValid() && scene.isLoaded;
        if (!wasOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

        try
        {
            int active = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
                {
                    if (listener.enabled && listener.gameObject.activeInHierarchy) active++;
                }
            }

            Assert.AreEqual(1, active, path);
        }
        finally
        {
            if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
