# 選角場景 (Character Select) 設計

## 目的
遊戲目前只有一個戰鬥場景 `mian game`，Boss 是在場景裡手動指定的。這次加一個開場的選角場景：玩家在這裡選角色與要挑戰的 Boss (或無盡模式)，按出擊後進入戰鬥。
角色目前只有模型、還沒實裝，所以這一步只「記下」選了哪個角色，戰鬥中仍是リオ；之後做角色差異時再接上。

## 目標
- 新場景 `CharacterSelect`，版面參考 Blue Archive 的編隊畫面 (玩家提供的截圖)：
  - 中間並排四個 Q 版角色模型：リオ、トキ、アリス、ワカモ (`Echelon_CH0331/0187/0334/0175.glb`)，腳下各一個名牌，只顯示名字。
  - 左邊原本的 Unit 1–4 分頁改成 Boss 選擇：Perorozilla、Binah、HoverCraft、無盡模式，配 `BattleUI/02_Tactics/06_Raid/Boss_Portrait_*_Lobby.png` 圖示。
  - 右下角出擊按鈕。
- 角色動畫：一開始全部播 `Formation_Idle`；被選上的角色改播 `Formation_Pickup` (選中期間持續播)；改選別人時原本的回到 Idle。
- 出擊後在 `mian game` 套用選擇：
  - 單一 Boss：打那一隻，擊倒後顯示結算 (現有 `BossResultScreen`)。
  - 無盡模式：依序 Perorozilla → Binah → HoverCraft，最後一隻打完從頭再來，直到玩家死亡。

## 不做 (這一步)
- 角色差異面板、戰鬥中換成選到的角色。
- HoverCraft Boss 本體 (還沒做完)。
- 參考圖上方的資源列、右側按鈕列、下方支援角色列。
- 無盡模式的難度遞增、結算/死亡後回到選角場景。

## 反目標 (做出來但算失敗的情況)
- 直接開 `mian game` 按 Play 不能再像現在一樣用場景裡指定的 Boss 測試。
- 在 Editor 遊玩後 `GameSelection.asset` 被改寫 (選擇殘留到下次或進版控)。

## 限制
- 遵守 CLAUDE.md：`[SerializeField] private` + Tooltip、Awake 快取、OnEnable/OnDisable 註冊事件、一檔一公共類、Editor 程式放 `Editor/`。
- TDD：先寫測試看它失敗再實作。
- 場景用 Editor builder (`Tools/STG/Build Character Select`) 產生，與其他 builder 一致；場景有未存的變更時 builder 拒絕執行。
- 名字是日文 (リオ 等)，名牌用的字型必須有日文字 (沿用專案現有 HUD 字型，缺字時改用支援 CJK 的字型)。

## 做法 (Approaches)
選擇結果要從選角場景帶進戰鬥場景，考慮過三種：
- **A. ScriptableObject `GameSelection.asset`** (採用)：選角場景寫入、`BossEncounter` 讀取；沒經過選角時沿用場景原本的 Boss。與現有 `BossProfile` 做法一致、Inspector 看得到、好測試。
- B. DontDestroyOnLoad 物件：直接開戰鬥場景時物件不存在，要另外處理；Inspector 不易觀察。
- C. PlayerPrefs：會存到硬碟 (用不到)，且是一堆字串、不好維護。

玩家選了 A。

## 已決定的設計
### GameSelection (ScriptableObject)
- 執行期欄位都是 `[NonSerialized]`，不會寫回資產 (避免反目標 2)；Editor 進入 Play 時視為「沒有選擇」。
- 內容：選到的角色 (角色 ID)、Boss 清單 (`BossProfile[]`)、是否循環 (`Loop`)、`HasSelection`。
- API：`Select(characterId, bosses, loop)`、`Clear()`。

### 角色資料
- 每個角色一份資料 (ID、顯示名稱、Echelon 模型 Prefab/glb、Idle 與 Pickup 片段)。四個角色：`CH0331` リオ、`CH0187` トキ、`CH0334` アリス、`CH0175` ワカモ。
- glb 裡的片段名有雜湊後綴 (`CH0331_Formation_Pickup--b12a…`)，builder 以「前綴 + `--`」比對。

### Boss 選項
- 四個分頁：Perorozilla、Binah、HoverCraft、無盡模式。每個分頁對應一份 `BossProfile[]`：單一 Boss 是一隻，無盡模式是三隻且循環。
- HoverCraft 不另外標示，正常顯示可選。它還沒有 `BossProfile`：選它時出擊按鈕不可按；無盡模式略過沒有 Profile 的項目 (目前實際是 Perorozilla ↔ Binah 輪流)。
- 預設選第一個分頁 (Perorozilla)，與參考圖「Unit 1」預設選中一致。

### 選角流程
- 一開始沒有角色被選中，全部 Idle；出擊按鈕要「有選角色」且「目前 Boss 選項至少有一隻有 Profile」才可按。
- 點角色模型或名牌都能選；選中的名牌外框變黃 (參考圖的黃框)。
- 按出擊：寫入 `GameSelection` → 載入 `mian game`。
- Build Settings：`CharacterSelect` 排第一、`mian game` 第二。

### 戰鬥場景
- `BossEncounter` 新增 `GameSelection` 參照：有選擇時用它的 Boss 清單，否則用場景原本的 `bossProfile` (單隻)。
- Boss 擊倒後：
  - 清單還有下一隻 (或循環)：換到下一隻 Profile，回到一般戰鬥；`CombatPhaseController` 回到 Normal 時重新開始 Boss 倒數 (目前只在開場倒數一次，這裡要補)，倒數結束後下一隻出場。相機、天空本來就會依 `BossEncounter.Profile` 切換。
  - 單一 Boss：照場景的 `defeatOutcome` 處理；從選角進來的單一 Boss 改用結算 (`ShowResults`)。
- 玩家死亡照現有 `PlayerDeathHandler`。

## 交給實作者決定
- 舞台與背景的外觀 (參考圖是淡藍天空)、角色間距、相機角度。
- 分頁、名牌、出擊按鈕的切圖從 `UIFormation.prefab` / BattleUI atlas 裡挑選。
- 選中名牌的其他強調方式 (例如稍微放大)。
