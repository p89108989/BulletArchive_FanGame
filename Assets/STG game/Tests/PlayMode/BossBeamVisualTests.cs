using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Boss 雷射外觀：光束從起點延伸到終點 (圓管網格沿本地 Z 從 0 到 1)，長度與方向都要對；收起時隱藏
public class BossBeamVisualTests
{
    private GameObject beamObject;
    private BossBeamVisual beam;
    private Transform tube;

    [SetUp]
    public void SetUp()
    {
        beamObject = new GameObject("Beam");
        beamObject.SetActive(false);
        tube = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        tube.SetParent(beamObject.transform, false);
        beam = beamObject.AddComponent<BossBeamVisual>();
        TestUtil.SetField(beam, "beamTubes", new[] { tube.GetComponent<Renderer>() });
        TestUtil.SetField(beam, "tubeWidthScales", new[] { 1f });
        TestUtil.SetField(beam, "width", 4f);
        beamObject.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(beamObject);
    }

    [UnityTest]
    public IEnumerator SetBeam_StretchesTubeFromStartToEnd()
    {
        Vector3 start = new Vector3(0f, 10f, 0f);
        Vector3 end = new Vector3(0f, 0f, 30f);

        beam.SetBeam(start, end, 1f);
        yield return null;

        float length = Vector3.Distance(start, end);
        Assert.Less(Vector3.Distance(tube.position, start), 0.01f, "the tube starts at the origin");
        Assert.Greater(Vector3.Dot(tube.forward, (end - start).normalized), 0.999f, "the tube points at the end");
        Assert.AreEqual(length, tube.lossyScale.z, 0.01f);
        Assert.AreEqual(4f, tube.lossyScale.x, 0.01f);
    }

    // 光束要從眼球裡面噴出來 (起點往後退)，起點的鼓起才能把整顆眼睛包住
    [UnityTest]
    public IEnumerator SetBeam_StartsBehindOriginByBackOffset()
    {
        TestUtil.SetField(beam, "startBackOffset", 3f);
        Vector3 start = new Vector3(0f, 10f, 0f);
        Vector3 end = new Vector3(0f, 10f, 30f);

        beam.SetBeam(start, end, 1f);
        yield return null;

        Assert.Less(Vector3.Distance(tube.position, new Vector3(0f, 10f, -3f)), 0.01f, "the tube starts inside the eye");
        Assert.AreEqual(33f, tube.lossyScale.z, 0.01f, "the beam still reaches the end");
    }

    [UnityTest]
    public IEnumerator Hide_DeactivatesBeam()
    {
        beam.SetBeam(Vector3.zero, Vector3.forward * 10f, 1f);
        yield return null;

        beam.Hide();

        Assert.IsFalse(beamObject.activeSelf);
    }
}
