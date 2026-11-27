using UnityEngine;

// 開場演出：物件從起點移到終點 (世界座標)，例如 リオ 往前滑、飛彈飛向鏡頭
public class IntroMover : MonoBehaviour, ITitleIntroPart
{
    [SerializeField, Tooltip("開場第一格的位置")] private Vector3 fromPosition;
    [SerializeField, Tooltip("定格時的位置")] private Vector3 toPosition;

    public void SetProgress(float progress)
    {
        transform.position = Vector3.Lerp(fromPosition, toPosition, progress);
    }
}
