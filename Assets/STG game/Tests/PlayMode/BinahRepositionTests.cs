using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Binah 換位：Move_Start 潛入 → 在地底換到新位置 (期間打不到) → Move_End 竄出，竄出時潑沙並通知外部 (例如生成沙堆)
public class BinahRepositionTests
{
    private GameObject boss;
    private GameObject splashPrefab;
    private BoxCollider body;
    private BinahReposition reposition;

    [SetUp]
    public void SetUp()
    {
        splashPrefab = new GameObject("Splash");
        splashPrefab.SetActive(false);

        boss = new GameObject("Binah");
        boss.SetActive(false);
        body = new GameObject("Body").AddComponent<BoxCollider>();
        body.transform.SetParent(boss.transform);
        reposition = boss.AddComponent<BinahReposition>();
        TestUtil.SetField(reposition, "diveDuration", 0.2f);
        TestUtil.SetField(reposition, "emergeDelay", 0.1f);
        TestUtil.SetField(reposition, "settleDuration", 0.2f);
        TestUtil.SetField(reposition, "splashPrefab", splashPrefab);
        boss.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(boss);
        Object.Destroy(splashPrefab);
        foreach (GameObject leftover in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (leftover.name.StartsWith("Splash(Clone)")) Object.Destroy(leftover);
        }
    }

    [UnityTest]
    public IEnumerator MoveTo_DivesTeleportsAndEmerges()
    {
        Vector3 destination = new Vector3(20f, -5f, 30f);
        int emerged = 0;
        reposition.OnEmerged += () => emerged++;

        reposition.StartCoroutine(reposition.MoveTo(destination, Quaternion.Euler(0f, 90f, 0f)));
        yield return null;
        Assert.IsTrue(reposition.IsMoving);
        Assert.AreEqual(Vector3.zero, boss.transform.position, "still diving at the old spot");

        yield return new WaitForSeconds(0.25f);
        Assert.AreEqual(destination, boss.transform.position);
        Assert.AreEqual(90f, boss.transform.eulerAngles.y, 0.01f);
        Assert.IsFalse(body.enabled, "can't be hit while underground");
        Assert.AreEqual(0, emerged);

        yield return new WaitForSeconds(0.15f);
        Assert.IsTrue(body.enabled, "hittable again once it breaks the surface");
        Assert.AreEqual(1, emerged);
        Assert.IsNotNull(GameObject.Find("Splash(Clone)"), "sand splashes on emergence");

        yield return new WaitForSeconds(0.3f);
        Assert.IsFalse(reposition.IsMoving);
    }
}
