using System;
using System.Collections.Generic;
using UnityEngine;

// 地面範圍攻擊：先顯示外圈，內部由中心往外擴張作為倒數，填滿瞬間判定範圍內的目標受傷並播放爆炸特效。
// 依屬性變色：紅色 (None) 無法被護盾吸收；藍/黃與玩家護盾同極性時會被吸收、不扣血
public class AreaStrike : MonoBehaviour
{
    [Serializable]
    private struct PolarityStyle
    {
        [Tooltip("對應的屬性")] public BulletPolarity polarity;
        [ColorUsage(true, true), Tooltip("外圈顏色")] public Color ringColor;
        [ColorUsage(true, true), Tooltip("倒數填充顏色")] public Color fillColor;
        [ColorUsage(true, true), Tooltip("引爆瞬間的閃光顏色")] public Color flashColor;

        public PolarityStyle(BulletPolarity polarity, Color ringColor, Color fillColor, Color flashColor)
        {
            this.polarity = polarity;
            this.ringColor = ringColor;
            this.fillColor = fillColor;
            this.flashColor = flashColor;
        }
    }

    [Header("Visuals")]
    [SerializeField, Tooltip("倒數時由中心往外擴張的填充圓盤")] private Transform fillDisc;
    [SerializeField, Tooltip("填充圓盤的 Renderer")] private Renderer fillRenderer;
    [SerializeField, Tooltip("外圈的 Renderer")] private Renderer ringRenderer;
    [SerializeField, Tooltip("填充擴張的節奏 (橫軸=倒數進度 0~1，縱軸=填充比例 0~1)")]
    private AnimationCurve fillCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField, Tooltip("引爆閃光持續時間 (秒)，之後隱藏圈形、等爆炸特效播完再回收")] private float flashDuration = 0.2f;
    [SerializeField, Tooltip("每種屬性的配色")]
    private PolarityStyle[] styles =
    {
        new PolarityStyle(BulletPolarity.None, new Color(2f, 0.15f, 0.1f, 1f), new Color(1f, 0.1f, 0.05f, 0.45f), new Color(4f, 0.8f, 0.6f, 0.9f)),
        new PolarityStyle(BulletPolarity.Blue, new Color(0.2f, 0.6f, 2.5f, 1f), new Color(0.1f, 0.4f, 1f, 0.45f), new Color(1.2f, 2.4f, 5f, 0.9f)),
        new PolarityStyle(BulletPolarity.Yellow, new Color(2.5f, 1.8f, 0.1f, 1f), new Color(1f, 0.75f, 0.05f, 0.45f), new Color(5f, 4f, 1.2f, 0.9f)),
    };

    [Header("Explosion")]
    [SerializeField, Tooltip("引爆時播放的爆炸粒子 (火光 + 火花 + 煙霧)")] private ParticleSystem explosionParticles;
    [SerializeField, Tooltip("爆炸特效設計時對應的範圍半徑；實際半徑不同時會等比例縮放特效")] private float explosionReferenceRadius = 10f;

    [Header("Damage")]
    [SerializeField, Tooltip("判定命中的目標 Tag")] private string hitTag = "Player";
    [SerializeField, Tooltip("命中後造成的傷害值")] private int damage = 1;
    [SerializeField, Tooltip("判定時偵測的 Layer")] private LayerMask hitLayers = ~0;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private readonly Collider[] overlapBuffer = new Collider[16];
    // 同一個目標可能有多個碰撞體且在結果中不相鄰，用集合確保每個目標每次爆炸只判定一次
    private readonly HashSet<IDamageable> processedTargets = new HashSet<IDamageable>();
    private MaterialPropertyBlock propertyBlock;
    private PolarityStyle currentStyle;
    private float radius;
    private float warningDuration;
    private float timer;
    private bool detonated;
    private bool indicatorHidden;
    private Action<AreaStrike> onFinished;

    public BulletPolarity Polarity { get; private set; }
    // 距離爆炸判定還剩幾秒 (已爆炸為 0)，供飛彈等演出對準爆炸時間
    public float TimeUntilDetonation => detonated ? 0f : Mathf.Max(0f, warningDuration - timer);

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        if (RequiredReference.IsMissing(ringRenderer, nameof(ringRenderer), this)) return;
        if (RequiredReference.IsMissing(fillRenderer, nameof(fillRenderer), this)) return;
        RequiredReference.IsMissing(fillDisc, nameof(fillDisc), this);
    }

    // 由 Spawner 從物件池取出後呼叫：設定半徑、倒數時間、屬性與結束時歸還物件池的回呼
    public void Begin(float strikeRadius, float warningTime, BulletPolarity polarity, Action<AreaStrike> finishedCallback)
    {
        // 必填欄位缺漏時本元件已自我停用 (見 Awake)；直接歸還物件池，避免存取缺漏的引用，也避免物件永遠不被回收
        if (!enabled)
        {
            finishedCallback?.Invoke(this);
            return;
        }

        radius = strikeRadius;
        warningDuration = Mathf.Max(0.01f, warningTime);
        Polarity = polarity;
        onFinished = finishedCallback;
        timer = 0f;
        detonated = false;
        indicatorHidden = false;

        // 只縮放圈形，根物件保持等比例，爆炸粒子才不會被壓扁
        ringRenderer.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
        SetIndicatorVisible(true);

        if (explosionParticles != null)
        {
            explosionParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            explosionParticles.transform.localScale = Vector3.one * (radius / explosionReferenceRadius);
        }

        currentStyle = GetStyle(polarity);
        SetColor(ringRenderer, currentStyle.ringColor);
        SetColor(fillRenderer, currentStyle.fillColor);
        SetFill(0f);
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (!detonated)
        {
            SetFill(fillCurve.Evaluate(Mathf.Clamp01(timer / warningDuration)));
            if (timer >= warningDuration) Detonate();
            return;
        }

        if (timer < warningDuration + flashDuration) return;

        if (!indicatorHidden)
        {
            SetIndicatorVisible(false);
            indicatorHidden = true;
        }

        // 煙霧散完才回收，避免特效被物件池停用時瞬間消失
        if (explosionParticles == null || !explosionParticles.IsAlive(true))
        {
            onFinished?.Invoke(this);
        }
    }

    private void Detonate()
    {
        detonated = true;
        SetFill(1f);
        SetColor(fillRenderer, currentStyle.flashColor);
        if (explosionParticles != null) explosionParticles.Play(true);
        DealDamage();
    }

    // 以目標中心點的水平距離判定是否在圈內，避免碰撞體邊緣擦到圈就被打中
    private void DealDamage()
    {
        Vector3 center = transform.position;
        int count = Physics.OverlapSphereNonAlloc(center, radius, overlapBuffer, hitLayers, QueryTriggerInteraction.Ignore);
        processedTargets.Clear();

        for (int i = 0; i < count; i++)
        {
            Collider hit = overlapBuffer[i];
            if (!hit.CompareTag(hitTag)) continue;

            Vector3 offset = hit.transform.position - center;
            offset.y = 0f;
            if (offset.sqrMagnitude > radius * radius) continue;

            if (!hit.TryGetComponent(out IDamageable damageable)) continue;
            if (!processedTargets.Add(damageable)) continue;

            // 同極性護盾吸收這次攻擊
            if (hit.TryGetComponent(out IPolarityAbsorber absorber) && absorber.TryAbsorb(Polarity)) continue;

            damageable.TakeDamage(damage);
        }
    }

    private PolarityStyle GetStyle(BulletPolarity polarity)
    {
        for (int i = 0; i < styles.Length; i++)
        {
            if (styles[i].polarity == polarity) return styles[i];
        }

        return styles.Length > 0 ? styles[0] : default;
    }

    private void SetFill(float amount)
    {
        float size = radius * 2f * amount;
        fillDisc.localScale = new Vector3(size, size, 1f);
    }

    private void SetIndicatorVisible(bool visible)
    {
        ringRenderer.enabled = visible;
        fillRenderer.enabled = visible;
    }

    private void SetColor(Renderer target, Color color)
    {
        if (target == null) return;

        propertyBlock.SetColor(BaseColorId, color);
        target.SetPropertyBlock(propertyBlock);
    }
}
