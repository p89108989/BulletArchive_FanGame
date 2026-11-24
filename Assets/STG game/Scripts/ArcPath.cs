using UnityEngine;

// 拋物線軌跡：起點到終點的直線，再加上中段最高 arcHeight 的拱起 (t = 0~1)
public static class ArcPath
{
    public static Vector3 Evaluate(Vector3 from, Vector3 to, float arcHeight, float t)
    {
        return Vector3.Lerp(from, to, t) + Vector3.up * (arcHeight * 4f * t * (1f - t));
    }
}
