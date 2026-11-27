# Bullet Archive — 3D Bullet Hell Fan Game

![Unity](https://img.shields.io/badge/Engine-Unity-black?logo=unity)
![C#](https://img.shields.io/badge/Language-C%23-512BD4)
![Claude](https://img.shields.io/badge/AI-Claude-orange)
![MCP](https://img.shields.io/badge/Integration-Unity%20MCP-blue)
![Superpowers](https://img.shields.io/badge/Workflow-Superpowers-purple)
![Status](https://img.shields.io/badge/Status-In%20Development-yellow)

## 開發狀態｜Development Status

**Work in Progress — Active Development**

本專案目前仍處於開發階段，遊戲玩法、系統架構、美術呈現及功能內容皆可能隨開發進度持續調整。

## 專案簡介｜Overview

Bullet Archive 是一款使用 Unity 開發的 3D 彈幕射擊遊戲，以《蔚藍檔案》（Blue Archive）為靈感所製作的非官方同人遊戲，目前仍處於開發階段。

遊戲以傳統彈幕射擊玩法為基礎，加入子彈屬性、彈幕吸收及轉化利用等機制，使玩家除了閃避敵方攻擊之外，也能透過不同屬性的子彈進行策略性的攻防操作。

本專案延續 Vibe Coding 的開發方式，並進一步導入 Claude Code、Superpowers 及 Unity MCP，探索具備結構化開發流程、軟體工程規範及遊戲引擎操作能力的 AI 輔助遊戲開發模式。

## 遊戲核心設計｜Core Gameplay

目前遊戲的主要設計方向包含：

- **Bullet Hell Combat**：以彈幕射擊及閃避作為核心戰鬥玩法。
- **Bullet Attribute System**：建立具有不同屬性的子彈，增加戰鬥策略的多樣性。
- **Bullet Absorption**：玩家可依據子彈屬性及相應機制吸收敵方彈幕。
- **Bullet Conversion**：將吸收的子彈轉化為可利用的戰鬥資源。
- **Character System**：以不同角色及其能力建立具有差異性的遊玩方式。
- **Enemy & Boss System**：設計具有不同攻擊模式及彈幕生成方式的敵人。
- **UI/UX Design**：參考《蔚藍檔案》的視覺風格，設計遊戲介面與操作體驗。
- **3D Visual Presentation**：透過角色模型、動畫、場景打光及視覺特效呈現遊戲內容。

上述內容為目前的遊戲設計與開發方向，實際功能將隨專案進度持續調整。

## AI 輔助開發｜AI-Assisted Development

本專案以 Vibe Coding 作為主要開發方式，由開發者負責遊戲玩法設計、系統需求定義、技術選擇及成果驗證，並透過 Claude Code 輔助程式碼生成、功能實作、問題排查及程式碼重構。

為避免 LLM 在長期開發過程中產生架構不一致、過度設計、非預期修改及技術債務等問題，本專案導入三個不同層級的 AI 輔助開發機制。

| Development Layer | Tool | Purpose |
|---|---|---|
| Project Guidelines | CLAUDE.md | 定義專案層級的開發規範與技術限制 |
| Development Workflow | Superpowers | 引導 LLM 遵循結構化的軟體開發流程 |
| Engine Integration | Unity MCP | 建立 AI Agent 與 Unity Editor 之間的操作介面 |

### 1. 專案層級開發規範（CLAUDE.md）

透過 [CLAUDE.md](./CLAUDE.md) 建立專案專屬的開發指引，作為 LLM 在程式碼生成及功能修改過程中的共同依據。

規範管理的主要目的包含：

- 維持程式碼風格與命名方式的一致性。
- 避免非必要的功能擴充與程式碼修改。
- 維持遊戲系統的模組化與可維護性。
- 遵循 Unity 開發相關的技術原則。
- 降低重複程式碼及不必要的系統複雜度。
- 限制 LLM 的修改範圍，降低對既有功能造成的影響。

### 2. 結構化開發流程（Superpowers）

本專案導入 [Superpowers](https://github.com/obra/superpowers) 作為 Claude Code 的開發輔助套件，透過 Agent Skills 引導 LLM 遵循較具結構性的軟體工程流程。

相較於直接使用自然語言要求 LLM 生成程式碼，Superpowers 提供從需求分析、設計規劃到實作驗證的工作流程，使 AI 輔助開發更具可控性。

主要採用的開發原則與流程包含：

| Workflow | Description |
|---|---|
| Brainstorming | 在實作前釐清需求、功能目標及設計方向 |
| Implementation Planning | 將開發需求拆分為具體且可驗證的實作任務 |
| Incremental Development | 依照規劃逐步實作功能，降低大規模修改的風險 |
| Test-Driven Development | 引入測試驅動開發原則 |
| Systematic Debugging | 以系統化方式分析問題及追蹤錯誤根本原因 |
| Code Review | 檢查生成程式碼的品質及需求符合程度 |
| Verification | 在宣告功能完成前進行必要的驗證 |

透過上述機制，使 LLM 不只是程式碼生成工具，也能在既定的開發流程下協助需求分析、功能設計與軟體實作。

相關設計與開發規劃文件存放於 [`docs/superpowers/`](./docs/superpowers)。

### 3. Unity MCP 整合（Model Context Protocol）

本專案使用 Unity MCP（Model Context Protocol），建立 AI Agent 與 Unity Editor 之間的互動管道，使 LLM 除了讀取及修改程式碼，也能透過 MCP 工具取得遊戲引擎中的專案資訊，並輔助相關操作。

Unity MCP 的應用方向包含：

- 讀取及分析 Unity 專案狀態。
- 取得場景階層與 GameObject 資訊。
- 輔助場景物件建立及設定。
- 管理 Unity Component 及相關屬性。
- 輔助 Prefab 與遊戲資源的操作。
- 取得 Unity Console 的錯誤與警告資訊。
- 輔助遊戲功能的測試與除錯。

實際可執行的操作取決於 MCP Server 所提供的工具與權限設定。

透過 MCP 整合，降低傳統 AI 輔助 Unity 開發過程中，需要於程式碼編輯器與 Unity Editor 之間反覆進行手動操作的成本。

## 開發流程｜Development Workflow

本專案將傳統 Vibe Coding 的自然語言互動方式與結構化軟體工程流程結合。

開發流程主要包含：

1. 由開發者提出遊戲設計及功能需求。
2. 透過 Superpowers 進行需求分析與設計討論。
3. 確認系統架構、功能範圍及實作方向。
4. 建立開發計畫並拆分實作任務。
5. 由 Claude Code 依照 CLAUDE.md 與開發計畫進行實作。
6. 透過 Unity MCP 輔助遊戲引擎內的相關操作。
7. 進行功能測試、錯誤分析及程式碼檢查。
8. 由開發者確認成果並持續迭代。

此開發模式旨在保留 Vibe Coding 快速原型開發的優勢，同時透過明確的規範、開發流程及人工驗證，提高專案的可控性與長期維護能力。

## 技術與工具｜Tech Stack

| Category | Technology |
|---|---|
| Game Engine | Unity |
| Programming Language | C# |
| Game Genre | 3D Bullet Hell / Shooting Game |
| AI Coding Agent | Claude Code |
| AI Development Methodology | Vibe Coding |
| Agent Skills & Workflow | Superpowers |
| LLM Instruction Management | CLAUDE.md |
| Engine Integration | Unity MCP |
| Communication Protocol | Model Context Protocol (MCP) |
| Version Control | Git / GitHub |

## 專案結構｜Project Structure

- `Assets/` — 遊戲場景、程式腳本、角色模型、動畫及相關遊戲資源。
- `Packages/` — Unity 套件與相依性設定。
- `ProjectSettings/` — Unity 專案與引擎設定。
- `docs/superpowers/` — 結構化開發需求、設計及規劃相關文件。
- `.mcp.json` — MCP Server 連線與相關設定。
- `CLAUDE.md` — AI Agent 專案背景、開發規範及限制。

## 個人開發內容｜My Contributions

本專案為個人開發專案，主要負責：

- 遊戲核心玩法與戰鬥機制設計。
- 子彈屬性、彈幕吸收及資源利用機制規劃。
- 遊戲系統架構與模組化設計。
- Unity 遊戲介面與視覺呈現設計。
- AI 輔助開發環境建置。
- Claude Code 與 Superpowers 開發流程整合。
- Unity MCP 環境設定與應用。
- LLM 開發規範及限制條件制定。
- AI 生成程式碼的檢查、測試與功能驗證。

## 相關工具｜References

- [Unity](https://unity.com/)
- [Claude Code](https://code.claude.com/)
- [Superpowers](https://github.com/obra/superpowers)
- [Model Context Protocol](https://modelcontextprotocol.io/)

## 素材來源｜Asset Sources

本專案使用部分《蔚藍檔案》（Blue Archive）的原作遊戲素材，包含角色模型、貼圖及動畫等資源，作為同人遊戲開發、視覺呈現及技術研究用途。

相關素材透過以下第三方開源工具取得：

- [Blue Archive Asset Downloader](https://github.com/ZM-Kimu/Blue-Archive-Asset-Downloader) — 由 ZM-Kimu 開發的《蔚藍檔案》遊戲素材下載工具。

感謝原工具開發者及相關開源社群的技術貢獻。

## 免責聲明｜Disclaimer

Bullet Archive 為受《蔚藍檔案》（Blue Archive）啟發的非官方、非營利同人遊戲開發專案，主要用於個人遊戲開發學習、技術研究及作品展示。

本專案與《蔚藍檔案》的官方開發商及發行商無任何隸屬、合作或授權關係。

專案中所涉及的原作角色、模型、貼圖、動畫及其他相關素材，其智慧財產權均歸原權利人所有。

素材下載工具僅作為資源取得的技術途徑，其開源授權不代表原作遊戲素材亦具有相同的授權。本專案不主張對原作素材擁有任何權利。
