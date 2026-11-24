#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// 建立直升機 Prefab：使用 Enemy_Apache01 (BA 的 Heli_Apache) 模型。
// 素材內沒有飛行動畫 (Animation/FBX 下的檔案只有網格與骨架，動畫資料遺失)，
// 但主旋翼 (bone_wing_01) 與尾旋翼 (bone_wing_B) 是獨立骨骼，直接旋轉骨骼即可轉動槳葉。
// 已存在的 prefab 就地更新：場景上的直升機掛了 EnemyHealth / ApacheGunship 等元件 (場景覆寫)，整個重建會遺失
public static class ApachePrefabBuilder
{
    private const string ModelPath = "Assets/STG game/resources/model/Enemy_Apache01/bundles/Assets/_MX/NPCs/HeliApache01_Original/Animation/FBX/Heli_Apache_Normal_Idle.glb";
    private const string PrefabPath = "Assets/STG game/resources/prefab/Apache.prefab";
    private const string PropellerBlurTexturePath = "Assets/STG game/resources/model/Enemy_Apache01/bundles/Assets/_MX/NPCs/HeliApache01_Original/Model/Texture/Heli_Apache_Prop_FX 1.png";
    private const string PropellerBlurMaterialPath = "Assets/STG game/resources/prefab/Apache_PropellerBlur.mat";
    // 主旋翼 Heli_Apache_propeller 與尾旋翼 Heli_Apache_propeller001 的模糊圓盤
    private const string PropellerBlurRendererPrefix = "Heli_Apache_propeller";
    private const float PropellerBlurAlpha = 0.45f;

    // 原始模型長約 4.1，放大後約 41 m (與舊模型相同大小)
    private const float ModelScale = 10f;
    private const string MainRotorBone = "bone_wing_01";
    private const string TailRotorBone = "bone_wing_B";
    private const string BodyRendererName = "Heli_Apache_Body";
    // 兩側飛彈莢艙的骨骼，當作飛彈發射點
    private static readonly string[] MissileLaunchBones = { "bone_WP_L_02", "bone_WP_R_02" };
    private const float MainRotorSpeed = 1080f;
    private const float TailRotorSpeed = 2160f;
    // 舊版 (Apache.glb 拆網格) 遺留的子物件，更新時移除
    private static readonly string[] LegacyParts = { "Body", "MainRotor", "TailRotor", "Mesh" };

    [MenuItem("Tools/STG/Build Apache Prefab")]
    private static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[ApachePrefabBuilder] 請先離開 Play Mode 再執行。");
            return;
        }

        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelAsset == null)
        {
            Debug.LogError("[ApachePrefabBuilder] 找不到模型 " + ModelPath);
            return;
        }

        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
        GameObject root = exists ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("Apache");
        try
        {
            Transform modelRoot = GetOrCreateChild(root.transform, "Model");
            modelRoot.localRotation = Quaternion.identity;
            modelRoot.gameObject.layer = LayerMask.NameToLayer("Enemy");
            foreach (string part in LegacyParts)
            {
                Transform legacy = modelRoot.Find(part);
                if (legacy != null) Object.DestroyImmediate(legacy.gameObject);
            }

            // 解除 prefab 連結後才能移除 glTF 匯入時自動加的空 Animator (沒有 Controller，留著也不會動)
            GameObject mesh = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            PrefabUtility.UnpackPrefabInstance(mesh, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            mesh.name = "Mesh";
            mesh.transform.SetParent(modelRoot, false);
            mesh.transform.localScale = Vector3.one * ModelScale;
            Animator unusedAnimator = mesh.GetComponent<Animator>();
            if (unusedAnimator != null) Object.DestroyImmediate(unusedAnimator);

            // 模型原點在機腹下方，平移讓機身中心對齊根物件 (盤旋高度與舊模型一致)
            mesh.transform.localPosition = -modelRoot.InverseTransformPoint(GetRendererBounds(mesh).center);
            ApplyPropellerBlurMaterial(mesh);
            AddRotorSpin(mesh.transform, MainRotorBone, modelRoot.up, MainRotorSpeed);
            AddRotorSpin(mesh.transform, TailRotorBone, modelRoot.right, TailRotorSpeed);
            SetupCollider(modelRoot.gameObject, mesh);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            if (exists) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        RewireSceneGunships();
        Debug.Log("[ApachePrefabBuilder] 已更新 " + PrefabPath);
    }

    // ApacheGunship 掛在場景實例上 (不在 prefab 內)，模型重建後它對機身與發射點骨骼的引用會失效，依名稱重新接上
    private static void RewireSceneGunships()
    {
        foreach (ApacheGunship gunship in Object.FindObjectsByType<ApacheGunship>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            SerializedObject serialized = new SerializedObject(gunship);
            serialized.FindProperty("bodyRenderer").objectReferenceValue = FindRenderer(gunship.transform, BodyRendererName);
            SerializedProperty launchPoints = serialized.FindProperty("missileLaunchPoints");
            launchPoints.arraySize = MissileLaunchBones.Length;
            for (int i = 0; i < MissileLaunchBones.Length; i++)
            {
                launchPoints.GetArrayElementAtIndex(i).objectReferenceValue = FindChild(gunship.transform, MissileLaunchBones[i]);
            }
            serialized.ApplyModifiedProperties();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gunship.gameObject.scene);
        }
    }

    // 機身的骨骼節點與 Renderer 同名，要找的是掛 Renderer 的那個
    private static Renderer FindRenderer(Transform root, string name)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.name == name) return renderer;
        }

        Debug.LogError("[ApachePrefabBuilder] 找不到 Renderer " + name);
        return null;
    }

    // 旋翼的動態模糊圓盤：glb 內的材質遺失了貼圖而變成不透明的實心圓盤，改用半透明材質 + 原本的模糊貼圖
    private static void ApplyPropellerBlurMaterial(GameObject mesh)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(PropellerBlurMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(material, PropellerBlurMaterialPath);
        }

        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PropellerBlurTexturePath));
        material.SetColor("_BaseColor", new Color(1f, 1f, 1f, PropellerBlurAlpha));
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(material);

        foreach (Renderer renderer in mesh.GetComponentsInChildren<Renderer>())
        {
            if (renderer.name.StartsWith(PropellerBlurRendererPrefix)) renderer.sharedMaterial = material;
        }
    }

    // 旋翼骨骼本身的座標軸是歪的，把「直升機的上方/側方」換算成骨骼自身座標當旋轉軸
    private static void AddRotorSpin(Transform meshRoot, string boneName, Vector3 worldAxis, float speed)
    {
        Transform bone = FindChild(meshRoot, boneName);
        if (bone == null) return;

        RotorSpin spin = bone.gameObject.AddComponent<RotorSpin>();
        SerializedObject serialized = new SerializedObject(spin);
        serialized.FindProperty("localAxis").vector3Value = bone.InverseTransformDirection(worldAxis).normalized;
        serialized.FindProperty("degreesPerSecond").floatValue = speed;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // Trigger：只給玩家雷射判定，不推擠任何東西
    private static void SetupCollider(GameObject modelRoot, GameObject mesh)
    {
        BoxCollider collider = modelRoot.GetComponent<BoxCollider>();
        if (collider == null) collider = modelRoot.AddComponent<BoxCollider>();

        Bounds world = GetRendererBounds(mesh);
        collider.center = modelRoot.transform.InverseTransformPoint(world.center);
        collider.size = world.size;
        collider.isTrigger = true;
    }

    private static Bounds GetRendererBounds(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static Transform GetOrCreateChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) return child;

        child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name) return child;
        }

        Debug.LogError("[ApachePrefabBuilder] 找不到骨骼 " + name);
        return null;
    }
}
#endif
