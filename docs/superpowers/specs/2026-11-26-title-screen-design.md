# 開始畫面 (主視覺構圖) 設計

## 目的
遊戲啟動的第一個畫面。左邊是 UI，右邊是一張靜態主視覺：リオ 在前景中下方滑剷，武器抱在胸前；
後方 Perorozilla 放雷射、Binah 開光炮；HoverCraft 船頭仰起從後上方壓過來，上面的 ワカモ 往前指。
按 start 進入選角場景 (CharacterSelect)。

## 範圍
- 要做：新場景 `Title.unity` (Build Settings 第一個)、`TitleSceneBuilder` (選單 `Tools/STG/Build Title`) 生成整個場景、
  按鈕邏輯 `TitleScreen`、卡通光束 shader `STG/ToonBeam`。
- 不做：教學內容 (按鈕先灰掉)、遊戲正式名稱 (先放「遊戲名稱 (再議)」)、方案一的跑步演出。

## 做法 (使用者選定：即時 3D 場景，畫面偏靜態)
- 角色、Boss 都用模型本體 (glb)，**不用**戰鬥用的 Boss prefab (上面掛了血量、攻擊流程，會在標題畫面自己跑起來)。
- 全部定格、不播動畫：要用某一格的模型在產生場景時用 `SampleAnimation` 套到骨架上存進場景，並拿掉 Animator
  (試過「狀態速度 0 + cycleOffset」，執行時不可靠)。
- リオ：CH0331 完整模型，`Kneel_Attack_Ing` 第 0 格；身體後仰、伸直的腳拉直並落到地面、頭往下壓看向前方；
  最低點貼地；身後揚沙粒子 (唯一會動的東西)。位置與朝向由使用者在場景中調整。
- Perorozilla：`Normal_Attack_Ing` 第 0 格。Binah、HoverCraft：維持預設姿勢 (使用者是對著這個姿勢擺位置的；
  Binah 的攻擊動畫會往前探，頭跑出畫面)。三者位置由使用者在場景中調整。
- HoverCraft 船頭仰起 18°。模型裡附的 ワカモ 沒有貼圖，隱藏後換成 CH0175 本體站在船頭，定格在 `Exs` 約 0.9 秒
  (單手把槍往前伸直指出去)，傘隱藏。
- 光束：`STG/ToonBeam` (三段純色：亮芯 / 主色 / 邊緣色 + 描邊，沒有時間動畫)，內建圓柱接在發射點與落點之間，
  兩端各一顆光球。Perorozilla 雙眼藍色雷射、Binah 橘黃光炮，在 リオ 身後交叉 (從側面看；直直打向鏡頭會縮成一團)。
  位置在產生場景時算好，執行時不需要任何腳本。
- リオ 舉槍：雙臂連同槍 (槍骨架掛在 Bip001 下，不跟手動) 繞肩膀往下轉到槍管水平，槍口約在眼睛高度。
- 落點爆炸：數顆卡通火球 (ToonBeam 爆炸配色) 組成，停在「剛要炸開」的大小。
- 飛彈兩顆：StrikeMissile 的紅色外觀網格 + 卡通尾焰，從 Perorozilla 臉旁 / Binah 頭旁交叉飛向鏡頭。
- 開場慢動作 (`TitleIntroSequence`，進度 0→1 越來越慢，最後定格)：光束從一半延伸到落點 (`IntroBeam`)，碰地後爆炸出現並長大；
  リオ 從身後 3m 滑進來、飛彈飛行 (`IntroMover`)；揚沙跟著放慢到停住 (`IntroParticles`)。
  場景存檔時是最後一格 (編輯模式看到的就是定格畫面)，執行時從第一格開始。
- 戰鬥中的 Boss 光束也改成同一套卡通風格 (`ToonBeamMaterials`)：Perorozilla 雷射 (Boss_Beam：一根卡通管 + 眼睛卡通光球)、
  Binah 長條光炮 (一根卡通圓柱)。Binah 主炮是蓋住整個場地的半透明光錐，換成不透明會擋住玩家，維持原樣。
  既有 prefab 用 `Tools/STG/Apply Toon Beams To Boss Prefabs` 更新 (不整個重建 Boss prefab)。
- 背景：戰鬥場景用的白天天空球；不透明沙色地面。
- 左側 UI：白色面板，右緣斜切；標題框 + start / 教學 (不能按) / 離開遊戲。

## 行為
- start → 載入 `CharacterSelect`。
- 教學 → 不能按 (還沒有教學)。
- 離開遊戲 → 打包版關閉程式；編輯器中停止 Play。
