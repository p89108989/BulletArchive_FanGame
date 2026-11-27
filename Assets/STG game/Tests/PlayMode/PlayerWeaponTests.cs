using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using VolumetricLines;

// Rio 的武器：平常按住射擊是一小段一小段的光束子彈 (不消除敵方子彈、不耗能量)；
// 按 F 花藍黃能量各 30% 開啟 5 秒的綠色粗光束 (同時消除藍、黃子彈)，時間到回到一般射擊。
// 使用 InputTestFixture 的虛擬滑鼠/鍵盤，不受 Editor 視窗焦點影響
public class PlayerWeaponTests : InputTestFixture
{
    private const float OriginHeight = 5.8f;

    private GameObject beamTemplate;
    private GameObject shotTemplate;
    private GameObject playerObject;
    private PlayerLaser laser;
    private PlayerEnergy energy;
    private readonly System.Collections.Generic.List<GameObject> created = new System.Collections.Generic.List<GameObject>();
    private int enemyLayer;
    private int bulletLayer;

    public override void Setup()
    {
        base.Setup();
        enemyLayer = LayerMask.NameToLayer("Enemy");
        bulletLayer = LayerMask.NameToLayer("EnemyBullet");

        beamTemplate = CreateBeamTemplate();
        shotTemplate = new GameObject("ShotTemplate");
        shotTemplate.AddComponent<LineRenderer>();

        playerObject = new GameObject("Player");
        playerObject.SetActive(false);
        playerObject.AddComponent<CapsuleCollider>();
        laser = playerObject.AddComponent<PlayerLaser>();
        energy = playerObject.GetComponent<PlayerEnergy>();
        playerObject.GetComponent<Rigidbody>().useGravity = false;
        TestUtil.SetField(laser, "beamPrefab", beamTemplate);
        TestUtil.SetField(laser, "shotPrefab", shotTemplate);
        TestUtil.SetField(laser, "originHeight", OriginHeight);
        TestUtil.SetField(laser, "enemyLayers", (LayerMask)LayerMask.GetMask("Enemy"));
        TestUtil.SetField(laser, "bulletLayers", (LayerMask)LayerMask.GetMask("EnemyBullet"));
        TestUtil.SetField(energy, "startingEnergy", 50f);
    }

    public override void TearDown()
    {
        Object.Destroy(playerObject);
        Object.Destroy(beamTemplate);
        Object.Destroy(shotTemplate);
        foreach (GameObject go in created) Object.Destroy(go);
        foreach (LaserBeamVisual beam in Object.FindObjectsByType<LaserBeamVisual>(FindObjectsSortMode.None)) Object.Destroy(beam.gameObject);
        foreach (LineRenderer line in Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.Destroy(line.gameObject);
        base.TearDown();
    }

    private static GameObject CreateBeamTemplate()
    {
        GameObject root = new GameObject("BeamTemplate");
        root.SetActive(false);
        GameObject lineObject = new GameObject("Line", typeof(MeshFilter), typeof(MeshRenderer));
        lineObject.transform.SetParent(root.transform, false);
        VolumetricLineBehavior line = lineObject.AddComponent<VolumetricLineBehavior>();
        ParticleSystem muzzle = new GameObject("Muzzle").AddComponent<ParticleSystem>();
        muzzle.transform.SetParent(root.transform, false);
        ParticleSystem impact = new GameObject("Impact").AddComponent<ParticleSystem>();
        impact.transform.SetParent(root.transform, false);
        LaserBeamVisual visual = root.AddComponent<LaserBeamVisual>();
        TestUtil.SetField(visual, "line", line);
        TestUtil.SetField(visual, "muzzleParticles", muzzle);
        TestUtil.SetField(visual, "impactParticles", impact);
        TestUtil.SetField(visual, "tintedParticles", new[] { muzzle, impact });
        root.SetActive(true);
        return root;
    }

    // 範本本身也帶 LaserBeamVisual (且光束是隱藏的)，要排除範本、只看武器生成的那個
    private LaserBeamVisual WeaponBeam()
    {
        foreach (LaserBeamVisual beam in Object.FindObjectsByType<LaserBeamVisual>(FindObjectsSortMode.None))
        {
            if (beam.gameObject != beamTemplate) return beam;
        }

        return null;
    }

    private bool BeamVisible()
    {
        LaserBeamVisual beam = WeaponBeam();
        return beam != null && beam.transform.Find("Line").gameObject.activeSelf;
    }

    private EnemyHealth CreateEnemy(Vector3 position)
    {
        GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Cube);
        enemy.layer = enemyLayer;
        enemy.transform.position = position;
        enemy.transform.localScale = Vector3.one * 4f;
        created.Add(enemy);
        return enemy.AddComponent<EnemyHealth>();
    }

    private void CreateBullet(Vector3 position, BulletPolarity polarity, System.Action<EnemyBullet> onDespawn)
    {
        GameObject bullet = new GameObject("Bullet");
        bullet.layer = bulletLayer;
        bullet.transform.position = position;
        bullet.AddComponent<SphereCollider>().isTrigger = true;
        bullet.AddComponent<EnemyBullet>().Launch(Vector3.forward, 0f, polarity, onDespawn);
        created.Add(bullet);
    }

    [UnityTest]
    public IEnumerator HoldingFire_ShootsSegmentsInsteadOfBeam()
    {
        playerObject.SetActive(true);
        Mouse mouse = InputSystem.AddDevice<Mouse>();
        Press(mouse.leftButton);
        yield return new WaitForSeconds(0.3f);

        Assert.IsTrue(laser.IsFiring);
        Assert.Greater(laser.ActiveShotCount, 0, "short beam segments are flying");
        Assert.IsFalse(BeamVisible(), "the continuous beam is no longer the default attack");
        Assert.AreEqual(50f, energy.GetEnergy(BulletPolarity.Blue), 0.001f, "normal shots cost no energy");
    }

    [UnityTest]
    public IEnumerator Shot_DamagesEnemyInFront()
    {
        EnemyHealth enemy = CreateEnemy(new Vector3(0f, OriginHeight, 25f));
        playerObject.SetActive(true);
        Mouse mouse = InputSystem.AddDevice<Mouse>();
        Press(mouse.leftButton);
        yield return new WaitForSeconds(0.6f);

        Assert.Less(enemy.CurrentHealth, enemy.MaxHealth);
    }

    [UnityTest]
    public IEnumerator Shot_DoesNotCancelEnemyBullets()
    {
        bool removed = false;
        CreateBullet(new Vector3(0f, OriginHeight, 12f), BulletPolarity.Yellow, b => removed = true);
        playerObject.SetActive(true);
        Mouse mouse = InputSystem.AddDevice<Mouse>();
        Press(mouse.leftButton);
        yield return new WaitForSeconds(0.4f);

        Assert.IsFalse(removed);
    }

    [UnityTest]
    public IEnumerator BurstKey_WithEnergy_FiresGreenBeamUntilTimeRunsOut()
    {
        TestUtil.SetField(laser, "burstDuration", 0.4f);
        playerObject.SetActive(true);
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Mouse mouse = InputSystem.AddDevice<Mouse>();
        yield return null;

        PressAndRelease(keyboard.fKey);
        Press(mouse.leftButton);
        yield return null;
        yield return null;

        Assert.IsTrue(laser.IsBurstActive, "F starts the burst");
        Assert.AreEqual(20f, energy.GetEnergy(BulletPolarity.Blue), 0.001f, "costs 30% blue");
        Assert.AreEqual(20f, energy.GetEnergy(BulletPolarity.Yellow), 0.001f, "costs 30% yellow");
        Assert.IsTrue(BeamVisible(), "the beam is shown while firing during the burst");
        // 光束顏色同時套在發射點的粒子上 (測試用的光束沒有材質，讀粒子顏色比較可靠)
        Color beamColor = WeaponBeam().transform.Find("Muzzle").GetComponent<ParticleSystem>().main.startColor.color;
        Assert.Greater(beamColor.g, beamColor.r);
        Assert.Greater(beamColor.g, beamColor.b, "the merged beam is green");

        yield return new WaitForSeconds(0.5f);

        Assert.IsFalse(laser.IsBurstActive);
        Assert.IsFalse(BeamVisible(), "back to normal shots");
        Assert.IsTrue(laser.IsFiring, "still shooting normally");
    }

    [UnityTest]
    public IEnumerator BurstKey_WithoutEnoughEnergy_DoesNothing()
    {
        TestUtil.SetField(energy, "startingEnergy", 10f);
        playerObject.SetActive(true);
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        yield return null;

        PressAndRelease(keyboard.fKey);
        yield return null;

        Assert.IsFalse(laser.IsBurstActive);
        Assert.AreEqual(10f, energy.GetEnergy(BulletPolarity.Blue), 0.001f);
    }

    // 技能卡用：藍黃都夠 30% 才能開；開啟中不能再開 (卡片顯示發動中而不是可用)
    [UnityTest]
    public IEnumerator CanStartBurst_NeedsEnergyAndNotAlreadyActive()
    {
        playerObject.SetActive(true);
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        yield return null;
        Assert.IsTrue(laser.CanStartBurst, "50% blue and yellow is enough");

        PressAndRelease(keyboard.fKey);
        yield return null;

        Assert.IsTrue(laser.IsBurstActive);
        Assert.IsFalse(laser.CanStartBurst, "already active");
        Assert.AreEqual(5f, laser.BurstDuration, 0.001f);
    }

    // 對話框等外部系統靠這個事件得知大招開了
    [UnityTest]
    public IEnumerator BurstKey_RaisesBurstStartedOnce()
    {
        playerObject.SetActive(true);
        int started = 0;
        laser.OnBurstStarted += () => started++;
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        yield return null;

        PressAndRelease(keyboard.fKey);
        yield return null;
        PressAndRelease(keyboard.fKey);
        yield return null;

        Assert.AreEqual(1, started, "pressing F again while active does not restart the burst");
    }

    [UnityTest]
    public IEnumerator CanStartBurst_FalseWithoutEnoughEnergy()
    {
        TestUtil.SetField(energy, "startingEnergy", 10f);
        playerObject.SetActive(true);
        yield return null;

        Assert.IsFalse(laser.CanStartBurst);
    }

    [UnityTest]
    public IEnumerator BurstBeam_CancelsBothBlueAndYellowBullets()
    {
        bool yellowRemoved = false;
        bool blueRemoved = false;
        CreateBullet(new Vector3(0f, OriginHeight, 12f), BulletPolarity.Yellow, b => yellowRemoved = true);
        CreateBullet(new Vector3(0f, OriginHeight, 18f), BulletPolarity.Blue, b => blueRemoved = true);
        playerObject.SetActive(true);
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Mouse mouse = InputSystem.AddDevice<Mouse>();
        yield return null;

        PressAndRelease(keyboard.fKey);
        Press(mouse.leftButton);
        yield return new WaitForSeconds(0.2f);

        Assert.IsTrue(yellowRemoved);
        Assert.IsTrue(blueRemoved);
    }
}
