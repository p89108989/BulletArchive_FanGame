using UnityEngine;

// Boss 的攻擊排隊：同一時間只允許一個攻擊 (召喚、普通攻擊、雷射) 播放動畫，避免互相打斷或觸發器殘留。
// 攻擊開始前呼叫 TryAcquire，動作結束後呼叫 Release
public class BossAttackGate : MonoBehaviour
{
    private Object owner;

    public bool IsBusy => owner != null;

    // 目前沒有其他攻擊在進行時取得使用權並回傳 true
    public bool TryAcquire(Object requester)
    {
        if (owner != null) return false;
        owner = requester;
        return true;
    }

    public void Release(Object requester)
    {
        if (owner == requester) owner = null;
    }
}
