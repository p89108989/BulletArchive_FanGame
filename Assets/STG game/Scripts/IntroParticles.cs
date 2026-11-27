using UnityEngine;

// 開場演出的粒子 (例如 リオ 身後的揚沙)：跟著慢動作一起放慢，定格時完全停住
public class IntroParticles : MonoBehaviour, ITitleIntroPart
{
    [SerializeField, Tooltip("要跟著放慢的粒子")] private ParticleSystem particles;

    public void SetProgress(float progress)
    {
        if (particles == null) return;

        ParticleSystem.MainModule main = particles.main;
        main.simulationSpeed = 1f - Mathf.Clamp01(progress);
    }
}
