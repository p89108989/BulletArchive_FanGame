using UnityEngine;
using VolumetricLines;

// 雷射光束的外觀：VolumetricLines 光束 + 槍口光暈 + 命中點火花。
// 根物件需放在世界原點、不掛在角色底下，光束端點直接以世界座標設定
public class LaserBeamVisual : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("光束本體 (VolumetricLines 光劍材質)")] private VolumetricLineBehavior line;
    [SerializeField, Tooltip("槍口光暈，放在光束起點")] private ParticleSystem muzzleParticles;
    [SerializeField, Tooltip("命中點特效，只在打中敵人時播放")] private ParticleSystem impactParticles;
    [SerializeField, Tooltip("需要跟著光束變色的粒子")] private ParticleSystem[] tintedParticles;

    private const int LineVertexCount = 8;
    private const float MinBeamLength = 0.01f;

    // 光束 Mesh 的頂點/法線緩衝區重複使用：VolumetricLines 的 SetStartAndEndPoints 每次呼叫都會 new 兩個陣列，
    // 雷射按住時每幀更新會持續產生 GC，所以改由這裡直接寫入同樣排列的頂點資料
    private readonly Vector3[] linePositions = new Vector3[LineVertexCount];
    private readonly Vector3[] lineOthers = new Vector3[LineVertexCount];
    private MeshFilter lineMeshFilter;
    private Color currentColor;
    private float currentWidth;
    private bool isVisible;

    private void Awake()
    {
        if (RequiredReference.IsMissing(line, nameof(line), this)) return;
        if (RequiredReference.IsMissing(muzzleParticles, nameof(muzzleParticles), this)) return;
        if (RequiredReference.IsMissing(impactParticles, nameof(impactParticles), this)) return;

        lineMeshFilter = line.GetComponent<MeshFilter>();
        Hide();
    }

    // 每幀更新光束位置；顏色/寬度只在改變時才寫入材質，避免每幀設定材質參數
    public void Show(Vector3 start, Vector3 end, Color color, float width, bool showImpact)
    {
        if (!isVisible)
        {
            line.gameObject.SetActive(true);
            muzzleParticles.Play(true);
            isVisible = true;
        }

        if (color != currentColor) ApplyColor(color);
        if (!Mathf.Approximately(width, currentWidth))
        {
            line.LineWidth = width;
            currentWidth = width;
        }

        WriteLineEndpoints(line.transform.InverseTransformPoint(start), line.transform.InverseTransformPoint(end), width);
        muzzleParticles.transform.position = start;

        // 貼著敵人開火時長度可能為 0，此時沿用上一幀的朝向，避免 LookRotation 收到零向量
        Vector3 back = start - end;
        if (back.sqrMagnitude > MinBeamLength * MinBeamLength) impactParticles.transform.SetPositionAndRotation(end, Quaternion.LookRotation(back, Vector3.up));
        else impactParticles.transform.position = end;

        if (showImpact && !impactParticles.isEmitting) impactParticles.Play(true);
        else if (!showImpact && impactParticles.isEmitting) impactParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    // 與 VolumetricLineBehavior.SetStartAndEndPoints 相同的頂點排列 (前 4 個為起點、後 4 個為終點，法線欄位存另一端點)，
    // 但重複使用陣列且自行設定 bounds；光束物件第一次 Start 前 Mesh 尚未建立，那一幀交給原本的方法處理
    private void WriteLineEndpoints(Vector3 localStart, Vector3 localEnd, float width)
    {
        Mesh mesh = lineMeshFilter.sharedMesh;
        if (mesh == null)
        {
            line.SetStartAndEndPoints(localStart, localEnd);
            return;
        }

        for (int i = 0; i < LineVertexCount; i++)
        {
            bool isStart = i < LineVertexCount / 2;
            linePositions[i] = isStart ? localStart : localEnd;
            lineOthers[i] = isStart ? localEnd : localStart;
        }
        mesh.vertices = linePositions;
        mesh.normals = lineOthers;

        Bounds bounds = new Bounds(localStart, Vector3.zero);
        bounds.Encapsulate(localEnd);
        bounds.Expand(width);
        mesh.bounds = bounds;

        // VolumetricLineBehavior 會在 transform.hasChanged 時用它記錄的舊端點重算 bounds，清掉旗標避免覆蓋上面的結果
        line.transform.hasChanged = false;
    }

    public void Hide()
    {
        line.gameObject.SetActive(false);
        muzzleParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        impactParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        isVisible = false;
    }

    private void ApplyColor(Color color)
    {
        currentColor = color;
        line.LineColor = color;
        for (int i = 0; i < tintedParticles.Length; i++)
        {
            ParticleSystem.MainModule main = tintedParticles[i].main;
            main.startColor = color;
        }
    }
}
