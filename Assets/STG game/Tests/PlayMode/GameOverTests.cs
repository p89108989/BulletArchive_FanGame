using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// 玩家死亡：停用操作元件、顯示 Game Over 並暫停遊戲；重新開始時恢復時間流速
public class GameOverTests
{
    private GameObject playerObject;
    private PlayerHealth health;
    private GameObject screenObject;
    private GameObject panel;

    [SetUp]
    public void SetUp()
    {
        playerObject = new GameObject("Player");
        health = playerObject.AddComponent<PlayerHealth>();

        screenObject = new GameObject("GameOverScreen");
        screenObject.SetActive(false);
        panel = new GameObject("Panel");
        panel.transform.SetParent(screenObject.transform);
        panel.SetActive(false);
        GameOverScreen screen = screenObject.AddComponent<GameOverScreen>();
        TestUtil.SetField(screen, "playerHealth", health);
        TestUtil.SetField(screen, "panel", panel);
        TestUtil.SetField(screen, "reloadSceneOnRestart", false);
        screenObject.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        Object.Destroy(screenObject);
        Object.Destroy(playerObject);
    }

    private void Kill()
    {
        health.TakeDamage(health.MaxHealth);
    }

    [UnityTest]
    public IEnumerator Death_DisablesConfiguredBehaviours()
    {
        Light controlled = playerObject.AddComponent<Light>();
        playerObject.SetActive(false);
        PlayerDeathHandler handler = playerObject.AddComponent<PlayerDeathHandler>();
        TestUtil.SetField(handler, "disableOnDeath", new Behaviour[] { controlled });
        playerObject.SetActive(true);
        yield return null;

        Kill();

        Assert.IsFalse(controlled.enabled);
    }

    [UnityTest]
    public IEnumerator Death_ShowsPanelAndPausesTime()
    {
        yield return null;

        Kill();

        Assert.IsTrue(panel.activeSelf);
        Assert.AreEqual(0f, Time.timeScale);
    }

    [UnityTest]
    public IEnumerator Restart_RestoresTimeScale()
    {
        yield return null;
        Kill();

        screenObject.GetComponent<GameOverScreen>().Restart();

        Assert.AreEqual(1f, Time.timeScale);
    }
}
