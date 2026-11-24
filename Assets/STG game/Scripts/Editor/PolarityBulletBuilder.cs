#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 一鍵建立藍/黃屬性子彈所需的發光材質、光暈粒子與點光源，並掛到 EnemyBullet_SM05 prefab 上。
// 可重複執行：已存在的資源會沿用並以這裡的數值重新設定，方便調整後再套一次。
public static class PolarityBulletBuilder
{
    private const string PrefabPath = "Assets/STG game/resources/prefab/EnemyBullet_SM05.prefab";
    private const string SourceBodyMaterialPath = "Assets/STG game/resources/model/shooting item/Bullets Pack/Materials/Bullets_M.mat";
    private const string OutputFolder = "Assets/STG game/resources/prefab/BulletPolarity";

    // 顏色要夠飽和，乘上 HDR 亮度後才不會被 Bloom/色調映射洗成白色
    private static readonly Color BlueColor = new Color(0.1f, 0.4f, 1f);
    private static readonly Color YellowColor = new Color(1f, 0.7f, 0.05f);

    // 彈體自發光強度 (HDR 倍數)，需超過 Bloom 門檻 1 才會泛光
    private const float BodyEmissionIntensity = 2f;
    // 光暈粒子亮度 (HDR 倍數)；粒子頂點色上限為 1，所以亮度放在材質上
    private const float GlowBrightness = 1.5f;
    // 光暈大小 (世界單位)，子彈本體長約 2 (prefab 根物件縮放 20)
    private const float GlowSize = 5f;

    [MenuItem("Tools/STG/Build Polarity Bullet")]
    private static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[PolarityBulletBuilder] 請先離開 Play Mode 再執行。");
            return;
        }

        EnsureFolder(OutputFolder);

        Material blueBody = CreateBodyMaterial("Bullet_Body_Blue", BlueColor);
        Material yellowBody = CreateBodyMaterial("Bullet_Body_Yellow", YellowColor);
        Material glowMaterial = CreateGlowMaterial(CreateGlowTexture());

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            ParticleSystem glow = SetupGlowParticles(root.transform, glowMaterial);
            Light glowLight = SetupGlowLight(root.transform);
            SetupPolarityVisual(root, glow, glowLight, blueBody, yellowBody);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[PolarityBulletBuilder] 已更新 " + PrefabPath);
    }

    private static Material CreateBodyMaterial(string name, Color color)
    {
        string path = OutputFolder + "/" + name + ".mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(path) == null)
        {
            AssetDatabase.CopyAsset(SourceBodyMaterialPath, path);
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        material.SetColor("_BaseColor", color);
        material.SetColor("_EmissionColor", color * BodyEmissionIntensity);
        // 來源材質帶有 EmissiveIsBlack 旗標，URP 匯入時會依此關閉 _EMISSION，所以要先清旗標再開 keyword
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        material.EnableKeyword("_EMISSION");
        EditorUtility.SetDirty(material);
        return material;
    }

    // URP Particles/Unlit 的半透明 (Alpha) 混合材質；地板很亮，加算混合會把顏色洗成白色。
    // 透過腳本設定時不會經過 ShaderGUI，所以混合狀態要手動寫齊
    private static Material CreateGlowMaterial(Texture2D texture)
    {
        string path = OutputFolder + "/Bullet_Glow.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", new Color(GlowBrightness, GlowBrightness, GlowBrightness, 1f));
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    // 產生柔邊圓形貼圖當作光暈形狀，避免依賴內建資源名稱
    private static Texture2D CreateGlowTexture()
    {
        string path = OutputFolder + "/Bullet_Glow_Tex.asset";
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        texture.Apply();
        AssetDatabase.CreateAsset(texture, path);
        return texture;
    }

    // 世界空間發射的光暈粒子：子彈頭部是一團光，移動時自然拖出一段漸隱的尾巴
    private static ParticleSystem SetupGlowParticles(Transform root, Material material)
    {
        Transform child = GetOrCreateChild(root, "Glow");
        ParticleSystem particles = child.GetComponent<ParticleSystem>();
        if (particles == null) particles = child.gameObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = 0.25f;
        main.startSpeed = 0f;
        main.startSize = GlowSize;
        main.startColor = Color.white;
        main.maxParticles = 64;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        // 只吃自己的縮放，大小以世界單位計算，不受子彈根物件 10 倍縮放影響
        main.scalingMode = ParticleSystemScalingMode.Local;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 20f;
        emission.rateOverDistance = 3f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = false;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = fade;

        ParticleSystemRenderer particleRenderer = child.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sharedMaterial = material;
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;

        return particles;
    }

    // 即時點光源預設關閉：同時存在的子彈很多時每顆一盞燈成本很高，需要時再手動打開
    private static Light SetupGlowLight(Transform root)
    {
        Transform child = GetOrCreateChild(root, "GlowLight");
        Light light = child.GetComponent<Light>();
        if (light == null) light = child.gameObject.AddComponent<Light>();

        light.type = LightType.Point;
        light.range = 6f;
        light.intensity = 3f;
        light.shadows = LightShadows.None;
        light.enabled = false;
        return light;
    }

    private static void SetupPolarityVisual(GameObject root, ParticleSystem glow, Light glowLight, Material blueBody, Material yellowBody)
    {
        BulletPolarityVisual visual = root.GetComponent<BulletPolarityVisual>();
        if (visual == null) visual = root.AddComponent<BulletPolarityVisual>();

        SerializedObject serialized = new SerializedObject(visual);
        serialized.FindProperty("bodyRenderer").objectReferenceValue = root.GetComponentInChildren<MeshRenderer>(true);
        serialized.FindProperty("glowParticles").objectReferenceValue = glow;
        serialized.FindProperty("glowLight").objectReferenceValue = glowLight;

        SerializedProperty styles = serialized.FindProperty("styles");
        styles.arraySize = 2;
        WriteStyle(styles.GetArrayElementAtIndex(0), BulletPolarity.Blue, blueBody, BlueColor);
        WriteStyle(styles.GetArrayElementAtIndex(1), BulletPolarity.Yellow, yellowBody, YellowColor);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WriteStyle(SerializedProperty style, BulletPolarity polarity, Material bodyMaterial, Color color)
    {
        style.FindPropertyRelative("polarity").enumValueIndex = (int)polarity;
        style.FindPropertyRelative("bodyMaterial").objectReferenceValue = bodyMaterial;
        style.FindPropertyRelative("glowColor").colorValue = color;
        style.FindPropertyRelative("lightColor").colorValue = color;
    }

    private static Transform GetOrCreateChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) return child;

        child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
    }
}
#endif
