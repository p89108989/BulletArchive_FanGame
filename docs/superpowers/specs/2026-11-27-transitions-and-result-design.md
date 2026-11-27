# 場景轉場、戰鬥演出與結算面板 設計

## 目的
參考蔚藍檔案的演出，讓場景切換與戰鬥開始/Boss 出場/結束有一致的轉場與字卡，最後進到新的結算面板。

## 流程
1. 開始畫面 → 選角：**稜鏡轉場** (三角形蓋滿畫面 → 載入場景 → 三角形散開露出新場景)。
2. 選角 → 戰鬥：**壓黑 → 稜鏡蓋滿 → 載入戰鬥 → 稜鏡散開並淡出 → START 字卡**。
   從載入戰鬥到 START 飛出前，**戰鬥暫停** (Time.timeScale = 0；演出用不受暫停影響的時間)。
3. Boss 戰開始 (進入 Boss 階段)：畫面壓暗 + **WARNING 字卡** (上下兩條橘色警示條紋)。不暫停 (同時進行鏡頭轉場與 Boss 出場)。
4. 戰鬥結束：
   - 打完最後一隻 Boss → 壓暗 + **VICTORY** 字卡。
   - 玩家死亡 → 壓暗 + **DEFEAT** 字卡。
   - 字卡結束後切到 **結算面板**，遊戲暫停。

## 稜鏡轉場 (程式繪製)
UI_Prefabs 裡原遊戲的稜鏡轉場 (UIMultiFloorRaid_Transition、FX_MAT_Triangle) 匯出時遺失了貼圖與 shader，無法使用，所以自己畫：
- 全螢幕 UI shader：稍微傾斜的三角形格子，每個三角形是深淺不一的淺藍色、藍色邊線 (參考截圖)。
- 一個進度值 0~1：0 = 完全透明，0.5 = 三角形蓋滿畫面，1 = 散開消失。三角形依離畫面中心的距離 + 隨機延遲依序長大/縮小。
- 轉場管理物件跨場景保留 (DontDestroyOnLoad)，負責「蓋起來 → 載入場景 → 打開」。

## 字卡 (同一套元件)
- **START**：`Ingame_Image_Start.png` + 閃光 `Ingame_Start_Light.png`，時間照原遊戲 `UIAni_Start.anim`：
  0.18~0.32 秒從左邊外面快速飛到中間偏左 → 0.32~1.43 秒減速慢慢往右 → 1.43~1.48 秒從右邊飛出；
  背景壓暗約 0.5 秒到 92%、1.6 秒退掉；閃光 0.5 秒亮起、1.5 秒淡出。總長約 1.85 秒。
- **WARNING**：`Ingame_Warning_Text_1/2` + 上下兩條 `Ingame_Warning_Deco_1/2` 條紋 (條紋橫向捲動)，同樣的壓暗。
- **VICTORY / DEFEAT**：`ImageFont_Campaign_Victory/Defeat`，壓暗 + 字樣 (只換單字)。

## 結算面板 (參考截圖三)
- 背景：淡藍斜線底 + 右側白色斜切區塊。
- 左邊：大標題「BATTLE COMPLETE」；
  - 關卡名稱：單一 Boss = Boss 名稱 (例如 Perorozilla)；無盡模式 = 「無盡模式」。
  - 戰鬥記錄只留兩項：**擊殺敵方單位數**、**使用時間** (START 結束到戰鬥結束，mm:ss)。
  - 移除玩家等級與其他數據。
- 右邊：所選角色的全身立繪 (`BattleUI/charaterfullimg`)：
  リオ = Rio_(Battle)_00、トキ = Toki_00、アリス = Arisu_(Battle)_00、ワカモ = Wakamo_(Swimsuit)_diorama_00。
- 右下黃色「確認」按鈕 → 稜鏡轉場回**開始畫面 (Title)**。

## 擊殺數
- 新增擊殺計數：任何敵人 (EnemyHealth，含 Boss) 死亡 +1。
- Boss 戰開始時由 BossPhaseDemolisher 清掉的場上砲台**不算** (不是玩家打倒的)。

## 取代的東西
- `BossResultScreen` (文字版結算) 與 `GameOverScreen` (死亡畫面) 由新的勝敗字卡 + 結算面板取代。

## 我的判斷 (可修改)
- 玩家死亡也進同一個結算面板，標題同樣顯示「BATTLE COMPLETE」(依你的描述)；若想改成「BATTLE FAILED」之類再說。
- WARNING 不暫停戰鬥。
- 結算面板只有「確認」一個按鈕 (回開始畫面)；原本按 R 重新開始的功能拿掉。
- 直接從戰鬥場景按 Play (不經過選角) 時：沒有前面的轉場，START 照樣播放並暫停；結算右邊立繪用 リオ。
