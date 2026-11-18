using NUnit.Framework;

// 確認遊戲程式碼已獨立成 STG.Gameplay 組件，測試組件才引用得到
public class SmokeTests
{
    [Test]
    public void Gameplay_Assembly_IsReferenced()
    {
        Assert.AreEqual("STG.Gameplay", typeof(PlayerEnergy).Assembly.GetName().Name);
    }
}
