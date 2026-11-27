using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Binah 光炮特殊機制 (血量 2/3、1/3 各一次)：
// 回到中間 → 潑水把玩家推到最下方、護盾失效、生成沙丘 → 蓄力 (期間受到足夠傷害就打斷、進入暈眩受傷加倍)
// → 沒被打斷就發射，沒躲在沙丘後面的玩家扣 15 血 → 結束後沙丘消失、護盾恢復
public class BinahCannonMechanicTests
{
    private const float ChargeDuration = 0.4f;

    private GameObject boss;
    private GameObject player;
    private GameObject dunePrefab;
    private EnemyHealth bossHealth;
    private BinahCannonMechanic mechanic;
    private BinahDuneField duneField;
    private PlayerHealth playerHealth;
    private PlayerPolarity polarity;
    private Rigidbody playerBody;

    [SetUp]
    public void SetUp()
    {
        player = new GameObject("Player");
        player.SetActive(false);
        player.transform.position = new Vector3(0f, 0f, 15f);
        player.AddComponent<CapsuleCollider>();
        PlayerMovement movement = player.AddComponent<PlayerMovement>();
        playerBody = player.GetComponent<Rigidbody>();
        playerBody.useGravity = false;
        playerHealth = player.GetComponent<PlayerHealth>();
        TestUtil.SetField(playerHealth, "maxHealth", 20);
        TestUtil.SetField(playerHealth, "invincibilityDuration", 0f);
        polarity = player.AddComponent<PlayerPolarity>();
        player.SetActive(true);

        // 沙丘：6 m 高的方塊 (擋在嘴與玩家之間就算掩護)
        dunePrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dunePrefab.name = "Dune";
        dunePrefab.transform.localScale = new Vector3(8f, 12f, 2f);
        dunePrefab.transform.position = new Vector3(0f, -500f, 0f);
        dunePrefab.SetActive(false);
        BinahDune duneHealth = dunePrefab.AddComponent<BinahDune>();
        TestUtil.SetField(duneHealth, "maxHealth", 30);
        // 掩護區 (倒梯形)：沙丘後緣 (Z = 11) 往後 20 m，末端寬度剩 40%；
        // 玩家在 Z = 20 (後方約 9 m) 處可用的半寬 ≈ 4 × (1 - 0.6 × 9/20) ≈ 2.9 m
        TestUtil.SetField(duneHealth, "coverLength", 20f);
        TestUtil.SetField(duneHealth, "tipWidthRatio", 0.4f);

        boss = new GameObject("Binah");
        boss.SetActive(false);
        boss.transform.position = new Vector3(30f, 0f, -10f);
        bossHealth = boss.AddComponent<EnemyHealth>();
        TestUtil.SetField(bossHealth, "maxHealth", 300);
        Transform muzzle = new GameObject("Fire_01").transform;
        muzzle.SetParent(boss.transform, false);
        muzzle.localPosition = new Vector3(0f, 10f, 0f);

        duneField = boss.AddComponent<BinahDuneField>();
        TestUtil.SetField(duneField, "dunePrefab", dunePrefab);
        TestUtil.SetField(duneField, "maxDunes", 3);

        mechanic = boss.AddComponent<BinahCannonMechanic>();
        TestUtil.SetField(mechanic, "duneField", duneField);
        TestUtil.SetField(mechanic, "playerHealth", playerHealth);
        TestUtil.SetField(mechanic, "playerMovement", movement);
        TestUtil.SetField(mechanic, "playerPolarity", polarity);
        TestUtil.SetField(mechanic, "triggerFractions", new[] { 2f / 3f, 1f / 3f });
        TestUtil.SetField(mechanic, "chargeDurations", new[] { ChargeDuration, ChargeDuration });
        TestUtil.SetField(mechanic, "centerPosition", Vector3.zero);
        TestUtil.SetField(mechanic, "splashDelay", 0.02f);
        TestUtil.SetField(mechanic, "pushDuration", 0.1f);
        TestUtil.SetField(mechanic, "pushZ", 20f);
        TestUtil.SetField(mechanic, "duneCount", new Vector2Int(0, 0));
        TestUtil.SetField(mechanic, "interruptDamage", 60);
        TestUtil.SetField(mechanic, "fireDuration", 0.2f);
        TestUtil.SetField(mechanic, "cannonDamage", 15);
        TestUtil.SetField(mechanic, "groggyDuration", 0.3f);
        TestUtil.SetField(mechanic, "groggyDamageMultiplier", 1.5f);
        boss.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(boss);
        Object.Destroy(player);
        Object.Destroy(dunePrefab);
        foreach (GameObject leftover in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (leftover.name.StartsWith("Dune(Clone)")) Object.Destroy(leftover);
        }
    }

    // 把 Boss 打到 2/3 以下 (300 → 190)
    private void DropToFirstThreshold()
    {
        bossHealth.TakeDamage(110);
    }

    private IEnumerator WaitForPhase(BinahCannonMechanic.Phase phase, float timeout = 3f)
    {
        float end = Time.realtimeSinceStartup + timeout;
        while (mechanic.CurrentPhase != phase && Time.realtimeSinceStartup < end) yield return null;
        Assert.AreEqual(phase, mechanic.CurrentPhase, "timed out waiting for " + phase);
    }

    // 流沙：觸發時鋪滿整個場地把玩家沖到最下方，沖完才消失
    [UnityTest]
    public IEnumerator Quicksand_ShowsWhilePushing()
    {
        GameObject quicksand = new GameObject("Quicksand");
        quicksand.transform.SetParent(boss.transform);
        TestUtil.SetField(mechanic, "quicksand", quicksand);
        yield return null;
        quicksand.SetActive(false);

        DropToFirstThreshold();
        Assert.IsTrue(quicksand.activeSelf, "the sand flows as soon as the mechanic starts");

        yield return WaitForPhase(BinahCannonMechanic.Phase.Charging);
        Assert.IsFalse(quicksand.activeSelf);
    }

    // 沙丘在流沙停下後才堆出來 (先出現的話玩家被沖到一半會卡在沙丘上)
    [UnityTest]
    public IEnumerator Dunes_FormAfterThePush()
    {
        TestUtil.SetField(mechanic, "duneCount", new Vector2Int(1, 1));
        yield return null;

        DropToFirstThreshold();
        Assert.AreEqual(0, mechanic.ActiveDuneCount, "no dunes while the player is being pushed");

        yield return WaitForPhase(BinahCannonMechanic.Phase.Charging);
        Assert.AreEqual(1, mechanic.ActiveDuneCount);
    }

    // 有換位元件時，回到中間改成 Move_Start 潛入 → 竄出 (不是瞬間移動)，竄出後才開始流沙推人
    [UnityTest]
    public IEnumerator WithReposition_DivesToTheCenterBeforeTheQuicksand()
    {
        BinahReposition reposition = boss.AddComponent<BinahReposition>();
        TestUtil.SetField(reposition, "diveDuration", 0.2f);
        TestUtil.SetField(reposition, "emergeDelay", 0.05f);
        TestUtil.SetField(reposition, "settleDuration", 1f);
        TestUtil.SetField(mechanic, "reposition", reposition);
        TestUtil.SetField(mechanic, "pushDuration", 0.6f);
        GameObject quicksand = new GameObject("Quicksand");
        quicksand.transform.SetParent(boss.transform);
        TestUtil.SetField(mechanic, "quicksand", quicksand);
        yield return null;
        quicksand.SetActive(false);

        DropToFirstThreshold();
        yield return null;
        Assert.AreEqual(new Vector3(30f, 0f, -10f), boss.transform.position, "still diving at the old spot");
        Assert.IsFalse(quicksand.activeSelf);

        yield return new WaitForSeconds(0.35f);
        Assert.AreEqual(Vector3.zero, boss.transform.position);
        Assert.IsTrue(reposition.IsMoving, "Binah is still rising out of the sand");
        Assert.IsTrue(quicksand.activeSelf, "the sand flows as soon as Binah breaks the surface");
    }

    // 蓄力期間地板警示從 Binah 那側漸漸塗滿，快發射時接近全滿
    [UnityTest]
    public IEnumerator ChargeWarning_FillsUpOverTheCharge()
    {
        GameObject pivot = new GameObject("FillPivot");
        pivot.transform.SetParent(boss.transform);
        WarningFill fill = pivot.AddComponent<WarningFill>();
        TestUtil.SetField(mechanic, "chargeFill", fill);
        // 蓄力拉長：0.4 秒時最後一幀稍長 (例如 0.18 秒) 就會讓最後取樣到的填滿度偏低
        TestUtil.SetField(mechanic, "chargeDurations", new[] { 1.5f, 1.5f });
        yield return null;
        DropToFirstThreshold();

        yield return WaitForPhase(BinahCannonMechanic.Phase.Charging);
        yield return null;
        Assert.Less(pivot.transform.localScale.z, 0.3f, "just started filling");

        // 記錄整段蓄力中最滿的程度 (Editor 沒有焦點時一幀可能很長，固定時間點取樣會拿到上一幀的值)
        float fullest = 0f;
        float end = Time.realtimeSinceStartup + 3f;
        while (mechanic.CurrentPhase == BinahCannonMechanic.Phase.Charging && Time.realtimeSinceStartup < end)
        {
            fullest = Mathf.Max(fullest, pivot.transform.localScale.z);
            yield return null;
        }

        Assert.Greater(fullest, 0.6f, "almost full by the time it fires");
    }

    // 機制進行中佔住攻擊使用權 (普通攻擊、長條光炮暫停)；正在進行的普通攻擊直接被接管；結束後釋放
    [UnityTest]
    public IEnumerator HoldsTheAttackGateWhileRunning()
    {
        BossAttackGate gate = boss.AddComponent<BossAttackGate>();
        TestUtil.SetField(mechanic, "attackGate", gate);
        GameObject normalAttack = new GameObject("NormalAttack");
        gate.TryAcquire(normalAttack);
        yield return null;

        DropToFirstThreshold();
        gate.Release(normalAttack);
        Assert.IsTrue(gate.IsBusy, "the mechanic took over the gate");

        yield return WaitForPhase(BinahCannonMechanic.Phase.Firing);
        Assert.IsTrue(gate.IsBusy);
        yield return WaitForPhase(BinahCannonMechanic.Phase.Idle);
        Assert.IsFalse(gate.IsBusy, "released when the mechanic ends");
        Object.Destroy(normalAttack);
    }

    [UnityTest]
    public IEnumerator AboveThreshold_DoesNothing()
    {
        yield return null;
        bossHealth.TakeDamage(50);
        yield return null;

        Assert.AreEqual(BinahCannonMechanic.Phase.Idle, mechanic.CurrentPhase);
    }

    [UnityTest]
    public IEnumerator Threshold_ReturnsToCenter_PushesPlayerDown_SuppressesShield()
    {
        yield return null;
        DropToFirstThreshold();
        yield return null;

        Assert.AreNotEqual(BinahCannonMechanic.Phase.Idle, mechanic.CurrentPhase);
        Assert.AreEqual(Vector3.zero, boss.transform.position, "back to the center");
        Assert.IsFalse(polarity.IsShieldSuppressed, "the shield holds until the player hits the bottom");

        yield return WaitForPhase(BinahCannonMechanic.Phase.Charging);
        Assert.AreEqual(20f, playerBody.position.z, 0.6f, "pushed to the bottom (pushZ)");
        Assert.AreEqual(0f, playerBody.position.x, 0.1f, "keeps its left/right position");
        Assert.IsTrue(polarity.IsShieldSuppressed, "the shield breaks once the player is at the bottom");
    }

    [UnityTest]
    public IEnumerator NotInterrupted_UncoveredPlayerTakesCannonDamage_ThenShieldReturns()
    {
        yield return null;
        DropToFirstThreshold();

        yield return WaitForPhase(BinahCannonMechanic.Phase.Firing);
        yield return WaitForPhase(BinahCannonMechanic.Phase.Idle);

        Assert.AreEqual(5, playerHealth.CurrentHealth, "20 - 15");
        Assert.IsFalse(polarity.IsShieldSuppressed);
    }

    // 沙丘固定生成在 (0, 10)，寬 8 m (X -4 ~ 4)；玩家被推到 Z = 20
    private void PlaceOneDuneAhead()
    {
        TestUtil.SetField(mechanic, "duneCount", new Vector2Int(1, 1));
        TestUtil.SetField(duneField, "areaMin", new Vector2(0f, 10f));
        TestUtil.SetField(duneField, "areaMax", new Vector2(0f, 10f));
    }

    // 擋住的是沙丘「正後方」(從 Binah 往場地的方向)，跟嘴的高度無關：
    // 嘴在 100 m 高時，嘴到玩家的視線會從沙丘上方越過，但站在正後方仍然算躲好
    [UnityTest]
    public IEnumerator PlayerDirectlyBehindDune_TakesNoDamage_EvenWithAHighMouth()
    {
        PlaceOneDuneAhead();
        boss.transform.Find("Fire_01").localPosition = new Vector3(0f, 100f, 0f);
        yield return null;
        DropToFirstThreshold();

        yield return WaitForPhase(BinahCannonMechanic.Phase.Firing);
        Assert.AreEqual(1, mechanic.ActiveDuneCount);
        yield return WaitForPhase(BinahCannonMechanic.Phase.Idle);

        Assert.AreEqual(20, playerHealth.CurrentHealth);
    }

    [UnityTest]
    public IEnumerator PlayerBesideDune_TakesDamage()
    {
        PlaceOneDuneAhead();
        player.transform.position = new Vector3(12f, 0f, 15f);
        yield return null;
        DropToFirstThreshold();

        yield return WaitForPhase(BinahCannonMechanic.Phase.Firing);
        yield return WaitForPhase(BinahCannonMechanic.Phase.Idle);

        Assert.AreEqual(5, playerHealth.CurrentHealth, "X = 12 is outside the dune's width");
    }

    // 掩護區是倒梯形：越往後越窄 — 在後方約 9 m 處只剩約 ±2.9 m，X = 3.5 已經在外面 (但仍在沙丘本身的寬度 ±4 內)
    [UnityTest]
    public IEnumerator CoverNarrowsWithDistance()
    {
        PlaceOneDuneAhead();
        player.transform.position = new Vector3(3.5f, 0f, 15f);
        yield return null;
        DropToFirstThreshold();

        yield return WaitForPhase(BinahCannonMechanic.Phase.Firing);
        yield return WaitForPhase(BinahCannonMechanic.Phase.Idle);

        Assert.AreEqual(5, playerHealth.CurrentHealth, "X = 3.5 is behind the dune but outside the narrowed cover");
    }

    // 梯形的末端還有寬度 (不是收成一點的三角形)：掩護區只有 10 m 長時，後方約 9 m、偏 1 m 仍然安全
    [UnityTest]
    public IEnumerator NearTheEnd_TrapezoidStillHasWidth()
    {
        PlaceOneDuneAhead();
        TestUtil.SetField(dunePrefab.GetComponent<BinahDune>(), "coverLength", 10f);
        player.transform.position = new Vector3(1f, 0f, 15f);
        yield return null;
        DropToFirstThreshold();

        yield return WaitForPhase(BinahCannonMechanic.Phase.Firing);
        yield return WaitForPhase(BinahCannonMechanic.Phase.Idle);

        Assert.AreEqual(20, playerHealth.CurrentHealth, "a triangle would be only ±0.4 m wide here; the trapezoid keeps ±1.8 m");
    }

    // 超過梯形的末端就不算掩護
    [UnityTest]
    public IEnumerator BeyondTheCoverTip_TakesDamage()
    {
        PlaceOneDuneAhead();
        TestUtil.SetField(dunePrefab.GetComponent<BinahDune>(), "coverLength", 5f);
        yield return null;
        DropToFirstThreshold();

        yield return WaitForPhase(BinahCannonMechanic.Phase.Firing);
        yield return WaitForPhase(BinahCannonMechanic.Phase.Idle);

        Assert.AreEqual(5, playerHealth.CurrentHealth, "9 m behind, the cover is only 5 m long");
    }

    // 場上沒有空位 (與既有沙丘的距離都小於 duneSpacing) 時不硬塞，避免沙丘疊在一起
    [UnityTest]
    public IEnumerator NoRoomLeft_DoesNotStackDunes()
    {
        TestUtil.SetField(mechanic, "duneCount", new Vector2Int(3, 3));
        TestUtil.SetField(duneField, "areaMin", new Vector2(0f, 10f));
        TestUtil.SetField(duneField, "areaMax", new Vector2(2f, 12f));
        TestUtil.SetField(duneField, "spacing", 20f);
        yield return null;
        DropToFirstThreshold();
        yield return WaitForPhase(BinahCannonMechanic.Phase.Charging);

        Assert.AreEqual(1, mechanic.ActiveDuneCount, "the 3 x 3 area only has room for one dune");
    }

    // 機制結束 (光炮打完) 時場上的沙丘全部破掉
    [UnityTest]
    public IEnumerator Dunes_BreakWhenTheMechanicEnds()
    {
        PlaceOneDuneAhead();
        yield return null;
        DropToFirstThreshold();

        yield return WaitForPhase(BinahCannonMechanic.Phase.Firing);
        Assert.AreEqual(1, mechanic.ActiveDuneCount, "still standing while the cannon fires");
        yield return WaitForPhase(BinahCannonMechanic.Phase.Idle);

        Assert.AreEqual(0, mechanic.ActiveDuneCount);
    }

    // 被打斷 (暈眩結束) 時也一樣破掉
    [UnityTest]
    public IEnumerator Dunes_BreakAfterTheGroggyToo()
    {
        PlaceOneDuneAhead();
        yield return null;
        DropToFirstThreshold();
        yield return WaitForPhase(BinahCannonMechanic.Phase.Charging);

        bossHealth.TakeDamage(60);
        yield return WaitForPhase(BinahCannonMechanic.Phase.Idle);

        Assert.AreEqual(0, mechanic.ActiveDuneCount);
    }

    // 只要沙丘還在場上，不論剩多少血都擋得住
    [UnityTest]
    public IEnumerator DamagedButStandingDune_StillCovers()
    {
        PlaceOneDuneAhead();
        yield return null;
        DropToFirstThreshold();
        yield return WaitForPhase(BinahCannonMechanic.Phase.Charging);

        BinahDune dune = Object.FindAnyObjectByType<BinahDune>();
        dune.TakeDamage(dune.MaxHealth - 1);

        yield return WaitForPhase(BinahCannonMechanic.Phase.Firing);
        yield return WaitForPhase(BinahCannonMechanic.Phase.Idle);
        Assert.AreEqual(20, playerHealth.CurrentHealth);
    }

    [UnityTest]
    public IEnumerator DestroyedDune_NoLongerCovers()
    {
        PlaceOneDuneAhead();
        yield return null;
        DropToFirstThreshold();
        yield return WaitForPhase(BinahCannonMechanic.Phase.Charging);

        BinahDune dune = Object.FindAnyObjectByType<BinahDune>();
        dune.TakeDamage(dune.MaxHealth);
        yield return null;
        Assert.AreEqual(0, mechanic.ActiveDuneCount);

        yield return WaitForPhase(BinahCannonMechanic.Phase.Firing);
        yield return WaitForPhase(BinahCannonMechanic.Phase.Idle);
        Assert.AreEqual(5, playerHealth.CurrentHealth);
    }

    [UnityTest]
    public IEnumerator EnoughDamageWhileCharging_InterruptsIntoGroggyWithBonusDamage()
    {
        yield return null;
        DropToFirstThreshold();
        yield return WaitForPhase(BinahCannonMechanic.Phase.Charging);

        bossHealth.TakeDamage(60);
        yield return null;

        Assert.AreEqual(BinahCannonMechanic.Phase.Groggy, mechanic.CurrentPhase);
        Assert.AreEqual(1.5f, bossHealth.DamageMultiplier);

        yield return WaitForPhase(BinahCannonMechanic.Phase.Idle);
        Assert.AreEqual(20, playerHealth.CurrentHealth, "the cannon never fired");
        Assert.AreEqual(1f, bossHealth.DamageMultiplier);
        Assert.IsFalse(polarity.IsShieldSuppressed);
    }

    [UnityTest]
    public IEnumerator SmallDamageWhileCharging_DoesNotInterrupt()
    {
        yield return null;
        DropToFirstThreshold();
        yield return WaitForPhase(BinahCannonMechanic.Phase.Charging);

        bossHealth.TakeDamage(20);

        yield return WaitForPhase(BinahCannonMechanic.Phase.Firing);
    }

    [UnityTest]
    public IEnumerator SecondThreshold_TriggersAgain()
    {
        yield return null;
        DropToFirstThreshold();
        yield return WaitForPhase(BinahCannonMechanic.Phase.Firing);
        yield return WaitForPhase(BinahCannonMechanic.Phase.Idle);

        bossHealth.TakeDamage(100);  // 190 → 90，低於 1/3 (100)
        yield return null;

        Assert.AreNotEqual(BinahCannonMechanic.Phase.Idle, mechanic.CurrentPhase);
    }

    [UnityTest]
    public IEnumerator BossDies_MidMechanic_CleansUp()
    {
        TestUtil.SetField(mechanic, "duneCount", new Vector2Int(2, 2));
        yield return null;
        DropToFirstThreshold();
        yield return WaitForPhase(BinahCannonMechanic.Phase.Charging);

        bossHealth.TakeDamage(1000);
        yield return null;

        Assert.AreEqual(BinahCannonMechanic.Phase.Idle, mechanic.CurrentPhase);
        Assert.AreEqual(0, mechanic.ActiveDuneCount);
        Assert.IsFalse(polarity.IsShieldSuppressed);
    }
}
