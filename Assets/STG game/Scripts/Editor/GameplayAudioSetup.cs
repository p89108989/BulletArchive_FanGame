#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// 戰鬥音效與角色語音的設定 (由 AudioSetupBuilder 呼叫)：
// 建立四個角色的 CharacterVoiceSet；設定砲台、戰車、小 Peroro、兩隻 Boss 的 Prefab；
// 設定戰鬥場景的玩家 (語音、大招聲)、砲台發射器、直升機；選角場景的角色語音。
// 音量偏小的 (槍聲、受擊、直升機…) 是依需求「聽得到就好」
public static class GameplayAudioSetup
{
    private const string VoiceFolder = "Assets/STG game/resources/Audio/Voices";
    private const string PrefabFolder = "Assets/STG game/resources/prefab/";
    private const string SelectionPath = "Assets/STG game/resources/prefab/CharacterSelect/GameSelection.asset";

    private static readonly string[] CharacterIds = { "CH0331", "CH0187", "CH0334", "CH0175" };

    // 受擊聲：連射時至少隔這麼久才再響
    private const float HurtInterval = 0.08f;
    // 直升機一波三顆飛彈依序爆炸，只響第一顆
    private const float MissileWaveInterval = 2f;

    // ---- 角色語音 ----

    public static CharacterVoiceSet[] GetOrCreateVoiceSets()
    {
        if (!AssetDatabase.IsValidFolder(VoiceFolder)) AssetDatabase.CreateFolder("Assets/STG game/resources/Audio", "Voices");

        CharacterVoiceSet[] sets = new CharacterVoiceSet[CharacterIds.Length];
        for (int i = 0; i < CharacterIds.Length; i++)
        {
            string id = CharacterIds[i];
            string lower = id.ToLowerInvariant();
            string path = VoiceFolder + "/Voice_" + id + ".asset";
            CharacterVoiceSet set = AssetDatabase.LoadAssetAtPath<CharacterVoiceSet>(path);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<CharacterVoiceSet>();
                AssetDatabase.CreateAsset(set, path);
            }

            SerializedObject serialized = new SerializedObject(set);
            serialized.FindProperty("characterId").stringValue = id;
            SetSound(serialized, "battleStart", lower + "_battle_in_1", 1f);
            SetSound(serialized, "dash", lower + "_battle_shout_3", 1f);
            SetSound(serialized, "move", lower + "_battle_move_2", 1f);
            SetSound(serialized, "victory", lower + "_battle_victory_1", 1f);
            SetSound(serialized, "select", lower + "_formation_select", 1f);
            SetSound(serialized, "defeat", lower + "_tactic_defeat_1", 1f);
            SetSound(serialized, "damage", lower + "_battle_damage_1", 1f);
            SetSound(serialized, "basicAttack", "SFX_Common_HG_Suppressed_01", 0.25f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);
            sets[i] = set;
        }

        AssetDatabase.SaveAssets();
        return sets;
    }

    // ---- Prefab ----

    public static void ApplyPrefabs()
    {
        EditPrefab("EnemyTurret", root =>
        {
            Configure(root.GetComponent<EnemyHealth>(), so =>
            {
                SetSound(so, "hurtSound", "SFX_Common_Hit_Mech_Short_01", 0.35f, HurtInterval);
                SetSound(so, "deathSound", "SFX_HeavyTank_Cannon_Explosion_01", 0.8f, 0.1f);
            });
        });

        EditPrefab("Tank", root =>
        {
            Configure(root.GetComponent<EnemyHealth>(), so => SetSound(so, "hurtSound", "SFX_Common_Hit_Mech_Short_01", 0.35f, HurtInterval));
            LoopingSound loop = GetOrAdd<LoopingSound>(root);
            Configure(loop, so =>
            {
                so.FindProperty("clip").objectReferenceValue = FindClip("SFX_HeavyTank_Common_Loop");
                so.FindProperty("volume").floatValue = 0.5f;
            });
            Configure(root.GetComponent<TankCharge>(), so => so.FindProperty("moveSound").objectReferenceValue = loop);
        });

        EditPrefab("Peroro_Minion", root =>
        {
            Configure(root.GetComponent<EnemyHealth>(), so =>
            {
                SetSound(so, "hurtSound", "SFX_Common_Hit_Mech_Short_01", 0.35f, HurtInterval);
                SetSound(so, "deathSound", "SFX_Perorozilla_Explosion_Splash_02_0", 0.8f);
            });
            Configure(root.GetComponent<PeroroMinion>(), so => SetSound(so, "boltSound", "SFX_Perorozilla_Laser", 0.5f, 0.05f));
        });

        EditPrefab("Boss_Binah", root =>
        {
            Configure(root.GetComponent<EnemyHealth>(), so => SetSound(so, "deathSound", "SFX_Binah_Death", 1f));
            Configure(root.GetComponent<BossDebrisAttack>(), so =>
            {
                SetSound(so, "attackSound", "SFX_Binah_NomalSkill_EXP_01", 0.8f);
                SetSound(so, "landSound", "SFX_Explosion_Grenade_L_B_01", 0.4f);
            });
            Configure(root.GetComponent<BinahReposition>(), so =>
            {
                SetSound(so, "moveSound", "SFX_Binah_Dying", 0.8f);
                SetSound(so, "splashSound", "SE_SandBlow_03", 0.8f);
            });
            Configure(root.GetComponent<BinahLaneBeamAttack>(), so =>
            {
                SetSound(so, "chargeSound", "SFX_Binah_EX1_02", 1f);
                SetSound(so, "fireSound", "SFX_Binah_EX3_03", 1f);
            });
            Configure(root.GetComponent<BinahCannonMechanic>(), so =>
            {
                SetSound(so, "chargeSound", "SFX_Binah_EX1_02", 1f);
                SetSound(so, "fireSound", "SFX_Binah_EX3_03", 1f);
            });
            Configure(GetOrAdd<PlaySoundOnStart>(root), so => SetSound(so, "sound", "SFX_Binah_Start", 1f));
        });

        EditPrefab("Boss_Perorozilla", root =>
        {
            Configure(root.GetComponent<BossLaserAttack>(), so =>
            {
                SetSound(so, "chargeSound", "SFX_Perorozilla_EX1_Beam_01", 1f);
                SetSound(so, "fireSound", "SFX_Perorozilla_EX1_Beam_02", 1f);
            });
            Configure(root.GetComponent<BossSummonAttack>(), so => SetSound(so, "summonSound", "SFX_Perorozilla_EX3_0", 1f));
            Configure(root.GetComponent<BossDebrisAttack>(), so =>
            {
                SetSound(so, "attackSound", "SFX_Perorozilla_EX2", 0.8f);
                SetSound(so, "landSound", "SE_Blow_01", 0.4f);
            });
        });
    }

    // ---- 場景 (在目前的 Active 場景操作) ----

    public static void ApplyBattleScene()
    {
        CharacterVoiceSet[] sets = GetOrCreateVoiceSets();
        PlayerLaser laser = Find<PlayerLaser>();
        if (laser != null)
        {
            Configure(laser, so => SetSound(so, "burstSound", "SE_Laser_02", 1f));
            PlayerVoice voice = GetOrAdd<PlayerVoice>(laser.gameObject);
            Configure(voice, so =>
            {
                SetArray(so.FindProperty("voiceSets"), sets);
                so.FindProperty("gameSelection").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameSelection>(SelectionPath);
                so.FindProperty("battleFlow").objectReferenceValue = Find<BattleFlow>();
            });
        }

        Configure(Find<EnemyBulletSpawner>(), so => SetSound(so, "fireSound", "SE_Laser_01", 0.35f, 0.05f));

        ApacheGunship apache = Find<ApacheGunship>();
        if (apache == null) return;

        LoopingSound rotor = GetOrAdd<LoopingSound>(apache.gameObject);
        Configure(rotor, so =>
        {
            so.FindProperty("clip").objectReferenceValue = FindClip("SFX_Common_Helicopter");
            so.FindProperty("volume").floatValue = 0.3f;
        });
        Configure(apache, so =>
        {
            so.FindProperty("rotorSound").objectReferenceValue = rotor;
            SetSound(so, "missileLaunchSound", "SFX_Common_Heli_Shot_01", 0.4f);
            SetSound(so, "missileExplosionSound", "SFX_Explosion_Blue_01", 0.8f, MissileWaveInterval);
        });
        Configure(apache.GetComponent<EnemyHealth>(), so => SetSound(so, "hurtSound", "SFX_Common_Hit_Mech_Short_01", 0.35f, HurtInterval));
    }

    public static void ApplyCharacterSelectScene()
    {
        CharacterVoiceSet[] sets = GetOrCreateVoiceSets();
        Configure(Find<CharacterSelectScreen>(), so => SetArray(so.FindProperty("voiceSets"), sets));
    }

    // ---- 小工具 ----

    private static void EditPrefab(string name, System.Action<GameObject> edit)
    {
        string path = PrefabFolder + name + ".prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            edit(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void Configure(Object target, System.Action<SerializedObject> edit)
    {
        if (target == null) return;

        SerializedObject serialized = new SerializedObject(target);
        edit(serialized);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSound(SerializedObject serialized, string field, string clipName, float volume, float minInterval = 0f)
    {
        SerializedProperty sound = serialized.FindProperty(field);
        if (sound == null)
        {
            Debug.LogWarning("[GameplayAudioSetup] " + serialized.targetObject.GetType().Name + " 沒有欄位 " + field);
            return;
        }

        sound.FindPropertyRelative("clip").objectReferenceValue = FindClip(clipName);
        sound.FindPropertyRelative("volume").floatValue = volume;
        sound.FindPropertyRelative("minInterval").floatValue = minInterval;
    }

    // 依檔名 (不含副檔名) 在整個專案找音檔
    private static AudioClip FindClip(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:AudioClip"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        Debug.LogWarning("[GameplayAudioSetup] 找不到音檔 " + name);
        return null;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static T Find<T>() where T : Component
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }

        return null;
    }

    private static void SetArray(SerializedProperty property, Object[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
#endif
