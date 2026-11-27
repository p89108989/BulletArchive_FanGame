# 選角場景 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 新增選角場景 (四角色 + Boss/無盡模式分頁 + 出擊)，選擇經 `GameSelection` 資產帶進 `mian game`，並支援無盡模式依序換 Boss。

**Architecture:** `GameSelection` (ScriptableObject，執行期欄位不序列化) 是兩個場景間唯一的橋。`CharacterSelectScreen` 管理選擇狀態並寫入；`BossEncounter` 讀取 Boss 清單、擊倒後換下一隻；`CombatPhaseController` 回到一般戰鬥時重新倒數。場景與動畫控制器由 Editor builder 產生。

**Tech Stack:** Unity 6000.0.74f1、URP、uGUI (legacy `Text`)、Unity Test Framework (EditMode/PlayMode)、UnityMCP。

**Spec:** `docs/superpowers/specs/2026-11-26-character-select-design.md`

## Global Constraints
- CLAUDE.md：`[SerializeField] private` + Tooltip/Header；Awake 快取；OnEnable/OnDisable 註冊/解註冊；不在 Update 配置記憶體；一檔一公共類；Editor 程式在 `Scripts/Editor/`。
- 執行期程式在 `Assets/STG game/Scripts/` (asmdef `STG.Gameplay`)，測試在 `Assets/STG game/Tests/{EditMode,PlayMode}/`，測試用 `TestUtil.SetField`。
- 直接開 `mian game` 時行為不變 (沿用場景的 `bossProfile` 與 `defeatOutcome`)。
- `GameSelection` 不可在 Play 後改寫資產檔。
- 不 commit (使用者未要求)。
- 回覆使用繁體中文。

## Review Focus
1. Editor 連續 Play 兩次：第二次直接開 `mian game` 不能沿用上次選角的 Boss → `GameSelection` 每次進入 Play 都是空的 (Task 1 測 `[NonSerialized]` 行為：新建實例 `HasSelection == false`；另以 `OnEnable` 清空)。
2. 無盡模式清單含沒有 Profile 的項目 (HoverCraft) → 略過，不卡住 (Task 2)。
3. 清單全部都沒有 Profile → 出擊按鈕不可按 (Task 3)。
4. 無盡模式第二隻 Boss 出場時相機/天空用第二隻的設定 → `BossEncounter.Profile` 在回到 Normal 前就已換好 (Task 2)。
5. 回到一般戰鬥後倒數只重啟一次，不會重複出兩隻 Boss (Task 2)。

---

### Task 1: GameSelection

**Files:**
- Create: `Assets/STG game/Scripts/GameSelection.cs`
- Test: `Assets/STG game/Tests/EditMode/GameSelectionTests.cs`

**Interfaces — Produces:**
- `class GameSelection : ScriptableObject` (`[CreateAssetMenu(menuName = "STG/Game Selection")]`)
- `bool HasSelection`、`string CharacterId`、`bool Loop`、`int BossCount`
- `void Select(string characterId, BossProfile[] bosses, bool loop)` — 複製清單並去掉 null
- `BossProfile GetBoss(int index)` — 依 `Loop` 取第 index 隻；不循環且超出範圍回傳 null
- `void Clear()`；`OnEnable` 時 `Clear()` (每次載入/進 Play 都是空的)

- [ ] **Step 1: 寫失敗測試** — `NewInstance_HasNoSelection`；`Select_SkipsMissingProfiles` (傳 `{a, null, b}` → `BossCount == 2`、`GetBoss(1) == b`)；`Loop_WrapsAround` (`GetBoss(2) == a`)；`NoLoop_PastTheEndIsNull`；`Clear_RemovesSelection`。
- [ ] **Step 2: 跑 EditMode `GameSelectionTests`** — Expected: 編譯失敗 / FAIL (型別不存在)。
- [ ] **Step 3: 實作** — 欄位全部 `[System.NonSerialized] private`。
- [ ] **Step 4: 跑測試** — Expected: 5/5 PASS。

### Task 2: 戰鬥場景讀取選擇 + 無盡模式換 Boss

**Files:**
- Modify: `Assets/STG game/Scripts/BossEncounter.cs`
- Modify: `Assets/STG game/Scripts/CombatPhaseController.cs`
- Test: `Assets/STG game/Tests/PlayMode/BossRunTests.cs`、`Assets/STG game/Tests/PlayMode/CombatPhaseTests.cs`

**Interfaces — Consumes:** Task 1 的 `GameSelection`。
**Produces:**
- `BossEncounter` 新欄位 `[SerializeField] private GameSelection gameSelection;`
- `BossEncounter.Profile` 回傳「目前這一隻」；`int BossIndex`
- 有選擇時：擊倒後 `GetBoss(BossIndex + 1)` 非 null → `BossIndex++`、回到 Normal；為 null → 顯示結算 (resultScreen 有設才顯示，否則回 Normal)。
- 沒選擇時：完全照舊。
- `CombatPhaseController`：`EnterPhase(Normal)` 時重新開始 `BossCountdown` (先停掉舊的)。

- [ ] **Step 1: 寫失敗測試 (BossRunTests)** — 沿用 `BossDefeatTests` 的建置方式，兩份 Profile (A、B)：
  - `Endless_AfterDefeat_NextBossProfileIsCurrent`：Select("CH0331", {A, B}, true) → 進 Boss → 擊倒 → `Profile == B` 且 `phaseController.CurrentPhase == Normal`。
  - `Endless_LoopsBackToFirst`：連打兩隻 → `Profile == A`。
  - `SingleBoss_FromSelection_ShowsResults`：Select(…, {A}, false) → 擊倒 → 結算面板顯示。
  - `NoSelection_UsesSceneProfile`：不 Select → 生成的是場景的 `bossProfile`。
- [ ] **Step 2: 寫失敗測試 (CombatPhaseTests)** — `ReturningToNormal_RestartsTheBossCountdown`：delay 0.2 → Boss → EnterPhase(Normal) → 等 0.4 秒 → `CurrentPhase == Boss`；`ReturningToNormal_CountdownRunsOnce`：計數 `OnPhaseChanged(Boss)` 只有 1 次。
- [ ] **Step 3: 跑 PlayMode 這兩個檔** — Expected: FAIL。
- [ ] **Step 4: 實作**。
- [ ] **Step 5: 跑 PlayMode 全部** — Expected: 全過 (原有 BossDefeatTests 不壞)。

### Task 3: 選角畫面邏輯

**Files:**
- Create: `Assets/STG game/Scripts/SelectableCharacter.cs`、`Assets/STG game/Scripts/CharacterSelectScreen.cs`
- Test: `Assets/STG game/Tests/PlayMode/CharacterSelectTests.cs`

**Interfaces — Consumes:** `GameSelection.Select`。
**Produces:**
- `SelectableCharacter : MonoBehaviour`：欄位 `characterId`、`displayName`、`animator`、`nameplateFrame (Graphic)`、`selectedColor`/`normalColor`；`string CharacterId`；`void SetPicked(bool)` → animator bool `Picked` (`static readonly int PickedHash`) + 名牌框顏色；`event Action<SelectableCharacter> OnClicked`；`OnMouseDown` 與名牌 Button 都呼叫 `Click()`。
- `CharacterSelectScreen : MonoBehaviour`：
  - `[Serializable] public struct BossOption { string label; Sprite icon; BossProfile[] bosses; bool loop; }`
  - 欄位：`characters (SelectableCharacter[])`、`bossOptions (BossOption[])`、`bossTabs (Button[])` 與分頁選中/未選中樣式、`mobilizeButton (Button)`、`gameSelection`、`gameSceneName = "mian game"`
  - `SelectedCharacter`、`int SelectedBossOption` (預設 0)、`bool CanMobilize`
  - `PickCharacter(SelectableCharacter)`、`PickBossOption(int)`、`Mobilize()` (寫入 → `gameSceneName` 非空才 `SceneManager.LoadScene`)
- [ ] **Step 1: 寫失敗測試** — `AtStart_NothingPicked_CannotMobilize`；`PickingACharacter_SetsPickedOnlyOnIt` (animator 參數用一個帶 `Picked` bool 的執行期 `AnimatorController`… PlayMode 無法建 controller → 改測 `SelectableCharacter.IsPicked` 屬性)；`PickingAnother_UnpicksThePrevious`；`BossOptionWithoutProfiles_CannotMobilize`；`Mobilize_WritesTheSelection` (`gameSceneName = ""`，檢查 `CharacterId`、`BossCount`、`Loop`)。
- [ ] **Step 2: 跑** — Expected: FAIL。
- [ ] **Step 3: 實作** (`SelectableCharacter` 加 `bool IsPicked`)。
- [ ] **Step 4: 跑 PlayMode 全部** — Expected: 全過。

### Task 4: Builder、場景、接線

**Files:**
- Create: `Assets/STG game/Scripts/Editor/CharacterSelectBuilder.cs` (選單 `Tools/STG/Build Character Select`)
- Output: `Assets/STG game/Scenes/CharacterSelect.unity`、`Assets/STG game/resources/prefab/CharacterSelect/` (每個角色的 AnimatorController、`GameSelection.asset`)
- Modify (由 builder 或 MCP)：`mian game.unity` 的 `BossEncounter.gameSelection`、`EditorBuildSettings` (CharacterSelect 第一)

內容：
- 角色：`CH0331 リオ`、`CH0187 トキ`、`CH0334 アリス`、`CH0175 ワカモ`；模型 `…/Character/CH0xxx/Echelon/Echelon_CH0xxx.glb`；片段用前綴 `CH0xxx_Formation_Idle--`、`CH0xxx_Formation_Pickup--` 比對。Controller：Idle (預設) ⇄ Pickup，條件 bool `Picked`，無 exit time，都設為循環。每個模型加一個包住身體的 BoxCollider 讓 `OnMouseDown` 可點。
- 舞台：淡藍背景、四個模型等距排成一列、透視相機略俯視 (參考截圖)。
- UI (Canvas Screen Space Overlay，1920×1080 參考)：左側四個分頁 (Perorozilla / Binah / HoverCraft / 無盡模式，圖示 `06_Raid/Boss_Portrait_Perorozilla_Lobby`、`…_Binah_Lobby`、`…_RaidHoverCraft_Lobby`，無盡模式用三張並排或 Binah_Insane)；名牌 (只有名字，選中黃框)；右下黃色出擊按鈕。字型 `CCG_LINESeedKR_Bd.ttf`，日文缺字時確認動態字型 fallback (必要時加 `Yu Gothic` fallback)。
- BossOption：Perorozilla `{Boss_Perorozilla}`、Binah `{Boss_Binah}`、HoverCraft `{}`、無盡 `{Boss_Perorozilla, Boss_Binah}` + loop (HoverCraft 做好後加入)。
- 需要 EventSystem (Input System UI module)。

- [ ] **Step 1: 寫 builder 並執行**；檢查 console 無錯。
- [ ] **Step 2: 截圖驗證** 選角畫面；Play 中點一個角色 → 播 Pickup；選 Binah → 出擊 → 進 `mian game` 且 Boss 戰是 Binah。
- [ ] **Step 3: 跑 EditMode + PlayMode 全部** — Expected: 全過。
