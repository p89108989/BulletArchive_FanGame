using System.Collections;
using NUnit.Framework;
using Is = UnityEngine.TestTools.Constraints.Is;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using VolumetricLines;

// 雷射外觀：按住發射時每幀都會更新光束端點，必須不產生 GC；光束長度為 0 (貼著敵人開火) 時不能每幀噴警告
public class LaserBeamVisualTests
{
    private GameObject root;
    private LaserBeamVisual visual;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Beam");
        root.SetActive(false);

        GameObject lineObject = new GameObject("Line", typeof(MeshFilter), typeof(MeshRenderer));
        lineObject.transform.SetParent(root.transform, false);
        VolumetricLineBehavior line = lineObject.AddComponent<VolumetricLineBehavior>();

        ParticleSystem muzzle = CreateParticles("Muzzle");
        ParticleSystem impact = CreateParticles("Impact");

        visual = root.AddComponent<LaserBeamVisual>();
        TestUtil.SetField(visual, "line", line);
        TestUtil.SetField(visual, "muzzleParticles", muzzle);
        TestUtil.SetField(visual, "impactParticles", impact);
        TestUtil.SetField(visual, "tintedParticles", new[] { muzzle, impact });
        root.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(root);
    }

    private ParticleSystem CreateParticles(string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = false;
        return system;
    }

    [UnityTest]
    public IEnumerator Show_SteadyState_DoesNotAllocate()
    {
        visual.Show(Vector3.zero, new Vector3(0f, 0f, 10f), Color.blue, 2f, false);
        yield return null;
        visual.Show(Vector3.zero, new Vector3(0f, 0f, 11f), Color.blue, 2f, false);

        Assert.That(
            () => visual.Show(new Vector3(1f, 0f, 0f), new Vector3(1f, 0f, 12f), Color.blue, 2f, false),
            Is.Not.AllocatingGCMemory());
    }

    [UnityTest]
    public IEnumerator Show_ZeroLength_LogsNothing()
    {
        yield return null;

        Vector3 point = new Vector3(3f, 0f, 3f);
        visual.Show(point, point, Color.blue, 2f, true);

        LogAssert.NoUnexpectedReceived();
    }
}
