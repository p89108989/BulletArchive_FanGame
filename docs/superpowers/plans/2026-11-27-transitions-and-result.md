# 場景轉場、戰鬥演出與結算面板 Implementation Plan

**Spec:** docs/superpowers/specs/2026-11-27-transitions-and-result-design.md

## Task 1: 擊殺數與戰鬥時間 (BattleStats)
- `EnemyHealth`: `public static event Action<EnemyHealth> OnAnyKilled` (被打死時觸發)；`public void Demolish()` (直接死亡但不算擊殺)。
- `BossPhaseDemolisher` 改用 `Demolish()`。
- `BattleStats` (MonoBehaviour)：`Kills`、`StartTiming()`、`StopTiming()`、`ElapsedSeconds` (遊戲時間，暫停不計)。
- Tests (PlayMode `BattleStatsTests`)：敵人被打死 +1、Demolish 不算、計時只算 StartTiming 之後到 StopTiming。

## Task 2: 字卡時間軸 (BannerTimeline，EditMode)
- `StartX(t)`、`StartDarkness(t)`、`StartLight(t)` 照原遊戲 UIAni_Start；`StampScale/Alpha(t)` 給 WARNING/VICTORY/DEFEAT。
- Tests：START 從畫面左外飛入、0.32 秒到 -406、中段慢慢往右、1.48 秒後在畫面右外；壓暗 0 → 0.92 → 0。

## Task 3: 字卡元件 (BattleBanner)
- `PlayStart(Action)`、`PlayStamp(Sprite, bool stripes, Action)`；用不受暫停影響的時間。`IsPlaying`。

## Task 4: 稜鏡轉場 (shader STG/PrismTransition + SceneTransition)
- `SceneTransition.Play(Action whileCovered, bool fromBlack)`、`LoadScene(string, bool fromBlack)`；`static bool IsBusy`；轉場期間跨場景保留，結束後自毀。
- Tests (PlayMode)：whileCovered 在完全蓋住時才呼叫、結束後 IsBusy = false。

## Task 5: 結算面板 (ResultPanel) + 戰鬥流程 (BattleFlow)
- `ResultPanel.Show(stage, kills, seconds)`：依 GameSelection.CharacterId 換立繪；確認 → 轉場回 Title。
- `BattleFlow`：開場暫停到 START 結束、Boss 階段放 WARNING、`ShowVictory()` / 玩家死亡 → 字卡 → 結算。
- `BossEncounter.resultScreen` 改成 `BattleFlow battleFlow`；刪除 BossResultScreen / GameOverScreen 與其測試、builder。
- Tests：開場暫停、START 後恢復；死亡 → DEFEAT → 結算顯示擊殺數與時間；勝利同理；Boss 階段播 WARNING。

## Task 6: 場景接線 (builders)
- `SceneTransitionBuilder` (共用)：Title、CharacterSelect、mian game 都放一個 SceneTransition。
- Title start / 選角出擊 / 結算確認 走 SceneTransition。
- `BattleFlowBuilder` (Tools/STG/Build Battle Flow)：在 mian game 建字卡、結算面板、BattleFlow、BattleStats 並接線，移除舊的 Game Over / Boss 結算物件。
- 截圖確認；跑完整測試。
