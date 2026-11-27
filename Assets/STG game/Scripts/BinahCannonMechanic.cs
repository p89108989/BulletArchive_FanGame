using System;
using System.Collections;
using UnityEngine;

// Binah 的光炮特殊機制：血量每掉 25% 觸發一次 (75%、50%、25%)，蓄力 5 秒
// 1. 潛入地底消失 (Move_Start) → 在場地遠側中間竄出 (Move_End)，探出地面的同時潑沙、流沙 (無傷害) 鋪滿整個場地
//    → 流沙把玩家沖到最下方，到底時玩家的藍黃護盾破掉 (暫時失效) → 流沙停下後堆出 2~3 座沙丘 (與例行潑沙共用，場上最多 3 座)
// 2. 蓄力：Binah 張大嘴、嘴前的能量球越聚越大，地板紅色警示從 Binah 那側漸漸塗滿 (塗滿 = 發射)、越閃越快：
//    - 蓄力期間玩家累積造成 interruptDamage 以上的傷害 → 打斷，Binah 暈眩 groggyDuration 秒、受到的傷害 ×1.5
//    - 沒打斷 → 光炮 (涵蓋整個場地) 發射 fireDuration 秒：閃亮出現、閃爍、淡出，鏡頭震動；
//      期間玩家不在任何沙丘正後方的掩護區就扣 cannonDamage 血 (只扣一次)
// 3. 結束：護盾恢復、場上的沙丘全部破掉；若血量已經又過了下一個門檻，緊接著再觸發一次
[RequireComponent(typeof(EnemyHealth))]
public class BinahCannonMechanic : MonoBehaviour
{
    public enum Phase
    {
        Idle,
        // 回到中間、潑沙、流沙推人、堆沙丘
        Splash,
        Charging,
        Firing,
        // 被打斷後的暈眩
        Groggy,
    }

    [Header("References")]
    [SerializeField, Tooltip("Binah 的 Animator (暈眩時觸發 Groggy)，可留空")] private Animator animator;
    [SerializeField, Tooltip("光炮發射點 (嘴裡的 Fire_01)：光錐從這裡張開，可留空")] private Transform muzzle;
    [SerializeField, Tooltip("蓄力時把頭轉向場地，可留空")] private BinahHeadAim headAim;
    [SerializeField, Tooltip("回到中間時的潛入/竄出 (含潑沙)；留空則直接瞬移")] private BinahReposition reposition;
    [SerializeField, Tooltip("場上的沙丘 (與例行潑沙共用)，可留空")] private BinahDuneField duneField;
    [SerializeField, Tooltip("攻擊使用權：機制進行中佔住 (普通攻擊、長條光炮暫停)；留空時自動抓同物件上的")] private BossAttackGate attackGate;
    [SerializeField, Tooltip("光炮發射時的鏡頭震動，留空時自動抓主相機上的")] private CameraShake cameraShake;
    [SerializeField, Tooltip("留空時自動抓場景中 Tag 為 Player 的角色")] private PlayerHealth playerHealth;
    [SerializeField, Tooltip("留空時自動抓場景中 Tag 為 Player 的角色")] private PlayerMovement playerMovement;
    [SerializeField, Tooltip("留空時自動抓場景中 Tag 為 Player 的角色")] private PlayerPolarity playerPolarity;

    [Header("Trigger")]
    [SerializeField, Tooltip("觸發的血量比例，由高到低 (預設每 25% 一次)")] private float[] triggerFractions = { 0.75f, 0.5f, 0.25f };
    [SerializeField, Tooltip("每一次觸發的蓄力時間 (秒)，對應 triggerFractions")] private float[] chargeDurations = { 5f, 5f, 5f };

    [Header("Splash")]
    [SerializeField, Tooltip("機制開始時 Binah 回到的位置 (場地遠側中間)")] private Vector3 centerPosition = new Vector3(-50f, -5f, -15f);
    [SerializeField, Tooltip("鋪滿整個場地、往下流動的流沙 (把玩家沖到最下方時顯示)，可留空")] private GameObject quicksand;
    [SerializeField, Min(0f), Tooltip("探出地面 (潑沙、流沙出現) 後多久開始把玩家沖走 (秒)")] private float splashDelay = 0.2f;
    [SerializeField, Min(0.01f), Tooltip("流沙把玩家沖到最下方花的時間 (秒)")] private float pushDuration = 1.5f;
    [SerializeField, Tooltip("玩家被沖到的 Z 座標 (整個場地的最下方，靠相機那側；地板邊緣約 Z = 88)")] private float pushZ = 84f;
    [SerializeField, Tooltip("流沙停下後堆出的沙丘數量 (最少, 最多)；場上最多 3 座，滿了就不再堆")] private Vector2Int duneCount = new Vector2Int(2, 3);

    [Header("Charge")]
    [SerializeField, Min(1), Tooltip("蓄力期間累積多少傷害就能打斷 (5 秒內一般射擊最多約 20、F 大招約 50：要開大招才打得斷)")] private int interruptDamage = 40;
    [SerializeField, Tooltip("蓄力期間顯示的整個場地紅色警示 (有 PulseAlpha 時越閃越快)，可留空")] private GameObject chargeWarning;
    [SerializeField, Tooltip("地板紅色警示從 Binah 那側漸漸塗滿 (塗滿 = 發射)，可留空")] private WarningFill chargeFill;
    [SerializeField, Tooltip("蓄力時嘴裡的光 (越聚越大)，可留空")] private GameObject chargeEffect;
    [SerializeField, Tooltip("嘴裡的光從多大長到多大 (縮放倍數)")] private Vector2 chargeGlowScale = new Vector2(0.3f, 1.8f);
    [SerializeField, Tooltip("紅色警示的閃爍週期：蓄力開始時 → 快發射時 (秒)")] private Vector2 warningPulsePeriod = new Vector2(1.2f, 0.2f);

    [Header("Fire")]
    [SerializeField, Min(0.01f), Tooltip("光炮持續時間 (秒)；比衝刺無敵時間長，只能靠沙丘或打斷")] private float fireDuration = 1.5f;
    [SerializeField, Min(0), Tooltip("沒躲好時扣的血")] private int cannonDamage = 15;
    [SerializeField, Tooltip("光炮外觀 (需有 MeshFilter：發射時從嘴到整個場地四角的光錐)，可留空")] private GameObject beamEffect;
    [SerializeField, Tooltip("光炮的顏色 (加亮混合，透明度 = 亮度)")] private Color beamColor = new Color(1f, 0.55f, 0.55f, 1f);
    [SerializeField, Min(0f), Tooltip("光炮從無到全亮的時間 (秒)")] private float beamFadeIn = 0.12f;
    [SerializeField, Min(0f), Tooltip("光炮結束前淡出的時間 (秒)")] private float beamFadeOut = 0.35f;
    [SerializeField, Range(0f, 1f), Tooltip("光炮閃爍的幅度 (亮度 ±)")] private float beamFlicker = 0.15f;
    [SerializeField, Min(0f), Tooltip("光炮發射時的鏡頭震動幅度 (m)")] private float shakeAmplitude = 1.5f;
    [SerializeField, Tooltip("整個場地 (地板) 的最小 X / Z，光炮要涵蓋的範圍")] private Vector2 fieldMin = new Vector2(-109f, -9f);
    [SerializeField, Tooltip("整個場地 (地板) 的最大 X / Z")] private Vector2 fieldMax = new Vector2(7.3f, 88.5f);
    [SerializeField, Tooltip("地板高度")] private float floorHeight = 0.35f;

    [Header("Groggy")]
    [SerializeField, Min(0f), Tooltip("被打斷後暈眩多久 (秒)")] private float groggyDuration = 10f;
    [SerializeField, Min(1f), Tooltip("暈眩期間受到的傷害倍率")] private float groggyDamageMultiplier = 1.5f;

    private static readonly int GroggyHash = Animator.StringToHash("Groggy");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private const float BeamFlickerSpeed = 55f;

    // 機制開始 / 結束時觸發 (例行潑沙換位會訂閱來暫停)
    public event Action OnMechanicStarted;
    public event Action OnMechanicEnded;

    public Phase CurrentPhase { get; private set; }
    public bool IsRunning => CurrentPhase != Phase.Idle;
    // 場上還在 (沒被打掉) 的沙丘數量
    public int ActiveDuneCount => duneField != null ? duneField.ActiveCount : 0;

    private readonly Vector3[] beamVertices = new Vector3[5];
    private Mesh beamMesh;
    private Renderer beamRenderer;
    private MaterialPropertyBlock beamBlock;
    private PulseAlpha warningPulse;
    private EnemyHealth health;
    private HealthThresholds thresholds;
    private int lastHealth;
    private int chargeDamage;
    private Coroutine routine;
    private Coroutine moveRoutine;
    private bool hasEmerged;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        if (attackGate == null) attackGate = GetComponent<BossAttackGate>();
        thresholds = new HealthThresholds(triggerFractions);
        FindPlayer();
        if (cameraShake == null) cameraShake = FindCameraShake();
        if (chargeWarning != null) warningPulse = chargeWarning.GetComponent<PulseAlpha>();
        if (beamEffect != null) beamRenderer = beamEffect.GetComponent<Renderer>();
        beamBlock = new MaterialPropertyBlock();
        SetActive(chargeWarning, false);
        SetActive(chargeEffect, false);
        SetActive(beamEffect, false);
        SetActive(quicksand, false);
        if (chargeFill != null) SetActive(chargeFill.gameObject, false);
    }

    // Boss 是執行時生成的 Prefab，抓不到場景物件的參照：找主相機或戰鬥用的 Up Camera 上的震動元件
    private static CameraShake FindCameraShake()
    {
        if (Camera.main != null && Camera.main.TryGetComponent(out CameraShake mainShake)) return mainShake;

        GameObject upCamera = GameObject.Find("Up Camera");
        return upCamera != null ? upCamera.GetComponent<CameraShake>() : null;
    }

    private void FindPlayer()
    {
        if (playerHealth != null && playerMovement != null && playerPolarity != null) return;

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return;
        if (playerHealth == null) playerHealth = player.GetComponentInParent<PlayerHealth>();
        if (playerMovement == null) playerMovement = player.GetComponentInParent<PlayerMovement>();
        if (playerPolarity == null) playerPolarity = player.GetComponentInParent<PlayerPolarity>();
    }

    private void OnEnable()
    {
        lastHealth = health.CurrentHealth;
        health.OnHealthChanged += HandleHealthChanged;
        health.OnDied += HandleDied;
    }

    private void OnDisable()
    {
        health.OnHealthChanged -= HandleHealthChanged;
        health.OnDied -= HandleDied;
        if (CurrentPhase != Phase.Idle) EndMechanic();
    }

    // 執行時建立的光炮網格不屬於任何資產，要自己釋放
    private void OnDestroy()
    {
        if (beamMesh != null) Destroy(beamMesh);
    }

    private void HandleHealthChanged(int current, int max)
    {
        int damage = lastHealth - current;
        lastHealth = current;

        if (CurrentPhase == Phase.Charging && damage > 0)
        {
            chargeDamage += damage;
            if (chargeDamage >= interruptDamage) BeginGroggy();
            return;
        }

        if (CurrentPhase == Phase.Idle) TryStart();
    }

    private void TryStart()
    {
        if (health.IsDead) return;

        int index = thresholds.Next(health.CurrentHealth, health.MaxHealth);
        if (index < 0) return;

        float charge = chargeDurations != null && chargeDurations.Length > 0
            ? chargeDurations[Mathf.Min(index, chargeDurations.Length - 1)]
            : 10f;
        routine = StartCoroutine(Run(charge));
    }

    private IEnumerator Run(float chargeDuration)
    {
        CurrentPhase = Phase.Splash;
        // 由血量觸發、不能等：直接接管攻擊使用權 (正在進行的普通攻擊之後釋放也不會放掉)
        if (attackGate != null) attackGate.ForceAcquire(this);
        OnMechanicStarted?.Invoke();

        // 回到中間：潛入消失 → 竄出；探出地面的那一刻 (竄出動作還在播) 就開始潑沙與流沙。沒有換位元件時直接瞬移
        if (reposition != null)
        {
            hasEmerged = false;
            reposition.OnEmerged += HandleEmerged;
            moveRoutine = StartCoroutine(reposition.MoveTo(centerPosition, Quaternion.identity));
            while (!hasEmerged) yield return null;
            reposition.OnEmerged -= HandleEmerged;
        }
        else
        {
            transform.position = centerPosition;
        }

        // 流沙把玩家沖到最下方，到底時護盾破掉；沖完流沙停下，沙丘才堆出來 (先出現的話玩家會卡在沙丘上)
        SetActive(quicksand, true);
        yield return new WaitForSeconds(splashDelay);
        PushPlayerDown();
        yield return new WaitForSeconds(pushDuration);
        if (playerPolarity != null) playerPolarity.SetShieldSuppressed(true);
        SetActive(quicksand, false);
        SpawnDunes();

        // 竄出動作播完 (回到 Idle) 才開始蓄力，頭部轉向才不會跟竄出動作打架
        while (reposition != null && reposition.IsMoving) yield return null;
        moveRoutine = null;

        // 蓄力：期間受到的傷害由 HandleHealthChanged 累計，達門檻時直接切到暈眩
        CurrentPhase = Phase.Charging;
        chargeDamage = 0;
        SetActive(chargeWarning, true);
        SetActive(chargeEffect, true);
        if (chargeFill != null)
        {
            chargeFill.SetProgress(0f);
            SetActive(chargeFill.gameObject, true);
        }

        if (headAim != null) headAim.AimAt(FireTarget());

        float chargeStart = Time.time;
        while (CurrentPhase == Phase.Charging && Time.time < chargeStart + chargeDuration)
        {
            UpdateChargeVisuals(chargeDuration > 0f ? (Time.time - chargeStart) / chargeDuration : 1f);
            yield return null;
        }

        if (CurrentPhase == Phase.Groggy)
        {
            yield return new WaitForSeconds(groggyDuration);
            health.DamageMultiplier = 1f;
            EndMechanic();
            yield break;
        }

        // 發射：只要有一刻沒躲好就扣一次血 (PlayerHealth 自己的受傷無敵與衝刺無敵照常生效)
        CurrentPhase = Phase.Firing;
        SetActive(chargeWarning, false);
        SetActive(chargeEffect, false);
        if (chargeFill != null) SetActive(chargeFill.gameObject, false);
        SetActive(beamEffect, true);
        if (cameraShake != null) cameraShake.Shake(shakeAmplitude, fireDuration);
        bool hit = false;
        float fireStart = Time.time;
        while (Time.time < fireStart + fireDuration)
        {
            PlaceBeam();
            UpdateBeamBrightness(Time.time - fireStart);
            if (!hit && playerHealth != null && !IsPlayerCovered())
            {
                int before = playerHealth.CurrentHealth;
                playerHealth.TakeDamage(cannonDamage);
                hit = playerHealth.CurrentHealth < before;
            }

            yield return null;
        }

        EndMechanic();
    }

    // 蓄力進度 0~1：嘴裡的光越聚越大、紅色警示越閃越快
    private void UpdateChargeVisuals(float progress)
    {
        if (chargeEffect != null) chargeEffect.transform.localScale = Vector3.one * Mathf.Lerp(chargeGlowScale.x, chargeGlowScale.y, progress);
        if (warningPulse != null) warningPulse.Period = Mathf.Lerp(warningPulsePeriod.x, warningPulsePeriod.y, progress * progress);
        if (chargeFill != null) chargeFill.SetProgress(progress);
    }

    // 光炮亮度：快速亮起 → 持續閃爍 → 結束前淡出 (加亮材質，透明度 = 亮度)
    private void UpdateBeamBrightness(float elapsed)
    {
        if (beamRenderer == null) return;

        float fadeIn = beamFadeIn > 0f ? Mathf.Clamp01(elapsed / beamFadeIn) : 1f;
        float fadeOut = beamFadeOut > 0f ? Mathf.Clamp01((fireDuration - elapsed) / beamFadeOut) : 1f;
        float flicker = 1f + Mathf.Sin(elapsed * BeamFlickerSpeed) * beamFlicker;
        float brightness = Mathf.Clamp01(fadeIn * fadeOut * flicker);
        beamRenderer.GetPropertyBlock(beamBlock);
        beamBlock.SetColor(BaseColorId, new Color(beamColor.r, beamColor.g, beamColor.b, brightness));
        beamRenderer.SetPropertyBlock(beamBlock);
    }

    // 光炮打向場地最下方的中間 (整個場地都在光炮範圍內，只有沙丘正後方安全)
    private Vector3 FireTarget()
    {
        return new Vector3(centerPosition.x, 0f, pushZ);
    }

    // 光炮外觀：從嘴到整個場地四角的光錐 (四面三角形)，整片場地都在光炮裡；頂點陣列重複使用，不產生垃圾
    private void PlaceBeam()
    {
        if (beamEffect == null || muzzle == null) return;
        if (beamMesh == null && !CreateBeamMesh()) return;

        // 光炮外觀掛在 Binah 底下，Binah 會換位；頂點是世界座標，所以每次都把外觀物件放回世界原點
        beamEffect.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        beamVertices[0] = muzzle.position;
        beamVertices[1] = new Vector3(fieldMin.x, floorHeight, fieldMin.y);
        beamVertices[2] = new Vector3(fieldMax.x, floorHeight, fieldMin.y);
        beamVertices[3] = new Vector3(fieldMax.x, floorHeight, fieldMax.y);
        beamVertices[4] = new Vector3(fieldMin.x, floorHeight, fieldMax.y);
        beamMesh.vertices = beamVertices;
        beamMesh.RecalculateBounds();
    }

    private bool CreateBeamMesh()
    {
        MeshFilter filter = beamEffect.GetComponent<MeshFilter>();
        if (filter == null) return false;

        // 頂點直接用世界座標，所以外觀物件本身要放在原點、不旋轉不縮放
        beamEffect.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        beamEffect.transform.localScale = Vector3.one;
        beamMesh = new Mesh { name = "CannonBeamVolume" };
        beamMesh.MarkDynamic();
        beamMesh.vertices = beamVertices;
        // 雙面：四個側面 + 底面 (相機在上方，從哪一面看都要看得到)
        beamMesh.triangles = new[]
        {
            0, 1, 2, 0, 2, 1,
            0, 2, 3, 0, 3, 2,
            0, 3, 4, 0, 4, 3,
            0, 4, 1, 0, 1, 4,
            1, 2, 3, 1, 3, 4, 1, 3, 2, 1, 4, 3,
        };
        filter.sharedMesh = beamMesh;
        return true;
    }

    private void HandleEmerged()
    {
        hasEmerged = true;
    }

    private void BeginGroggy()
    {
        CurrentPhase = Phase.Groggy;
        health.DamageMultiplier = groggyDamageMultiplier;
        SetActive(chargeWarning, false);
        SetActive(chargeEffect, false);
        if (chargeFill != null) SetActive(chargeFill.gameObject, false);
        if (headAim != null) headAim.StopAiming();
        if (animator != null) animator.SetTrigger(GroggyHash);
    }

    // 站在任何一座還在場上的沙丘正後方 (倒梯形掩護區) 就算躲過；光炮方向 = 從中間往場地最下方
    private bool IsPlayerCovered()
    {
        if (playerHealth == null || duneField == null) return false;

        return duneField.IsCovered(playerHealth.transform.position, new Vector3(0f, 0f, pushZ - centerPosition.z));
    }

    private void PushPlayerDown()
    {
        if (playerMovement == null) return;

        Vector3 current = playerMovement.transform.position;
        playerMovement.PushTo(new Vector3(current.x, current.y, pushZ), pushDuration);
    }

    private void SpawnDunes()
    {
        if (duneField == null) return;

        int count = UnityEngine.Random.Range(duneCount.x, Mathf.Max(duneCount.x, duneCount.y) + 1);
        for (int i = 0; i < count; i++) duneField.TrySpawnRandom();
    }

    private void HandleDied()
    {
        if (CurrentPhase == Phase.Idle) return;

        if (routine != null) StopCoroutine(routine);
        if (moveRoutine != null) StopCoroutine(moveRoutine);
        EndMechanic();
    }

    private void EndMechanic()
    {
        routine = null;
        moveRoutine = null;
        if (reposition != null) reposition.OnEmerged -= HandleEmerged;
        CurrentPhase = Phase.Idle;
        health.DamageMultiplier = 1f;
        SetActive(chargeWarning, false);
        SetActive(chargeEffect, false);
        SetActive(beamEffect, false);
        SetActive(quicksand, false);
        if (chargeFill != null) SetActive(chargeFill.gameObject, false);
        if (headAim != null) headAim.StopAiming();
        if (playerPolarity != null) playerPolarity.SetShieldSuppressed(false);
        // 機制結束時場上的沙丘全部破掉 (Boss 死亡時由 BinahDuneField 自己清除)
        if (duneField != null && !health.IsDead) duneField.BreakAll();
        if (attackGate != null) attackGate.Release(this);
        OnMechanicEnded?.Invoke();

        // 機制進行中又跨過了下一個門檻 (例如暈眩時被打很多血)：緊接著再觸發
        if (isActiveAndEnabled) TryStart();
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active) target.SetActive(active);
    }
}
