using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// HUD 右下的能量條：跟著玩家能量更新格子與數字，只顯示自己顏色 (Pulse = 藍、Flame = 黃) 的能量
public class EnergyGaugeUITests
{
    private GameObject player;
    private GameObject gauge;
    private PlayerEnergy energy;

    [SetUp]
    public void SetUp()
    {
        player = new GameObject("Player");
        player.SetActive(false);
        player.AddComponent<PlayerPolarity>();
        energy = player.AddComponent<PlayerEnergy>();
        TestUtil.SetField(energy, "startingEnergy", 35f);
        player.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(player);
        Object.Destroy(gauge);
    }

    private EnergyGaugeUI CreateGauge(BulletPolarity polarity, out Image[] segments, out Text number)
    {
        gauge = new GameObject("Gauge");
        gauge.SetActive(false);
        segments = new Image[10];
        for (int i = 0; i < segments.Length; i++)
        {
            segments[i] = new GameObject("Segment" + i).AddComponent<Image>();
            segments[i].transform.SetParent(gauge.transform);
            segments[i].type = Image.Type.Filled;
        }

        number = new GameObject("Number").AddComponent<Text>();
        number.transform.SetParent(gauge.transform);
        EnergyGaugeUI ui = gauge.AddComponent<EnergyGaugeUI>();
        TestUtil.SetField(ui, "playerEnergy", energy);
        TestUtil.SetField(ui, "polarity", polarity);
        TestUtil.SetField(ui, "segmentFills", segments);
        TestUtil.SetField(ui, "numberLabel", number);
        gauge.SetActive(true);
        return ui;
    }

    [UnityTest]
    public IEnumerator ShowsStartingEnergy()
    {
        CreateGauge(BulletPolarity.Blue, out Image[] segments, out Text number);
        yield return null;

        Assert.AreEqual("3", number.text);
        Assert.AreEqual(1f, segments[2].fillAmount, 0.001f);
        Assert.AreEqual(0.5f, segments[3].fillAmount, 0.001f);
        Assert.AreEqual(0f, segments[4].fillAmount, 0.001f);
    }

    [UnityTest]
    public IEnumerator UpdatesWhenItsColorChanges()
    {
        CreateGauge(BulletPolarity.Yellow, out Image[] segments, out Text number);
        yield return null;

        energy.TrySpend(BulletPolarity.Yellow, 30f);

        Assert.AreEqual("0", number.text);
        Assert.AreEqual(0.5f, segments[0].fillAmount, 0.001f);
    }

    [UnityTest]
    public IEnumerator IgnoresTheOtherColor()
    {
        CreateGauge(BulletPolarity.Yellow, out _, out Text number);
        yield return null;

        energy.TrySpend(BulletPolarity.Blue, 30f);

        Assert.AreEqual("3", number.text);
    }
}
