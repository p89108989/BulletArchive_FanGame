using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 選角畫面上的一個角色：平常播 Idle，被選上時播 Pickup (Animator 的 bool 參數 Picked)，名牌外框跟著變色。
// 點模型 (相機需有 PhysicsRaycaster、模型要有 Collider) 或點名牌按鈕都算點到這個角色
public class SelectableCharacter : MonoBehaviour, IPointerClickHandler
{
    private static readonly int PickedHash = Animator.StringToHash("Picked");

    [Header("Character")]
    [SerializeField, Tooltip("角色 ID (CH0331 等)，寫進 GameSelection")] private string characterId;
    [SerializeField, Tooltip("名牌上顯示的名字")] private string displayName;

    [Header("References")]
    [SerializeField, Tooltip("角色模型的 Animator (需有 bool 參數 Picked：false = Idle、true = Pickup)，可留空")] private Animator animator;
    [SerializeField, Tooltip("名牌按鈕 (點名牌也能選角色)，可留空")] private Button nameplateButton;
    [SerializeField, Tooltip("名牌外框 (選中時變色)，可留空")] private Graphic nameplateFrame;

    [Header("Nameplate")]
    [SerializeField, Tooltip("沒被選中時的名牌外框顏色")] private Color normalFrameColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField, Tooltip("被選中時的名牌外框顏色 (參考圖的黃框)")] private Color pickedFrameColor = new Color(1f, 0.87f, 0.2f, 1f);

    public string CharacterId => characterId;
    public string DisplayName => displayName;
    public bool IsPicked { get; private set; }

    // 這個角色被點到 (由 CharacterSelectScreen 決定要不要選它)
    public event Action<SelectableCharacter> OnClicked;

    private void OnEnable()
    {
        if (nameplateButton != null) nameplateButton.onClick.AddListener(Click);
        ApplyPicked();
    }

    private void OnDisable()
    {
        if (nameplateButton != null) nameplateButton.onClick.RemoveListener(Click);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Click();
    }

    public void Click()
    {
        OnClicked?.Invoke(this);
    }

    public void SetPicked(bool picked)
    {
        IsPicked = picked;
        ApplyPicked();
    }

    private void ApplyPicked()
    {
        if (animator != null && animator.runtimeAnimatorController != null) animator.SetBool(PickedHash, IsPicked);
        if (nameplateFrame != null) nameplateFrame.color = IsPicked ? pickedFrameColor : normalFrameColor;
    }
}
