using UnityEngine;
using UnityEngine.InputSystem;

// 玩家雷射：按住射擊鍵朝滑鼠游標方向發射持續光束，顏色跟隨目前極性，發射時持續消耗同色能量 (用完即停)。
// 光束停在第一個敵人 (帶 EnemyHealth) 身上並持續造成傷害；路徑上與光束異色的敵方子彈會被消除
[RequireComponent(typeof(PlayerPolarity))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerEnergy))]
public class PlayerLaser : MonoBehaviour
{
    [Header("Input")]
    [SerializeField, Tooltip("按住滑鼠左鍵發射")] private bool fireWithMouse = true;
    [SerializeField, Tooltip("另一個可按住發射的鍵盤按鍵")] private Key alternateFireKey = Key.J;

    [Header("Beam")]
    [SerializeField, Tooltip("光束外觀 Prefab (需帶有 LaserBeamVisual)")] private GameObject beamPrefab;
    [SerializeField, Tooltip("光束起點，留空時以角色位置往上 originHeight 計算")] private Transform beamOrigin;
    [SerializeField, Tooltip("光束起點離地高度 (未指定 beamOrigin 時)")] private float originHeight = 5.8f;
    [SerializeField, Tooltip("光束起點往瞄準方向前移的距離，避免從角色身體中間射出")] private float originForwardOffset = 2.5f;
    [SerializeField, Tooltip("光束最大射程 (m)")] private float maxRange = 150f;
    [SerializeField, Tooltip("光束外觀寬度")] private float beamWidth = 2.5f;
    [SerializeField, Tooltip("光束判定半徑 (m)，越大越容易掃到子彈")] private float hitRadius = 1.5f;
    [SerializeField, Tooltip("藍色極性的光束顏色")] private Color blueColor = new Color(0.2f, 0.5f, 1f, 1f);
    [SerializeField, Tooltip("黃色極性的光束顏色")] private Color yellowColor = new Color(1f, 0.75f, 0.1f, 1f);

    [Header("Energy")]
    [SerializeField, Min(0f), Tooltip("發射時每秒消耗的同色能量 (%)")] private float energyCostPerSecond = 2f;

    [Header("Damage")]
    [SerializeField, Tooltip("每次判定造成的傷害")] private int damagePerTick = 1;
    [SerializeField, Tooltip("傷害判定間隔 (秒)")] private float tickInterval = 0.1f;
    [SerializeField, Tooltip("光束會停下並造成傷害的敵人 Layer")] private LayerMask enemyLayers;
    [SerializeField, Tooltip("光束可攔截的敵方子彈 Layer")] private LayerMask bulletLayers;

    private LaserBeamTracer beamTracer;
    private readonly Camera[] cameraBuffer = new Camera[4];
    private PlayerPolarity playerPolarity;
    private PlayerMovement playerMovement;
    private PlayerEnergy playerEnergy;
    private LaserBeamVisual beamVisual;
    private float tickTimer;
    private bool isFiring;

    public bool IsFiring => isFiring;

    private void Awake()
    {
        playerPolarity = GetComponent<PlayerPolarity>();
        playerMovement = GetComponent<PlayerMovement>();
        playerEnergy = GetComponent<PlayerEnergy>();
        if (RequiredReference.IsMissing(beamPrefab, nameof(beamPrefab), this)) return;
        beamTracer = new LaserBeamTracer(hitRadius, maxRange, enemyLayers, bulletLayers);

        // 光束外觀不掛在角色底下，端點直接用世界座標設定
        GameObject beamObject = Instantiate(beamPrefab, Vector3.zero, Quaternion.identity);
        beamObject.name = beamPrefab.name;
        beamVisual = beamObject.GetComponent<LaserBeamVisual>();

        // 光束外觀缺元件或因自身欄位缺漏而停用時，雷射也停用，避免之後每幀呼叫 Show 時才丟 NullReferenceException
        if (RequiredReference.IsMissing(beamVisual, nameof(beamPrefab) + " 上的 LaserBeamVisual", this) || !beamVisual.enabled)
        {
            enabled = false;
            Destroy(beamObject);
        }
    }

    private void OnDisable()
    {
        StopFiring();
    }

    private void Update()
    {
        BulletPolarity beamPolarity = playerPolarity.CurrentPolarity;
        if (!IsFirePressed() || !playerEnergy.HasEnergy(beamPolarity))
        {
            StopFiring();
            return;
        }

        playerEnergy.Drain(beamPolarity, energyCostPerSecond * Time.deltaTime);

        Vector3 origin = beamOrigin != null ? beamOrigin.position : transform.position + Vector3.up * originHeight;
        Vector3 direction = GetAimDirection(origin);
        playerMovement.SetFacingOverride(direction);

        Vector3 start = origin + direction * originForwardOffset;
        float length = beamTracer.Trace(start, direction, playerPolarity.CurrentPolarity, out EnemyHealth target);
        beamVisual.Show(start, start + direction * length, CurrentColor(), beamWidth, target != null);
        isFiring = true;

        tickTimer -= Time.deltaTime;
        if (target != null && tickTimer <= 0f)
        {
            target.TakeDamage(damagePerTick);
            tickTimer = tickInterval;
        }
    }

    private bool IsFirePressed()
    {
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        return (fireWithMouse && mouse != null && mouse.leftButton.isPressed)
            || (keyboard != null && keyboard[alternateFireKey].isPressed);
    }

    private void StopFiring()
    {
        if (!isFiring) return;

        isFiring = false;
        beamVisual.Hide();
        playerMovement.ClearFacingOverride();
    }

    // 滑鼠游標射線打到「光束高度的水平面」的位置即為瞄準點；打不到時沿角色正前方
    private Vector3 GetAimDirection(Vector3 origin)
    {
        Camera cam = GetActiveCamera();
        Mouse mouse = Mouse.current;
        if (cam != null && mouse != null)
        {
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            Plane aimPlane = new Plane(Vector3.up, origin);
            if (aimPlane.Raycast(ray, out float enter))
            {
                Vector3 toAim = ray.GetPoint(enter) - origin;
                toAim.y = 0f;
                if (toAim.sqrMagnitude > 0.01f) return toAim.normalized;
            }
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;
        return forward.normalized;
    }

    // 場景中有兩台可切換的相機，取目前啟用中、深度最高的那台
    private Camera GetActiveCamera()
    {
        int count = Camera.GetAllCameras(cameraBuffer);
        Camera best = null;
        for (int i = 0; i < count; i++)
        {
            if (best == null || cameraBuffer[i].depth > best.depth) best = cameraBuffer[i];
        }
        return best;
    }

    private Color CurrentColor()
    {
        return playerPolarity.CurrentPolarity == BulletPolarity.Yellow ? yellowColor : blueColor;
    }
}
