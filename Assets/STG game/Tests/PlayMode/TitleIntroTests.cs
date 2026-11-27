using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// 開始畫面的開場慢動作：總控把同一個進度 (0 → 1，越來越慢) 套到每個演出元件；
// 光束從一半長度延伸到落點、碰地後爆炸才出現並長大；移動物件從起點滑到終點
public class TitleIntroTests
{
    private readonly List<Object> spawned = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object o in spawned) Object.Destroy(o);
        spawned.Clear();
    }

    private GameObject New(string name)
    {
        GameObject go = new GameObject(name);
        spawned.Add(go);
        return go;
    }

    private IntroMover CreateMover(Vector3 from, Vector3 to)
    {
        GameObject go = New("Mover");
        IntroMover mover = go.AddComponent<IntroMover>();
        TestUtil.SetField(mover, "fromPosition", from);
        TestUtil.SetField(mover, "toPosition", to);
        return mover;
    }

    private IntroBeam CreateBeam(out Transform tube, out Transform explosion)
    {
        GameObject go = New("Beam");
        tube = New("Tube").transform;
        explosion = New("Explosion").transform;
        IntroBeam beam = go.AddComponent<IntroBeam>();
        TestUtil.SetField(beam, "tube", tube);
        TestUtil.SetField(beam, "explosion", explosion);
        TestUtil.SetField(beam, "from", new Vector3(0f, 20f, 0f));
        TestUtil.SetField(beam, "to", new Vector3(0f, 0f, 20f));
        TestUtil.SetField(beam, "width", 2f);
        TestUtil.SetField(beam, "startFraction", 0.5f);
        TestUtil.SetField(beam, "hitAt", 0.6f);
        TestUtil.SetField(beam, "explosionScale", new Vector3(4f, 2f, 4f));
        return beam;
    }

    private static float Length(Transform cylinder)
    {
        // 內建圓柱高 2，Y 縮放 = 長度的一半
        return cylinder.localScale.y * 2f;
    }

    [Test]
    public void Mover_GoesFromStartToEnd()
    {
        IntroMover mover = CreateMover(new Vector3(0f, 0f, 10f), new Vector3(4f, 0f, 2f));

        mover.SetProgress(0f);
        Assert.Less(Vector3.Distance(mover.transform.position, new Vector3(0f, 0f, 10f)), 0.001f);

        mover.SetProgress(1f);
        Assert.Less(Vector3.Distance(mover.transform.position, new Vector3(4f, 0f, 2f)), 0.001f);
    }

    [Test]
    public void Beam_StartsAtHalfLength()
    {
        IntroBeam beam = CreateBeam(out Transform tube, out _);
        float full = Vector3.Distance(new Vector3(0f, 20f, 0f), new Vector3(0f, 0f, 20f));

        beam.SetProgress(0f);

        Assert.AreEqual(full * 0.5f, Length(tube), 0.01f);
        Assert.AreEqual(2f, tube.localScale.x, 0.001f, "width");
    }

    [Test]
    public void Beam_ReachesTheGroundAtHitTime()
    {
        IntroBeam beam = CreateBeam(out Transform tube, out _);
        float full = Vector3.Distance(new Vector3(0f, 20f, 0f), new Vector3(0f, 0f, 20f));

        beam.SetProgress(0.6f);

        Assert.AreEqual(full, Length(tube), 0.01f);
        Assert.Greater(Vector3.Dot(tube.up, new Vector3(0f, -20f, 20f).normalized), 0.999f, "the cylinder points from the source to the ground");
    }

    [Test]
    public void Explosion_OnlyAppearsAfterTheBeamHits_ThenGrows()
    {
        IntroBeam beam = CreateBeam(out _, out Transform explosion);

        beam.SetProgress(0.5f);
        Assert.IsFalse(explosion.gameObject.activeSelf, "no explosion before the hit");

        beam.SetProgress(0.8f);
        Assert.IsTrue(explosion.gameObject.activeSelf);
        Vector3 halfway = explosion.localScale;

        beam.SetProgress(1f);
        Assert.Less(Vector3.Distance(explosion.localScale, new Vector3(4f, 2f, 4f)), 0.001f, "full size at the end");
        Assert.Less(halfway.x, explosion.localScale.x, "still growing halfway");
        Assert.AreEqual(new Vector3(0f, 0f, 20f), explosion.position, "explosion sits on the impact point");
    }

    private static float ShaderTime(Renderer renderer)
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        return block.GetFloat("_CustomTime");
    }

    // 開始畫面的光束動畫 (雷射波浪、雷電閃動) 跟著慢動作放慢，定格時停住
    [UnityTest]
    public IEnumerator ShaderTime_RunsAtStart_AndFreezesAtTheEnd()
    {
        GameObject go = New("Beam");
        Renderer renderer = GameObject.CreatePrimitive(PrimitiveType.Cube).GetComponent<Renderer>();
        spawned.Add(renderer.gameObject);
        IntroShaderTime time = go.AddComponent<IntroShaderTime>();
        TestUtil.SetField(time, "renderers", new[] { renderer });

        time.SetProgress(0f);
        yield return null;
        yield return null;
        float running = ShaderTime(renderer);
        Assert.Greater(running, 0f, "time advances at full speed");

        time.SetProgress(1f);
        yield return null;
        float frozen = ShaderTime(renderer);
        yield return null;
        yield return null;

        Assert.AreEqual(frozen, ShaderTime(renderer), 1e-6f, "frozen when the intro ends");
    }

    [UnityTest]
    public IEnumerator Sequence_DrivesPartsToTheEnd_AndStops()
    {
        IntroMover mover = CreateMover(Vector3.zero, new Vector3(0f, 0f, 5f));
        GameObject go = New("Sequence");
        go.SetActive(false);
        TitleIntroSequence sequence = go.AddComponent<TitleIntroSequence>();
        TestUtil.SetField(sequence, "duration", 0.3f);
        TestUtil.SetField(sequence, "parts", new MonoBehaviour[] { mover });
        go.SetActive(true);

        yield return null;
        Assert.Less(mover.transform.position.z, 5f, "still moving at the start");

        yield return new WaitForSeconds(0.5f);
        Assert.AreEqual(5f, mover.transform.position.z, 0.001f, "arrives exactly at the end");
        Assert.IsTrue(sequence.IsFinished);
    }
}
