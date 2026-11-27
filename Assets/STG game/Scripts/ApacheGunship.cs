using System.Collections;
using UnityEngine;

// 武裝直升機出場流程 (每隔固定時間一輪)：
// 從場外飛到場景中擺放的起始位置 → 以場地中心為圓心盤旋 (機頭始終朝向中心)，期間定時呼叫一波圈形攻擊，
// 每個圈快爆炸時從機翼發射一顆飛彈飛向圈中心 → 飛離場地並隱藏，等下一輪。
// 場景中擺放的位置與俯仰角就是盤旋起點與盤旋時的姿勢。Boss 戰期間不再出場，若正在場上則立刻飛離
[RequireComponent(typeof(EnemyHealth))]
public class ApacheGunship : MonoBehaviour, ICombatPhaseListener
{
    [Header("References")]
    [SerializeField, Tooltip("直升機外觀根物件，離場時隱藏")] private GameObject modelRoot;
    [SerializeField, Tooltip("機身的 Renderer (MeshRenderer 或 SkinnedMeshRenderer)，用來換算直升機長度以決定飛彈大小")] private Renderer bodyRenderer;
    [SerializeField, Tooltip("負責生成圈形攻擊的 Spawner (建議關閉它的自動波次)")] private AreaStrikeSpawner strikeSpawner;

    [Header("Schedule")]
    [SerializeField, Tooltip("開場後第一次出場的延遲 (秒)")] private float firstAppearDelay = 5f;
    [SerializeField, Tooltip("每次出場的間隔 (秒，從上一次出場開始算)")] private float appearInterval = 30f;

    [Header("Entry / Exit")]
    [SerializeField, Tooltip("從場外飛到盤旋起點的時間 (秒)")] private float entryDuration = 3f;
    [SerializeField, Tooltip("從盤旋終點飛離場地的時間 (秒)")] private float exitDuration = 3f;
    [SerializeField, Tooltip("進場/離場時與盤旋圓周的水平距離 (m)")] private float travelDistance = 90f;
    [SerializeField, Tooltip("進場/離場時比盤旋高度再高多少 (m)")] private float travelRise = 20f;

    [Header("Orbit")]
    [SerializeField, Tooltip("盤旋圓心 (場地中心，只取 X/Z)")] private Vector3 orbitCenter = new Vector3(-50.4f, 0f, 39.4f);
    [SerializeField, Tooltip("盤旋持續時間 (秒)")] private float orbitDuration = 15f;
    [SerializeField, Tooltip("盤旋期間繞過的角度 (度)，180 = 半圈")] private float orbitAngle = 180f;
    [SerializeField, Tooltip("每次出場隨機決定順時針或逆時針盤旋")] private bool randomizeDirection = true;
    [SerializeField, Tooltip("不隨機時是否順時針盤旋 (由上往下看)")] private bool clockwise;

    [Header("Attack")]
    [SerializeField, Tooltip("開始盤旋後第一波攻擊的延遲 (秒)")] private float firstAttackDelay = 2f;
    [SerializeField, Tooltip("盤旋期間每波攻擊的間隔 (秒)")] private float attackInterval = 5f;

    [Header("Missile")]
    [SerializeField, Tooltip("飛彈 Prefab (需帶有 StrikeMissile 元件)")] private GameObject missilePrefab;
    [SerializeField, Tooltip("飛彈發射點，依序輪流使用；留空時從機身中心發射")] private Transform[] missileLaunchPoints;
    [SerializeField, Tooltip("飛彈飛行時間 (秒)，會在圈爆炸前這麼久發射，剛好在爆炸時抵達")] private float missileFlightTime = 0.8f;
    [SerializeField, Tooltip("飛彈長度佔直升機全長的比例 (模型上掛載的 Hellfire 約 0.14)")] private float missileLengthRatio = 0.14f;
    [SerializeField, Tooltip("飛彈物件池預先建立的數量")] private int missilePoolPrewarmCount = 6;

    [Header("Sound")]
    [SerializeField, Tooltip("在場上時的旋翼聲 (循環)，可留空")] private LoopingSound rotorSound;
    [SerializeField, Tooltip("發射飛彈的聲音")] private SoundEffect missileLaunchSound = new SoundEffect();
    [SerializeField, Tooltip("飛彈爆炸的聲音 (一波依序三顆，用最短間隔讓一波只響一次)")] private SoundEffect missileExplosionSound = new SoundEffect();

    private EnemyHealth health;
    private GameObjectPool missilePool;
    private Vector3 homePosition;
    private float pitchAngle;
    private float orbitRadius;
    private float startAngle;
    private float missileLength;
    private int nextLaunchPointIndex;
    private bool isAttacking;
    private bool isBossPhase;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        if (RequiredReference.IsMissing(modelRoot, nameof(modelRoot), this)) return;
        if (RequiredReference.IsMissing(bodyRenderer, nameof(bodyRenderer), this)) return;
        if (RequiredReference.IsMissing(missilePrefab, nameof(missilePrefab), this)) return;

        homePosition = transform.position;
        pitchAngle = Mathf.DeltaAngle(0f, transform.eulerAngles.x);
        Vector3 offset = Flatten(homePosition - orbitCenter);
        orbitRadius = offset.magnitude;
        startAngle = Mathf.Atan2(offset.z, offset.x);

        // localBounds 對 MeshRenderer 與 SkinnedMeshRenderer 都可用，且不受模型是否顯示影響
        Vector3 localSize = Vector3.Scale(bodyRenderer.localBounds.size, bodyRenderer.transform.lossyScale);
        float bodyLength = Mathf.Max(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
        missileLength = bodyLength * missileLengthRatio;

        // 飛彈不掛在直升機底下，否則會跟著直升機一起移動
        missilePool = new GameObjectPool(missilePrefab, null, missilePoolPrewarmCount);
        modelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (strikeSpawner != null) strikeSpawner.OnStrikeSpawned += HandleStrikeSpawned;
        if (!isBossPhase) StartCoroutine(AppearLoop(firstAppearDelay));
    }

    private void OnDisable()
    {
        if (strikeSpawner != null) strikeSpawner.OnStrikeSpawned -= HandleStrikeSpawned;
        StopAllCoroutines();
        isAttacking = false;
        SetRotor(false);
        if (modelRoot != null) modelRoot.SetActive(false);
    }

    private void SetRotor(bool playing)
    {
        if (rotorSound != null) rotorSound.SetPlaying(playing);
    }

    // Boss 戰：停止出場排程與攻擊，在場上的話立刻飛離；回到一般戰鬥後隔一個出場間隔再出場
    public void OnCombatPhaseChanged(CombatPhase phase)
    {
        isBossPhase = phase == CombatPhase.Boss;
        if (!isActiveAndEnabled) return;

        StopAllCoroutines();
        isAttacking = false;
        if (isBossPhase)
        {
            if (modelRoot.activeSelf) StartCoroutine(LeaveArena());
            return;
        }

        StartCoroutine(AppearLoop(appearInterval));
    }

    private IEnumerator LeaveArena()
    {
        yield return Exit();
        SetRotor(false);
        modelRoot.SetActive(false);
    }

    private IEnumerator AppearLoop(float initialDelay)
    {
        yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            float runStart = Time.time;
            yield return StrikeRun();
            yield return new WaitForSeconds(Mathf.Max(0f, appearInterval - (Time.time - runStart)));
        }
    }

    private IEnumerator StrikeRun()
    {
        health.ResetHealth();
        modelRoot.SetActive(true);
        SetRotor(true);

        yield return Entry();
        yield return Orbit();
        yield return Exit();

        SetRotor(false);
        modelRoot.SetActive(false);
    }

    // 從起點正外側的高空減速飛到盤旋起點
    private IEnumerator Entry()
    {
        Vector3 from = homePosition + Flatten(homePosition - orbitCenter).normalized * travelDistance + Vector3.up * travelRise;
        for (float elapsed = 0f; elapsed < entryDuration; elapsed += Time.deltaTime)
        {
            float t = 1f - Mathf.Pow(1f - elapsed / entryDuration, 3f);
            Vector3 position = Vector3.Lerp(from, homePosition, t);
            transform.SetPositionAndRotation(position, FacingCenter(position));
            yield return null;
        }
    }

    // 繞場地中心盤旋，機頭始終朝向中心；期間定時發動攻擊，被擊落時提早離場
    private IEnumerator Orbit()
    {
        bool isClockwise = randomizeDirection ? Random.value < 0.5f : clockwise;
        float direction = isClockwise ? -1f : 1f;
        float nextAttackTime = firstAttackDelay;
        isAttacking = true;

        for (float elapsed = 0f; elapsed < orbitDuration && !health.IsDead; elapsed += Time.deltaTime)
        {
            float angle = startAngle + direction * orbitAngle * Mathf.Deg2Rad * (elapsed / orbitDuration);
            Vector3 position = new Vector3(
                orbitCenter.x + Mathf.Cos(angle) * orbitRadius,
                homePosition.y,
                orbitCenter.z + Mathf.Sin(angle) * orbitRadius);
            transform.SetPositionAndRotation(position, FacingCenter(position));

            if (elapsed >= nextAttackTime && strikeSpawner != null)
            {
                strikeSpawner.SpawnWave();
                nextAttackTime += attackInterval;
            }
            yield return null;
        }

        isAttacking = false;
    }

    // 轉向場外並加速爬升離開
    private IEnumerator Exit()
    {
        Vector3 from = transform.position;
        Quaternion fromRotation = transform.rotation;
        Vector3 outward = Flatten(from - orbitCenter).normalized;
        Vector3 to = from + outward * travelDistance + Vector3.up * travelRise;
        Quaternion toRotation = Quaternion.LookRotation(outward, Vector3.up) * Quaternion.Euler(pitchAngle, 0f, 0f);

        for (float elapsed = 0f; elapsed < exitDuration; elapsed += Time.deltaTime)
        {
            float t = elapsed / exitDuration;
            transform.SetPositionAndRotation(
                Vector3.Lerp(from, to, t * t),
                Quaternion.Slerp(fromRotation, toRotation, Mathf.Clamp01(t * 2f)));
            yield return null;
        }
    }

    private void HandleStrikeSpawned(AreaStrike strike)
    {
        if (!isAttacking) return;
        StartCoroutine(FireMissileWhenReady(strike));
    }

    // 等到圈剩下「飛彈飛行時間」才發射，讓飛彈剛好在爆炸瞬間抵達
    private IEnumerator FireMissileWhenReady(AreaStrike strike)
    {
        while (strike.isActiveAndEnabled && strike.TimeUntilDetonation > missileFlightTime) yield return null;
        if (!strike.isActiveAndEnabled || strike.TimeUntilDetonation <= 0f) yield break;
        // 等待期間直升機可能已被擊落或離場隱藏，就不再發射
        if (health.IsDead || !modelRoot.activeSelf) yield break;

        Vector3 launchPosition = NextLaunchPosition();
        GameObject missileObject = missilePool.Get(launchPosition, transform.rotation);
        StrikeMissile missile = missileObject.GetComponent<StrikeMissile>();
        missile.Launch(launchPosition, strike.transform.position, strike.TimeUntilDetonation, strike.Polarity, missileLength, ReturnMissileToPool);
        missileLaunchSound.Play();
    }

    private Vector3 NextLaunchPosition()
    {
        if (missileLaunchPoints == null || missileLaunchPoints.Length == 0) return transform.position;

        Transform point = missileLaunchPoints[nextLaunchPointIndex];
        nextLaunchPointIndex = (nextLaunchPointIndex + 1) % missileLaunchPoints.Length;
        return point.position;
    }

    // 飛彈抵達 = 圈爆炸的瞬間
    private void ReturnMissileToPool(StrikeMissile missile)
    {
        missileExplosionSound.Play();
        missilePool.Release(missile.gameObject);
    }

    private Quaternion FacingCenter(Vector3 position)
    {
        Vector3 toCenter = Flatten(orbitCenter - position);
        if (toCenter.sqrMagnitude < 0.0001f) return transform.rotation;
        return Quaternion.LookRotation(toCenter, Vector3.up) * Quaternion.Euler(pitchAngle, 0f, 0f);
    }

    private static Vector3 Flatten(Vector3 vector)
    {
        vector.y = 0f;
        return vector;
    }
}
