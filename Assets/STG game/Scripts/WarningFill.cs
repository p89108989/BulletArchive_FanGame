using UnityEngine;

// 地板警示漸漸塗滿 (Binah 光炮蓄力)：掛在放在起點邊緣的空物件上，子物件 (警示面) 沿本地 +Z 排在起點之後；
// 依進度縮放自己的 Z，警示面就從起點邊緣往前長，塗滿 = 光炮發射
public class WarningFill : MonoBehaviour
{
    // 縮放為 0 時矩陣不可逆，保留極小值
    private const float MinScale = 0.0001f;

    public void SetProgress(float progress)
    {
        Vector3 scale = transform.localScale;
        scale.z = Mathf.Max(MinScale, Mathf.Clamp01(progress));
        transform.localScale = scale;
    }
}
