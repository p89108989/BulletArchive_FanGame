using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// 警示區的呼吸閃爍：透明度在 minAlpha ~ maxAlpha 之間來回變化 (顏色不變)，用 MaterialPropertyBlock 不動到共用材質
public class PulseAlphaTests
{
    private GameObject quad;

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(quad);
    }

    private float CurrentAlpha()
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        quad.GetComponent<Renderer>().GetPropertyBlock(block);
        return block.GetColor("_BaseColor").a;
    }

    [UnityTest]
    public IEnumerator AlphaPulsesBetweenMinAndMax()
    {
        quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.SetActive(false);
        PulseAlpha pulse = quad.AddComponent<PulseAlpha>();
        TestUtil.SetField(pulse, "color", new Color(1f, 0f, 0f, 1f));
        TestUtil.SetField(pulse, "minAlpha", 0.1f);
        TestUtil.SetField(pulse, "maxAlpha", 0.5f);
        TestUtil.SetField(pulse, "period", 0.4f);
        quad.SetActive(true);

        float min = 1f, max = 0f;
        float end = Time.time + 0.5f;
        while (Time.time < end)
        {
            yield return null;
            float alpha = CurrentAlpha();
            min = Mathf.Min(min, alpha);
            max = Mathf.Max(max, alpha);
        }

        Assert.GreaterOrEqual(min, 0.099f);
        Assert.LessOrEqual(max, 0.501f);
        Assert.Greater(max - min, 0.2f, "it actually pulses");
    }
}
