using UnityEngine;

// 召喚落點的挑選：在矩形範圍內隨機取點，彼此保持距離，並避開玩家附近 (不直接砸在玩家身上)
public static class SummonPlacement
{
    private const int AttemptsPerPoint = 30;

    // 填入 results，回傳實際找到的點數 (範圍太擠時可能少於 results.Length)
    public static int PickPoints(Vector2 areaMin, Vector2 areaMax, float minSeparation, Vector3 avoidCenter, float avoidRadius, float height, Vector3[] results)
    {
        avoidCenter.y = height;
        int count = 0;
        for (int i = 0; i < results.Length; i++)
        {
            for (int attempt = 0; attempt < AttemptsPerPoint; attempt++)
            {
                Vector3 candidate = new Vector3(Random.Range(areaMin.x, areaMax.x), height, Random.Range(areaMin.y, areaMax.y));
                if (Vector3.Distance(candidate, avoidCenter) < avoidRadius) continue;
                if (!IsSeparated(candidate, results, count, minSeparation)) continue;

                results[count++] = candidate;
                break;
            }
        }
        return count;
    }

    private static bool IsSeparated(Vector3 candidate, Vector3[] points, int count, float minSeparation)
    {
        for (int i = 0; i < count; i++)
        {
            if (Vector3.Distance(candidate, points[i]) < minSeparation) return false;
        }
        return true;
    }
}
