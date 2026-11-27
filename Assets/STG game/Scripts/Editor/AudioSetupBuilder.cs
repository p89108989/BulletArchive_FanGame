#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 把 BGM 與 UI 音效接到三個場景 (Tools/STG/Setup Audio)，各場景的建置工具也會在最後呼叫：
// 開始畫面 / 選角畫面放 SceneMusic；選角的每個 Boss 選項指定戰鬥 BGM；戰鬥場景的 BattleFlow 指定勝利 BGM、START / WARNING 音效；
// 三個場景都放 UiClickSounds (所有按鈕的點擊音效)。
// 選單執行時：未開啟的場景開來改完存檔；已開啟且有未存修改的場景只套用不存檔 (避免存到使用者還沒決定的修改)
public static class AudioSetupBuilder
{
    private const string AudioFolder = "Assets/STG game/resources/Audio/";
    private const string TitleMusic = AudioFolder + "Theme_114.ogg";
    private const string CharacterSelectMusic = AudioFolder + "Theme_16.ogg";
    private const string VictoryMusic = AudioFolder + "Theme_23.ogg";
    private const string BinahMusic = AudioFolder + "Theme_24.ogg";
    private const string EndlessMusic = AudioFolder + "Theme_74.ogg";
    private const string PerorozillaMusic = AudioFolder + "Theme_98.ogg";
    private const string HoverCraftMusic = AudioFolder + "Theme_121.ogg";
    private const string ClickSound = AudioFolder + "UI_Login.ogg";
    private const string StartSound = AudioFolder + "UI_START_01.ogg";
    private const string WarningSound = AudioFolder + "UI_Alarm.ogg";
    private const string AudioObjectName = "SceneAudio";

    private const string TitleScenePath = "Assets/STG game/Scenes/Title.unity";
    private const string CharacterSelectScenePath = "Assets/STG game/Scenes/CharacterSelect.unity";
    private const string BattleScenePath = "Assets/STG game/Scenes/mian game.unity";

    [MenuItem("Tools/STG/Setup Audio")]
    private static void SetupAll()
    {
        if (EditorApplication.isPlaying) return;

        GameplayAudioSetup.ApplyPrefabs();
        Apply(TitleScenePath, ApplyTitle);
        Apply(CharacterSelectScenePath, ApplyCharacterSelect);
        Apply(BattleScenePath, ApplyBattle);
    }

    private static void Apply(string path, System.Action apply)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        bool wasOpen = scene.IsValid() && scene.isLoaded;
        bool hadChanges = wasOpen && scene.isDirty;
        Scene previousActive = SceneManager.GetActiveScene();
        if (!wasOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

        SceneManager.SetActiveScene(scene);
        apply();
        EditorSceneManager.MarkSceneDirty(scene);
        if (hadChanges) Debug.LogWarning("[AudioSetupBuilder] " + scene.name + " 有未儲存的修改，音效已套用但沒有存檔，請自行存檔");
        else EditorSceneManager.SaveScene(scene);

        if (previousActive.IsValid() && previousActive != scene) SceneManager.SetActiveScene(previousActive);
        if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
    }

    // 以下三個在「目前的 Active 場景」操作，給各場景建置工具在存檔前呼叫
    public static void ApplyTitle()
    {
        SetSceneAudio(Load(TitleMusic));
    }

    public static void ApplyCharacterSelect()
    {
        SetSceneAudio(Load(CharacterSelectMusic));
        GameplayAudioSetup.ApplyCharacterSelectScene();
        CharacterSelectScreen screen = FindInActiveScene<CharacterSelectScreen>();
        if (screen == null) return;

        SerializedObject serialized = new SerializedObject(screen);
        SerializedProperty options = serialized.FindProperty("bossOptions");
        for (int i = 0; i < options.arraySize; i++)
        {
            SerializedProperty option = options.GetArrayElementAtIndex(i);
            option.FindPropertyRelative("battleMusic").objectReferenceValue = MusicFor(option);
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void ApplyBattle()
    {
        SetSceneAudio(null);
        GameplayAudioSetup.ApplyBattleScene();
        BattleFlow flow = FindInActiveScene<BattleFlow>();
        if (flow == null)
        {
            Debug.LogWarning("[AudioSetupBuilder] 戰鬥場景找不到 BattleFlow，請先執行 Tools/STG/Build Battle Flow");
            return;
        }

        SerializedObject serialized = new SerializedObject(flow);
        // 直接開戰鬥場景時打的是預設 Boss (Binah)
        serialized.FindProperty("defaultBattleMusic").objectReferenceValue = Load(BinahMusic);
        serialized.FindProperty("victoryMusic").objectReferenceValue = Load(VictoryMusic);
        serialized.FindProperty("startSound").objectReferenceValue = Load(StartSound);
        serialized.FindProperty("warningSound").objectReferenceValue = Load(WarningSound);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // 依選項：無盡模式 → 無盡 BGM，否則看名稱
    private static AudioClip MusicFor(SerializedProperty option)
    {
        if (option.FindPropertyRelative("loop").boolValue) return Load(EndlessMusic);

        string label = option.FindPropertyRelative("label").stringValue;
        if (label.Contains("Binah")) return Load(BinahMusic);
        if (label.Contains("Perorozilla")) return Load(PerorozillaMusic);
        if (label.Contains("HoverCraft")) return Load(HoverCraftMusic);
        return null;
    }

    // 場景裡放一個 SceneAudio 物件：點擊音效 + (有的話) 場景 BGM；並確保有一個收音的 AudioListener
    private static void SetSceneAudio(AudioClip music)
    {
        EnsureAudioListener();

        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == AudioObjectName) Object.DestroyImmediate(root);
        }

        GameObject go = new GameObject(AudioObjectName);
        SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());
        SetReference(go.AddComponent<UiClickSounds>(), "clickSound", Load(ClickSound));
        if (music != null) SetReference(go.AddComponent<SceneMusic>(), "music", music);
    }

    // 沒有啟用的 AudioListener 就完全聽不到聲音：掛到場景中啟用的相機上 (已經有就不動，避免出現兩個)
    private static void EnsureAudioListener()
    {
        Camera target = null;
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
            {
                if (listener.enabled && listener.gameObject.activeInHierarchy) return;
            }

            foreach (Camera camera in root.GetComponentsInChildren<Camera>())
            {
                if (target == null && camera.enabled) target = camera;
            }
        }

        if (target == null)
        {
            Debug.LogWarning("[AudioSetupBuilder] " + SceneManager.GetActiveScene().name + " 找不到可以掛 AudioListener 的相機");
            return;
        }

        AudioListener existing = target.GetComponent<AudioListener>();
        if (existing != null) existing.enabled = true;
        else target.gameObject.AddComponent<AudioListener>();
    }

    private static T FindInActiveScene<T>() where T : Component
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }

        return null;
    }

    private static AudioClip Load(string path)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null) Debug.LogWarning("[AudioSetupBuilder] 找不到音檔 " + path);
        return clip;
    }

    private static void SetReference(Object target, string field, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
