using UnityEngine;
using UnityEngine.InputSystem;

// 玩家武器：按住射擊鍵朝滑鼠游標方向攻擊，有兩種模式。
// 一般射擊 — 連射一小段一小段的光束子彈 (顏色跟隨目前極性)，打到第一個敵人造成傷害；不消除敵方子彈、不耗能量。
// 大招光束 — 按 F 花藍、黃能量各一筆，開啟一段時間的綠色 (藍黃合併) 粗光束：持續傷害並同時消除藍、黃兩色子彈
// (紅色攻擊仍只能閃避)。時間到自動回到一般射擊
[RequireComponent(typeof(PlayerPolarity))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerEnergy))]
public class PlayerLaser : MonoBehaviour, ICombatPhaseListener
{
    [Header("Input")]
    [SerializeField, Tooltip("按住滑鼠左鍵射擊")] private bool fireWithMouse = true;
    [SerializeField, Tooltip("另一個可按住射擊的鍵盤按鍵")] private Key alternateFireKey = Key.J;
    [SerializeField, Tooltip("開啟大招光束的按鍵")] private Key burstKey = Key.F;

    [Header("Aim")]
    [SerializeField, Tooltip("射擊起點，留空時以角色位置往上 originHeight 計算")] private Transform beamOrigin;
    [SerializeField, Tooltip("射擊起點離地高度 (未指定 beamOrigin 時)")] private float originHeight = 5.8f;
    [SerializeField, Tooltip("射擊起點往瞄準方向前移的距離，避免從角色身體中間射出")] private float originForwardOffset = 2.5f;
    [SerializeField, Tooltip("射程 (m)")] private float maxRange = 150f;

    [Header("Normal Shot")]
    [SerializeField, Tooltip("一般射擊的光束子彈外觀 (需帶 LineRenderer)")] private GameObject shotPrefab;
    [SerializeField, Min(0.1f), Tooltip("每秒射出幾發")] private float shotsPerSecond = 8f;
    [SerializeField, Min(1f), Tooltip("子彈飛行速度 (m/s)")] private float shotSpeed = 150f;
    [SerializeField, Min(0.1f), Tooltip("每發光束的長度 (m)")] private float shotLength = 7f;
    [SerializeField, Min(0.05f), Tooltip("子彈的命中判定半徑 (m)")] private float shotHitRadius = 0.8f;
    [SerializeField, Min(1), Tooltip("每發的傷害")] private int shotDamage = 1;
    [SerializeField, Tooltip("藍色極性的子彈顏色")] private Color blueColor = new Color(0.2f, 0.5f, 1f, 1f);
    [SerializeField, Tooltip("黃色極性的子彈顏色")] private Color yellowColor = new Color(1f, 0.75f, 0.1f, 1f);

    [Header("Burst Beam")]
    [SerializeField, Tooltip("大招光束外觀 Prefab (需帶有 LaserBeamVisual)")] private GameObject beamPrefab;
    [SerializeField, Min(0.1f), Tooltip("按下後可以使用光束的時間 (秒)")] private float burstDuration = 5f;
    [SerializeField, Min(0f), Tooltip("開啟時藍、黃能量各扣多少 (%)，任一色不足就無法開啟")] private float burstEnergyCost = 30f;
    [SerializeField, Tooltip("光束顏色 (藍 + 黃 = 綠)")] private Color burstColor = new Color(0.3f, 1f, 0.35f, 1f);
    [SerializeField, Tooltip("光束外觀寬度")] private float beamWidth = 10f;
    [SerializeField, Tooltip("光束判定半徑 (m)，越大越容易掃到子彈")] private float hitRadius = 3f;
    [SerializeField, Tooltip("光束每次判定造成的傷害")] private int damagePerTick = 1;
    [SerializeField, Tooltip("光束傷害判定間隔 (秒)")] private float tickInterval = 0.1f;

    [Header("Layers")]
    [SerializeField, Tooltip("會被打中的敵人 Layer")] private LayerMask enemyLayers;
    [SerializeField, Tooltip("大招光束可消除的敵方子彈 Layer")] private LayerMask bulletLayers;

    [Header("Phase Transition")]
    [SerializeField, Min(0f), Tooltip("進入 Boss 戰後幾秒內無法射擊 (與相機轉場時間相同)")] private float bossTransitionLockDuration = 2f;

    // 連射最多同時在場上的子彈數 (射速 × 飛行時間，再留餘裕)
    private const int ShotPoolSize = 32;

    private LaserBeamTracer beamTracer;
    private PlayerShotVolley volley;
    private readonly Camera[] cameraBuffer = new Camera[4];
    private PlayerPolarity playerPolarity;
    private PlayerMovement playerMovement;
    private PlayerEnergy playerEnergy;
    private LaserBeamVisual beamVisual;
    private float tickTimer;
    private float nextShotTime;
    private float lockedUntil;
    private float burstUntil;
    private bool isFiring;
    private bool beamShowing;

    public bool IsFiring => isFiring;
    public bool IsBurstActive => Time.time < burstUntil;
    // 大招光束剩餘秒數，供 UI 顯示
    public float BurstTimeRemaining => Mathf.Max(0f, burstUntil - Time.time);
    public int ActiveShotCount => volley != null ? volley.ActiveCount : 0;

    private void Awake()
    {
        playerPolarity = GetComponent<PlayerPolarity>();
        playerMovement = GetComponent<PlayerMovement>();
        playerEnergy = GetComponent<PlayerEnergy>();
        if (RequiredReference.IsMissing(beamPrefab, nameof(beamPrefab), this)) return;
        if (RequiredReference.IsMissing(shotPrefab, nameof(shotPrefab), this)) return;
        beamTracer = new LaserBeamTracer(hitRadius, maxRange, enemyLayers, bulletLayers);
        volley = new PlayerShotVolley(shotPrefab, ShotPoolSize, shotHitRadius, enemyLayers);

        // 光束外觀不掛在角色底下，端點直接用世界座標設定
        GameObject beamObject = Instantiate(beamPrefab, Vector3.zero, Quaternion.identity);
        beamObject.name = beamPrefab.name;
        beamVisual = beamObject.GetComponent<LaserBeamVisual>();

        // 光束外觀缺元件或因自身欄位缺漏而停用時，武器也停用，避免之後每幀呼叫 Show 時才丟 NullReferenceException
        if (RequiredReference.IsMissing(beamVisual, nameof(beamPrefab) + " 上的 LaserBeamVisual", this) || !beamVisual.enabled)
        {
            enabled = false;
            Destroy(beamObject);
        }
    }

    private void OnDisable()
    {
        StopFiring();
        if (volley != null) volley.Clear();
    }

    private void OnDestroy()
    {
        if (volley != null) volley.DestroyVisuals();
    }

    // 進入 Boss 戰的轉場 (相機移到 Boss 視角) 期間鎖住武器，避免在畫面還在移動時亂射
    public void OnCombatPhaseChanged(CombatPhase phase)
    {
        if (phase == CombatPhase.Boss) lockedUntil = Time.time + bossTransitionLockDuration;
    }

    private void Update()
    {
        // 已射出的子彈放開射擊鍵後仍繼續飛
        volley.Tick(Time.deltaTime);

        if (Time.time < lockedUntil)
        {
            StopFiring();
            return;
        }

        TryStartBurst();
        if (!IsFirePressed())
        {
            StopFiring();
            return;
        }

        Vector3 origin = beamOrigin != null ? beamOrigin.position : transform.position + Vector3.up * originHeight;
        Vector3 direction = GetAimDirection(origin);
        playerMovement.SetFacingOverride(direction);
        Vector3 start = origin + direction * originForwardOffset;
        isFiring = true;

        if (IsBurstActive) FireBeam(start, direction);
        else FireShots(start, direction);
    }

    // 藍、黃能量都夠才開啟，兩色各扣一筆；光束使用中再按不會重複扣
    private void TryStartBurst()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard[burstKey].wasPressedThisFrame || IsBurstActive) return;
        if (playerEnergy.GetEnergy(BulletPolarity.Blue) < burstEnergyCost || playerEnergy.GetEnergy(BulletPolarity.Yellow) < burstEnergyCost) return;

        playerEnergy.TrySpend(BulletPolarity.Blue, burstEnergyCost);
        playerEnergy.TrySpend(BulletPolarity.Yellow, burstEnergyCost);
        burstUntil = Time.time + burstDuration;
        tickTimer = 0f;
    }

    // 綠色光束以 None 為「放行的顏色」：藍、黃子彈都會被消除，紅色 (None) 攻擊不受影響
    private void FireBeam(Vector3 start, Vector3 direction)
    {
        float length = beamTracer.Trace(start, direction, BulletPolarity.None, out EnemyHealth target);
        beamVisual.Show(start, start + direction * length, burstColor, beamWidth, target != null);
        beamShowing = true;

        tickTimer -= Time.deltaTime;
        if (target != null && tickTimer <= 0f)
        {
            target.TakeDamage(damagePerTick);
            tickTimer = tickInterval;
        }
    }

    private void FireShots(Vector3 start, Vector3 direction)
    {
        HideBeam();
        if (Time.time < nextShotTime) return;

        volley.Fire(start, direction, shotSpeed, maxRange, shotLength, shotDamage, ShotColor());
        nextShotTime = Time.time + 1f / shotsPerSecond;
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
        HideBeam();
        if (!isFiring) return;

        isFiring = false;
        playerMovement.ClearFacingOverride();
    }

    private void HideBeam()
    {
        if (!beamShowing) return;

        beamShowing = false;
        beamVisual.Hide();
    }

    // 滑鼠游標射線打到「射擊高度的水平面」的位置即為瞄準點；打不到時沿角色正前方
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

    private Color ShotColor()
    {
        return playerPolarity.CurrentPolarity == BulletPolarity.Yellow ? yellowColor : blueColor;
    }
}
