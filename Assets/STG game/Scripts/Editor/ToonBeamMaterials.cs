#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// 卡通光束材質 (STG/ToonBeam) 的共用建立工具與配色：開始畫面、戰鬥中 Perorozilla 雷射、Binah 光炮都用同一套，風格一致
public static class ToonBeamMaterials
{
    public const string Folder = "Assets/STG game/resources/prefab/ToonBeam";
    private const string ShaderName = "STG/ToonBeam";

    public struct Palette
    {
        public Color core;
        public Color main;
        public Color edge;
        public Color outline;
    }

    // Perorozilla 的藍色雷射
    public static readonly Palette Laser = new Palette
    {
        core = Color.white,
        main = new Color(0.35f, 0.85f, 1f),
        edge = new Color(0.12f, 0.4f, 0.95f),
        outline = new Color(0.05f, 0.12f, 0.35f),
    };

    // Binah 的橘黃光炮
    public static readonly Palette Fire = new Palette
    {
        core = new Color(1f, 0.98f, 0.85f),
        main = new Color(1f, 0.78f, 0.25f),
        edge = new Color(0.95f, 0.35f, 0.12f),
        outline = new Color(0.3f, 0.08f, 0.05f),
    };

    // 爆炸火球 (亮芯較小、橘紅較多)
    public static readonly Palette Explosion = new Palette
    {
        core = new Color(1f, 0.95f, 0.6f),
        main = new Color(1f, 0.55f, 0.12f),
        edge = new Color(0.85f, 0.18f, 0.06f),
        outline = new Color(0.25f, 0.05f, 0.03f),
    };

    // 波動 (一波波鼓起沿光束往前推進)；預設全 0 = 靜止 (開始畫面用)
    public struct Wave
    {
        // 鼓起的比例 (0~1)
        public float amplitude;
        // 一波的長度 (公尺)
        public float length;
        // 往前推進的速度 (公尺/秒)
        public float speed;
    }

    // 戰鬥中的雷射/光炮：參考原本特效的波速 (約 50 m/s)，卡通版波長拉長、鼓起明顯一點，一眼看得出在流動
    public static readonly Wave BattleLaserWave = new Wave { amplitude = 0.3f, length = 30f, speed = 90f };
    public static readonly Wave BattleCannonWave = new Wave { amplitude = 0.25f, length = 28f, speed = 70f };

    public static readonly Vector3 AxisY = Vector3.up;
    public static readonly Vector3 AxisZ = Vector3.forward;
    // 球：沒有長度方向，描邊往四面八方推
    public static readonly Vector3 NoAxis = Vector3.zero;

    /// <summary>取得或建立材質 (存在 Folder/name.mat，重複執行會更新數值)</summary>
    public static Material GetOrCreate(string name, Palette palette, Vector3 lengthAxis, float coreSize = 0.35f, float edgeSize = 0.3f, float outlineWidth = 0.08f, Wave wave = default)
    {
        EnsureFolder();
        string path = Folder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(ShaderName));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_CoreColor", palette.core);
        material.SetColor("_MainColor", palette.main);
        material.SetColor("_EdgeColor", palette.edge);
        material.SetColor("_OutlineColor", palette.outline);
        material.SetFloat("_CoreSize", coreSize);
        material.SetFloat("_EdgeSize", edgeSize);
        material.SetFloat("_OutlineWidth", outlineWidth);
        material.SetVector("_OutlineAxis", lengthAxis);
        material.SetFloat("_WaveAmplitude", wave.amplitude);
        if (wave.length > 0f) material.SetFloat("_WaveLength", wave.length);
        material.SetFloat("_WaveSpeed", wave.speed);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder()
    {
        if (AssetDatabase.IsValidFolder(Folder)) return;

        AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(Folder).Replace('\\', '/'), System.IO.Path.GetFileName(Folder));
    }
}
#endif
