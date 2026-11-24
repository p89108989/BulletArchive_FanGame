using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// 敵人受傷時本體保持顯示、短暫反白 (把材質的基本色調亮再恢復)；持續受傷時一亮一暗地閃；結束或死亡時回到原本的顏色
public class EnemyHitFlashTests
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private GameObject enemyObject;
    private EnemyHealth health;
    private Renderer body;
    private MaterialPropertyBlock block;
    private float originalBrightness;

    [SetUp]
    public void SetUp()
    {
        // 測試用 Cube 的預設材質是 URP Lit，基本色屬性為 _BaseColor
        enemyObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        enemyObject.SetActive(false);
        health = enemyObject.AddComponent<EnemyHealth>();
        EnemyHitFlash flash = enemyObject.AddComponent<EnemyHitFlash>();
        TestUtil.SetField(flash, "holdDuration", 0.05f);
        TestUtil.SetField(flash, "fadeDuration", 0.1f);
        TestUtil.SetField(flash, "pulseInterval", 0.25f);
        body = enemyObject.GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
        Color original = body.sharedMaterial.GetColor(BaseColorId);
        originalBrightness = original.r + original.g + original.b;
        enemyObject.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(enemyObject);
    }

    // 相對於原本基本色多亮了多少 (0 = 原色)
    private float ExtraBrightness()
    {
        body.GetPropertyBlock(block, 0);
        if (!block.HasColor(BaseColorId)) return 0f;
        Color color = block.GetColor(BaseColorId);
        return color.r + color.g + color.b - originalBrightness;
    }

    [UnityTest]
    public IEnumerator Damage_WhitensWhileStayingVisible_ThenRestores()
    {
        yield return null;

        health.TakeDamage(1);
        yield return null;
        Assert.IsTrue(body.enabled, "the enemy must stay visible");
        Assert.Greater(ExtraBrightness(), 0.5f, "the enemy should be brightened");

        yield return new WaitForSeconds(0.4f);
        Assert.AreEqual(0f, ExtraBrightness(), 0.001f);
    }

    // 雷射每 0.1 秒扣一次血：要看得出一亮一暗的閃爍，而不是一直維持在亮的狀態
    [UnityTest]
    public IEnumerator ContinuousDamage_PulsesInsteadOfStayingLit()
    {
        yield return null;

        bool sawLit = false;
        bool sawRestored = false;
        for (float elapsed = 0f; elapsed < 0.8f; elapsed += Time.deltaTime)
        {
            health.TakeDamage(1);
            float extra = ExtraBrightness();
            if (extra > 0.5f) sawLit = true;
            if (sawLit && extra < 0.001f) sawRestored = true;
            yield return null;
        }

        Assert.IsTrue(sawLit, "a hit should flash clearly");
        Assert.IsTrue(sawRestored, "under continuous damage the flash must go back to normal between pulses");
    }

    [UnityTest]
    public IEnumerator Death_RestoresOriginalColor()
    {
        yield return null;

        health.TakeDamage(health.MaxHealth);

        Assert.IsTrue(body.enabled);
        Assert.AreEqual(0f, ExtraBrightness(), 0.001f);
    }
}
