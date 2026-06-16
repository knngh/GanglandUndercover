# Phase 2 -- UI/UX and Copy Unification Audit

> Project: Gangland Undercover /港区潜线
> Date: 2026-06-16
> Scope: MainMenuController, LobbyController, PrototypeHud, OnlineMatchHud, OnlineMatchController.OnGUI, SocialPrototypeHud, GameOverController, Localization, SettingsManager/SettingsData, UnifiedGameUI, ThemeManager

---

## 0. Executive Summary

The project currently has **three parallel UI systems** coexisting:

| System | Files | Status |
|--------|-------|--------|
| **uGUI (Canvas)** | `MainMenuController`, `LobbyController`, `PrototypeHud`, `GameOverController`, `OnlineMatchHud`, `SocialPrototypeHud` | Primary path for release |
| **IMGUI (OnGUI)** | `OnlineMatchController.OnGUI.cs` (~300 calls), `OnlineMatchController.Gameplay.cs` (~78 calls) | Editor-only fallback since M7.3; stripped in release builds |
| **Localization.cs** | 174 CN keys + 168 EN keys | Partially integrated -- many UI controllers still hardcode strings instead of calling `Localization.T()` |

The `OnlineMatchHud.cs` (Canvas) has been the main production HUD since M7.3, with OnGUI gated behind `#if UNITY_EDITOR`. Most hardcoded Chinese strings live in `OnlineMatchHud.cs` (~60 labels), `OnlineMatchController.OnGUI.cs` (~80 labels), and `MainMenuController.cs` (~40 labels). The `Localization.cs` dictionary covers meeting, map, task, action, and result terminology well but is **not yet wired into** the main menu, lobby, settings overlay, or game-over screens.

**Theme inconsistency**: `MainMenuController` comments still say "Among Us 太空主题 v2" (space theme) and references "深空背景" (deep-space background) and "StarfieldParticles", which clash with the Kowloon harbour undercover-cop setting.

---

## 1. Chinese / English UI Copy Table

Strings are grouped by screen. "Source" indicates which file the string was extracted from.

### 1.1 Main Menu (`MainMenuController.cs`)

| # | Control | Current Text (CN) | Suggested CN | Suggested EN | Notes |
|---|---------|-------------------|--------------|--------------|-------|
| 1 | Title | 港 区 潜 线 | 港区潜线 | Gangland Undercover | OK |
| 2 | Subtitle | Gangland Undercover | Gangland Undercover | Gangland Undercover | OK |
| 3 | Tagline | 社交推理 \| 九龙港区 \| 警匪卧底 \| 4人局 | 社交推理 \| 九龙港区 \| 警匪卧底 \| 4人局 | Social Deduction \| Kowloon Port \| Undercover Cop \| 4-Player | OK |
| 4 | Offline panel header | 离 线 模 式 | 离线模式 | Offline Mode | OK |
| 5 | Offline panel sub | 单机体验 . 4人对战 | 单机体验 . 4人对战 | Solo Play . 4-Player Match | OK |
| 6 | Section header | -- 选 择 身 份 -- | -- 选择身份 -- | -- Choose Your Role -- | OK |
| 7 | Role card 0 | 卧底 / Undercover | 卧底 | Undercover | OK |
| 8 | Role card 1 | 黑帮 / Gang | 黑帮 | Gang | OK |
| 9 | Role card 2 | 警察 / Police | 警察 | Police | OK |
| 10 | Role card 3 | 线人 / Mole | 线人 | Mole | OK |
| 11 | Role desc 0 | 潜伏黑帮内部,窃取证据提交警方专案组 | (keep) | Infiltrate the gang, steal evidence, and deliver it to the police task force | OK |
| 12 | Role desc 1 | 掌控九龙港区,阻止证据链闭合暴露身份 | (keep) | Control Kowloon Port, prevent the evidence chain from closing, and protect your identity | OK |
| 13 | Role desc 2 | 带队收网清剿黑帮,尽快完成证据链锁定 | (keep) | Lead the raid to dismantle the gang; complete the evidence chain ASAP | OK |
| 14 | Role desc 3 | 混入警方技侦部门,暗中收集卧底活动情报 | (keep) | Embed yourself in police tech-intel; secretly gather intelligence on undercover operations | OK |
| 15 | Section header | -- 选 择 地 图 -- | -- 选择地图 -- | -- Choose Map -- | OK |
| 16 | Map button 0 | 九龙港区 | 九龙港区 | Kowloon Port District | OK |
| 17 | Map button 1 | 警察局 | 警察局 | Police Station | OK |
| 18 | Map desc 0 | 夜幕下的九龙港区:货柜码头、夜市巷、地下诊所...黑帮与警察的暗战之地 | (keep) | Kowloon Port at night: container docks, night-market alleys, underground clinics... the shadow war between gangs and police | OK |
| 19 | Map desc 1 | 警方总部大楼:大厅、审讯室、证物室、武器库...暗流涌动的警局内部 | (keep) | Police HQ: lobby, interrogation rooms, evidence lockup, armory... undercurrents inside the station | OK |
| 20 | Start button | 开 始 游 戏 | 开始游戏 | Start Game | OK |
| 21 | Online panel header | 登 录 / 联 机 | 登录 / 联机 | Login / Online | OK |
| 22 | Online panel sub | 匿名登录 . Lobby / Relay / Sessions | 匿名登录 . Lobby / Relay / Sessions | Anonymous Login . Lobby / Relay / Sessions | OK |
| 23 | Online info | 使用 Unity 匿名登录进入大厅\n房间列表、Relay 房间码和文本聊天会沿用这个玩家代号 | (keep) | Sign in anonymously to enter the lobby.\nRoom list, Relay codes, and text chat will use this player name | OK |
| 24 | Name label | 玩家代号 | 玩家代号 | Player Callsign | OK |
| 25 | Placeholder | 港区玩家 | 港区玩家 | Port Player | OK |
| 26 | Login button | 匿 名 登 录 | 匿名登录 | Anonymous Login | OK |
| 27 | Enter lobby button | 进 入 大 厅 | 进入大厅 | Enter Lobby | OK |
| 28 | Tutorial button | 教 程 回 顾 | 教程回顾 | Replay Tutorial | OK |
| 29 | Section header | -- 设 置 中 心 -- | -- 设置中心 -- | -- Settings -- | OK |
| 30 | Settings button | 打开设置 | 打开设置 | Open Settings | OK |
| 31 | Version line | v0.8 . Gangland Undercover . Among Us Inspired | v0.8 . Gangland Undercover | v0.8 . Gangland Undercover | **Remove "Among Us Inspired"** -- not on-theme for shipping; see 1.8 |
| 32 | Login status | 登录: 匿名账号将在进入大厅后初始化\nCloud/Auth/Lobby/Relay 状态会在联机 HUD 中继续显示 | (keep) | Login: Anonymous account will initialize upon lobby entry.\nCloud/Auth/Lobby/Relay status continues in the online HUD | OK |
| 33 | Login status | 匿名账号已就绪 / 等待匿名登录 | (keep) | Anonymous Account Ready / Waiting for Login | OK |
| 34 | Settings status | 音量 X% \| 画质 高 \| 全屏 \| 帧率 60 FPS \| VSync 开 \| 聊天 按键发送 \| 色盲 0 | (keep) | Volume X% \| Quality High \| Fullscreen \| 60 FPS \| VSync On \| Chat Push-to-Send \| Colorblind 0 | OK -- generated dynamically |

### 1.2 Settings Overlay (`MainMenuController.BuildSettingsOverlay`)

| # | Control | Current Text (CN) | Suggested CN | Suggested EN | Notes |
|---|---------|-------------------|--------------|--------------|-------|
| 35 | Header | 设 置 中 心 | 设置中心 | Settings | OK |
| 36 | Sub header | 音频 . 画面 . 游戏 . 辅助功能 | (keep) | Audio . Display . Gameplay . Accessibility | OK |
| 37 | Slider | 主音量 | 主音量 | Master Volume | OK |
| 38 | Slider | 音效 | 音效 | SFX Volume | OK |
| 39 | Slider | 音乐 | 音乐 | BGM Volume | OK |
| 40 | Slider | 聊天音量 | 聊天音量 | Chat Volume | OK |
| 41 | Slider | 鼠标灵敏度 | 鼠标灵敏度 | Mouse Sensitivity | OK |
| 42 | Button | 画质 | 画质 | Quality | OK |
| 43 | Button | 窗口 | 窗口 | Window Mode | OK |
| 44 | Button | 色盲 | 色盲模式 | Colorblind Mode | "色盲" alone is slightly abrupt; suggest "色盲模式" |
| 45 | Button | 帧率 | 帧率 | Frame Rate | OK |
| 46 | Button | 垂直同步 | 垂直同步 | VSync | OK |
| 47 | Button | 自由发送 | 自由发送 | Free Send | **Outdated**: voice chat was removed; this now controls text chat send mode. Suggest "聊天模式" / "Chat Mode" |
| 48 | Button | 重置设置 | 重置设置 | Reset Settings | OK |
| 49 | Button | 关闭 | 关闭 | Close | OK |
| 50 | Hint | 设置会立即保存并应用;联机语音已改为文本聊天,聊天音量用于后续提示音与文本聊天反馈。 | (keep) | Settings are saved and applied instantly. Voice chat has been replaced by text chat; chat volume covers notification sounds. | OK |

### 1.3 Lobby (`LobbyController.cs`)

| # | Control | Current Text (CN) | Suggested CN | Suggested EN | Notes |
|---|---------|-------------------|--------------|--------------|-------|
| 51 | Title | 联 机 大 厅 | 联机大厅 | Online Lobby | OK |
| 52 | Room code label | 房间码 | 房间码 | Room Code | OK |
| 53 | Input placeholder | 输入 4~6 位房间码 | (keep) | Enter 4-6 digit room code | OK |
| 54 | Copy button | (clipboard emoji) | 复制 | Copy | Emoji-only button -- should have tooltip/accessibility label |
| 55 | Create button | 创 建 房 间 | 创建房间 | Create Room | OK |
| 56 | Join button | 加 入 房 间 | 加入房间 | Join Room | OK |
| 57 | Status default | 输入房间码加入,或创建新房间 | (keep) | Enter a room code to join, or create a new room | OK |
| 58 | Player list header | -- 玩 家 列 表 -- | -- 玩家列表 -- | -- Player List -- | OK |
| 59 | Player entry | (house emoji) 房主 / (person emoji) 玩家 N | (keep) | (house) Host / (person) Player N | Emoji usage OK but should add text fallback |
| 60 | Player entry empty | 等待加入... | 等待加入... | Waiting... | OK |
| 61 | Start button | 开 始 游 戏 | 开始游戏 | Start Game | OK |
| 62 | Ready button | 准 备 / 取消准备 | 准备 / 取消准备 | Ready / Cancel Ready | OK |
| 63 | Leave button | 离 开 房 间 | 离开房间 | Leave Room | OK |
| 64 | Leave confirm | 确定离开?对局进度将丢失。 | (keep) | Are you sure? Match progress will be lost. | OK |
| 65 | Confirm leave | 确 定 离 开 | 确定离开 | Confirm Leave | OK |
| 66 | Cancel leave | 取 消 | 取消 | Cancel | OK |
| 67 | Back button | 返 回 主 菜 单 | 返回主菜单 | Back to Main Menu | OK |
| 68 | Status: creating | 正在通过 Relay 创建房间... | (keep) | Creating room via Relay... | OK |
| 69 | Status: joining | 正在加入房间: XXXX | (keep) | Joining room: XXXX | OK |
| 70 | Status: error | 控制器未就绪,请重启。 | (keep) | Controller not ready. Please restart. | OK |
| 71 | Status: code copied | 房间码已复制: XXXX | (keep) | Room code copied: XXXX | OK |
| 72 | Status: no code | 暂无房间码,请先创建房间 | (keep) | No room code yet. Please create a room first. | OK |
| 73 | Status: ready | 已准备,等待房主开始... | (keep) | Ready. Waiting for host to start... | OK |
| 74 | Status: not ready | 已取消准备 | (keep) | Ready cancelled | OK |

### 1.4 HUD -- PrototypeHud (Offline / Single-player)

| # | Control | Current Text (CN) | Suggested CN | Suggested EN | Notes |
|---|---------|-------------------|--------------|--------------|-------|
| 75 | Task list title | 任务列表 | 任务列表 | Task List | OK |
| 76 | Info panel title | 案件板 | 案件板 | Case Board | OK |
| 77 | Day counter | 第 X 天 | 第 X 天 | Day X | OK |
| 78 | Evidence label | 证据 | 证据 | Evidence | OK |
| 79 | Intel label | 情报 | 情报 | Intel | OK |
| 80 | Action button | 通 风 管 | 通风管 | Vent | OK |
| 81 | Action button | 击 杀 | 击杀 | Kill | OK |
| 82 | Action button | 破 坏 | 破坏 | Sabotage | OK |
| 83 | Action button | 报 告 | 报告 | Report | OK |
| 84 | Action button | 会 议 | 会议 | Meeting | OK |
| 85 | Action button | 热 度 榜 | 热度榜 | Heat Board | OK -- thematic for gangland |
| 86 | Task item | 搜集证据 | 搜集证据 | Gather Evidence | OK |
| 87 | Task item | 降低嫌疑 | 降低嫌疑 | Lower Suspicion | OK |
| 88 | Task item | 控制区域 | 控制区域 | Control Districts | OK |
| 89 | Task item | 线人情报 | 线人情报 | Mole Intel | OK |
| 90 | Info panel text | 阵营控制:黑帮 X / 警察 Y / 争议 Z | (keep) | Faction Control: Gang X / Police Y / Contested Z | OK |
| 91 | Info panel text | 警力热度 / 货运进度 / 卧底掩护 / 嫌疑程度 | (keep) | Police Heat / Shipment / Cover / Suspicion | OK |
| 92 | Phase label | 选择身份 / 你的回合 / 对手行动中... / 会议投票 / 游戏结束 | (keep) | Role Select / Your Turn / Opponent Acting... / Meeting Vote / Game Over | OK |
| 93 | Role display | 黑帮 / 卧底 / 警察 / 线人 | (keep) | Gang / Undercover / Police / Mole | OK |
| 94 | Faction label | 黑帮阵营 / 卧底阵营 / 警察阵营 / 线人阵营 / 中立 | (keep) | Gang Faction / Undercover Faction / Police Faction / Mole Faction / Neutral | OK |

### 1.5 HUD -- OnlineMatchHud (Online Canvas HUD)

| # | Control | Current Text (CN) | Suggested CN | Suggested EN | Notes |
|---|---------|-------------------|--------------|--------------|-------|
| 95 | Section header | 连接与开局 | 连接与开局 | Connection & Setup | Hardcoded -- should use Localization |
| 96 | Input label | 玩家 | 玩家 | Player | Hardcoded |
| 97 | Input default | 港区玩家 | 港区玩家 | Port Player | Hardcoded |
| 98 | Input label | 房间 | 房间 | Room | Hardcoded |
| 99 | Input default | 九龙港区夜局 | 九龙港区夜局 | Kowloon Night Session | Hardcoded |
| 100 | Input label | 直连 | 直连地址 | Direct Connect | Hardcoded |
| 101 | Input label | 房间码 | 房间码 | Room Code | Hardcoded |
| 102 | Button | 创建 Host | 创建主机 | Create Host | Mixed CN/EN -- "Host" untranslated |
| 103 | Button | 加入 Client | 加入客户端 | Join Client | Mixed CN/EN -- "Client" untranslated |
| 104 | Button | Relay 开房 | Relay 开房 | Relay Host | Mixed |
| 105 | Button | Relay 加入 | Relay 加入 | Relay Join | Mixed |
| 106 | Button | 本地完整局 | 本地完整局 | Local Preview | Hardcoded |
| 107 | Button | 离开房间 | 离开房间 | Leave Room | Hardcoded |
| 108 | Section header | 房间规则 | 房间规则 | Room Rules | Hardcoded |
| 109 | Slider label | 最少人数 | 最少人数 | Min Players | Hardcoded |
| 110 | Slider label | 最大人数 | 最大人数 | Max Players | Hardcoded |
| 111 | Slider label | 证据目标 | 证据目标 | Evidence Target | Hardcoded |
| 112 | Toggle | 人数不足时 AI 补位 | (keep) | Auto-fill with AI | Hardcoded |
| 113 | Toggle | 出局时公开身份 | (keep) | Reveal Role on Eject | Hardcoded |
| 114 | Toggle | 行动阶段近距离聊天 | (keep) | Proximity Chat (Action Phase) | Hardcoded -- corrected from "语音" to "聊天" |
| 115 | Section header | 房间流程 | 房间流程 | Room Flow | Hardcoded |
| 116 | Button | Ready | 准备 | Ready | Hardcoded EN in CN build |
| 117 | Button | 取消 Ready | 取消准备 | Cancel Ready | Mixed CN/EN |
| 118 | Button | 开始 | 开始 | Start | Hardcoded |
| 119 | Button | 补 AI 开局 | 补AI开局 | Fill Bots & Start | Hardcoded |
| 120 | Section header | 行动快捷 | 行动快捷 | Quick Actions | Hardcoded |
| 121 | Button | E 互动 | E 互动 | E Interact | Hardcoded |
| 122 | Button | R 报案 | R 报案 | R Report | Hardcoded |
| 123 | Button | Q 击倒 | Q 击倒 | Q Kill | Hardcoded |
| 124 | Button | F 技能 | F 技能 | F Ability | Hardcoded |
| 125 | Button | V 通风管 | V 通风管 | V Vent | Hardcoded |
| 126 | Button | M 大地图 | M 大地图 | M Map | Hardcoded |
| 127 | Button | I 案情板 | I 案情板 | I Intel Board | Hardcoded |
| 128 | Section header | 结算控制 | 结算控制 | Result Controls | Hardcoded |
| 129 | Button | 重开 | 重开 | Restart | Hardcoded |
| 130 | Button | 返回房间 | 返回房间 | Return to Lobby | Hardcoded |
| 131 | Section header | 当前对局 | 当前对局 | Current Match | Hardcoded |
| 132 | Section header | 进度 | 进度 | Progress | Hardcoded |
| 133 | Progress bar | 证据链 | 证据链 | Evidence Chain | Hardcoded |
| 134 | Progress bar | 任务完成 | 任务完成 | Tasks Done | Hardcoded |
| 135 | Progress bar | 存活人数 | 存活人数 | Survivors | Hardcoded |
| 136 | Section header | 目标与局势 | 目标与局势 | Objectives & Situation | Hardcoded |
| 137 | Notebook tab | 人员 | 人员 | Roster | Hardcoded |
| 138 | Notebook tab | 案情 | 案情 | Intel | Hardcoded |
| 139 | Notebook tab | 日志 | 日志 | Log | Hardcoded |
| 140 | Notebook tab | 服务 | 服务 | Services | Hardcoded |
| 141 | Section header | 情报板 | 情报板 | Intel Board | Hardcoded |
| 142 | Section header | 情报内容 | 情报内容 | Intel Content | Hardcoded |
| 143 | Section header | 文本聊天 | 文本聊天 | Text Chat | Hardcoded |
| 144 | Input label | 发言 | 发言 | Message | Hardcoded |
| 145 | Input placeholder | 输入消息 | 输入消息 | Type a message | Hardcoded |
| 146 | Button | 发送 | 发送 | Send | Hardcoded |
| 147 | Button | 举报最近 | 举报最近 | Report Latest | Hardcoded |
| 148 | Button | 屏蔽最近 | 屏蔽最近 | Block Latest | Hardcoded |
| 149 | Meeting chip | 会议原因 / 证据墙 / 票型 / 语音 | (keep) | Reason / Evidence / Tally / Voice | Hardcoded |
| 150 | Meeting header | 证据板 | 证据板 | Evidence Board | Hardcoded |
| 151 | Meeting header | 投票面板 | 投票面板 | Vote Panel | Hardcoded |
| 152 | Meeting seat | 会议圆桌 | 会议圆桌 | Round Table | Hardcoded |
| 153 | Meeting seat | 证据墙 | 证据墙 | Evidence Wall | Hardcoded |
| 154 | Meeting seat | 语音全员 | 语音全员 | Voice All | Hardcoded |
| 155 | Vote button | 跳过投票 | 跳过投票 | Skip Vote | Hardcoded |
| 156 | Vote subtitle | 保留疑点, 等待下一轮证据 | (keep) | Reserve doubt; wait for next round of evidence | Hardcoded |
| 157 | Task header chip | 现场终端 / 三步校验 / 扫描确认 | (keep) | Field Terminal / 3-Step Verify / Scan Confirm | Hardcoded |
| 158 | Task progress | 现场进度 | 现场进度 | Field Progress | Hardcoded |
| 159 | Task button | 键 1 / 键 2 / 键 3 | 键 1 / 键 2 / 键 3 | Key 1 / Key 2 / Key 3 | Hardcoded |
| 160 | Task button | 按住扫描 | 按住扫描 | Hold to Scan | Hardcoded |
| 161 | Task button | 退出 | 退出 | Exit | Hardcoded |
| 162 | Map legend | 黄: 玩家 \| 青: 任务 \| 橙框: 垂直切片核心区 \| 红: 破坏/尸体 \| 灰: 出局 \| M 收起 | (keep) | Yellow: Player \| Cyan: Task \| Orange: Core Area \| Red: Sabotage/Corpse \| Gray: Out \| M to close | Hardcoded |
| 163 | Map button | 收起大地图 | 收起大地图 | Close Map | Hardcoded |
| 164 | Result progress | 证据链 / 任务完成 / 存活人数 | (keep) | Evidence Chain / Tasks Done / Survivors | Hardcoded |
| 165 | Result button | 重开同房间 | 重开同房间 | Replay Same Room | Hardcoded |
| 166 | Result button | 返回房间 | 返回房间 | Return to Lobby | Hardcoded |
| 167 | Title bar | 港区潜线 \| {roomName} \| Relay {code} | (keep) | Gangland Undercover \| {roomName} \| Relay {code} | Dynamic |
| 168 | Compact action | E 互动 / Q 击倒 Xs / R 报案 / F 技能 Xs / M 地图 / I 案情 | (keep) | E Interact / Q Kill Xs / R Report / F Ability Xs / M Map / I Intel | Dynamic |
| 169 | Footer | (uses Localization.T("hud.compact.hint")) | -- | -- | Already localized |

### 1.6 Meeting / Vote (OnGUI fallback + Canvas)

| # | Control | Current Text (CN) | Suggested CN | Suggested EN | Notes |
|---|---------|-------------------|--------------|--------------|-------|
| 170 | Meeting title (Loc.) | 九龙港城会议 | (keep) | Kowloon Port Meeting | Localization key `meeting.title` -- CN key present |
| 171 | Discuss label | 讨论 | (keep) | Discussion | `meeting.discuss` |
| 172 | Vote label | 投票 | (keep) | Voting | `meeting.vote` |
| 173 | Reason label | 会议原因: | (keep) | Reason: | `meeting.reason` |
| 174 | Evidence label | 证据墙: | (keep) | Evidence: | `meeting.evidence` |
| 175 | Tally label | 票型: | (keep) | Vote tally: | `meeting.tally` |
| 176 | Outcome label | 上轮结论: | (keep) | Last outcome: | `meeting.outcome` |
| 177 | Skip vote | 跳过投票 | (keep) | Skip Vote | `meeting.vote.skip` |
| 178 | Vote panel (OnGUI) | 投票面板 | (keep) | Vote Panel | `meeting.vote.panel` |
| 179 | OnGUI meeting | 讨论 / 对照证据 / 准备投票 | (keep) | Discuss / Review evidence / Prepare to vote | Phase roadmap |

### 1.7 Task / Mini-Games

| # | Control | Current Text (CN) | Suggested CN | Suggested EN | Notes |
|---|---------|-------------------|--------------|--------------|-------|
| 180 | Task panel title | 现场任务 \| {name} | (keep) | Field Task \| {name} | Dynamic |
| 181 | Progress text | 证据价值 +X \| 错误 X/3 | (keep) | Evidence Value +X \| Mistakes X/3 | Dynamic |
| 182 | Feedback pass | 校验通过 | (keep) | Verified | `minigame.feedback.pass` |
| 183 | Feedback fail | 输入不匹配 | (keep) | Input mismatch | `minigame.feedback.fail` |
| 184 | Feedback hint | 按顺序校验,再按住扫描推进现场结果 | (keep) | Follow the sequence, then hold scan to submit | `minigame.feedback.hint` |
| 185 | Task types (OnGUI) | 监控追踪、封条查验、电力修复、证物扫描、账本冻结、路线巡查 | (keep) | Surveillance, Seal Check, Power Repair, Evidence Scan, Ledger Freeze, Route Patrol | Hardcoded in OnGUI |
| 186 | Task step labels | 已校验 / 下一步 / 等待 | (keep) | Verified / Next / Waiting | OnGUI sequence rail |
| 187 | Task step button | 完成 键 X / 执行 键 X / 待命 键 X | (keep) | Done Key X / Execute Key X / Standby Key X | OnGUI |
| 188 | OnGUI scan | 按住 Space 扫描/同步, Esc 退出 | (keep) | Hold Space to scan/sync, Esc to exit | Instruction line |

### 1.8 Result / Game Over

| # | Control | Current Text (CN) | Suggested CN | Suggested EN | Notes |
|---|---------|-------------------|--------------|--------------|-------|
| 189 | Result title (Loc.) | 行动结算 | (keep) | Match Result | `result.title` |
| 190 | GameOver: Gang wins | 黑 帮 胜 利 | 黑帮胜利 | Gang Wins | `GameOverController` |
| 191 | GameOver: UC wins | 卧 底 胜 利 | 卧底胜利 | Undercover Wins | `GameOverController` |
| 192 | GameOver: Police wins | 警 察 胜 利 | 警察胜利 | Police Wins | `GameOverController` |
| 193 | GameOver: Draw | 游 戏 结 束 | 游戏结束 | Game Over | `GameOverController` |
| 194 | Card status | WIN / LOSE | 胜利 / 失败 | WIN / LOSE | Could localize |
| 195 | Card alive | 存活 / 已出局 | (keep) | Alive / Eliminated | `GameOverController` |
| 196 | Card summary | 击杀 X / 情报 X / 任务 X/Y | (keep) | Kills X / Intel X / Tasks X/Y | Dynamic |
| 197 | Stats title | 统 计 数 据 | 统计数据 | Statistics | `GameOverController` |
| 198 | Timeline title | 淘汰记录 | 淘汰记录 | Elimination Record | `GameOverController` |
| 199 | Timeline label | 被淘汰 | 被淘汰 | Eliminated | `GameOverController` |
| 200 | Skip button | X秒后返回主菜单(点击跳过) | (keep) | Return to menu in Xs (click to skip) | Dynamic countdown |
| 201 | Result button | 重开同房间 | (keep) | Replay Same Room | `result.resume` |
| 202 | Result button | 返回房间 | (keep) | Return to Lobby | `result.return_lobby` |
| 203 | Result summary (OnGUI) | 用时 X:XX \| 存活 X/Y \| 完成任务 X/Y \| 破坏残留 X \| 尸体 X | (keep) | Time X:XX \| Survivors X/Y \| Tasks X/Y \| Sabotaged X \| Bodies X | Dynamic |

### 1.9 SocialPrototypeHud (Offline Prototype)

| # | Control | Current Text (CN) | Suggested CN | Suggested EN | Notes |
|---|---------|-------------------|--------------|--------------|-------|
| 204 | Title | 港区潜线 | (keep) | Harbor Undercover | Uses `T()` for CN/EN |
| 205 | Role buttons | 警察 / 卧底 / 黑帮 / 线人 | (keep) | Police / Undercover / Gang / Mole | Already localized via `T()` |
| 206 | Game info | WASD 移动 \| E 交互 \| R 报告 \| Q 击倒 | (keep) | WASD Move \| E Interact \| R Report \| Q Kill | Uses `T()` |
| 207 | Side panel | 任务清单 / 案件板 / 嫌疑榜 / 存活名单 / 玩法 | (keep) | Tasks / Case Board / Suspects / Roster / Loop | Uses `T()` |
| 208 | Meeting | 会议 / 跳过投票 / 自动投票 | (keep) | Meeting / Skip Vote / Auto Vote | Uses `T()` |
| 209 | Modal | 身份揭示 / 开始行动 / 结算 | (keep) | Role Reveal / Start Run / Result | Uses `T()` |
| 210 | Language toggle | Language: English / 语言:中文 | (keep) | (same) | Uses `T()` |

### 1.10 OnGUI-Specific Strings (Editor fallback only)

| # | Control | Current Text (CN) | Suggested CN | Suggested EN | Notes |
|---|---------|-------------------|--------------|--------------|-------|
| 211 | Title bar | 港区潜线 Release Candidate | (keep) | Gangland Undercover Release Candidate | OnGUI editor only |
| 212 | Phase line | 阶段: X \| 局时: X:XX/20:00 \| 证据链: X/Y \| 危机: Z | (keep) | Phase: X \| Time: X:XX/20:00 \| Evidence: X/Y \| Hazard: Z | Dynamic |
| 213 | Identity | 本机身份: X \| 职责: Y | (keep) | Local Role: X \| Profession: Y | Dynamic |
| 214 | Button | 创建 Host | 创建主机 | Create Host | Mixed CN/EN |
| 215 | Button | 单机试玩局 | 单机试玩局 | Local Preview | OnGUI |
| 216 | Button | 加入 Client | 加入客户端 | Join Client | Mixed CN/EN |
| 217 | Button | 开始在线局 | 开始在线局 | Start Online Match | OnGUI |
| 218 | Button | 补 AI 并开始本地可玩局 | 补AI并开始本地可玩局 | Fill Bots & Start Local Match | OnGUI |
| 219 | Button | 跳过简报进入行动 | 跳过简报进入行动 | Skip Briefing, Enter Action | OnGUI |
| 220 | Button | 重开同房间 | (keep) | Replay Same Room | OnGUI |
| 221 | Button | 返回房间 | (keep) | Return to Lobby | OnGUI |
| 222 | Button | 离开房间 | (keep) | Leave Room | OnGUI |
| 223 | Operations hint | 操作: WASD 移动 \| E 查证/破坏 \| Q 击倒 \| R 报案/紧急会议 \| F 技能 \| M/Tab 大地图 \| I 案情板 | (keep) | Controls: WASD Move \| E Inspect/Sabotage \| Q Kill \| R Report/Emergency \| F Ability \| M/Tab Map \| I Intel | OnGUI |
| 224 | Objective | 目标: 警方完成证据链或清除黑帮;黑帮破坏、击倒并争取人数压制;卧底加速取证但要隐藏路线。 | (keep) | Objective: Police complete evidence chain or eliminate gang; Gang sabotages, kills, and seeks majority; Undercover accelerates evidence but must hide routes. | OnGUI |
| 225 | Map title | 小地图 / 案情板 | (keep) | Minimap / Intel Board | OnGUI |
| 226 | Map label | 港区小地图 | (keep) | Port Minimap | OnGUI |
| 227 | Map large | 九龙港区封控全图 \| M/Tab 收起 | (keep) | Kowloon Port Full Map \| M/Tab to close | OnGUI |
| 228 | Map legend | 黄点 玩家 \| 青点 任务 \| 红点 被破坏/尸体 \| 紫点 暗线 \| 蓝色区域 警方据点 \| 棕红区域 黑帮高风险区 | (keep) | Yellow: Player \| Cyan: Task \| Red: Sabotage/Corpse \| Purple: Underworld \| Blue: Police Stronghold \| Brown-Red: Gang High-Risk | OnGUI |
| 229 | Hazard terms | 黑灯 / 封锁 / 断讯 / 泄证 / 巡逻 | (keep) | Blackout / Lockdown / Comms Jam / Evidence Leak / Patrol | OnGUI |
| 230 | Match pressure | 警方领先 / 黑帮逼近人数优势 / 局势胶着 \| 高压 / 时间压力 / 可控 | (keep) | Police Leading / Gang Approaching Majority / Deadlocked \| High Pressure / Time Pressure / Under Control | OnGUI |
| 231 | Case log header | 案情记录 | (keep) | Case Log | OnGUI |
| 232 | Task list header | 港区任务 \| 调查组推进任务,黑帮可伪装靠近并破坏 | (keep) | Port Tasks \| Investigators push tasks; gang can infiltrate and sabotage | OnGUI |
| 233 | Opening briefing | 专案简报 | 专案简报 | Task Force Briefing | OnGUI |
| 234 | Opening route cards | 货柜码头 / 监控中心 / 夜市情报 / 洗钱账房 / 证物冷库 | (keep) | Container Docks / Surveillance Center / Night Market Intel / Money Laundering Office / Evidence Cold Storage | OnGUI |
| 235 | Skill meter | 技能 \| {profession} | (keep) | Ability \| {profession} | OnGUI |
| 236 | Skill status | F 可用 / 冷却 Xs | (keep) | F Ready / Cooldown Xs | OnGUI |

### 1.11 Chat Safety (`OnlineMatchController.OnGUI.cs`)

| # | Control | Current Text (CN) | Suggested CN | Suggested EN | Notes |
|---|---------|-------------------|--------------|--------------|-------|
| 237 | Status | 聊天安全: 最近消息可举报/屏蔽 \| 屏蔽 X \| 举报 X | (keep) | Chat Safety: Report/block recent messages \| Blocked X \| Reported X | Dynamic |
| 238 | Status | 聊天安全未连接 | (keep) | Chat safety not connected | |
| 239 | Status | 已记录最近聊天消息举报。 | (keep) | Latest chat message reported. | |
| 240 | Status | 暂无可举报的聊天消息。 | (keep) | No chat messages to report. | |
| 241 | Status | 已屏蔽最近聊天发送者。 | (keep) | Latest chat sender blocked. | |
| 242 | Status | 暂无可屏蔽的聊天发送者。 | (keep) | No chat senders to block. | |
| 243 | Status | 聊天内容为空。 | (keep) | Chat message is empty. | |
| 244 | Status | 当前阶段暂不能发言。 | (keep) | Cannot chat in this phase. | |
| 245 | Status | 发言冷却中。 | (keep) | Chat cooldown. | |
| 246 | Channel status | 聊天未连接 | (keep) | Chat not connected | |

### 1.12 UnifiedGameUI Status Labels

| # | Control | Current Text (CN) | Suggested CN | Suggested EN | Notes |
|---|---------|-------------------|--------------|--------------|-------|
| 247 | Status label | [警] / [匪] / [卧] / [鼠] / [死] / [禁] | (keep) | [Cop] / [Gang] / [UC] / [Mole] / [Dead] / [Muted] | Colorblind-safe labels |
| 248 | Sabotage | 停电 / 封锁 | (keep) | Blackout / Lockdown | |
| 249 | Status | [断] / [就绪] | (keep) | [DC] / [Ready] | |

### 1.13 Theme-Inconsistent Strings (Off-Theme)

| # | File | Current Text | Issue | Suggested Fix |
|---|------|-------------|-------|---------------|
| T1 | `MainMenuController.cs` L12 | Comment: "Among Us 太空主题 v2" | References space theme | Update comment to "警匪暗战主题 v2" |
| T2 | `MainMenuController.cs` L13 | Comment: "全屏深空背景 + 浮动粒子星空" | Space/deep-space terminology | Update comment to "暗夜港区背景 + 浮动粒子灯光" |
| T3 | `MainMenuController.cs` L136 | "StarfieldParticles" | GameObject name references stars | Rename to "CityLightParticles" or keep as internal name |
| T4 | `MainMenuController.cs` L276 | "Among Us Inspired" in version line | Visible to player | Remove -- not needed in shipping UI |
| T5 | `PrototypeHud.cs` L10-14 | Comment: "Among Us 太空主题 v2" | Same as T1 | Update comment |
| T6 | `Localization.cs` L9 | `"game.title"` = "黑街卧底" | Older title, differs from "港区潜线" | Update to "港区潜线" to match all other references |

---

## 2. Button State Table

### 2.1 Main Menu Buttons

| Button | Normal | Disabled | Hover | Pressed | Cooldown | Missing States |
|--------|--------|----------|-------|---------|----------|----------------|
| Role Card (x4) | Faction color 12% alpha | 15% alpha gray | 35% alpha | 25% alpha | N/A | No tooltip |
| Map Button (x2) | Primary 60% / PoliceGray 60% | -- | -- | -- | N/A | No explicit disabled |
| 开始游戏 | Primary color | -- | Scale 1.3x brightness | Scale 0.65x brightness | N/A | No disable logic (always clickable) |
| 匿名登录 | MoleTeal | -- | Scale 1.3x | Scale 0.65x | N/A | No in-progress state |
| 进入大厅 | Primary | -- | Scale 1.3x | Scale 0.65x | N/A | No disable when not logged in |
| 教程回顾 | MoleTeal | -- | Scale 1.3x | Scale 0.65x | N/A | No disable when tutorial not found |
| 打开设置 | MoleTeal 58% alpha | -- | Scale 1.3x | Scale 0.65x | N/A | -- |

### 2.2 Lobby Buttons

| Button | Normal | Disabled | Hover | Pressed | Cooldown | Missing States |
|--------|--------|----------|-------|---------|----------|----------------|
| 创建房间 | AccentOrange | -- | Scale 1.35x | Scale 0.65x | No in-progress state | **No disabled when already connected** |
| 加入房间 | AccentBlue | -- | Scale 1.35x | Scale 0.65x | No in-progress state | **No disabled when room code empty** |
| 复制 (clipboard) | MutedColor | -- | -- | -- | N/A | **No visual feedback after copy** |
| 开始游戏 | AccentOrange | `interactable = false` | Scale 1.35x | Scale 0.65x | N/A | Disabled state uses generic gray |
| 准备 | AccentGreen (ready) / ButtonNormal (not ready) | -- | Scale 1.35x | Scale 0.65x | N/A | **Color change is correct but normal color changes on toggle -- should show distinct "Ready" visual** |
| 离开房间 | AccentRed | -- | Scale 1.35x | Scale 0.65x | N/A | -- |
| 确定离开 | AccentRed | -- | Scale 1.35x | Scale 0.65x | N/A | -- |
| 取消 | ButtonNormal | -- | Scale 1.35x | Scale 0.65x | N/A | -- |
| 返回主菜单 | ButtonNormal | -- | Scale 1.35x | Scale 0.65x | N/A | -- |

### 2.3 HUD -- OnlineMatchHud Buttons (Canvas)

| Button | Normal | Disabled | Hover | Pressed | Cooldown | Missing States |
|--------|--------|----------|-------|---------|----------|----------------|
| 创建 Host | Default | `interactable = false` when online | Standard color block | Standard | N/A | **No loading/in-progress spinner** |
| 加入 Client | Default | Same | Standard | Standard | N/A | **No loading spinner** |
| Relay 开房 | Default | Disabled when online or relay busy | Standard | Standard | N/A | **No "creating..." state** |
| Relay 加入 | Default | Same | Standard | Standard | N/A | **No "joining..." state** |
| 本地完整局 | Default | Disabled when online | Standard | Standard | N/A | -- |
| 离开房间 | Default | Disabled when offline and no disconnect | Standard | Standard | N/A | -- |
| Ready | Default | Disabled when not in lobby | Standard | Standard | N/A | **Button text toggles "Ready" / "取消 Ready" -- OK** |
| 开始 | Default | Disabled when not host or can't start | Standard | Standard | N/A | -- |
| 补 AI 开局 | Default | Disabled when not host or not in lobby | Standard | Standard | N/A | -- |
| E 互动 | Default | Disabled when not action phase or dead | Standard | Standard | N/A | **No cooldown display** |
| Q 击倒 | Default | Disabled when not gang or not action | Standard | Standard | Kill cooldown shown in text | **Button label doesn't show cooldown** |
| R 报案 | Default | Same as E | Standard | Standard | N/A | **No cooldown** |
| F 技能 | Default | Same as E | Standard | Standard | Ability cooldown shown in text | **Button label doesn't show cooldown** |
| V 通风管 | Default | Same as E | Standard | Standard | N/A | -- |
| M 大地图 | Default | Disabled when not online | Standard | Standard | N/A | -- |
| I 案情板 | Default | Disabled when not online | Standard | Standard | N/A | -- |
| 重开 | Default | Disabled when not host or not result phase | Standard | Standard | N/A | -- |
| 返回房间 | Default | Disabled when not online or not result | Standard | Standard | N/A | -- |
| 发送 (chat) | Default | Disabled when can't send or input empty | Standard | Standard | Chat cooldown (text shown in status) | **Button doesn't display cooldown timer** |
| 举报最近 | Default | Disabled when no chat messages | Standard | Standard | N/A | -- |
| 屏蔽最近 | Default | Same | Standard | Standard | N/A | -- |
| Vote buttons | Default | Disabled when dead or not meeting/voting | Highlighted border | Pressed dark | N/A | **No "voted" state -- user doesn't know if they already voted** |
| 跳过投票 | Same as vote buttons | Same | Same | Same | N/A | -- |
| 键 1/2/3 (task) | Default | -- | Standard | Standard | N/A | **No "completed" visual state in Canvas (OnGUI had green)** |
| 按住扫描 | Default | -- | Standard | Standard (hold) | N/A | **Hold-to-scan visual feedback unclear** |
| 退出 (task) | Default | -- | Standard | Standard | N/A | -- |

### 2.4 PrototypeHud Buttons (Offline)

| Button | Normal | Disabled | Hover | Pressed | Cooldown | Missing States |
|--------|--------|----------|-------|---------|----------|----------------|
| 通风管 | Color 18% alpha | Color 12% gray 40% alpha | 45% alpha | 30% alpha | N/A | No cooldown |
| 击杀 | DangerRed 18% | Same | 45% | 30% | N/A | No cooldown display |
| 破坏 | MoleTeal 18% | Same | 45% | 30% | N/A | No cooldown |
| 报告 | SafeGreen 18% | Same | 45% | 30% | N/A | No cooldown |
| 会议 | TitleGold 18% | Same | 45% | 30% | N/A | No cooldown |
| 热度榜 | TextMuted 18% | Same | 45% | 30% | N/A | No cooldown |

### 2.5 GameOverController Buttons

| Button | Normal | Disabled | Hover | Pressed | Cooldown | Missing States |
|--------|--------|----------|-------|---------|----------|----------------|
| Skip / Return | ButtonPrimary | -- | Standard | Standard | Auto-countdown (3s) | **No visual countdown progress bar** |

### 2.6 Summary of Missing Button States

| Gap | Severity | Recommendation |
|-----|----------|----------------|
| No loading/in-progress state for Host/Client/Relay buttons | High | Add spinner or "Creating..." / "Joining..." text overlay |
| No "already voted" indicator | High | Add checkmark or gray-out after casting vote |
| No cooldown overlay on action buttons (Q/F) | Medium | Add circular cooldown fill or countdown text on button |
| Copy button has no success feedback | Medium | Flash green or show "Copied!" tooltip |
| Task step buttons lack completed state in Canvas | Medium | Add green fill or checkmark for completed steps |
| Hold-to-scan has no visual progress feedback in Canvas | Medium | Add radial fill or progress bar on button |
| No disable for "Start Game" when player count insufficient (offline menu) | Low | Add disabled state with tooltip |

---

## 3. HUD Layout Analysis and Recommendations

### 3.1 Current OnlineMatchHud Layout Structure

The `OnlineMatchHud.cs` builds a **three-column dock layout** with overlays:

```
+------------------------------------------------------------------+
|                       Header (top bar)                             |
|  [Title] [Phase/Time/Evidence/Survivors]        [Voice/Chat Info] |
+----------+------------------------+-----------+-------------------+
|          |                        |           |                    |
| Left     |    Center Dock         | Right     |   Chat Panel      |
| Dock     |    - Current Match     | Dock      |   - Title         |
| - Conn   |    - Progress bars     | - Tabs    |   - Feed          |
| - Rules  |    - Objectives        | - Body    |   - Input         |
| - Lobby  |                        |           |   - Buttons       |
| - Action |                        |           |                    |
| - Result |                        |           |                    |
+----------+------------------------+-----------+-------------------+
|                       Footer (bottom bar)                          |
|  [Status] [Controls Hint]                                          |
+------------------------------------------------------------------+
```

Plus four overlay panels (shown/hidden based on phase):
- **Meeting Overlay**: Full-screen, 3-column (Evidence | Seat Board | Vote Panel)
- **Result Overlay**: Centered modal with progress bars and buttons
- **Task Overlay**: Centered modal with task brief, mini-game board, progress bar
- **Map Overlay**: Full-screen tactical map with markers and legend
- **Compact Action HUD**: Shown during action phase, hides docks, shows minimal status + action bar + ability card

### 3.2 Recommended HUD Zone Division

| Zone | Position | Content | Priority |
|------|----------|---------|----------|
| **Mini-map** | Top-right corner (collapsible) | Tactical minimap with player/task/body markers | P1 |
| **Role/Identity Card** | Top-left | Avatar frame (faction color), role icon, name, profession, alive status | P0 -- already exists in compact HUD |
| **Evidence & Progress** | Top-center | Evidence chain bar, task completion bar, survivor bar, match timer | P0 -- already exists |
| **Action Buttons** | Bottom-center | E Interact, Q Kill, R Report, F Ability, V Vent -- with cooldown overlays | P0 -- already exists in compact HUD |
| **Task List** | Left sidebar (collapsible) | Active tasks with progress, checkmarks for completed | P1 -- currently in notebook tabs |
| **Intel/Case Board** | Right sidebar (collapsible) | Focused intel, case log, task details | P1 -- currently in notebook tabs |
| **Chat Panel** | Right-bottom | Text chat feed, input field, send/report/block | P0 -- already exists |
| **Meeting/Vote** | Full overlay | Round table with player cards, evidence board, vote panel | P0 -- already exists |
| **Alert Banner** | Center-top (transient) | Corpse found, sabotage event, emergency meeting notification | P2 -- currently only text in status |
| **Ability/Skill Meter** | Bottom-right (compact HUD) | Profession name, ability cooldown bar | P1 -- already exists in compact HUD |

### 3.3 OnGUI to uGUI Migration Priority

The OnGUI path (`OnlineMatchController.OnGUI.cs`) is already gated behind `#if UNITY_EDITOR` since M7.3. The Canvas HUD (`OnlineMatchHud.cs`) covers all player-facing scenarios. The remaining migration work is:

| Priority | Element | Current State | Recommendation |
|----------|---------|---------------|----------------|
| P0 | Meeting/Vote overlay | Canvas version exists and functional | **Verify vote button "already voted" state** and test full meeting flow |
| P0 | Chat system | Canvas chat section exists in OnlineMatchHud | **Verify send cooldown visual** and test multi-channel |
| P1 | Task overlay (mini-games) | Canvas task overlay exists; OnGUI fallback for editor | **Add task step completion visuals** (green/checkmark) |
| P1 | Compact action HUD | Exists and functional | **Add cooldown timers on button labels** |
| P2 | Large tactical map | Canvas map overlay exists | **Verify marker refresh** and test at all supported resolutions |
| P2 | Result overlay | Canvas result overlay exists | **Verify progress bar animations** match GameOverController |
| P3 | OnGUI editor fallback | ~300 calls in OnGUI.cs | Keep as editor debugging tool; no migration needed |
| P3 | HostMigration overlay | ~8 OnGUI calls | Migrate to a simple Canvas overlay text when needed |

### 3.4 Mobile Adaptation Recommendations

The current layout uses `CanvasScaler.ScaleMode.ScaleWithScreenSize` with reference resolution `1600x900`, which provides basic scaling. For future mobile support:

| Area | Current | Mobile Concern | Recommendation |
|------|---------|----------------|----------------|
| Three-column dock | Fixed 30%/39%/29.5% columns | Too cramped on phone screens | Collapse to single-column scrollable layout on aspect ratios < 16:9 |
| Compact action HUD | Bottom bar + top-left card + bottom-right card | Touch targets too small | Increase button min-size to 44x44pt; use radial action menu |
| Meeting overlay | 3-column (evidence/table/vote) | Same column issue | Stack vertically: evidence on top, table middle, vote bottom scroll |
| Task overlay | Centered modal with mini-game board | Mini-game may need touch adaptation | Redesign mini-game inputs for touch (drag instead of key press) |
| Chat panel | Right dock section | Hidden on mobile | Add floating chat bubble with expand/collapse |
| Map overlay | Full-screen with marker dots | Touch-to-select markers needed | Add pinch-zoom and tap-to-select markers |
| Input fields | Text-based input | Keyboard overlay issues | Use `TouchScreenKeyboard` API; adjust layout when keyboard visible |
| Font sizes | 10-24px range | May be too small on high-DPI phones | Use `CanvasScaler.ScaleMode.ConstantPixelSize` with DPI-aware scaling, or switch to TextMeshPro with SDF rendering |

### 3.5 Localization Integration Gaps

The following controllers still hardcode all strings instead of using `Localization.T()`:

| Controller | Hardcoded strings | Localization keys used | Gap |
|------------|-------------------|------------------------|-----|
| `MainMenuController` | ~40 strings | 0 | **Full extraction needed** |
| `LobbyController` | ~25 strings | 0 | **Full extraction needed** |
| `PrototypeHud` | ~20 strings | 0 | **Full extraction needed** |
| `GameOverController` | ~15 strings | 0 | **Full extraction needed** |
| `OnlineMatchHud` | ~60 strings | ~8 keys (meeting, result, map, minigame) | **Partial -- most labels hardcoded** |
| `OnlineMatchController.OnGUI.cs` | ~80 strings | 0 (editor only) | Low priority -- editor fallback |
| `SocialPrototypeHud` | 0 | Uses `T()` method for all strings | **Already localized** -- model for others |

**Total hardcoded UI strings requiring extraction: ~240**

---

## 4. Appendix: File Inventory

| File | Lines | Role | UI System |
|------|-------|------|-----------|
| `Assets/_Project/Scripts/UI/MainMenuController.cs` | 1019 | Main menu with settings overlay | uGUI (Canvas) |
| `Assets/_Project/Scripts/UI/LobbyController.cs` | 622 | Online lobby with room code flow | uGUI (Canvas) |
| `Assets/_Project/Scripts/UI/PrototypeHud.cs` | 535 | Offline single-player HUD | uGUI (Canvas) |
| `Assets/_Project/Scripts/UI/GameOverController.cs` | 760 | Result/game-over animated sequence | uGUI (Canvas) |
| `Assets/_Project/Scripts/UI/UIManager.cs` | 283 | Canvas lifecycle and panel switching | uGUI (Canvas) |
| `Assets/_Project/Scripts/UI/UnifiedGameUI.cs` | 126 | Shared styling constants and helpers | Static utility |
| `Assets/_Project/Scripts/UI/ThemeManager.cs` | -- | Theme colors, fonts, animation durations | Static utility |
| `Assets/_Project/Scripts/UI/UIStyle.cs` | -- | Visual styling helpers | Static utility |
| `Assets/_Project/Scripts/UI/SettingsManager.cs` | 425 | Settings persistence and events | Singleton service |
| `Assets/_Project/Scripts/UI/SettingsData.cs` | 371 | Settings data model | Data class |
| `Assets/_Project/Scripts/UI/SettingsUIHelper.cs` | -- | Settings UI factory helpers | Static utility |
| `Assets/_Project/Scripts/UI/LoadingScreen.cs` | -- | Loading screen overlay | uGUI (Canvas) |
| `Assets/_Project/Scripts/UI/TransitionEffect.cs` | -- | Screen transition effects | uGUI (Canvas) |
| `Assets/_Project/Scripts/UI/UIParticleEffect.cs` | -- | Particle effects for UI | uGUI component |
| `Assets/_Project/Scripts/UI/UiButtonSfx.cs` | -- | Button hover/click sound effects | uGUI component |
| `Assets/_Project/Scripts/Online/OnlineMatchHud.cs` | ~2000 | Online match Canvas HUD (primary) | uGUI (Canvas) |
| `Assets/_Project/Scripts/Online/OnlineMatchController.OnGUI.cs` | ~1500 | OnGUI editor fallback | IMGUI (editor only) |
| `Assets/_Project/Scripts/Online/OnlineMatchController.cs` | -- | Core match controller (partial class) | Controller |
| `Assets/_Project/Scripts/SocialDeduction/SocialPrototypeHud.cs` | ~510 | Offline social deduction prototype HUD | uGUI (Canvas) |
| `Assets/_Project/Scripts/Core/Localization.cs` | 365 | Localization dictionary (CN/EN) | Service |
