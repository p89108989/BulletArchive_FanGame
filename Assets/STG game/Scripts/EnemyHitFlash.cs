using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 敵人受傷時短暫反白：本體保持顯示，把材質的基本色調亮 (整體變白)，維持一下再快速淡回 (類似 BA 被攻擊時的閃白)。
// 不用自發光：glTF 匯入的材質 (glTF-pbrMetallicRoughness) 自發光幾乎看不出效果，調亮基本色則一定明顯。
// 雷射每 0.1 秒扣一次血，若每次都重新亮起會一直停在亮的狀態、看不出閃爍，所以同一次閃白期間與間隔內的受傷都忽略，
// 持續受傷時呈現固定節奏的一亮一暗。以 MaterialPropertyBlock 逐材質覆寫，不改共用材質
[RequireComponent(typeof(EnemyHealth))]
public class EnemyHitFlash : MonoBehaviour
{
    [Header("Flash")]
    [SerializeField, Min(1f), Tooltip("閃白最亮時基本色的倍數，數值越大越白 (原色 = 1)")] private float flashBrightness = 1.8f;
    [SerializeField, Min(0f), Tooltip("維持最亮的時間 (秒)")] private float holdDuration = 0.06f;
    [SerializeField, Min(0.01f), Tooltip("淡回原色的時間 (秒)")] private float fadeDuration = 0.08f;
    [SerializeField, Min(0f), Tooltip("兩次閃白之間至少相隔 (秒)；要比 維持 + 淡出 長，持續受傷時才看得到暗下來的空檔")] private float pulseInterval = 0.25f;

    // 依序嘗試的基本色屬性：glTF 匯入材質與 URP Lit/Unlit
    private static readonly int[] BaseColorIds =
    {
        Shader.PropertyToID("baseColorFactor"),
        Shader.PropertyToID("_BaseColor"),
    };

    // 每個 (Renderer, 材質索引) 的基本色屬性與原本的顏色，閃白時以此為基準
    private struct FlashSlot
    {
        public Renderer renderer;
        public int materialIndex;
        public int propertyId;
        public Color originalColor;
    }

    private readonly List<FlashSlot> slots = new List<FlashSlot>();
    private EnemyHealth health;
    private MaterialPropertyBlock propertyBlock;
    private Coroutine flashRoutine;
    private float nextFlashTime;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        propertyBlock = new MaterialPropertyBlock();
        CollectSlots();
    }

    private void OnEnable()
    {
        health.OnDamaged += HandleDamaged;
        health.OnDied += StopFlash;
    }

    private void OnDisable()
    {
        health.OnDamaged -= HandleDamaged;
        health.OnDied -= StopFlash;
        StopFlash();
    }

    // 只處理網格 (粒子特效不反白)；沒有基本色屬性的材質略過
    private void CollectSlots()
    {
        foreach (Renderer target in GetComponentsInChildren<Renderer>(true))
        {
            if (!(target is MeshRenderer) && !(target is SkinnedMeshRenderer)) continue;

            Material[] materials = target.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null) continue;

                for (int p = 0; p < BaseColorIds.Length; p++)
                {
                    if (!materials[i].HasProperty(BaseColorIds[p])) continue;

                    slots.Add(new FlashSlot
                    {
                        renderer = target,
                        materialIndex = i,
                        propertyId = BaseColorIds[p],
                        originalColor = materials[i].GetColor(BaseColorIds[p]),
                    });
                    break;
                }
            }
        }
    }

    // 正在閃白或還沒到下一次閃白的時間就忽略，讓持續受傷時保有暗下來的空檔
    private void HandleDamaged()
    {
        if (flashRoutine != null || Time.time < nextFlashTime) return;

        nextFlashTime = Time.time + pulseInterval;
        flashRoutine = StartCoroutine(FlashRoutine());
    }

    // 致命一擊同樣會先觸發受傷；死亡時立即恢復原色，避免爆炸或死亡動畫期間一直停在白色
    private void StopFlash()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }
        ApplyFlash(0f);
    }

    private IEnumerator FlashRoutine()
    {
        ApplyFlash(1f);
        if (holdDuration > 0f) yield return new WaitForSeconds(holdDuration);

        for (float elapsed = 0f; elapsed < fadeDuration; elapsed += Time.deltaTime)
        {
            ApplyFlash(1f - elapsed / fadeDuration);
            yield return null;
        }

        ApplyFlash(0f);
        flashRoutine = null;
    }

    // strength 0 = 原色，1 = 最亮；只調 RGB，透明度維持原樣
    private void ApplyFlash(float strength)
    {
        float multiplier = Mathf.Lerp(1f, flashBrightness, strength);
        for (int i = 0; i < slots.Count; i++)
        {
            FlashSlot slot = slots[i];
            if (slot.renderer == null) continue;

            Color color = slot.originalColor;
            color.r *= multiplier;
            color.g *= multiplier;
            color.b *= multiplier;

            slot.renderer.GetPropertyBlock(propertyBlock, slot.materialIndex);
            propertyBlock.SetColor(slot.propertyId, color);
            slot.renderer.SetPropertyBlock(propertyBlock, slot.materialIndex);
        }
    }
}
