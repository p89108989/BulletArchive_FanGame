# 開始畫面 Implementation Plan

**Spec:** docs/superpowers/specs/2026-11-26-title-screen-design.md

## Task 1: TitleScreen (按鈕邏輯)
- Test `Tests/PlayMode/TitleScreenTests.cs`:
  - `TutorialButton_IsNotInteractable`
  - `ClickingStart_RequestsStart` (nextSceneName = "" → 只觸發事件、不換場景)
  - `ClickingQuit_RequestsQuit` (quitApplication = false → 只觸發事件)
- Code `Scripts/TitleScreen.cs`: `[SerializeField] Button startButton, tutorialButton, quitButton; string nextSceneName = "CharacterSelect"; bool quitApplication = true;`
  `event Action OnStartRequested, OnQuitRequested; void StartGame(); void QuitGame();`

## Task 2: StaticBeam
- Test `Tests/PlayMode/StaticBeamTests.cs`: `Beam_SpansFromStartToEnd` (光束圓管的 Z 縮放 = 兩點距離)。
- Code `Scripts/StaticBeam.cs`: `[SerializeField] BossBeamVisual beam; Transform from, to; float intensity = 1;` LateUpdate 呼叫 SetBeam。

## Task 3: TitleSceneBuilder + 場景
- `Scripts/Editor/TitleSceneBuilder.cs`：開著的場景有未存變更就不執行；生成 `Scenes/Title.unity`；Build Settings = Title, CharacterSelect, mian game。
- 以截圖調整構圖；跑完整 EditMode / PlayMode 測試。
