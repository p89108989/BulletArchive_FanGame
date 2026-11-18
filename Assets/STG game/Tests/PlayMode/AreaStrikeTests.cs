using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// 範圍攻擊：周圍有大量其他碰撞體時仍要打到玩家；同極性吸收只算一次
public class AreaStrikeTests
{
    private readonly List<GameObject> created = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in created) Object.Destroy(go);
        created.Clear();
    }

    private AreaStrike CreateStrike(Vector3 position)
    {
        GameObject root = new GameObject("AreaStrike");
        root.SetActive(false);
        root.transform.position = position;
        Renderer ring = CreateQuad(root.transform);
        Renderer fill = CreateQuad(root.transform);
        AreaStrike strike = root.AddComponent<AreaStrike>();
        TestUtil.SetField(strike, "ringRenderer", ring);
        TestUtil.SetField(strike, "fillRenderer", fill);
        TestUtil.SetField(strike, "fillDisc", fill.transform);
        TestUtil.SetField(strike, "hitLayers", (LayerMask)LayerMask.GetMask("Player"));
        root.SetActive(true);
        created.Add(root);
        return strike;
    }

    private Renderer CreateQuad(Transform parent)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(parent, false);
        return quad.GetComponent<Renderer>();
    }

    private GameObject CreatePlayer(Vector3 position)
    {
        GameObject player = new GameObject("Player");
        player.tag = "Player";
        player.layer = LayerMask.NameToLayer("Player");
        player.transform.position = position;
        player.AddComponent<CapsuleCollider>();
        player.AddComponent<PlayerHealth>();
        created.Add(player);
        return player;
    }

    [UnityTest]
    public IEnumerator Detonate_DamagesPlayerAmongManyDistractors()
    {
        for (int i = 0; i < 30; i++)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = new Vector3(Random.Range(-8f, 8f), 0f, Random.Range(-8f, 8f));
            created.Add(cube);
        }
        PlayerHealth health = CreatePlayer(new Vector3(2f, 0f, 0f)).GetComponent<PlayerHealth>();
        yield return new WaitForFixedUpdate();

        CreateStrike(Vector3.zero).Begin(10f, 0.05f, BulletPolarity.None, s => { });
        yield return new WaitForSeconds(0.2f);

        Assert.AreEqual(health.MaxHealth - 1, health.CurrentHealth);
    }

    [UnityTest]
    public IEnumerator Detonate_SamePolarity_AbsorbedOnce()
    {
        GameObject player = CreatePlayer(Vector3.zero);
        player.AddComponent<BoxCollider>();
        PlayerPolarity polarity = player.AddComponent<PlayerPolarity>();
        int absorbed = 0;
        polarity.OnAttackAbsorbed += p => absorbed++;
        yield return new WaitForFixedUpdate();

        CreateStrike(Vector3.zero).Begin(10f, 0.05f, BulletPolarity.Blue, s => { });
        yield return new WaitForSeconds(0.2f);

        Assert.AreEqual(1, absorbed);
    }
}
