using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 按鈕的手感：滑鼠移上去稍微放大、按下稍微縮小，平滑過渡 (不受暫停影響)；不能按的按鈕不動
public class UiButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField, Range(1f, 1.2f), Tooltip("滑鼠移上去的大小")] private float hoverScale = 1.04f;
    [SerializeField, Range(0.8f, 1f), Tooltip("按下時的大小")] private float pressedScale = 0.96f;
    [SerializeField, Min(0.01f), Tooltip("過渡時間 (秒)")] private float duration = 0.15f;

    private Selectable selectable;
    private bool hovered;
    private bool pressed;
    private float current = 1f;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
    }

    private void OnDisable()
    {
        hovered = false;
        pressed = false;
        current = 1f;
        transform.localScale = Vector3.one;
    }

    public void OnPointerEnter(PointerEventData eventData) { hovered = true; }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; }
    public void OnPointerDown(PointerEventData eventData) { pressed = true; }
    public void OnPointerUp(PointerEventData eventData) { pressed = false; }

    private void Update()
    {
        float target = TargetScale();
        if (Mathf.Approximately(current, target)) return;

        // 固定速度靠近目標：從 1 到放大/縮小約 duration 秒
        float step = Mathf.Abs(hoverScale - pressedScale) / duration * AnimationClock.UnscaledDelta;
        current = Mathf.MoveTowards(current, target, step);
        transform.localScale = Vector3.one * current;
    }

    private float TargetScale()
    {
        if (selectable != null && !selectable.interactable) return 1f;
        if (pressed) return pressedScale;
        return hovered ? hoverScale : 1f;
    }
}
