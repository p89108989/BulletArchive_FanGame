# Review Fixes + Test Suite Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix every issue confirmed in the 2026-11-18 code review, add a 2 s dash cooldown and a simple Game Over screen, and introduce the project's first automated tests (EditMode + PlayMode).

**Architecture:** Split gameplay code into its own assembly so test assemblies can reference it. Each fix lands test-first. Hard-to-test logic gets a small seam: `PlayerMovement.TryStartDash()` and a pure `LaserBeamTracer` class extracted from `PlayerLaser`. Physics queries are filtered by three new layers (Player / Enemy / EnemyBullet) so fixed-size NonAlloc buffers cannot be crowded out.

**Tech Stack:** Unity 6000.0.74f1, URP 17, Input System 1.19, Unity Test Framework 1.6 (NUnit), UnityMCP for running tests and editing scenes/prefabs.

**Spec:** Code review report in conversation (2026-11-18) + user decisions: fix all listed issues; dash cooldown default 2 s; on death show a simple "GAME OVER" (art later); start automated tests.

## Global Constraints

- Follow `CLAUDE.md` exactly: `[SerializeField] private` + Header/Tooltip, cache in Awake, events in OnEnable/OnDisable, no per-frame GC, one public class per file, Editor code under `Editor/` with `#if UNITY_EDITOR`, comments say *why* (Traditional Chinese, matching existing files).
- Gameplay runtime assembly: `STG.Gameplay` (`Assets/STG game/Scripts/STG.Gameplay.asmdef`), refs `Unity.InputSystem`, `UnityEngine.UI`, `VolumetricLines`.
- Editor assembly: `STG.Gameplay.Editor` (`Assets/STG game/Scripts/Editor/`), Editor-only, refs `STG.Gameplay`, `VolumetricLines`.
- Third-party: `VolumetricLines` (`Assets/STG game/resources/shooting item/VolumetricLines/Scripts/VolumetricLines.asmdef`). Do not otherwise edit third-party code.
- Tests: `Assets/STG game/Tests/EditMode/STG.Tests.EditMode.asmdef` (Editor only) and `Assets/STG game/Tests/PlayMode/STG.Tests.PlayMode.asmdef`; both ref `STG.Gameplay`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner` (EditMode only), `nunit.framework.dll` precompiled, `UNITY_INCLUDE_TESTS` define constraint.
- Layers: 6 = `Player`, 7 = `Enemy`, 8 = `EnemyBullet`.
- Commits: the repo currently has all of this work uncommitted and the user has not asked for commits — skip commit steps unless the user requests them.

## Review Focus

- Player dies while the laser is held → beam must hide and stay hidden; no further energy drain. (Task 4 test `Death_DisablesLaserAndHidesBeam`)
- Game Over then restart (R) → `Time.timeScale` must return to 1 after reload. (Task 4 test `Restart_RestoresTimeScale`)
- Turret component re-enabled mid-respawn → turret returns alive and damageable. (Task 8 test `ReEnable_DuringRespawn_ReturnsAliveTurret`)
- Many enemy bullets on the beam path in front of a turret → turret is still hit. (Task 6 test `Trace_FindsEnemyBehindManyBullets`)
- Area strike near walls/turrets/props → player inside radius still damaged. (Task 6 test `Detonate_DamagesPlayerAmongManyDistractors`)

---

### Task 1: Assemblies + test infrastructure

**Files:**
- Create: the 5 `.asmdef` files listed in Global Constraints
- Create: `Assets/STG game/Tests/PlayMode/TestUtil.cs` (`internal static class TestUtil` with `SetField(object target, string fieldName, object value)` via reflection, for private `[SerializeField]` refs)
- Test: `Assets/STG game/Tests/EditMode/SmokeTests.cs`

- [ ] Step 1: Create asmdefs; write `SmokeTests.Gameplay_Assembly_IsReferenced` asserting `typeof(PlayerEnergy).Assembly.GetName().Name == "STG.Gameplay"`.
- [ ] Step 2: Refresh/compile; `read_console` must show 0 errors (all builders, VolumetricLines example scripts, and scene components still resolve).
- [ ] Step 3: `run_tests(mode="EditMode")` → smoke test PASS. Re-run each Tools/STG builder menu once is NOT needed; just confirm the scene still opens without missing-script warnings.

### Task 2: Damage i-frames not shortened by dash (review #1)

**Files:** Modify `Scripts/PlayerHealth.cs`; Test `Tests/PlayMode/PlayerHealthTests.cs`

**Interfaces:** Produces `PlayerHealth.GrantInvincibility(float)` (unchanged signature) and `IsInvincible => Time.time < invincibleUntil`.

- [ ] Step 1: Tests (PlayMode, `[UnityTest]`):
  - `ShortGrant_DoesNotShortenDamageInvincibility`: TakeDamage(1) (window 1 s) → GrantInvincibility(0.2f) → wait 0.5 s → TakeDamage(1) → `CurrentHealth == max-1`.
  - `Invincibility_ExpiresAfterDuration`: TakeDamage(1) → wait 1.1 s → TakeDamage(1) → `CurrentHealth == max-2`.
- [ ] Step 2: Run → first test FAILS (health max-2).
- [ ] Step 3: Replace the coroutine with `private float invincibleUntil`; `GrantInvincibility` sets `invincibleUntil = Mathf.Max(invincibleUntil, Time.time + duration)`. Remove `InvincibilityWindow` and `invincibilityRoutine`. Fix the misleading tooltip text in `PlayerMovement` ("互不影響" → windows overlap, longer one wins).
- [ ] Step 4: Run → PASS.

### Task 3: Dash cooldown 2 s (review #2)

**Files:** Modify `Scripts/PlayerMovement.cs`; Test `Tests/PlayMode/PlayerMovementTests.cs`

**Interfaces:** Produces `public bool TryStartDash()` (returns false while dashing or cooling down); `[SerializeField, Min(0f)] private float dashCooldown = 2f;` cooldown counts from dash start. Also add `[RequireComponent(typeof(CapsuleCollider))]`.

- [ ] Step 1: Tests: `TryStartDash_SecondCallWithinCooldown_ReturnsFalse` (true then false at +0.5 s); `TryStartDash_AfterCooldown_ReturnsTrue` (true, wait 2.1 s, true).
- [ ] Step 2: Run → FAIL (method missing / compile).
- [ ] Step 3: Implement; `Update` calls `TryStartDash()` when Shift pressed.
- [ ] Step 4: Run → PASS.

### Task 4: Player death + simple Game Over (review #3)

**Files:**
- Create: `Scripts/PlayerDeathHandler.cs` — on `PlayerHealth.OnDied`: disables `[SerializeField] private Behaviour[] disableOnDeath` (scene: PlayerMovement, PlayerLaser, PlayerPolarity) and zeroes rigidbody velocity.
- Create: `Scripts/GameOverScreen.cs` — on `OnDied`: activates `[SerializeField] private GameObject panel`, sets `Time.timeScale = 0`; while shown, `Key.R` (`[SerializeField] private Key restartKey = Key.R`) reloads the active scene after restoring `Time.timeScale = 1`. `OnDisable` restores timeScale if it paused it.
- Modify: `Scripts/PlayerLaser.cs` — `OnDisable` already calls `StopFiring()`; verify beam hides.
- Scene: add `GameOverPanel` under the existing Canvas (full-screen black 60% Image + centered legacy `Text` "GAME OVER\n按 R 重新開始", inactive by default) with `GameOverScreen`; add `PlayerDeathHandler` to `Rio_Mesh`.
- Test: `Tests/PlayMode/GameOverTests.cs`

- [ ] Step 1: Tests: `Death_DisablesConfiguredBehaviours`; `Death_ShowsPanelAndPausesTime`; `Restart_RestoresTimeScale` (call the restart method directly via a `public void Restart()`; assert `Time.timeScale == 1` — stub the scene reload behind `[SerializeField] private bool reloadSceneOnRestart = true`, false in the test); `Death_DisablesLaserAndHidesBeam` (PlayerLaser present, firing state forced via disabling → `IsFiring == false`). TearDown always resets `Time.timeScale = 1`.
- [ ] Step 2: Run → FAIL.
- [ ] Step 3: Implement both components; wire scene via UnityMCP; save scene.
- [ ] Step 4: Run → PASS. Play Mode check: take 5 hits → panel visible, game frozen, R restarts.

### Task 5: UI initial value independent of init order (review #4)

**Files:** Modify `Scripts/HealthBarUI.cs`, `Scripts/EnergyBarUI.cs`; Test `Tests/PlayMode/BarUITests.cs`

- [ ] Step 1: Test `HealthBar_CreatedBeforePlayer_ShowsFullHealth`: create inactive player GO with PlayerHealth (max 5), create bar GO (Slider + Text set via `TestUtil.SetField`, `playerHealth` field set), then activate player; `yield return null` → `slider.value == 5` and text `"5 / 5"`. Same pattern `EnergyBar_...` with `startingEnergy = 40` → text `"40%"`.
- [ ] Step 2: Run → FAIL.
- [ ] Step 3: Keep subscribe/unsubscribe in OnEnable/OnDisable; move the initial refresh to `Start` (and keep it in OnEnable only when `started` is true, so re-enabling still refreshes).
- [ ] Step 4: Run → PASS.

### Task 6: Layer-filtered physics queries (review #5, #6) + LaserBeamTracer

**Files:**
- Modify: `ProjectSettings/TagManager.asset` via UnityMCP (`manage_editor add_layer`) — layers 6/7/8.
- Create: `Scripts/LaserBeamTracer.cs` — plain C# class: ctor `(float hitRadius, float maxRange, LayerMask enemyLayers, LayerMask bulletLayers)`; `public float Trace(Vector3 start, Vector3 direction, BulletPolarity beamPolarity, out EnemyHealth target)`. Two separate `SphereCastNonAlloc` calls with their own buffers (enemies: 16, bullets: 64) so bullets can never crowd out enemies; caches `Collider → EnemyHealth` / `Collider → EnemyBullet` in Dictionaries to avoid per-frame GetComponent.
- Modify: `Scripts/PlayerLaser.cs` — replace `hitLayers` with `enemyLayers` and `bulletLayers`; delegate to `LaserBeamTracer`.
- Modify: `Scripts/AreaStrike.cs` — `hitLayers` used as-is; prefab value set to `Player`. Replace "only previous hit" dedup with a reused `HashSet<IDamageable>` cleared per detonation.
- Assets: `Rio_Mesh` (+children) → Player; `EnemyTurret.prefab` + Apache scene object (+children) → Enemy; `EnemyBullet_SM05.prefab` → EnemyBullet; `AreaStrike.prefab hitLayers = Player`; `Rio_Mesh PlayerLaser enemyLayers = Enemy, bulletLayers = EnemyBullet`.
- Tests: `Tests/PlayMode/LaserBeamTracerTests.cs`, `Tests/PlayMode/AreaStrikeTests.cs`

- [ ] Step 1: Tests:
  - `Trace_FindsEnemyBehindManyBullets`: 40 bullet colliders on EnemyBullet layer along +Z at 5..45 m, enemy box at 50 m → returns ≈50 and target non-null.
  - `Trace_InterceptsOnlyOppositePolarityBullets`: one Yellow + one Blue bullet, beam Blue → Yellow intercepted, Blue untouched.
  - `Trace_ZeroEnemies_ReturnsMaxRange`.
  - `Detonate_DamagesPlayerAmongManyDistractors`: 30 Default-layer cubes inside radius + player on Player layer → player loses 1 HP (AreaStrike built in-test with quads, fields via TestUtil).
  - `Detonate_SamePolarity_AbsorbedOnce`: player with two colliders + PlayerPolarity Blue, Blue strike → `OnAttackAbsorbed` fired exactly once.
- [ ] Step 2: Run → FAIL.
- [ ] Step 3: Implement + set layers/masks via UnityMCP; save scene and prefabs.
- [ ] Step 4: Run → PASS. Play Mode check: laser still kills a turret; strike still damages; bullets still hit the player (layer collision matrix left all-enabled).

### Task 7: Laser visual — no per-frame GC, no zero-length warning (review #7, #8)

**Files:** Modify `Scripts/LaserBeamVisual.cs`; Test `Tests/PlayMode/LaserBeamVisualTests.cs`

- [ ] Step 1: Tests: `Show_SteadyState_DoesNotAllocate` (after first Show + one frame, `Assert.That(() => visual.Show(...moving endpoints...), Is.Not.AllocatingGCMemory())`); `Show_ZeroLength_LogsNothing` (`Show(p, p, ...)` then `LogAssert.NoUnexpectedReceived()`).
- [ ] Step 2: Run → FAIL (allocates; LookRotation warning).
- [ ] Step 3: Stop calling `VolumetricLineBehavior.SetStartAndEndPoints` per frame. Write the line's mesh directly with two cached `Vector3[8]` arrays (same layout as the asset: positions = 4×start, 4×end; normals = 4×end, 4×start, in the line object's local space) and set `mesh.bounds` yourself (expanded by width). Color/width still go through the asset's setters on change only. Impact rotation uses the beam direction; skip rotation when length < 0.01.
- [ ] Step 4: Run → PASS; Play Mode screenshot shows the beam unchanged.

### Task 8: Remaining review items

**Files:** Modify `Scripts/DisableOnDeath.cs`, `Scripts/ApacheGunship.cs`, `Scripts/Editor/ApachePrefabBuilder.cs`, plus null guards in `ApacheGunship`, `PlayerLaser`, `AreaStrike`, `LaserBeamVisual`, `HealthBarUI`, `EnergyBarUI`. Tests `Tests/PlayMode/DisableOnDeathTests.cs`, `Tests/PlayMode/NullGuardTests.cs`, `Tests/EditMode/PlayerEnergyTests.cs`, `Tests/EditMode/GameObjectPoolTests.cs`, `Tests/PlayMode/PolarityTests.cs`.

- [ ] Step 1: Tests:
  - `ReEnable_DuringRespawn_ReturnsAliveTurret`: kill → disable component → enable → target active, `IsDead == false`.
  - `MissingBeamPrefab_LogsErrorAndDisables` (PlayerLaser): `LogAssert.Expect(LogType.Error, new Regex("beamPrefab"))`, component `enabled == false`, no exception.
  - Coverage the reviewer asked for: PlayerEnergy (absorb +5 clamps at 100; Drain clamps at 0 and returns drained; None ignored), PlayerPolarity.TryAbsorb matrix (Blue/Yellow/None × shield), GameObjectPool (reuse same instance; Release deactivates), EnemyHealth (OnDied fires once; ResetHealth clears IsDead).
- [ ] Step 2: Run → new behavior tests FAIL; coverage tests should PASS immediately (they pin existing behavior).
- [ ] Step 3: Implement:
  - `DisableOnDeath`: OnDisable only unsubscribes + stops coroutines; OnEnable restores a still-disabled target with `ResetHealth()` + `SetActive(true)`.
  - Null guards: in Awake, missing required ref → `Debug.LogError("[Class] <field> 未指定", this); enabled = false; return;`.
  - `ApacheGunship.FireMissileWhenReady`: don't launch if `health.IsDead` or `!modelRoot.activeSelf`.
  - `ApachePrefabBuilder`: `isPlaying` guard; if the prefab exists, `LoadPrefabContents` and replace only the three meshes/positions, preserving other components and children.
- [ ] Step 4: Run all EditMode + PlayMode → PASS. Re-run Tools/STG/Build Apache Prefab and confirm the scene Apache still has its BoxCollider, launch points, ApacheGunship, EnemyHealth.

### Task 9: Final verification

- [ ] Run both test suites; `read_console` 0 errors/warnings from project code.
- [ ] Play Mode smoke: bullets hit/absorb, energy UI, laser kills turret (respawn 30 s), helicopter run with missiles, dash cooldown, death → Game Over → R restart.
- [ ] Dispatch final code reviewer (superpowers:requesting-code-review) on the changed files.
