---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9470d846c7eff9b24afb94a99a2cb3f0_e2d41a435dbc11f1a4f35254002afed2
    ReservedCode1: /UmDdpqYPTdf0uDrwR2RTrsAaCF04PJtFOsfLmeKtZV/bGleqo4CEqXuBmMHn8BQjw6wKXmDqv+RGLVw7DSfJP+HjTP6IU0yYLtDlNsRYxOX9sQVZh6VeeIja3OTqzvtzJ8iBG6qspPTB4f17CKsePEI9Dx0T0jOziof7iFQSuj+HQMErZgX4eCAEug=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9470d846c7eff9b24afb94a99a2cb3f0_e2d41a435dbc11f1a4f35254002afed2
    ReservedCode2: /UmDdpqYPTdf0uDrwR2RTrsAaCF04PJtFOsfLmeKtZV/bGleqo4CEqXuBmMHn8BQjw6wKXmDqv+RGLVw7DSfJP+HjTP6IU0yYLtDlNsRYxOX9sQVZh6VeeIja3OTqzvtzJ8iBG6qspPTB4f17CKsePEI9Dx0T0jOziof7iFQSuj+HQMErZgX4eCAEug=
---

# Stage 5 改动说明：对手 AI 与会议投票系统完善

## 概述

第 4 阶段已将 GameController 桥接入 SocialPrototypeController，但 **会议投票链路完全缺失**。本次修改打通了 "玩家行动 → AI 对手 → 环境事件 → 会议投票 → 淘汰 → 胜负判定" 的完整离线游戏循环。

## 修改文件清单

| 文件 | 路径 | 改动 |
|------|------|------|
| GameController.cs | Assets/_Project/Scripts/Gameplay/ | 新增 MeetingInterval / ShouldHoldMeeting / RunMeeting() / PlayerCastVote() / ForceMeeting() |
| OpponentAi.cs | Assets/_Project/Scripts/Gameplay/ | GangEliminated 淘汰检查补全 |
| VictoryEvaluator.cs | Assets/_Project/Scripts/Gameplay/ | 中文化 + 按阵营拆分 + 会议淘汰胜利条件 |
| GameState.cs | Assets/_Project/Scripts/Core/ | 新增 GangEliminated 属性和淘汰处理 |
| SocialPrototypeController.cs | Assets/_Project/Scripts/SocialDeduction/ | 会议阶段自动处理 + SyncTurnElimination |

## 详细改动

### 1. GameController.cs (+92 行)

**新增常量/属性**

| 项 | 说明 |
|----|------|
| `MeetingInterval = 3` | 每 3 天自动触发阵营会议 |
| `ShouldHoldMeeting` | 会议标志位，由 RunPlayerAction 设置、RunMeeting 消费 |

**修改 RunPlayerAction()**
```
原流程：PlayerTurn → AiTurn → 事件结算 → AdvanceDay → PlayerTurn
新流程：PlayerTurn → AiTurn → 事件结算 → (Day % 3 == 0 ? Meeting : AdvanceDay → PlayerTurn)
```

当回合数是 3 的倍数时，不推进日期而是进入 Meeting 阶段，等待会议投票后再推进。

**新增 RunMeeting()**
- 调用 `opponentAi.CastMeetingVote()`，AI 阵营按多数票淘汰一个 Faction
- 淘汰结果写入 `State.EliminateFaction()`
- 完成后调用 `AdvanceToNextDay()` 回到 PlayerTurn
- 如果淘汰触发胜负条件，通过 `TryEndGame()` 结束游戏

**新增 PlayerCastVote(Faction)**
- 玩家投票直接覆盖 AI 投票：直接淘汰目标阵营
- 适用于手动会议 UI 交互

**新增 ForceMeeting()**
- 强制触发紧急会议，绕过 MeetingInterval 检查
- 由 SocialPrototypeController.StartTurnMeeting() 调用

### 2. OpponentAi.cs (+1 行)

**Run() 方法**：Gang 阵营的 `!state.GangEliminated` 淘汰检查补全（此前缺少此判断，已淘汰的 Gang 仍会继续行动）。

**CastMeetingVote() 方法**：Gang 投票收集处同样增加 `!state.GangEliminated` 检查。

### 3. VictoryEvaluator.cs (重写)

**中文化**：所有结果字符串从英文翻译为中文。

**按阵营拆分**：
- `EvaluateEliminationVictory()` — 会议淘汰触发的胜利
- `EvaluateGangVictory()` — 黑帮专属 4 条件
- `EvaluateUndercoverVictory()` — 卧底专属 2 条件（含替代路径）
- `EvaluatePoliceVictory()` — 警察专属 3 条件（含策略压制路径）

**新增淘汰胜利条件**：
| 条件 | 结果 |
|------|------|
| 玩家 Gang + Police/Undercover 均淘汰 | 黑帮胜利 |
| 玩家 Undercover + Police 淘汰 + 黑帮势力弱 | 卧底胜利 |
| 玩家 Police + Undercover 淘汰 + 黑帮势力弱 | 警察胜利 |

### 4. GameState.cs (+2 行)

新增 `GangEliminated` 属性，允许会议投票淘汰黑帮阵营。`EliminateFaction()` 和 `ClearEliminations()` 同步处理。

### 5. SocialPrototypeController.cs (+46 行)

**OnTurnStateChanged()**：新增 `GamePhase.Meeting` 分支处理：
1. 检测 `ShouldHoldMeeting` 标志
2. 调用 `turnController.RunMeeting()` 执行 AI 投票
3. 调用 `SyncTurnElimination()` 将淘汰结果同步到 3D 世界
4. 检查是否触发 GameOver
5. RunMeeting 内部已触发 `AdvanceToNextDay → Changed`，Meeting 分支直接 return 不重复触发

**新增 SyncTurnElimination()**：读取 `turnController.State.VotedOut`，找到对应阵营的 3D 角色并 `Kill()` + `RemoveBodiesFor()`。如果被淘汰的是玩家，触发 `FinishGame()`。

**StartTurnMeeting()**：现在通过 `turnController.ForceMeeting()` 走官方会议流程。

**CastTurnVote()**：新增 `turnController.PlayerCastVote(targetFaction)` 调用，确保玩家投票同步到 GameController。

## 完整游戏循环

```
┌─ 身份选择 (PrototypeHud)
├─ 第 N 天 PlayerTurn
│   ├─ 玩家点击区域 → SelectDistrict
│   ├─ 玩家选择行动 → RunPlayerAction
│   │   ├─ ActionResolver 结算玩家行动
│   │   ├─ OpponentAi.Run() — 三个 AI 阵营按角色策略行动
│   │   │   ├─ Gang: 高风险区 (Dockyard/NightMarket) 扩张/运货
│   │   │   ├─ Police: 高黑帮影响力区 封锁/取证/突袭
│   │   │   └─ Undercover: 信息区 (PolicePrecinct/Clinic/WarehouseRow) 取证
│   │   ├─ EventResolver 结算环境事件 (目击者/公众反弹等)
│   │   └─ 天数为 3 的倍数 → 进入 Meeting 阶段
│   └─ AdvanceDay → PlayerTurn
├─ Meeting 阶段 (每 3 天 / 紧急会议)
│   ├─ OpponentAi.CastMeetingVote() — AI 阵营按策略投票
│   ├─ 多数票淘汰一个 Faction → State.EliminateFaction()
│   ├─ SyncTurnElimination() — 淘汰同步到 3D 角色
│   └─ VictoryEvaluator.TryEvaluate() → GameOver 或继续
└─ GameOver
    ├─ 黑帮胜利: 嫌疑满/掩护崩/地盘 4+/货物运出
    ├─ 卧底胜利: 证据 8+ 且身份未暴露
    ├─ 警察胜利: 证据+热度足够收网
    ├─ 淘汰胜利: 对手阵营全部被会议清除
    └─ 僵局: Day > 10
```

## 未修改文件

以下文件保持原样，无需修改即可工作：

- **ActionResolver.cs** — 12 种玩家行动已完整实现，区域事件已写入 GameState.Log
- **EventResolver.cs** — 4 种环境事件 (目击者/公众反弹/忠诚测试/Boss车队) 已完整
- **PrototypeHud.cs** — Bind(GameController) 签名不变
- **DistrictMapView.cs** — Bind(GameController) 签名不变，区域点击自动映射
- **SocialPrototypeHud.cs** — Bind(SocialPrototypeController) 签名不变
- **PlayerAction.cs / SocialRole.cs / DistrictState.cs / DistrictType.cs / Faction.cs / GamePhase.cs** — 枚举/数据类无改动
- **StoryEvent.cs** — EventResolver 依赖，无改动
*（内容由AI生成，仅供参考）*
