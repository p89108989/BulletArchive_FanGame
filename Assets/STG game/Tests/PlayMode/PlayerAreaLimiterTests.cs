using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Boss 戰的玩家活動範圍：由 Boss 設定 (BossProfile) 決定要不要限制 —— Perorozilla 的相機只看得到場地一部分要限制，
// Binah 看得到整個場地不限制。BossEncounter 在 Boss 出場時套用、回到一般戰鬥時解除
public class PlayerAreaLimiterTests
{
    private readonly List<Object> spawned = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object o in spawned) Object.Destroy(o);
        spawned.Clear();
    }

    private T Track<T>(T o) where T : Object
    {
        spawned.Add(o);
        return o;
    }

    private PlayerAreaLimiter CreateBody(out Rigidbody body)
    {
        GameObject player = Track(new GameObject("Player"));
        body = player.AddComponent<Rigidbody>();
        body.useGravity = false;
        return player.AddComponent<PlayerAreaLimiter>();
    }

    private BossProfile CreateProfile(bool limit)
    {
        GameObject template = Track(new GameObject("BossTemplate"));
        BossProfile profile = Track(ScriptableObject.CreateInstance<BossProfile>());
        TestUtil.SetField(profile, "bossPrefab", template);
        TestUtil.SetField(profile, "limitPlayerArea", limit);
        TestUtil.SetField(profile, "playerAreaMin", new Vector2(-10f, -10f));
        TestUtil.SetField(profile, "playerAreaMax", new Vector2(10f, 10f));
        return profile;
    }

    private BossEncounter CreateEncounter(BossProfile profile, PlayerAreaLimiter limiter)
    {
        GameObject go = Track(new GameObject("Encounter"));
        go.SetActive(false);
        BossEncounter encounter = go.AddComponent<BossEncounter>();
        TestUtil.SetField(encounter, "bossProfile", profile);
        TestUtil.SetField(encounter, "playerAreaLimiter", limiter);
        go.SetActive(true);
        return encounter;
    }

    [UnityTest]
    public IEnumerator Restrict_KeepsBodyInsideArea()
    {
        PlayerAreaLimiter limiter = CreateBody(out Rigidbody body);
        limiter.Restrict(new Vector2(-10f, -10f), new Vector2(10f, 10f));
        body.position = new Vector3(8f, 0f, 0f);
        body.linearVelocity = new Vector3(20f, 0f, 0f);

        yield return new WaitForSeconds(0.3f);

        Assert.LessOrEqual(body.position.x, 10.01f);
    }

    [UnityTest]
    public IEnumerator ByDefault_DoesNotRestrict()
    {
        CreateBody(out Rigidbody body);
        body.position = new Vector3(8f, 0f, 0f);
        body.linearVelocity = new Vector3(20f, 0f, 0f);

        yield return new WaitForSeconds(0.3f);

        Assert.Greater(body.position.x, 10.5f);
    }

    [UnityTest]
    public IEnumerator Release_StopsRestricting()
    {
        PlayerAreaLimiter limiter = CreateBody(out Rigidbody body);
        limiter.Restrict(new Vector2(-10f, -10f), new Vector2(10f, 10f));
        limiter.Release();
        body.position = new Vector3(8f, 0f, 0f);
        body.linearVelocity = new Vector3(20f, 0f, 0f);

        yield return new WaitForSeconds(0.3f);

        Assert.Greater(body.position.x, 10.5f);
    }

    [UnityTest]
    public IEnumerator BossThatLimits_RestrictsWhenItAppears_AndReleasesAfter()
    {
        PlayerAreaLimiter limiter = CreateBody(out _);
        BossEncounter encounter = CreateEncounter(CreateProfile(true), limiter);
        yield return null;

        encounter.OnCombatPhaseChanged(CombatPhase.Boss);
        Track(encounter.Boss);
        Assert.IsTrue(limiter.IsRestricting, "Perorozilla-style boss limits the player");

        encounter.OnCombatPhaseChanged(CombatPhase.Normal);
        Assert.IsFalse(limiter.IsRestricting, "back to normal combat = free movement");
    }

    [UnityTest]
    public IEnumerator BossThatDoesNotLimit_LeavesPlayerFree()
    {
        PlayerAreaLimiter limiter = CreateBody(out _);
        BossEncounter encounter = CreateEncounter(CreateProfile(false), limiter);
        yield return null;

        encounter.OnCombatPhaseChanged(CombatPhase.Boss);
        Track(encounter.Boss);

        Assert.IsFalse(limiter.IsRestricting);
    }
}
