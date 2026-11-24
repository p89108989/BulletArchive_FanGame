using UnityEngine;

// Boss 雷射的外觀 (只有視覺，不判定傷害)：數層圓管 (BossBeamWave shader，沿光束一波波鼓起) + 發射點光團 + 落點地面波紋。
// 圓管網格沿本地 Z 從 0 到 1，所以把 Z 縮放成光束長度、X/Y 縮放成寬度即可；強度 0~1 控制粗細與亮度，用來做展開/消散
public class BossBeamVisual : MonoBehaviour
{
    [Header("Beam")]
    [SerializeField, Tooltip("光束圓管 (外層波動光暈、內層亮芯…)，網格沿本地 Z 從 0 到 1、直徑 1")] private Renderer[] beamTubes = System.Array.Empty<Renderer>();
    [SerializeField, Tooltip("每層圓管的寬度倍數 (對應 beamTubes)")] private float[] tubeWidthScales = System.Array.Empty<float>();
    [SerializeField, Min(0f), Tooltip("光束基本寬度 (m)")] private float width = 6f;
    [SerializeField, Min(0f), Tooltip("光束起點往發射點後方退幾公尺：讓光束從眼球裡面噴出，起點鼓起的光團把整顆眼睛包住")] private float startBackOffset;

    [Header("Ends")]
    [SerializeField, Tooltip("發射點的光團 (跟著起點)，可留空")] private Transform muzzle;
    [SerializeField, Tooltip("落點的效果 (地面波紋、火花)，可留空")] private Transform impact;
    [SerializeField, Tooltip("落點效果的粒子，光束出現時播放、消失時停止")] private ParticleSystem impactParticles;
    [SerializeField, Tooltip("發射點效果的粒子")] private ParticleSystem muzzleParticles;

    private static readonly int BeamLengthId = Shader.PropertyToID("_BeamLength");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
    }

    // 設定光束起訖點與強度 (0 = 完全收起)；每幀由攻擊流程呼叫，跟著 Boss 的眼睛移動
    public void SetBeam(Vector3 beamStart, Vector3 beamEnd, float beamIntensity)
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            if (impactParticles != null) impactParticles.Play(true);
            if (muzzleParticles != null) muzzleParticles.Play(true);
        }

        Vector3 direction = beamEnd - beamStart;
        float length = direction.magnitude;
        if (length < 0.01f) return;

        Vector3 forward = direction / length;
        Vector3 tubeStart = beamStart - forward * startBackOffset;
        length += startBackOffset;

        float intensity = Mathf.Clamp01(beamIntensity);
        Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);

        for (int i = 0; i < beamTubes.Length; i++)
        {
            if (beamTubes[i] == null) continue;

            float tubeWidth = width * intensity * (i < tubeWidthScales.Length ? tubeWidthScales[i] : 1f);
            Transform tube = beamTubes[i].transform;
            tube.SetPositionAndRotation(tubeStart, rotation);
            tube.localScale = new Vector3(tubeWidth, tubeWidth, length);

            beamTubes[i].GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(BeamLengthId, length);
            propertyBlock.SetFloat(IntensityId, intensity);
            beamTubes[i].SetPropertyBlock(propertyBlock);
        }

        if (muzzle != null)
        {
            muzzle.SetPositionAndRotation(beamStart, rotation);
            muzzle.localScale = Vector3.one * Mathf.Max(0.01f, intensity);
        }

        // 落點效果平貼地面，不跟著光束傾斜
        if (impact != null) impact.SetPositionAndRotation(beamEnd, Quaternion.identity);
    }

    public void Hide()
    {
        if (impactParticles != null) impactParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (muzzleParticles != null) muzzleParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        gameObject.SetActive(false);
    }
}
