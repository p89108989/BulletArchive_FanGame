using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// 血條/能量條的初始值不能依賴「UI 與玩家誰先初始化」：UI 先啟用、玩家後初始化時也要顯示正確數值
public class BarUITests
{
    private GameObject playerObject;
    private GameObject barObject;

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(barObject);
        Object.Destroy(playerObject);
    }

    private (Slider slider, Text text) CreateBarParts()
    {
        barObject = new GameObject("Bar");
        barObject.SetActive(false);
        Slider slider = new GameObject("Slider").AddComponent<Slider>();
        slider.transform.SetParent(barObject.transform);
        Text text = new GameObject("Text").AddComponent<Text>();
        text.transform.SetParent(barObject.transform);
        return (slider, text);
    }

    [UnityTest]
    public IEnumerator HealthBar_CreatedBeforePlayer_ShowsFullHealth()
    {
        playerObject = new GameObject("Player");
        playerObject.SetActive(false);
        PlayerHealth health = playerObject.AddComponent<PlayerHealth>();

        (Slider slider, Text text) = CreateBarParts();
        HealthBarUI bar = barObject.AddComponent<HealthBarUI>();
        TestUtil.SetField(bar, "playerHealth", health);
        TestUtil.SetField(bar, "healthSlider", slider);
        TestUtil.SetField(bar, "healthText", text);
        barObject.SetActive(true);
        playerObject.SetActive(true);

        yield return null;

        Assert.AreEqual(health.MaxHealth, slider.value);
        Assert.AreEqual(health.MaxHealth + " / " + health.MaxHealth, text.text);
    }

    [UnityTest]
    public IEnumerator EnergyBar_CreatedBeforePlayer_ShowsStartingEnergy()
    {
        playerObject = new GameObject("Player");
        playerObject.SetActive(false);
        PlayerEnergy energy = playerObject.AddComponent<PlayerEnergy>();
        TestUtil.SetField(energy, "startingEnergy", 40f);

        (Slider slider, Text text) = CreateBarParts();
        EnergyBarUI bar = barObject.AddComponent<EnergyBarUI>();
        TestUtil.SetField(bar, "playerEnergy", energy);
        TestUtil.SetField(bar, "polarity", BulletPolarity.Blue);
        TestUtil.SetField(bar, "energySlider", slider);
        TestUtil.SetField(bar, "energyText", text);
        barObject.SetActive(true);
        playerObject.SetActive(true);

        yield return null;

        Assert.AreEqual("40%", text.text);
    }
}
