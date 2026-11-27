using UnityEngine;

// 長條範圍 (Binah 的長條光炮與地面火焰)：從 start 沿 direction 延伸 length、寬 width (中線左右各一半)。只看水平位置
public static class LaneGeometry
{
    public static bool Contains(Vector3 start, Vector3 direction, float length, float width, Vector3 point)
    {
        Vector3 forward = new Vector3(direction.x, 0f, direction.z);
        if (forward.sqrMagnitude < 1e-6f) return false;
        forward.Normalize();

        Vector3 offset = new Vector3(point.x - start.x, 0f, point.z - start.z);
        float along = Vector3.Dot(offset, forward);
        if (along < 0f || along > length) return false;

        float across = Vector3.Cross(forward, offset).y;
        return Mathf.Abs(across) <= width * 0.5f;
    }
}
