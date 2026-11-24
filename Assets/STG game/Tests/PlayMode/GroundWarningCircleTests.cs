using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// 可移動的地面紅圈警示：外圈依半徑縮放、內圈依倒數進度由中心擴張，可每幀改位置 (跟著玩家走)
public class GroundWarningCircleTests
{
    private GameObject root;
    private GroundWarningCircle warning;
    private Transform ring;
    private Transform fill;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Warning");
        root.SetActive(false);
        ring = new GameObject("Ring").transform;
        ring.SetParent(root.transform, false);
        fill = new GameObject("Fill").transform;
        fill.SetParent(root.transform, false);
        warning = root.AddComponent<GroundWarningCircle>();
        TestUtil.SetField(warning, "ring", ring);
        TestUtil.SetField(warning, "fill", fill);
        root.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(root);
    }

    [UnityTest]
    public IEnumerator Show_PlacesRingAtCenterWithRadius()
    {
        warning.Show(new Vector3(5f, 0.3f, 2f), 4f);
        yield return null;

        Assert.AreEqual(5f, root.transform.position.x, 0.01f);
        Assert.AreEqual(2f, root.transform.position.z, 0.01f);
        Assert.AreEqual(8f, ring.localScale.x, 0.01f, "ring diameter = 2 x radius");
        Assert.AreEqual(0f, fill.localScale.x, 0.01f, "the countdown starts empty");
    }

    [UnityTest]
    public IEnumerator SetProgress_GrowsFillFromCenter()
    {
        warning.Show(Vector3.zero, 4f);
        warning.SetProgress(0.5f);
        yield return null;

        Assert.AreEqual(4f, fill.localScale.x, 0.01f);
    }

    [UnityTest]
    public IEnumerator Hide_DeactivatesWarning()
    {
        warning.Show(Vector3.zero, 4f);
        warning.Hide();
        yield return null;

        Assert.IsFalse(root.activeSelf);
    }
}
