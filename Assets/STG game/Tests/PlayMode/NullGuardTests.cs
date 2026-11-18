using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Inspector 必填欄位留空時：記錄清楚的錯誤並停用元件，而不是丟出 NullReferenceException
public class NullGuardTests
{
    private GameObject subject;

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(subject);
    }

    [UnityTest]
    public IEnumerator PlayerLaser_MissingBeamPrefab_LogsErrorAndDisables()
    {
        subject = new GameObject("Player");
        subject.SetActive(false);
        PlayerLaser laser = subject.AddComponent<PlayerLaser>();

        LogAssert.Expect(LogType.Error, new Regex("beamPrefab"));
        subject.SetActive(true);
        yield return null;

        Assert.IsFalse(laser.enabled);
    }

    [UnityTest]
    public IEnumerator PlayerLaser_BeamPrefabWithoutVisual_LogsErrorAndDisables()
    {
        GameObject notABeam = new GameObject("NotABeam");
        subject = new GameObject("Player");
        subject.SetActive(false);
        PlayerLaser laser = subject.AddComponent<PlayerLaser>();
        TestUtil.SetField(laser, "beamPrefab", notABeam);

        LogAssert.Expect(LogType.Error, new Regex("LaserBeamVisual"));
        subject.SetActive(true);
        yield return null;

        Assert.IsFalse(laser.enabled);
        Object.Destroy(notABeam);
        foreach (GameObject clone in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (clone.name == "NotABeam") Object.Destroy(clone);
        }
    }

    // 必填欄位缺漏而自我停用的範圍攻擊，被 Spawner 呼叫 Begin 時不能丟例外，且要立即歸還物件池 (否則永遠不會回收)
    [UnityTest]
    public IEnumerator AreaStrike_MissingRingRenderer_BeginReturnsToPoolWithoutThrowing()
    {
        subject = new GameObject("AreaStrike");
        subject.SetActive(false);
        GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fill.transform.SetParent(subject.transform);
        AreaStrike strike = subject.AddComponent<AreaStrike>();
        TestUtil.SetField(strike, "fillRenderer", fill.GetComponent<Renderer>());
        TestUtil.SetField(strike, "fillDisc", fill.transform);

        LogAssert.Expect(LogType.Error, new Regex("ringRenderer"));
        subject.SetActive(true);
        yield return null;

        bool returned = false;
        strike.Begin(10f, 1f, BulletPolarity.None, s => returned = true);

        Assert.IsTrue(returned);
    }

    [UnityTest]
    public IEnumerator ApacheGunship_MissingBodyMesh_LogsErrorAndDisables()
    {
        subject = new GameObject("Apache");
        subject.SetActive(false);
        ApacheGunship gunship = subject.AddComponent<ApacheGunship>();
        GameObject model = new GameObject("Model");
        model.transform.SetParent(subject.transform);
        TestUtil.SetField(gunship, "modelRoot", model);

        LogAssert.Expect(LogType.Error, new Regex("bodyMesh"));
        subject.SetActive(true);
        yield return null;

        Assert.IsFalse(gunship.enabled);
    }
}
