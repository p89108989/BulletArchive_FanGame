using System;
using UnityEngine;

// 直升機發射的飛彈：沿弧線在指定時間內飛到目標點 (紅圈中心)，抵達後通知回收。
// 純演出用，不做碰撞判定；傷害由 AreaStrike 在爆炸時判定
public class StrikeMissile : MonoBehaviour
{
    [Serializable]
    private struct PolarityVisual
    {
        [Tooltip("對應的屬性")] public BulletPolarity polarity;
        [Tooltip("該屬性使用的飛彈外觀")] public GameObject visual;
    }

    [Header("Visuals")]
    [SerializeField, Tooltip("每種屬性對應的飛彈外觀，發射時只顯示對應的一個")] private PolarityVisual[] visuals;
    [SerializeField, Tooltip("尾焰與煙霧粒子，每次發射時重新播放")] private ParticleSystem trailParticles;

    [Header("Flight")]
    [SerializeField, Tooltip("飛行弧線的拱起高度，佔飛行距離的比例")] private float arcHeightRatio = 0.15f;

    private Vector3 startPoint;
    private Vector3 controlPoint;
    private Vector3 endPoint;
    private float flightDuration;
    private float elapsed;
    private Action<StrikeMissile> onArrived;

    // 由發射者從物件池取出後呼叫：設定起終點、飛行時間、屬性外觀、長度 (世界單位) 與抵達時的回呼
    public void Launch(Vector3 from, Vector3 to, float duration, BulletPolarity polarity, float length, Action<StrikeMissile> arrivedCallback)
    {
        startPoint = from;
        endPoint = to;
        controlPoint = (from + to) * 0.5f + Vector3.up * (Vector3.Distance(from, to) * arcHeightRatio);
        flightDuration = Mathf.Max(0.01f, duration);
        elapsed = 0f;
        onArrived = arrivedCallback;

        // 外觀子物件以長度 1 建模，根物件縮放即為實際長度
        transform.localScale = Vector3.one * length;
        ShowVisual(polarity);
        UpdatePose(0f);

        if (trailParticles != null)
        {
            trailParticles.Clear(true);
            trailParticles.Play(true);
        }
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / flightDuration);
        UpdatePose(t);

        if (t >= 1f) onArrived?.Invoke(this);
    }

    // 二次貝茲曲線：位置沿曲線前進，機頭朝向切線方向
    private void UpdatePose(float t)
    {
        float u = 1f - t;
        transform.position = u * u * startPoint + 2f * u * t * controlPoint + t * t * endPoint;

        Vector3 tangent = 2f * u * (controlPoint - startPoint) + 2f * t * (endPoint - controlPoint);
        if (tangent.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
    }

    private void ShowVisual(BulletPolarity polarity)
    {
        bool shown = false;
        for (int i = 0; i < visuals.Length; i++)
        {
            bool match = !shown && visuals[i].polarity == polarity;
            if (visuals[i].visual != null) visuals[i].visual.SetActive(match);
            shown |= match;
        }

        // 沒有對應外觀時退回第一個，避免飛彈隱形
        if (!shown && visuals.Length > 0 && visuals[0].visual != null) visuals[0].visual.SetActive(true);
    }
}
