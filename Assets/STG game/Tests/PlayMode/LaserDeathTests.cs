using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using VolumetricLines;

// 玩家在按住雷射時死亡：光束必須立即隱藏、之後保持隱藏，且不再消耗能量。
// 使用 InputTestFixture 的虛擬滑鼠，不受 Editor 視窗焦點影響
public class LaserDeathTests : InputTestFixture
{
    private GameObject beamTemplate;
    private GameObject playerObject;
    private PlayerLaser laser;
    private PlayerEnergy energy;
    private PlayerHealth health;

    public override void Setup()
    {
        base.Setup();

        beamTemplate = CreateBeamTemplate();

        playerObject = new GameObject("Player");
        playerObject.SetActive(false);
        playerObject.AddComponent<CapsuleCollider>();
        laser = playerObject.AddComponent<PlayerLaser>();
        energy = playerObject.GetComponent<PlayerEnergy>();
        health = playerObject.GetComponent<PlayerHealth>();
        playerObject.GetComponent<Rigidbody>().useGravity = false;
        TestUtil.SetField(laser, "beamPrefab", beamTemplate);
        TestUtil.SetField(energy, "startingEnergy", 50f);
        PlayerDeathHandler death = playerObject.AddComponent<PlayerDeathHandler>();
        TestUtil.SetField(death, "disableOnDeath", new Behaviour[] { laser });
        playerObject.SetActive(true);
    }

    public override void TearDown()
    {
        Object.Destroy(playerObject);
        Object.Destroy(beamTemplate);
        foreach (LaserBeamVisual beam in Object.FindObjectsByType<LaserBeamVisual>(FindObjectsSortMode.None)) Object.Destroy(beam.gameObject);
        base.TearDown();
    }

    // 與 LaserBeamVisualTests 相同的最小光束外觀，作為 PlayerLaser 要生成的 beamPrefab
    private static GameObject CreateBeamTemplate()
    {
        GameObject root = new GameObject("BeamTemplate");
        root.SetActive(false);
        GameObject lineObject = new GameObject("Line", typeof(MeshFilter), typeof(MeshRenderer));
        lineObject.transform.SetParent(root.transform, false);
        VolumetricLineBehavior line = lineObject.AddComponent<VolumetricLineBehavior>();
        ParticleSystem muzzle = CreateParticles(root.transform, "Muzzle");
        ParticleSystem impact = CreateParticles(root.transform, "Impact");
        LaserBeamVisual visual = root.AddComponent<LaserBeamVisual>();
        TestUtil.SetField(visual, "line", line);
        TestUtil.SetField(visual, "muzzleParticles", muzzle);
        TestUtil.SetField(visual, "impactParticles", impact);
        TestUtil.SetField(visual, "tintedParticles", new[] { muzzle, impact });
        // 範本需為啟用狀態：Instantiate 會複製啟用狀態，停用的複本不會執行 Awake
        root.SetActive(true);
        return root;
    }

    private static ParticleSystem CreateParticles(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = false;
        return system;
    }

    [UnityTest]
    public IEnumerator DyingWhileFiring_HidesBeamAndStopsDrain()
    {
        Mouse mouse = InputSystem.AddDevice<Mouse>();
        Press(mouse.leftButton);
        yield return null;
        yield return null;
        Assert.IsTrue(laser.IsFiring, "precondition: laser should be firing while LMB is held");

        health.TakeDamage(health.MaxHealth);
        float energyAtDeath = energy.GetEnergy(BulletPolarity.Blue);
        for (int i = 0; i < 5; i++) yield return null;

        LaserBeamVisual beam = Object.FindAnyObjectByType<LaserBeamVisual>();
        Assert.IsFalse(laser.IsFiring);
        Assert.IsFalse(beam.transform.Find("Line").gameObject.activeSelf);
        Assert.AreEqual(energyAtDeath, energy.GetEnergy(BulletPolarity.Blue));
    }
}
