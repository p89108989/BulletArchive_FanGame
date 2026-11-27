#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 產生開始畫面 (Title.unity)：左邊 UI (遊戲名稱、start / 教學 / 離開遊戲)，右邊即時 3D 主視覺：
// リオ 在前景滑剷 (跪姿射擊定格 + 沙塵)、後方 Perorozilla 雙眼雷射、Binah 光炮、最後面 HoverCraft 上的 ワカモ 往前指。
// 模型都用 glb 本體 + 專用 AnimatorController (不用戰鬥 prefab，上面的血量、攻擊流程會自己跑起來)。
// 會切換並儲存場景，所以開著的場景有未存的修改時不執行；可重複執行
public static class TitleSceneBuilder
{
    private const string OutputFolder = "Assets/STG game/resources/prefab/Title";
    private const string MouthFolder = OutputFolder + "/Mouth";
    private const string MaterialFolder = OutputFolder + "/Materials";
    private const string ModelRoot = "Assets/STG game/resources/model/";
    private const string RioPath = ModelRoot + "CH0331_調月リオ/bundles/Assets/_MX/AddressableAsset/Character/CH0331/CH0331.glb";
    private const string WakamoPath = ModelRoot + "CH0175_狐坂ワカモ/bundles/Assets/_MX/AddressableAsset/Character/CH0175/CH0175.glb";
    private const string PerorozillaPath = ModelRoot + "Boss_Perorozilla/bundles/Assets/_MX/AddressableAsset/Character/Perorozilla_Original/Perorozilla_Original.glb";
    private const string BinahPath = ModelRoot + "Boss_Binah/bundles/Assets/_MX/AddressableAsset/Character/Binah_Original/Binah_Original.glb";
    private const string HoverCraftPath = ModelRoot + "Boss_HoverCraft/bundles/Assets/_MX/AddressableAsset/Character/Wakamo_HoverCraft_Original/Wakamo_HoverCraft_Original.glb";
    private const string BinahPrefabPath = "Assets/STG game/resources/prefab/Boss_Binah.prefab";
    private const string SkyMeshPath = "Assets/STG game/resources/skybox/SM_Skybox.FBX";
    private const string SkyMaterialPath = "Assets/STG game/resources/skybox/Materials/MI_Skybox_v3_Inst.mat";
    private const string NextSceneName = "CharacterSelect";
    // 待機時的嘴型 (與選角場景相同)
    private const int MouthCode = 401;

    // ---- 鏡頭：放低、略微仰視，讓後方的 Boss 有壓迫感 ----
    private static readonly Vector3 CameraPosition = new Vector3(0f, 4f, 0f);
    private const float CameraPitch = -6f;
    private const float CameraFov = 45f;

    // ---- 構圖 (世界座標；鏡頭在原點往 +Z 看，左側約 38% 被 UI 面板蓋住)。數值是在場景中實際擺過、截圖確認的 ----
    private struct Placement
    {
        public Vector3 position;
        public float yaw;
        public float scale;
    }

    // リオ：Kneel_Attack_Ing 第 0 格 (一腳伸直、一腳跪、槍舉在胸前)，身體往後仰、伸直的腳再拉直 → 滑剷。
    // 位置與外層朝向是使用者在場景中調好的；高度由程式算 (模型最低點貼地)。yaw 是模型相對外層的角度
    private static readonly Placement RioPlacement = new Placement { position = new Vector3(7.42f, 0f, 17.33f), yaw = 250f, scale = 10f };
    private const float RioRootYaw = 309.05f;
    private const string RioPose = "CH0331_Kneel_Attack_Ing";
    private const float RioPoseTime = 0f;
    private const float RioLean = -28f;
    // 身體後仰後視線會朝上，頭往下壓讓她看向前方
    private const float RioHeadDown = 22f;
    // 伸直的那條腿 (膝蓋彎曲較少的一邊) 再拉直多少 (0 = 不動，1 = 完全打直)
    private const float RioLegStraighten = 0.85f;
    // Boss 拉近，壓迫感比較大 (位置由使用者在場景中調整)
    private static readonly Placement PerorozillaPlacement = new Placement { position = new Vector3(-0.91f, 0f, 46.09f), yaw = 160f, scale = 1f };
    // Binah 的模型有一半在地面下 (從沙裡鑽出來)，抬高才看得到身體
    private static readonly Placement BinahPlacement = new Placement { position = new Vector3(28.76f, 12f, 40.82f), yaw = 228.7f, scale = 1.6f };
    // HoverCraft 在兩隻 Boss 中間的後上方，船頭仰起 (像要壓下來)
    private static readonly Placement HoverCraftPlacement = new Placement { position = new Vector3(16.57f, 3.17f, 56.53f), yaw = 165f, scale = 3.4f };
    private const float HoverCraftBowPitch = -18f;
    // ワカモ：Exs 約 0.9 秒 (單手把槍往前伸直指出去)，站在 HoverCraft 船頭甲板上 (原本的位置在駕駛艙後面，從正面看會被擋住)。
    // 位置/角度是相對船身 (船頭朝鏡頭，位置由使用者在場景中調整)，放大一點在遠處才看得清楚
    private const string WakamoPose = "CH0175_Exs";
    private const float WakamoPoseTime = 0.9f;
    private static readonly Vector3 WakamoDeckPosition = new Vector3(1.344f, 1.929f, 4.661f);
    private const float WakamoYaw = 10f;
    private const float WakamoScale = 2.2f;
    // 這一格的傘被拋到頭頂很高的地方，看起來像浮在空中的雜物，隱藏
    private static readonly string[] WakamoHiddenParts = { "CH0175_Umbrella" };
    // HoverCraft 模型裡附的 ワカモ 沒有貼圖 (全灰)，隱藏後換成 CH0175 本體
    private static readonly string[] HoverCraftHiddenParts = { "CH0175_Body", "CH0175_Umbrella", "CH0175_Weapon" };

    // 雷射/光炮落點 (世界 X 相對 リオ，Z 為絕對深度)：兩道光束在 リオ 身後交叉，從側面看才有長度 (直直打向鏡頭會縮成一團光)
    private static readonly Vector3 LaserTargetOffset = new Vector3(16f, 0f, 38f);
    private static readonly Vector3 CannonTargetOffset = new Vector3(-14f, 0f, 34f);

    // ---- Boss 定格 (整張主視覺是靜止的，模型不播動畫；HoverCraft 維持預設姿勢 = 擺放 ワカモ 時看到的樣子) ----
    private const string PerorozillaPose = "Perorozilla_Original_Normal_Attack_Ing";
    private const float PerorozillaPoseTime = 0f;

    // ---- 卡通光束 (STG/ToonBeam，材質與配色見 ToonBeamMaterials)；發射口與落點各一顆光球 ----
    private const float LaserWidth = 1.5f;
    private const float CannonWidth = 3f;
    // 戰鬥中光束的粗細 (公尺)：開始畫面的光束比較細，波動波長、雷電鋸齒依這個比例縮小，看起來才跟戰鬥中一樣
    // (Perorozilla 雷射 9 × 光管比例 0.7；Binah 光炮 BinahLaneBeamAttack.beamWidth 12)
    private const float BattleLaserWidth = 6.3f;
    private const float BattleCannonWidth = 12f;
    // 光球大小 (光束寬度的倍數)；落點光球壓扁貼地
    private const float MuzzleBallScale = 1.6f;
    private const float ImpactBallScale = 2.6f;
    private const float ImpactBallFlatten = 0.45f;

    // ---- 開場慢動作 (TitleIntroSequence)：所有演出共用同一條越來越慢的進度，最後一起定格 ----
    private const float IntroDuration = 4f;
    private const float IntroSlowDown = 3f;
    // 光束開場時已經伸出一半，進度到 BeamHitAt 時碰地，之後落點爆炸慢慢長大
    private const float BeamStartFraction = 0.5f;
    private const float BeamHitAt = 0.55f;
    // 落點爆炸定格時的大小 (公尺；停在「剛要炸開」)
    private const float LaserExplosionSize = 7f;
    private const float CannonExplosionSize = 10f;
    // リオ 從現在位置後方滑進來的距離
    private const float RioSlideDistance = 3f;

    // ---- 飛彈 (StrikeMissile 的外觀網格)：從 Boss 附近交叉飛向鏡頭。位置用畫面座標 (x, y 為 0~1，z 為離鏡頭的距離) ----
    private const string MissilePrefabPath = "Assets/STG game/resources/prefab/StrikeMissile.prefab";
    private const string MissileVisual = "Visual_Red";
    // 終點在 リオ 後方 (離鏡頭約 23 m)，放大才有「快要飛出鏡頭」的大小
    private const float MissileScale = 8.5f;
    private struct MissileFlight
    {
        public Vector3 start;
        public Vector3 end;
    }
    private static readonly MissileFlight[] Missiles =
    {
        // 終點都在 リオ (離鏡頭約 17 m) 的後方，飛彈不會蓋到她
        // Perorozilla 臉旁 → リオ 右後方
        new MissileFlight { start = new Vector3(0.51f, 0.67f, 44f), end = new Vector3(0.82f, 0.36f, 24f) },
        // Binah 頭旁 → リオ 左後方
        new MissileFlight { start = new Vector3(0.77f, 0.64f, 40f), end = new Vector3(0.47f, 0.4f, 22f) },
    };

    // リオ 舉槍：雙臂連同槍往下轉，讓槍管變水平、高度跟眼睛差不多
    private const float RioAimMaxDown = 60f;

    // ---- 地面與光線 ----
    private const float GroundSize = 60f;
    private static readonly Color SandColor = new Color(0.93f, 0.84f, 0.66f);
    private static readonly Quaternion SunRotation = Quaternion.Euler(40f, -25f, 0f);
    private const float SunIntensity = 1.1f;
    private static readonly Color SkyAmbient = new Color(0.78f, 0.82f, 0.9f);
    private static readonly Color EquatorAmbient = new Color(0.62f, 0.6f, 0.58f);
    private static readonly Color GroundAmbient = new Color(0.45f, 0.4f, 0.35f);
    private const float SkyScale = 0.003f;

    // ---- 左側 UI (設計稿：主要文字 #233E65、主題藍 #4A9DF5、面板 #DCEEFF→白、按鈕 #F5FAFF、重點黃 #FFEC59、按鈕陰影 #8DAFD5) ----
    // 遊戲名稱：「Bullet」深藍、「Archive」主題藍 (同一行)；下方中文名稱，面板最下方註明非官方作品
    private const string GameTitleRich = "Bullet <color=#5EA3F5>Archive</color>";
    private const string GameSubtitle = "彈 幕 檔 案";
    private const string FanGameNote = "A  B L U E   A R C H I V E   F A N   G A M E";
    private static readonly Color LogoBlue = new Color32(0x5E, 0xA3, 0xF5, 0xFF);
    private static readonly Color LogoSubText = new Color32(0x3E, 0x5A, 0x86, 0xFF);
    private static readonly Color FooterText = new Color32(0x6F, 0x86, 0xAE, 0xFF);
    private static readonly Color TextNavy = new Color32(0x23, 0x3E, 0x65, 0xFF);
    private static readonly Color ThemeBlue = new Color32(0x4A, 0x9D, 0xF5, 0xFF);
    private static readonly Color PanelTop = new Color32(0xDC, 0xEE, 0xFF, 0xFF);
    private static readonly Color PanelBottom = new Color32(0xF8, 0xFB, 0xFF, 0xFF);
    private static readonly Color ButtonBackground = new Color32(0xF5, 0xFA, 0xFF, 0xFF);
    private static readonly Color AccentYellow = new Color32(0xFF, 0xEC, 0x59, 0xFF);
    private static readonly Color ButtonShadow = new Color32(0x8D, 0xAF, 0xD5, 0xFF);
    private static readonly Color SubText = new Color32(0x4E, 0x6E, 0x98, 0xFF);
    private const float PanelWidth = 760f;
    private const float PanelSlant = 12f;
    // 按鈕 (1920×1080 參考解析度，從畫面左上角算)
    private const float ButtonLeft = 100f;
    private const float ButtonWidth = 580f;
    private const float ButtonSlant = 34f;
    private const float StartButtonTop = 480f;
    private const float StartButtonHeight = 126f;
    private const float TutorialButtonTop = 640f;
    private const float QuitButtonTop = 785f;
    private const float ButtonHeight = 112f;
    // 左邊 UI 進場 (秒)：慢動作開始後多久才出現、每個元件淡入多久、下一個元件晚多久
    private const float MenuIntroDelay = 0.6f;
    private const float MenuIntroItemDuration = 0.6f;
    private const float MenuIntroStagger = 0.25f;

    [MenuItem("Tools/STG/Build Title")]
    public static void Build()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (!SceneManager.GetSceneAt(i).isDirty) continue;

            Debug.LogWarning("[TitleSceneBuilder] 場景 " + SceneManager.GetSceneAt(i).name + " 有未儲存的修改，請先存檔再執行。");
            return;
        }

        CharacterSelectBuilder.EnsureFolder(MouthFolder);
        CharacterSelectBuilder.EnsureFolder(MaterialFolder);

        // 切換場景會卸載沒被參照的資產，所以資產都在切換之後才載入
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Camera camera = CreateCamera();
        CreateLighting();
        CreateSky();
        CreateGround();
        CreateEventSystem();

        System.Collections.Generic.List<MonoBehaviour> intro = new System.Collections.Generic.List<MonoBehaviour>();
        Transform rio = CreateRio(intro);
        CreatePerorozilla(rio, intro);
        CreateBinah(rio, intro);
        CreateHoverCraft();
        CreateMissiles(camera, intro);
        CreateIntroSequence(intro);

        Font font = AssetDatabase.LoadAssetAtPath<Font>(CharacterSelectBuilder.FontPath);
        CreateMenu(font);

        EditorSceneManager.SaveScene(scene, CharacterSelectBuilder.TitleScenePath);
        CharacterSelectBuilder.UpdateBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("[TitleSceneBuilder] 已產生 " + CharacterSelectBuilder.TitleScenePath);
    }

    private static Camera CreateCamera()
    {
        GameObject go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        Camera camera = go.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.62f, 0.8f, 0.95f);
        camera.fieldOfView = CameraFov;
        camera.aspect = 16f / 9f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 1000f;
        go.transform.SetPositionAndRotation(CameraPosition, Quaternion.Euler(CameraPitch, 0f, 0f));
        return camera;
    }

    // 主光從鏡頭這側斜上方打 (角色、Boss 正面受光)；三色環境光補亮背光面
    private static void CreateLighting()
    {
        Light sun = new GameObject("Directional Light").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = SunIntensity;
        sun.color = new Color(1f, 0.97f, 0.92f);
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = SunRotation;
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = SkyAmbient;
        RenderSettings.ambientEquatorColor = EquatorAmbient;
        RenderSettings.ambientGroundColor = GroundAmbient;
    }

    // 戰鬥場景同一個天空球 (白天貼圖)，以鏡頭為中心
    private static void CreateSky()
    {
        GameObject mesh = AssetDatabase.LoadAssetAtPath<GameObject>(SkyMeshPath);
        if (mesh == null)
        {
            Debug.LogWarning("[TitleSceneBuilder] 找不到天空球 " + SkyMeshPath);
            return;
        }

        GameObject sky = (GameObject)PrefabUtility.InstantiatePrefab(mesh);
        sky.name = "Skybox";
        sky.transform.position = CameraPosition;
        sky.transform.localScale = Vector3.one * SkyScale;
        Material material = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
        foreach (Renderer renderer in sky.GetComponentsInChildren<Renderer>())
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    // 不透明的沙地 (Binah 流沙的沙子材質是半透明的，單獨用會透出天空變成藍色)
    private static void CreateGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        Object.DestroyImmediate(ground.GetComponent<Collider>());
        ground.transform.position = new Vector3(0f, 0f, GroundSize * 5f - 10f);
        ground.transform.localScale = new Vector3(GroundSize, 1f, GroundSize);

        string path = OutputFolder + "/Title_Sand.mat";
        Material sand = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (sand == null)
        {
            sand = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(sand, path);
        }

        sand.SetColor("_BaseColor", SandColor);
        sand.SetFloat("_Smoothness", 0.1f);
        EditorUtility.SetDirty(sand);
        ground.GetComponent<Renderer>().sharedMaterial = sand;
    }

    private static void CreateEventSystem()
    {
        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    private static GameObject Spawn(string path, string name, Placement placement)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            Debug.LogError("[TitleSceneBuilder] 找不到模型 " + path);
            return null;
        }

        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        model.name = name;
        model.transform.SetPositionAndRotation(placement.position, Quaternion.Euler(0f, placement.yaw, 0f));
        model.transform.localScale = Vector3.one * placement.scale;
        return model;
    }

    // 開場慢動作總控；場景存檔時每個演出元件都停在最後一格 (編輯模式看到的就是定格畫面)，執行時從第一格開始
    private static void CreateIntroSequence(System.Collections.Generic.List<MonoBehaviour> parts)
    {
        foreach (MonoBehaviour part in parts) ((ITitleIntroPart)part).SetProgress(1f);

        GameObject go = new GameObject("TitleIntro");
        TitleIntroSequence sequence = go.AddComponent<TitleIntroSequence>();
        SerializedObject serialized = new SerializedObject(sequence);
        serialized.FindProperty("duration").floatValue = IntroDuration;
        serialized.FindProperty("slowDownPower").floatValue = IntroSlowDown;
        SerializedProperty list = serialized.FindProperty("parts");
        list.arraySize = parts.Count;
        for (int i = 0; i < parts.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = parts[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static IntroMover AddMover(GameObject target, Vector3 from, Vector3 to)
    {
        IntroMover mover = target.AddComponent<IntroMover>();
        SerializedObject serialized = new SerializedObject(mover);
        serialized.FindProperty("fromPosition").vector3Value = from;
        serialized.FindProperty("toPosition").vector3Value = to;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return mover;
    }

    private static Transform CreateRio(System.Collections.Generic.List<MonoBehaviour> intro)
    {
        // 外層負責位置與傾斜，模型本身的 Animator 只管姿勢
        GameObject root = new GameObject("Rio");
        root.transform.SetPositionAndRotation(RioPlacement.position, Quaternion.Euler(0f, RioRootYaw, 0f));
        GameObject model = Spawn(RioPath, "CH0331", RioPlacement);
        model.transform.SetParent(root.transform, true);
        CharacterMouthBaker.ApplyTo(model, MouthCode, MouthFolder);
        NonMetalMaterials.ApplyTo(model, MaterialFolder);
        BakePose(model, RioPath, RioPose, RioPoseTime);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(0f, RioPlacement.yaw, 0f) * Quaternion.Euler(RioLean, 0f, 0f);
        StraightenExtendedLeg(model.transform);
        LowerHead(model.transform);
        AimGunAtEyeLevel(model.transform);
        SnapToGround(root.transform, model);

        ParticleSystem dust = CreateSlideDust(root.transform, model.transform);
        IntroParticles slowDust = dust.gameObject.AddComponent<IntroParticles>();
        SerializedObject dustSettings = new SerializedObject(slowDust);
        dustSettings.FindProperty("particles").objectReferenceValue = dust;
        dustSettings.ApplyModifiedPropertiesWithoutUndo();
        intro.Add(slowDust);

        // 從身後一點點滑進現在的位置
        Vector3 back = -Vector3.ProjectOnPlane(model.transform.forward, Vector3.up).normalized;
        intro.Add(AddMover(root, root.transform.position + back * RioSlideDistance, root.transform.position));
        return root.transform;
    }

    // 雙臂與槍 (槍骨架掛在 Bip001 下，不會跟著手動) 一起繞肩膀往下轉，直到槍管水平；轉完槍口大約落在眼睛高度
    private static void AimGunAtEyeLevel(Transform model)
    {
        Transform leftArm = FindChild(model, "Bip001 L UpperArm");
        Transform rightArm = FindChild(model, "Bip001 R UpperArm");
        Transform weapon = FindChild(model, "Bip001_Weapon");
        Transform muzzle = FindChild(model, "fire_01");
        Transform grip = FindChild(model, "fire_02");
        if (leftArm == null || rightArm == null || weapon == null || muzzle == null || grip == null) return;

        Vector3 barrel = muzzle.position - grip.position;
        Vector3 flat = Vector3.ProjectOnPlane(barrel, Vector3.up);
        if (flat.sqrMagnitude < 0.0001f) return;

        // 槍管往上翹多少度，就往下轉多少度 (繞「上方 × 槍管水平方向」的軸，往上翹時角度為負)
        Vector3 axis = Vector3.Cross(Vector3.up, flat).normalized;
        float upAngle = Mathf.Min(-Vector3.SignedAngle(flat, barrel, axis), RioAimMaxDown);
        if (upAngle <= 0f) return;

        Vector3 pivot = (leftArm.position + rightArm.position) * 0.5f;
        Quaternion down = Quaternion.AngleAxis(upAngle, axis);
        foreach (Transform part in new[] { leftArm, rightArm, weapon })
        {
            part.SetPositionAndRotation(pivot + down * (part.position - pivot), down * part.rotation);
        }
    }

    // 膝蓋彎得比較少的那條腿是伸出去的腿：先把小腿往大腿方向轉 (腳更直)，
    // 再轉大腿讓腳跟落到跪地那條腿的高度 (身體後仰時伸出的腳會翹起來，滑剷時腳應該貼著地面)
    private static void StraightenExtendedLeg(Transform model)
    {
        Transform[] left = FindLeg(model, "L");
        Transform[] right = FindLeg(model, "R");
        if (left == null || right == null) return;

        Transform[] extended = KneeBend(left) <= KneeBend(right) ? left : right;
        Transform[] kneeling = extended == left ? right : left;
        Transform thigh = extended[0];
        Transform calf = extended[1];
        Transform foot = extended[2];

        Quaternion straight = Quaternion.FromToRotation(foot.position - calf.position, calf.position - thigh.position);
        calf.rotation = Quaternion.Slerp(Quaternion.identity, straight, RioLegStraighten) * calf.rotation;

        float groundY = Mathf.Min(kneeling[1].position.y, kneeling[2].position.y);
        Vector3 leg = foot.position - thigh.position;
        float drop = Mathf.Min(thigh.position.y - groundY, leg.magnitude * 0.95f);
        Vector3 ahead = Vector3.ProjectOnPlane(leg, Vector3.up).normalized;
        Vector3 target = ahead * Mathf.Sqrt(leg.sqrMagnitude - drop * drop) + Vector3.down * drop;
        thigh.rotation = Quaternion.FromToRotation(leg, target) * thigh.rotation;
    }

    // 繞著身體左右的軸把頭往前低 (正角度 = 下巴往下)
    private static void LowerHead(Transform model)
    {
        Transform head = FindChild(model, "Bip001 Head");
        if (head == null) return;

        head.rotation = Quaternion.AngleAxis(RioHeadDown, model.right) * head.rotation;
    }

    private static Transform[] FindLeg(Transform model, string side)
    {
        Transform thigh = FindChild(model, "Bip001 " + side + " Thigh");
        Transform calf = FindChild(model, "Bip001 " + side + " Calf");
        Transform foot = FindChild(model, "Bip001 " + side + " Foot");
        return thigh != null && calf != null && foot != null ? new[] { thigh, calf, foot } : null;
    }

    private static float KneeBend(Transform[] leg)
    {
        return Vector3.Angle(leg[1].position - leg[0].position, leg[2].position - leg[1].position);
    }

    // 模型目前姿勢的最低點對齊地面 (y = 0)：用蒙皮後的網格算，跪地的膝蓋、伸出的腳跟才會剛好貼地
    private static void SnapToGround(Transform root, GameObject model)
    {
        float lowest = float.MaxValue;
        Mesh baked = new Mesh();
        foreach (SkinnedMeshRenderer skinned in model.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            skinned.BakeMesh(baked, true);
            Matrix4x4 toWorld = skinned.transform.localToWorldMatrix;
            foreach (Vector3 vertex in baked.vertices) lowest = Mathf.Min(lowest, toWorld.MultiplyPoint3x4(vertex).y);
        }

        Object.DestroyImmediate(baked);
        if (lowest == float.MaxValue) return;

        root.position += Vector3.down * lowest;
    }

    // 腳下揚起的沙塵：從 リオ 身後往後上方噴，表現往前滑的速度感
    // (Binah 流沙的沙塵粒子尺寸是整片流沙用的，太大，所以另外做小的)
    private static ParticleSystem CreateSlideDust(Transform parent, Transform model)
    {
        GameObject go = new GameObject("SlideDust");
        go.transform.SetParent(parent, false);
        Vector3 back = -Vector3.ProjectOnPlane(model.forward, Vector3.up).normalized;
        go.transform.position = parent.position + back * 2.5f + Vector3.up * 0.3f;
        go.transform.rotation = Quaternion.LookRotation(back, Vector3.up) * Quaternion.Euler(-50f, 0f, 0f);
        ParticleSystem dust = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = dust.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 3f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(0.9f, 0.78f, 0.58f, 0.7f);
        main.gravityModifier = 0.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;
        ParticleSystem.EmissionModule emission = dust.emission;
        emission.rateOverTime = 30f;
        ParticleSystem.ShapeModule shape = dust.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 25f;
        shape.radius = 1.2f;
        ParticleSystem.ColorOverLifetimeModule fade = dust.colorOverLifetime;
        fade.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        ParticleSystem.SizeOverLifetimeModule grow = dust.sizeOverLifetime;
        grow.enabled = true;
        grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));

        ParticleSystem source = FindInPrefab<ParticleSystem>(BinahPrefabPath, "SandDust");
        if (source != null) go.GetComponent<ParticleSystemRenderer>().sharedMaterial = source.GetComponent<ParticleSystemRenderer>().sharedMaterial;
        return dust;
    }

    private static void CreatePerorozilla(Transform rio, System.Collections.Generic.List<MonoBehaviour> intro)
    {
        GameObject model = Spawn(PerorozillaPath, "Perorozilla", PerorozillaPlacement);
        if (model == null) return;

        NonMetalMaterials.ApplyTo(model, MaterialFolder);
        BakePose(model, PerorozillaPath, PerorozillaPose, PerorozillaPoseTime);
        Place(model.transform, PerorozillaPlacement);

        Vector3 target = new Vector3(rio.position.x + LaserTargetOffset.x, 0f, LaserTargetOffset.z);
        // 戰鬥版雷射的波動 (一節節鼓起往前流)，波長與速度依光束粗細等比例縮小
        float waveScale = LaserWidth / BattleLaserWidth;
        ToonBeamMaterials.Wave battleWave = ToonBeamMaterials.BattleLaserWave;
        ToonBeamMaterials.Wave wave = new ToonBeamMaterials.Wave { amplitude = battleWave.amplitude, length = battleWave.length * waveScale, speed = battleWave.speed * waveScale };
        Material material = ToonBeamMaterials.GetOrCreate("Title_Laser", ToonBeamMaterials.Laser, ToonBeamMaterials.AxisY, wave: wave);
        // 波動跟著開場慢動作放慢、定格時停住 (IntroShaderTime)
        material.SetFloat("_UseCustomTime", 1f);
        Material ball = ToonBeamMaterials.GetOrCreate("Title_LaserBall", ToonBeamMaterials.Laser, ToonBeamMaterials.NoAxis);
        // 兩道雷射打在同一點，共用一個爆炸
        Transform explosion = CreateExplosion("LaserExplosion", target);
        foreach (string eye in new[] { "FX_Eye_L_DM", "FX_Eye_R_DM" })
        {
            Transform origin = FindChild(model.transform, eye);
            if (origin == null) continue;

            IntroBeam beam = CreateToonBeam("Laser_" + eye, origin.position, target, LaserWidth, material, ball, explosion, LaserExplosionSize, null);
            intro.Add(beam);
            intro.Add(beam.GetComponent<IntroShaderTime>());
        }
    }

    private static void CreateBinah(Transform rio, System.Collections.Generic.List<MonoBehaviour> intro)
    {
        GameObject model = Spawn(BinahPath, "Binah", BinahPlacement);
        if (model == null) return;

        NonMetalMaterials.ApplyTo(model, MaterialFolder);
        // 維持預設姿勢 (位置是對著這個姿勢擺的；攻擊動畫會讓她往前探，頭跑出畫面)
        Animator animator = model.GetComponent<Animator>();
        if (animator != null) Object.DestroyImmediate(animator);

        Transform muzzle = FindChild(model.transform, "Fire_01");
        if (muzzle == null) return;

        Vector3 target = new Vector3(rio.position.x + CannonTargetOffset.x, 0f, CannonTargetOffset.z);
        Material material = ToonBeamMaterials.GetOrCreate("Title_Cannon", ToonBeamMaterials.Fire, ToonBeamMaterials.AxisY);
        Material ball = ToonBeamMaterials.GetOrCreate("Title_CannonBall", ToonBeamMaterials.Fire, ToonBeamMaterials.NoAxis);
        IntroBeam cannon = CreateToonBeam("Cannon", muzzle.position, target, CannonWidth, material, ball, CreateExplosion("CannonExplosion", target), CannonExplosionSize, GetTitleLightningMaterial());
        intro.Add(cannon);
        intro.Add(cannon.GetComponent<IntroShaderTime>());
    }

    // 戰鬥版光炮外圍的黑色雷電：複製戰鬥用材質，鋸齒長度與流動速度依光炮粗細等比例縮小 (其他數值沿用戰鬥版)
    private static Material GetTitleLightningMaterial()
    {
        Material battle = BinahAttackFxBuilder.GetOrCreateLightningMaterial();
        string path = MaterialFolder + "/Title_CannonDarkLightning.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(battle);
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.CopyPropertiesFromMaterial(battle);
        }

        float scale = CannonWidth / (BattleCannonWidth * BinahAttackFxBuilder.LaneBeamToonScale);
        material.SetFloat("_SegmentLength", battle.GetFloat("_SegmentLength") * scale);
        material.SetFloat("_Speed", battle.GetFloat("_Speed") * scale);
        // 雷電閃動跟著開場慢動作放慢、定格時停住 (IntroShaderTime)
        material.SetFloat("_UseCustomTime", 1f);
        EditorUtility.SetDirty(material);
        return material;
    }

    // 取樣動畫會動到根物件，取樣之後再擺回指定位置
    private static void Place(Transform model, Placement placement)
    {
        model.SetPositionAndRotation(placement.position, Quaternion.Euler(0f, placement.yaw, 0f));
        model.localScale = Vector3.one * placement.scale;
    }

    // 光束：密圓柱 (規格同內建 Cylinder：沿 Y 軸、高 2；切很多段，波動鼓起才看得出來) + 發射口光球 + 落點壓扁的光球；
    // lightning 不為 null 時外面再套一層黑色雷電 (跟著光柱伸長)。長度與爆炸由 IntroBeam 依開場進度控制
    private static IntroBeam CreateToonBeam(string name, Vector3 from, Vector3 to, float width, Material material, Material ballMaterial, Transform explosion, float explosionSize, Material lightning)
    {
        GameObject root = new GameObject(name);
        Transform tube = CreateToonPart(PrimitiveType.Cylinder, "Beam", root.transform, material);
        tube.GetComponent<MeshFilter>().sharedMesh = ToonBeamMeshes.GetOrCreateCylinder();
        System.Collections.Generic.List<Renderer> animated = new System.Collections.Generic.List<Renderer> { tube.GetComponent<Renderer>() };
        if (lightning != null)
        {
            Transform shell = CreateToonPart(PrimitiveType.Cylinder, "DarkLightning", tube, lightning);
            shell.GetComponent<MeshFilter>().sharedMesh = ToonBeamMeshes.GetOrCreateCylinder();
            shell.localScale = new Vector3(BinahAttackFxBuilder.LaneLightningScale, 1f, BinahAttackFxBuilder.LaneLightningScale);
            animated.Add(shell.GetComponent<Renderer>());
        }

        // 光束的 shader 動畫 (波動、雷電) 跟著慢動作放慢、定格時停住
        IntroShaderTime shaderTime = root.AddComponent<IntroShaderTime>();
        SerializedObject timeSettings = new SerializedObject(shaderTime);
        SerializedProperty renderers = timeSettings.FindProperty("renderers");
        renderers.arraySize = animated.Count;
        for (int i = 0; i < animated.Count; i++) renderers.GetArrayElementAtIndex(i).objectReferenceValue = animated[i];
        timeSettings.ApplyModifiedPropertiesWithoutUndo();

        Transform muzzle = CreateToonPart(PrimitiveType.Sphere, "Muzzle", root.transform, ballMaterial);
        muzzle.position = from;
        muzzle.localScale = Vector3.one * width * MuzzleBallScale;

        Transform impact = CreateToonPart(PrimitiveType.Sphere, "Impact", root.transform, ballMaterial);
        impact.position = to;
        impact.localScale = new Vector3(1f, ImpactBallFlatten, 1f) * width * ImpactBallScale;

        IntroBeam beam = root.AddComponent<IntroBeam>();
        SerializedObject serialized = new SerializedObject(beam);
        serialized.FindProperty("tube").objectReferenceValue = tube;
        serialized.FindProperty("from").vector3Value = from;
        serialized.FindProperty("to").vector3Value = to;
        serialized.FindProperty("width").floatValue = width;
        serialized.FindProperty("startFraction").floatValue = BeamStartFraction;
        serialized.FindProperty("hitAt").floatValue = BeamHitAt;
        serialized.FindProperty("impact").objectReferenceValue = impact;
        serialized.FindProperty("explosion").objectReferenceValue = explosion;
        serialized.FindProperty("explosionScale").vector3Value = Vector3.one * explosionSize;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return beam;
    }

    // 卡通爆炸 (大小 1 的單位尺寸，由 IntroBeam 放大)：中間一顆大火球、周圍幾顆小火球往外鼓、貼地一圈扁火球
    private static readonly Vector4[] ExplosionPuffs =
    {
        // x, y, z = 位置，w = 直徑
        new Vector4(0f, 0.3f, 0f, 0.8f),
        new Vector4(0.35f, 0.5f, 0.1f, 0.45f),
        new Vector4(-0.32f, 0.45f, -0.05f, 0.5f),
        new Vector4(0.05f, 0.7f, -0.25f, 0.4f),
        new Vector4(-0.1f, 0.62f, 0.3f, 0.35f),
        new Vector4(0.45f, 0.08f, -0.3f, 0.35f),
        new Vector4(-0.5f, 0.08f, 0.25f, 0.3f),
        new Vector4(0.2f, 0.08f, 0.5f, 0.3f),
        new Vector4(-0.25f, 0.08f, -0.5f, 0.3f),
    };

    private static Transform CreateExplosion(string name, Vector3 position)
    {
        Transform root = new GameObject(name).transform;
        root.position = position;
        Material material = ToonBeamMaterials.GetOrCreate("Title_Explosion", ToonBeamMaterials.Explosion, ToonBeamMaterials.NoAxis, 0.25f, 0.35f, 0.06f);
        for (int i = 0; i < ExplosionPuffs.Length; i++)
        {
            Transform puff = CreateToonPart(PrimitiveType.Sphere, "Puff", root, material);
            puff.localPosition = ExplosionPuffs[i];
            puff.localScale = Vector3.one * ExplosionPuffs[i].w;
        }

        return root;
    }

    // 飛彈：StrikeMissile 的外觀網格 (彈頭朝 +Z) + 尾端一團卡通尾焰，沿直線從 Boss 附近飛向鏡頭
    private static void CreateMissiles(Camera camera, System.Collections.Generic.List<MonoBehaviour> intro)
    {
        Transform visual = FindInPrefab<Transform>(MissilePrefabPath, MissileVisual);
        if (visual == null) return;

        Material flame = ToonBeamMaterials.GetOrCreate("Title_MissileFlame", ToonBeamMaterials.Fire, ToonBeamMaterials.NoAxis, 0.3f, 0.3f, 0.06f);
        for (int i = 0; i < Missiles.Length; i++)
        {
            Vector3 from = camera.ViewportToWorldPoint(Missiles[i].start);
            Vector3 to = camera.ViewportToWorldPoint(Missiles[i].end);
            GameObject missile = new GameObject("Missile_" + i);
            missile.transform.SetPositionAndRotation(to, Quaternion.LookRotation(to - from, Vector3.up));
            missile.transform.localScale = Vector3.one * MissileScale;

            GameObject body = Object.Instantiate(visual.gameObject, missile.transform, false);
            body.name = "Body";
            Transform exhaust = CreateToonPart(PrimitiveType.Sphere, "Exhaust", missile.transform, flame);
            exhaust.localPosition = new Vector3(0f, 0f, -0.7f);
            exhaust.localScale = new Vector3(0.3f, 0.3f, 0.6f);

            intro.Add(AddMover(missile, from, to));
        }
    }

    private static Transform CreateToonPart(PrimitiveType type, string name, Transform parent, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        Object.DestroyImmediate(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        Renderer renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return part.transform;
    }

    private static void CreateHoverCraft()
    {
        GameObject model = Spawn(HoverCraftPath, "HoverCraft", HoverCraftPlacement);
        if (model == null) return;

        // 船頭 (本地 +Z，朝鏡頭) 仰起，營造要壓下來的感覺
        model.transform.rotation = Quaternion.Euler(0f, HoverCraftPlacement.yaw, 0f) * Quaternion.Euler(HoverCraftBowPitch, 0f, 0f);

        foreach (Transform part in model.GetComponentsInChildren<Transform>(true))
        {
            if (System.Array.IndexOf(HoverCraftHiddenParts, part.name) >= 0) part.gameObject.SetActive(false);
        }

        NonMetalMaterials.ApplyTo(model, MaterialFolder);
        // 不播動畫，維持預設姿勢 (ワカモ 的甲板位置是對著這個姿勢擺的)
        Animator animator = model.GetComponent<Animator>();
        if (animator != null) Object.DestroyImmediate(animator);

        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(WakamoPath);
        if (asset == null) return;

        GameObject wakamo = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        wakamo.name = "Wakamo";
        wakamo.transform.SetParent(model.transform, false);
        CharacterMouthBaker.ApplyTo(wakamo, MouthCode, MouthFolder);
        NonMetalMaterials.ApplyTo(wakamo, MaterialFolder);
        BakePose(wakamo, WakamoPath, WakamoPose, WakamoPoseTime);
        wakamo.transform.localPosition = WakamoDeckPosition;
        wakamo.transform.localRotation = Quaternion.Euler(0f, WakamoYaw, 0f);
        wakamo.transform.localScale = Vector3.one * WakamoScale;
        foreach (Transform part in wakamo.GetComponentsInChildren<Transform>(true))
        {
            if (System.Array.IndexOf(WakamoHiddenParts, part.name) >= 0) part.gameObject.SetActive(false);
        }
    }

    // 定格姿勢：產生場景時把動畫那一格直接套到骨架上存進場景，並拿掉 Animator (執行時不再計算動畫，也不會被蓋回預設姿勢)。
    // 取樣會動到根物件的位置/角度，所以呼叫端取樣之後才擺放
    private static void BakePose(GameObject model, string modelPath, string clipName, float time)
    {
        AnimationClip clip = PerorozillaSummonBuilder.FindClip(modelPath, clipName);
        if (clip == null)
        {
            Debug.LogWarning("[TitleSceneBuilder] 找不到動畫 " + clipName);
            return;
        }

        clip.SampleAnimation(model, time);
        Animator animator = model.GetComponent<Animator>();
        if (animator != null) Object.DestroyImmediate(animator);
    }

    // 左側：漸層 + 三角形圖案的斜切面板 (藍白雙層邊線) + 遊戲名稱 + 三個斜切按鈕；進場時依序滑入淡入
    private static void CreateMenu(Font font)
    {
        GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform));
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();
        RectTransform root = (RectTransform)canvasObject.transform;

        // 斜切面板：一塊旋轉過的長方形，左緣超出畫面、右緣就是斜線；外側藍、白兩層邊線
        RectTransform panelGroup = CharacterSelectBuilder.NewRect("PanelGroup", root);
        CharacterSelectBuilder.Stretch(panelGroup, 0f);
        RectTransform blueEdge = CharacterSelectBuilder.NewRect("EdgeBlue", panelGroup);
        SlantedPanel(blueEdge, PanelWidth + 22f);
        CharacterSelectBuilder.AddImage(blueEdge.gameObject, null, ThemeBlue).raycastTarget = false;
        RectTransform whiteEdge = CharacterSelectBuilder.NewRect("EdgeWhite", panelGroup);
        SlantedPanel(whiteEdge, PanelWidth + 9f);
        CharacterSelectBuilder.AddImage(whiteEdge.gameObject, null, Color.white).raycastTarget = false;
        RectTransform panel = CharacterSelectBuilder.NewRect("Panel", panelGroup);
        SlantedPanel(panel, PanelWidth);
        Image panelImage = CharacterSelectBuilder.AddImage(panel.gameObject, null, Color.white);
        panelImage.material = GetOrCreatePanelMaterial();
        panelImage.raycastTarget = false;

        CreateFooter(panelGroup, font);
        RectTransform titleGroup = CreateTitle(root, font);
        Button start = CreateMenuButton(root, font, "Start", "開始遊戲", "S T A R T", TitleIconBaker.Play(OutputFolder), true, StartButtonTop, StartButtonHeight);
        Button tutorial = CreateMenuButton(root, font, "Tutorial", "教學", "T U T O R I A L", TitleIconBaker.Cap(OutputFolder), false, TutorialButtonTop, ButtonHeight);
        Button quit = CreateMenuButton(root, font, "Quit", "離開遊戲", "E X I T", TitleIconBaker.Exit(OutputFolder), false, QuitButtonTop, ButtonHeight);

        UiSequentialIntro intro = canvasObject.AddComponent<UiSequentialIntro>();
        SerializedObject introSettings = new SerializedObject(intro);
        SerializedProperty items = introSettings.FindProperty("items");
        RectTransform[] order = { panelGroup, titleGroup, (RectTransform)start.transform, (RectTransform)tutorial.transform, (RectTransform)quit.transform };
        items.arraySize = order.Length;
        for (int i = 0; i < order.Length; i++) items.GetArrayElementAtIndex(i).objectReferenceValue = order[i];
        // 右側慢動作 (IntroDuration 秒) 播放的同時，左邊 UI 慢慢依序淡入
        introSettings.FindProperty("startDelay").floatValue = MenuIntroDelay;
        introSettings.FindProperty("duration").floatValue = MenuIntroItemDuration;
        introSettings.FindProperty("delayBetween").floatValue = MenuIntroStagger;
        introSettings.ApplyModifiedPropertiesWithoutUndo();

        TitleScreen screen = canvasObject.AddComponent<TitleScreen>();
        SerializedObject serialized = new SerializedObject(screen);
        serialized.FindProperty("startButton").objectReferenceValue = start;
        serialized.FindProperty("tutorialButton").objectReferenceValue = tutorial;
        serialized.FindProperty("quitButton").objectReferenceValue = quit;
        serialized.FindProperty("nextSceneName").stringValue = NextSceneName;
        serialized.FindProperty("sceneTransition").objectReferenceValue = SceneTransitionBuilder.Create();
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // 遊戲名稱 logo：上方藍色軌道準心標誌、「Bullet」深藍 +「Archive」主題藍的粗斜體、下方「彈幕檔案」
    private static RectTransform CreateTitle(RectTransform root, Font font)
    {
        RectTransform group = CharacterSelectBuilder.NewRect("GameTitle", root);
        group.anchorMin = group.anchorMax = new Vector2(0f, 1f);
        group.pivot = new Vector2(0f, 1f);
        group.anchoredPosition = new Vector2(70f, -95f);
        group.sizeDelta = new Vector2(620f, 330f);

        RectTransform emblem = CharacterSelectBuilder.NewRect("Emblem", group);
        emblem.anchorMin = emblem.anchorMax = new Vector2(0.5f, 1f);
        emblem.pivot = new Vector2(0.5f, 1f);
        emblem.sizeDelta = new Vector2(360f, 135f);
        emblem.anchoredPosition = new Vector2(20f, 0f);
        Image emblemImage = CharacterSelectBuilder.AddImage(emblem.gameObject, TitleIconBaker.Emblem(OutputFolder), LogoBlue);
        emblemImage.raycastTarget = false;
        emblemImage.preserveAspect = true;

        Text title = CharacterSelectBuilder.AddText(LogoLine(group, "Title", 120f, 115f), font, GameTitleRich, 88, TextNavy);
        title.fontStyle = FontStyle.BoldAndItalic;
        title.supportRichText = true;

        Text subtitle = CharacterSelectBuilder.AddText(LogoLine(group, "Subtitle", 245f, 60f), font, GameSubtitle, 42, LogoSubText);
        subtitle.fontStyle = FontStyle.Bold;
        return group;
    }

    // logo 裡置中的一行：top 從 group 上緣往下算
    private static RectTransform LogoLine(RectTransform group, string name, float top, float height)
    {
        RectTransform rect = CharacterSelectBuilder.NewRect(name, group);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(0f, -top - height);
        rect.offsetMax = new Vector2(0f, -top);
        return rect;
    }

    // 左側面板最下方的「A BLUE ARCHIVE FAN GAME」(跟著面板一起進場)
    private static void CreateFooter(RectTransform panelGroup, Font font)
    {
        RectTransform rect = CharacterSelectBuilder.NewRect("FanGameNote", panelGroup);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(680f, 40f);
        rect.anchoredPosition = new Vector2(ButtonLeft + ButtonWidth * 0.5f, 34f);
        Text note = CharacterSelectBuilder.AddText(rect, font, FanGameNote, 24, FooterText);
        note.raycastTarget = false;
    }

    private static void SlantedPanel(RectTransform rect, float width)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(width + 600f, 1700f);
        rect.anchoredPosition = new Vector2(width, 0f);
        rect.localRotation = Quaternion.Euler(0f, 0f, PanelSlant);
    }

    // 斜切按鈕：淺藍陰影 + 白底 (開始遊戲多一塊黃色重點區塊) + 圖示 + 中文主標題與英文副標題 + 右下斜紋裝飾
    private static Button CreateMenuButton(RectTransform root, Font font, string name, string label, string subLabel, Sprite icon, bool highlight, float top, float height)
    {
        RectTransform rect = CharacterSelectBuilder.NewRect("Button_" + name, root);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(ButtonWidth, height);
        rect.anchoredPosition = new Vector2(ButtonLeft + ButtonWidth * 0.5f, -top - height * 0.5f);

        SlantedRect shadow = Slanted("Shadow", rect, ButtonShadow);
        shadow.rectTransform.anchoredPosition = new Vector2(7f, -7f);
        shadow.raycastTarget = false;
        SlantedRect body = Slanted("Body", rect, ButtonBackground);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = body;
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.9f, 0.95f, 1f);
        colors.disabledColor = new Color(0.92f, 0.94f, 0.97f, 1f);
        button.colors = colors;
        rect.gameObject.AddComponent<UiButtonFeedback>();

        float iconCenter = 85f;
        if (highlight)
        {
            SlantedRect accent = Slanted("Accent", rect, AccentYellow);
            accent.raycastTarget = false;
            RectTransform accentRect = accent.rectTransform;
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.pivot = new Vector2(0f, 0.5f);
            accentRect.sizeDelta = new Vector2(150f, 0f);
            accentRect.anchoredPosition = Vector2.zero;
        }

        RectTransform iconRect = CharacterSelectBuilder.NewRect("Icon", rect);
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.sizeDelta = new Vector2(60f, 60f);
        iconRect.anchoredPosition = new Vector2(iconCenter, 0f);
        CharacterSelectBuilder.AddImage(iconRect.gameObject, icon, TextNavy).raycastTarget = false;

        Text main = CharacterSelectBuilder.AddText(TextRect(rect, "Label", 175f, 14f, 60f), font, label, 50, TextNavy);
        main.alignment = TextAnchor.MiddleLeft;
        main.fontStyle = FontStyle.BoldAndItalic;
        Text sub = CharacterSelectBuilder.AddText(TextRect(rect, "SubLabel", 178f, -30f, 28f), font, subLabel, 20, SubText);
        sub.alignment = TextAnchor.MiddleLeft;
        sub.fontStyle = FontStyle.Italic;

        // 右下斜紋
        for (int i = 0; i < 4; i++)
        {
            SlantedRect hatch = Slanted("Hatch", rect, new Color(ButtonShadow.r, ButtonShadow.g, ButtonShadow.b, 0.75f));
            hatch.raycastTarget = false;
            hatch.Slant = 16f;
            RectTransform hatchRect = hatch.rectTransform;
            hatchRect.anchorMin = hatchRect.anchorMax = new Vector2(1f, 0f);
            hatchRect.pivot = new Vector2(1f, 0f);
            hatchRect.sizeDelta = new Vector2(7f, 30f);
            hatchRect.anchoredPosition = new Vector2(-34f - i * 15f, 12f);
        }

        return button;
    }

    private static SlantedRect Slanted(string name, RectTransform parent, Color color)
    {
        RectTransform rect = CharacterSelectBuilder.NewRect(name, parent);
        CharacterSelectBuilder.Stretch(rect, 0f);
        SlantedRect graphic = rect.gameObject.AddComponent<SlantedRect>();
        graphic.color = color;
        graphic.Slant = ButtonSlant;
        return graphic;
    }

    // 按鈕裡的一行文字：x 從左邊算，y 是相對按鈕中心的偏移
    private static RectTransform TextRect(RectTransform parent, string name, float x, float y, float height)
    {
        RectTransform rect = CharacterSelectBuilder.NewRect(name, parent);
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = new Vector2(x, y - height * 0.5f);
        rect.offsetMax = new Vector2(-40f, y + height * 0.5f);
        return rect;
    }

    private static Material GetOrCreatePanelMaterial()
    {
        string path = OutputFolder + "/Title_PanelPattern.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("STG/UITrianglePattern"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_TopColor", PanelTop);
        material.SetColor("_BottomColor", PanelBottom);
        material.SetColor("_TriangleColor", ThemeBlue);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static T FindInPrefab<T>(string prefabPath, string childName) where T : Component
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return null;

        foreach (T component in prefab.GetComponentsInChildren<T>(true))
        {
            if (component.name == childName) return component;
        }

        Debug.LogWarning("[TitleSceneBuilder] " + prefabPath + " 裡找不到 " + childName);
        return null;
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name) return child;
        }

        Debug.LogWarning("[TitleSceneBuilder] " + root.name + " 裡找不到 " + name);
        return null;
    }
}
#endif
