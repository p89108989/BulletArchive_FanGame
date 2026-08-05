using UnityEngine;
using UnityEngine.InputSystem;

public class CameraViewToggle : MonoBehaviour
{
    [SerializeField, Tooltip("預設使用的相機，未切換時啟用")] private Camera defaultCamera;
    [SerializeField, Tooltip("按下 P 後切換成的第三人稱相機")] private Camera thirdPersonCamera;

    private bool isThirdPerson;

    private void Awake()
    {
        if (defaultCamera == null)
        {
            GameObject upCamera = GameObject.Find("Up Camera");
            if (upCamera != null) defaultCamera = upCamera.GetComponent<Camera>();
        }

        if (thirdPersonCamera == null)
        {
            GameObject thirdViewCamera = GameObject.Find("Third view Camera");
            if (thirdViewCamera != null) thirdPersonCamera = thirdViewCamera.GetComponent<Camera>();
        }

        ApplyView();
    }

    // 每幀讀取 P 鍵，切換固定視角與第三人稱視角
    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.pKey.wasPressedThisFrame)
        {
            isThirdPerson = !isThirdPerson;
            ApplyView();
        }
    }

    // 依目前狀態開關兩台相機與各自的 AudioListener，確保同時間只有一台在運作
    private void ApplyView()
    {
        SetCameraActive(defaultCamera, !isThirdPerson);
        SetCameraActive(thirdPersonCamera, isThirdPerson);
    }

    private void SetCameraActive(Camera cam, bool active)
    {
        if (cam == null) return;

        cam.enabled = active;

        AudioListener listener = cam.GetComponent<AudioListener>();
        if (listener != null) listener.enabled = active;
    }
}
