#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 在目前場景建立稜鏡轉場物件 (最上層的全螢幕 Canvas：黑幕 + 稜鏡圖)；開始畫面、選角、戰鬥場景的生成工具共用
public static class SceneTransitionBuilder
{
    public const string Folder = "Assets/STG game/resources/prefab/Transition";
    private const string MaterialPath = Folder + "/PrismTransition.mat";
    private const string ShaderName = "STG/PrismTransition";
    // 蓋在所有 UI (字卡、結算面板) 之上
    private const int SortingOrder = 1000;
    // 稜鏡三角形邊長 (畫面高度的比例)；0.22 → 0.246 = 數量少約 20%
    private const float PrismCellSize = 0.246f;

    public static SceneTransition Create()
    {
        GameObject root = new GameObject("SceneTransition", typeof(RectTransform));
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        // 轉場期間擋住點擊 (圖關掉時不會擋)
        root.AddComponent<GraphicRaycaster>();

        Image black = FullScreenImage("Blackout", root.transform);
        black.color = new Color(0f, 0f, 0f, 0f);
        Image prism = FullScreenImage("Prism", root.transform);
        prism.material = GetOrCreateMaterial();

        SceneTransition transition = root.AddComponent<SceneTransition>();
        SerializedObject serialized = new SerializedObject(transition);
        serialized.FindProperty("prism").objectReferenceValue = prism;
        serialized.FindProperty("blackout").objectReferenceValue = black;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return transition;
    }

    private static Image FullScreenImage(string name, Transform parent)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = rect.gameObject.AddComponent<Image>();
        image.enabled = false;
        return image;
    }

    private static Material GetOrCreateMaterial()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/STG game/resources/prefab", "Transition");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find(ShaderName));
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.SetFloat("_CellSize", PrismCellSize);
        EditorUtility.SetDirty(material);
        return material;
    }
}
#endif
