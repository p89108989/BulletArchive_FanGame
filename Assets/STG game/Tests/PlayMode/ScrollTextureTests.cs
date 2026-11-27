using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// 貼圖捲動 (流沙往下流)：_BaseMap_ST 的偏移量依速度隨時間增加，用 MaterialPropertyBlock 不動到共用材質
public class ScrollTextureTests
{
    private GameObject quad;

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(quad);
    }

    [UnityTest]
    public IEnumerator OffsetMovesOverTime()
    {
        quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.SetActive(false);
        ScrollTexture scroll = quad.AddComponent<ScrollTexture>();
        TestUtil.SetField(scroll, "tiling", new Vector2(2f, 3f));
        TestUtil.SetField(scroll, "speed", new Vector2(0f, -1f));
        quad.SetActive(true);
        yield return null;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        quad.GetComponent<Renderer>().GetPropertyBlock(block);
        Vector4 first = block.GetVector("_BaseMap_ST");
        yield return new WaitForSeconds(0.2f);
        quad.GetComponent<Renderer>().GetPropertyBlock(block);
        Vector4 later = block.GetVector("_BaseMap_ST");

        Assert.AreEqual(2f, later.x, 0.001f);
        Assert.AreEqual(3f, later.y, 0.001f);
        Assert.Less(later.w, first.w, "the V offset scrolls in the speed direction");
        Assert.AreEqual(first.z, later.z, 0.001f);
    }
}
