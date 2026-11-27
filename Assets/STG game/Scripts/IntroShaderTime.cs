using UnityEngine;

// 開場演出：讓 shader 的動畫 (雷射波浪、光炮外圍的雷電閃動) 跟著慢動作一起越來越慢，定格時停住。
// shader 改讀 _CustomTime (材質要開 _UseCustomTime) 而不是全域時間；這裡每幀推進它，速度 = 1 - 開場進度
public class IntroShaderTime : MonoBehaviour, ITitleIntroPart
{
    [SerializeField, Tooltip("要跟著放慢的光束 (材質用 STG/ToonBeam 或 STG/BeamDarkLightning，且開啟 Use Custom Time)")] private Renderer[] renderers = System.Array.Empty<Renderer>();

    private static readonly int CustomTimeId = Shader.PropertyToID("_CustomTime");

    private MaterialPropertyBlock block;
    private float time;
    private float speed = 1f;

    public void SetProgress(float progress)
    {
        speed = 1f - Mathf.Clamp01(progress);
    }

    private void Update()
    {
        if (speed <= 0f) return;

        time += AnimationClock.ScaledDelta * speed;
        if (block == null) block = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;

            renderers[i].GetPropertyBlock(block);
            block.SetFloat(CustomTimeId, time);
            renderers[i].SetPropertyBlock(block);
        }
    }
}
