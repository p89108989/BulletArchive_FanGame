using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// 鏡頭震動：震動期間位置在原點附近晃動，結束後準確回到原位；
// 震動期間鏡頭被其他系統移動 (例如視角轉場) 時，以新位置為準，不會被拉回舊位置
public class CameraShakeTests
{
    private GameObject cameraObject;
    private CameraShake shake;

    [SetUp]
    public void SetUp()
    {
        cameraObject = new GameObject("Camera");
        cameraObject.transform.position = new Vector3(1f, 2f, 3f);
        shake = cameraObject.AddComponent<CameraShake>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(cameraObject);
    }

    [UnityTest]
    public IEnumerator Shake_MovesThenReturnsExactly()
    {
        yield return null;
        shake.Shake(1f, 0.2f);

        float maxOffset = 0f;
        float end = Time.time + 0.15f;
        while (Time.time < end)
        {
            yield return null;
            maxOffset = Mathf.Max(maxOffset, Vector3.Distance(cameraObject.transform.position, new Vector3(1f, 2f, 3f)));
        }

        Assert.Greater(maxOffset, 0.05f, "it actually shakes");
        Assert.LessOrEqual(maxOffset, 1.01f, "within the amplitude");

        yield return new WaitForSeconds(0.2f);
        Assert.AreEqual(new Vector3(1f, 2f, 3f), cameraObject.transform.position);
    }

    [UnityTest]
    public IEnumerator MovedDuringShake_SettlesAtTheNewPosition()
    {
        yield return null;
        shake.Shake(1f, 0.2f);
        yield return null;
        yield return null;

        cameraObject.transform.position = new Vector3(10f, 20f, 30f);
        yield return new WaitForSeconds(0.35f);

        Assert.AreEqual(new Vector3(10f, 20f, 30f), cameraObject.transform.position);
    }
}
