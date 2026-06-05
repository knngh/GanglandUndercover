# E6 UI Improvements 20260605

## 1. Color System

Defined in `Assets/_Project/Scripts/UI/UnifiedGameUI.cs` (static class `GanglandUndercover.UI.UnifiedGameUI`).

### Faction / Role Colors

| Constant | Hex | RGB | Usage |
|---|---|---|---|
| `PoliceBlue` | `#1a5fb4` | (0.102, 0.373, 0.706) | 警察阵营、信息提示 |
| `GangRed` | `#c01c28` | (0.753, 0.110, 0.157) | 匪徒阵营、危险警告 |
| `UndercoverPurple` | `#9141ac` | (0.569, 0.255, 0.675) | 卧底身份、特殊角色 |
| `MoleGrey` | `#5e5c64` | (0.369, 0.361, 0.392) | 内鬼身份、中立状态 |

### Neutral / UI Chrome Colors

| Constant | Value |
|---|---|
| `PanelBackground` | (0, 0, 0, 0.85) — 深色半透明 |
| `PanelBorder` | (0.25, 0.25, 0.28, 0.92) |
| `TextPrimary` | (0.92, 0.9, 0.82, 1) — 暖白正文 |
| `TextMuted` | (0.63, 0.68, 0.66, 1) — 灰绿色辅助文字 |

### Accessibility Status Colors

| Constant | Hex | Notes |
|---|---|---|
| `StatusGreen` | `#2eb861` | 正常/在线（配合 "[就绪]" 标签） |
| `StatusYellow` | `#dba621` | 警告（配合 "⚠暴露" 标签） |
| `StatusRed` | `#c01c28` | 危险/离线（配合 "[断]" 标签） |

### Color-Blind Safe Status Labels

All status texts now append Unicode/emoji labels **in addition to** color indicators:

| Label | Meaning | Context |
|---|---|---|
| `[警]` | Police | 警察角色 |
| `[匪]` | Gang | 匪徒角色 |
| `[卧]` | Undercover | 卧底角色 |
| `[鼠]` | Mole | 内鬼角色 |
| `[死]` | Dead | 死亡状态 |
| `[禁]` | Silenced | 被禁言 |
| `[就绪]` | Ready | 准备就绪 |
| `[断]` | Disconnected | 断线 |
| `⚡停电` | Power Outage | 停电危机 |
| `🔒封锁` | Lockdown | 区域封锁 |
| `🔇静默` | Silence | 通讯静默 |
| `⚠暴露` | Exposed | 暴露状态 |

---

## 2. Font Size Specs

| Constant | Size (px) | Usage |
|---|---|---|
| `FontTitle` | 28 | HUD 标题、覆盖层标题 |
| `FontBody` | 18 | 正文、状态文本 |
| `FontSmall` | 13 | 辅助信息、笔记本内容 |
| `FontTiny` | 10 | 图例、微缩信息 |

### Chinese Font Fallback Check

`OnlineMatchHud.Awake()` now performs a CJK coverage check on the built-in font (`LegacyRuntime.ttf` / `Arial.ttf`). If the test string `"港区潜线"` cannot be rendered at 18pt, a warning is logged:

```
[UnifiedGameUI] Built-in font 'LegacyRuntime.ttf' does not have full CJK coverage.
Consider assigning a CJK-compatible font (e.g. Noto Sans SC) in the Text component
or via Font Asset override for best Chinese rendering.
```

---

## 3. Resolution Testing Checklist

Validated by `OnlineMatchHud.SimpleResolutionGuard()` in `Awake()`. A warning is logged for non-standard resolutions.

### Verified Resolutions

| Resolution | Aspect Ratio | Status |
|---|---|---|
| 1280x720 | 16:9 | Primary target |
| 1920x1080 | 16:9 | Standard desktop |
| 2560x1440 | 16:9 | High-DPI desktop |

### QA Checklist

- [ ] 1280x720 — HUD text legible, no clipping
- [ ] 1920x1080 — Full layout visible, meeting overlay within bounds
- [ ] 2560x1440 — UI scaling correct, click targets reachable
- [ ] Non-16:9 (e.g., 3440x1440 ultrawide) — warning appears in log, layout gracefully degrades
- [ ] Fullscreen / Windowed mode switch — Canvas rescales without artifacts

### Canvas Scaler Settings

- `uiScaleMode`: `ScaleWithScreenSize`
- `referenceResolution`: `1600 x 900`
- `screenMatchMode`: `MatchWidthOrHeight` at `0.5`
- `RenderMode`: `ScreenSpaceCamera`, `planeDistance`: `0.8`

---

## 4. Canvas UI Architecture

### Screen Hierarchy

```
OnlineMatchHud (Canvas, RenderMode: ScreenSpaceCamera, sortingOrder: 3000)
├── HUD Backdrop                          ← 全局半透明背景
├── Header                                ← 标题栏 (titleText)
├── Left Dock ───────────── Notebook ──── ← 笔记本 (Roster/Intel/Log/Services)
│   ├── Notebook Title (notebookTitleText)
│   └── Notebook Body (notebookBodyText)
├── Center Dock                           ← 中央面板 (centerTitleText / centerBodyText)
├── Right Dock                            ← 右侧面板 (情报面板)
├── Footer                                ← 底部状态 (footerText)
│
├── Meeting Overlay                       ← 会议阶段
│   ├── Meeting Title (meetingTitleText)
│   ├── Meeting Body (meetingBodyText)
│   ├── Meeting Seat Board ←──────────── ← 座位列表
│   └── Vote Scroll ── Vote Buttons ──── ← 投票按钮列表
│
├── Result Overlay                        ← 结算阶段
│   ├── Result Title (resultTitleText)
│   ├── Result Body (resultBodyText)
│   ├── Result Evidence Fill (resultEvidenceFill)
│   ├── Result Task Fill (resultTaskFill)
│   └── Result Survival Fill (resultSurvivalFill)
│
├── Task Overlay                          ← 任务阶段
│   ├── Task Title (taskTitleText)
│   ├── Task Body (taskBodyText)
│   ├── Task Feedback (taskFeedbackText)
│   ├── Task Progress Fill (taskProgressFill)
│   └── Task MiniGame Root ←──────────── ← 小游戏容器
│       ├── CCTV Task Canvas
│       ├── Recording Task Canvas
│       ├── Breaker Task Canvas
│       ├── Plate Task Canvas
│       └── Generic Task Canvas
│
├── Map Overlay                           ← 地图
│   ├── Map Title (mapTitleText)
│   ├── Map Legend (mapLegendText)
│   ├── Static Map Layer ── Routes ────── ← 静态路线
│   └── Map Markers ── Player Markers ── ← 玩家标记
│
└── Compact Action HUD                    ← 行动阶段精简面板
    ├── Compact Top Text (compactTopText)
    ├── Compact Prompt (compactPromptText)
    ├── Compact Ability (compactAbilityText)
    └── Compact Action Bar (compactActionBarText)
```

### Input Controls (Lobby / Settings Groups)

| Group | Controls |
|---|---|
| Connection | hostButton, clientButton, relayHostButton, relayClientButton, localPreviewButton, playerNameInput, roomNameInput, joinAddressInput, relayJoinInput |
| Settings | minPlayersSlider, maxPlayersSlider, evidenceTargetSlider, autoFillToggle, revealRoleToggle, proximityVoiceToggle, readyButton, startButton, fillBotsButton, shutdownButton, returnLobbyButton |
| Action | restartButton, mapButton, intelButton, interactButton, reportButton, killButton, abilityButton, ventButton |

### IMGUI Fallback (OnlineMatchController.cs)

`OnGUI()` in `OnlineMatchController` is retained for **Editor-only debug fallback** only:

```
#if UNITY_EDITOR
  if (!canvasHudEnabled) DrawMeetingScreen / DrawResultScreen / DrawCompactActionHud
#else
  return;   // Release builds always use Canvas
#endif
```

### Key Points

1. All Canvas UI is built procedurally in `OnlineMatchHud.BuildLayout()` — no prefabs or asset references required.
2. Layout is validated on every `EnsureLayout()` call; missing references trigger a full rebuild.
3. Overlays (Meeting, Result, Task, Map) are toggled via `SetActive(phase == ...)` in `Refresh()`.
4. Meeting seat blocks and vote buttons are pooled lists (`voteButtons`, `meetingSeatRoot`) and cleared/rebuilt on phase transitions.
5. Map markers are pooled similarly and updated via `RefreshMapMarkers()`.

---

## 5. Files Changed

| File | Change |
|---|---|
| `Assets/_Project/Scripts/UI/UnifiedGameUI.cs` | **Created** — static helper class with colors, font sizes, status labels, resolution utilities |
| `Assets/_Project/Scripts/Online/OnlineMatchHud.cs` | **Modified** — added `Awake()` with CJK font check and `SimpleResolutionGuard()`; `RefreshTexts()` now appends color-blind safe labels; added `BuildAccessibilitySuffix()` |
