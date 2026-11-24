using UnityEngine;
using UnityEngine.Rendering;

// 敵人頭上的血條 (世界空間)：底板 + 填充兩片平面，填充依血量從右邊縮短 (左緣固定)。
// 每台鏡頭渲染前把血條轉成跟鏡頭同方向 (billboard)，切換第一/第三人稱鏡頭都始終面向畫面；敵人死亡時隱藏
public class WorldHealthBar : MonoBehaviour
{
    [SerializeField, Tooltip("血條對應的血量，留空時自動抓父物件上的 EnemyHealth")] private EnemyHealth health;
    [SerializeField, Tooltip("血條的外觀 (底板與填充的父物件)，死亡時隱藏")] private GameObject visual;
    [SerializeField, Tooltip("填充 (以中心為原點、寬 1 的平面)，依血量縮放")] private Transform fill;
    [SerializeField, Min(0.1f), Tooltip("血條寬度 (m)")] private float width = 8f;
    [SerializeField, Tooltip("填充在底板前方多少 (m，朝鏡頭)，避免兩片重疊閃爍")] private float fillDepthOffset = 0.02f;

    private void Awake()
    {
        if (health == null) health = GetComponentInParent<EnemyHealth>();
        if (RequiredReference.IsMissing(health, nameof(health), this)) return;
        if (RequiredReference.IsMissing(visual, nameof(visual), this)) return;
        RequiredReference.IsMissing(fill, nameof(fill), this);
    }

    private void OnEnable()
    {
        if (health == null) return;

        health.OnHealthChanged += HandleHealthChanged;
        health.OnDied += HandleDied;
        RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
        if (visual != null) visual.SetActive(!health.IsDead);
        HandleHealthChanged(health.CurrentHealth, health.MaxHealth);
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
        if (health == null) return;

        health.OnHealthChanged -= HandleHealthChanged;
        health.OnDied -= HandleDied;
    }

    // 與鏡頭同方向 (不是「看向」鏡頭)，所有血條都跟畫面平行，不會因位置不同而歪斜
    public void FaceCamera(Camera targetCamera)
    {
        if (targetCamera == null) return;

        transform.rotation = targetCamera.transform.rotation;
    }

    private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
    {
        FaceCamera(renderingCamera);
    }

    private void HandleHealthChanged(int current, int max)
    {
        if (fill == null) return;

        float ratio = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
        Vector3 scale = fill.localScale;
        fill.localScale = new Vector3(width * ratio, scale.y, scale.z);
        fill.localPosition = new Vector3(-width * (1f - ratio) * 0.5f, fill.localPosition.y, -fillDepthOffset);
    }

    private void HandleDied()
    {
        if (visual != null) visual.SetActive(false);
    }
}
