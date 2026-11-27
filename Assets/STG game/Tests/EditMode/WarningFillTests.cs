using NUnit.Framework;
using UnityEngine;

// 地板警示漸漸塗滿：以起點邊緣為基準，沿 +Z 方向依進度 (0~1) 拉長，塗滿 = 光炮發射
public class WarningFillTests
{
    private GameObject pivot;
    private Renderer fill;

    [SetUp]
    public void SetUp()
    {
        pivot = new GameObject("FillPivot");
        pivot.transform.position = new Vector3(0f, 0f, -10f);
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.transform.SetParent(pivot.transform, false);
        // 長 100 的面放在起點之後 (z 從 0 到 100)
        quad.transform.localPosition = new Vector3(0f, 0f, 50f);
        quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        quad.transform.localScale = new Vector3(20f, 100f, 1f);
        fill = quad.GetComponent<Renderer>();
        pivot.AddComponent<WarningFill>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(pivot);
    }

    [Test]
    public void Half_FillsFromTheStartEdgeToHalfway()
    {
        pivot.GetComponent<WarningFill>().SetProgress(0.5f);

        Assert.AreEqual(-10f, fill.bounds.min.z, 0.01f, "anchored at the start edge");
        Assert.AreEqual(40f, fill.bounds.max.z, 0.01f, "half of the 100 m length");
    }

    [Test]
    public void Full_CoversTheWholeLength()
    {
        pivot.GetComponent<WarningFill>().SetProgress(1f);

        Assert.AreEqual(90f, fill.bounds.max.z, 0.01f);
    }

    [Test]
    public void OutOfRange_IsClamped()
    {
        WarningFill warning = pivot.GetComponent<WarningFill>();

        warning.SetProgress(3f);
        Assert.AreEqual(90f, fill.bounds.max.z, 0.01f);

        warning.SetProgress(-1f);
        Assert.Less(fill.bounds.max.z, -9.9f, "nothing filled");
    }
}
