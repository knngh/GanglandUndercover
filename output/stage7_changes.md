---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9470d846c7eff9b24afb94a99a2cb3f0_fdac71175dc811f1b5095254007bceed
    ReservedCode1: da5PHxvxagjHTT+mxwwVMfJPBj5JNdBayhmxSe7OiY7QgfFrV4+UcjpAAmylnQzaSlfgCp8f78R/KMwAKMqcSjxuuFBcBfjaBfYgH0/3nvG0X2vSGDW7GZuERxpYp3w8LulOCZOd/taUg8d2dNmXhxRfbIlVszXFF2U9KofMKp6iqqzoIqU0gdsy5Sg=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9470d846c7eff9b24afb94a99a2cb3f0_fdac71175dc811f1b5095254007bceed
    ReservedCode2: da5PHxvxagjHTT+mxwwVMfJPBj5JNdBayhmxSe7OiY7QgfFrV4+UcjpAAmylnQzaSlfgCp8f78R/KMwAKMqcSjxuuFBcBfjaBfYgH0/3nvG0X2vSGDW7GZuERxpYp3w8LulOCZOd/taUg8d2dNmXhxRfbIlVszXFF2U9KofMKp6iqqzoIqU0gdsy5Sg=
---

# Stage 7: 联机模式游戏功能完善

## 日期
2026-06-01

## 变更摘要

第 7 阶段创建了联机同步基础设施层（5 个新文件），并完成了 8 处核心集成点到 `OnlineMatchController` 的接线。同时将 `OnlineSyncManager` 挂入 `PrototypeBootstrap` 的在线模式启动流程。

---

## 新文件清单

| 文件 | 大小 | 职责 |
|------|------|------|
| `TaskSync.cs` | 4.7 KB | 联机任务分派同步 — 按阵营（Gang/非Gang）分配任务、跟踪完成/破坏进度、统计已破坏/已完成比率 |
| `MeetingSync.cs` | 3.7 KB | 联机会议同步 — Begin/RegisterVote/Resolve/End 生命周期、全票收集检测、票型追踪 |
| `PlayerStateSync.cs` | 5.4 KB | 联机玩家状态同步 — DetectChanges 帧同步、存活/死亡/角色/位置变更检测、击杀记录 |
| `OnlineVictoryBridge.cs` | 12 KB | VictoryEvaluator 联机适配器 — 将联机状态映射到离线 GameState 概念（Cover/Suspicion/Evidence/GangControlledDistricts），提供双重判定策略 |
| `OnlineSyncManager.cs` | 8.2 KB | 统一同步管理器 — MonoBehaviour，RequireComponent(typeof(OnlineMatchController))，在 Awake 初始化全部子系统，提供公共 API |

---

## 修改文件清单

### OnlineMatchController.cs（8 处集成点）

| 集成点 | 方法 | 变更 |
|--------|------|------|
| 字段声明 | 类体 L160 | 新增 `private OnlineSyncManager syncManager;` |
| Awake 初始化 | `Awake()` L733 | `syncManager = GetComponent<OnlineSyncManager>();` |
| 比赛开始 | `StartOnlineMatchCore()` L2690 | `syncManager?.OnMatchStarted(players, tasks, evidenceTarget);` |
| 任务破坏 | `TryInteractWithTask()` L2842 | `syncManager?.OnTaskSabotagedLocally(senderClientId, taskId, sabotageType);` |
| 任务完成 | `TryInteractWithTask()` L2877 | `syncManager?.OnTaskCompletedLocally(senderClientId, taskId);` |
| 击倒 | `TryKill()` L3108 | `syncManager?.OnKilled(victimClientId, killerClientId);` |
| 会议开始 | `BeginMeeting()` L3281 | `syncManager?.OnMeetingBegan(reason, phase);` |
| 投票 | `ApplyVote()` L3322 | `syncManager?.OnVoteCast(voterClientId, targetClientId);` |
| 投票结果（平局/跳过） | `ResolveVotes()` L3376 | `syncManager?.OnMeetingResolved(SkipVoteTarget, tied, tally);` |
| 投票结果（淘汰） | `ResolveVotes()` L3404-3406 | `syncManager?.RegisterElimination(...); syncManager?.OnMeetingResolved(...);` |
| 会议结束 | `ResolveVotes()` L3419 | `syncManager?.OnMeetingEnded();` |
| 胜负判定 | `EvaluateWinConditions()` L3422-3435 | 优先走 `OnlineVictoryBridge.EvaluateVictory(...)`，兜底原生判定 |
| 超时判定 | `ResolveTimeLimitOutcome()` L3491-3504 | 优先走 `OnlineVictoryBridge.TryTimeLimitEvaluation(...)`，兜底原生判定 |

### PrototypeBootstrap.cs

| 变更 | 说明 |
|------|------|
| `BuildOnlinePrototype()` L113 | 新增 `onlineObject.AddComponent<OnlineSyncManager>();` |

---

## 架构概览

```
OnlineMatchController (游戏核心)
    │
    ├─ Awake → GetComponent<OnlineSyncManager>()
    │
    ├─ StartOnlineMatchCore → OnMatchStarted(players, tasks, evidenceTarget)
    ├─ TryInteractWithTask → OnTaskCompletedLocally / OnTaskSabotagedLocally
    ├─ TryKill → OnKilled
    ├─ BeginMeeting → OnMeetingBegan
    ├─ ApplyVote → OnVoteCast
    ├─ ResolveVotes → OnMeetingResolved / RegisterElimination / OnMeetingEnded
    ├─ EvaluateWinConditions → EvaluateVictory (OnlineVictoryBridge)
    └─ ResolveTimeLimitOutcome → TryTimeLimitEvaluation (OnlineVictoryBridge)
                                  │
                        OnlineSyncManager
                        ├─ TaskSync (任务状态字典)
                        ├─ MeetingSync (生命周期状态机)
                        ├─ PlayerStateSync (帧检测 + 击杀记录)
                        └─ OnlineVictoryBridge (离线-在线状态映射 + 双重判定)
```

## OnlineVictoryBridge 判定流程

```
EvaluateVictory(evidenceScore, evidenceTarget, players, tasks, ...)
    │
    ├─ 1. 原生在线规则（快速路径）
    │   ├─ 证据链闭合 (evidenceScore >= evidenceTarget) → 警方胜利
    │   ├─ 黑帮全部出局 → 警方胜利
    │   └─ 非帮派全灭/人数压制 → 黑帮胜利
    │
    ├─ 2. 离线 VictoryEvaluator 映射
    │   ├─ evidenceScore → GameState.Evidence
    │   ├─ 任务破坏比 → GameState.Cover
    │   ├─ 任务完成比 → GameState.PoliceHeat
    │   ├─ 存活阵营比 → GameState.GangControlledDistricts
    │   └─ 调用 VictoryEvaluator.Evaluate(GameState)
    │       ├─ 淘汰胜利（会议淘汰清空对手阵营）
    │       ├─ 黑帮胜利（Cover ≥ 85 + GangControlledDistricts ≥ 3）
    │       └─ 警方胜利（Evidence ≥ 8 或 PoliceHeat ≥ 80 或黑帮全淘汰）
    │
    └─ 返回 EvaluateResult { HasResult, ResultText }
```

## 风险评估

- **兼容性**：所有 `syncManager?.` 调用均通过 null-conditional 保护，未挂载 OnlineSyncManager 时不影响原生流程
- **运行时依赖**：OnlineVictoryBridge 依赖离线模式 `VictoryEvaluator` 类型，需确保该项目中存在该类型
- **网络同步**：当前阶段所有同步调用均在 **Host 本地**执行；多客户端网络同步需后续阶段配合 Netcode RPC 实现
- **Unity Editor 依赖**：网络层代码（Netcode/Relay/UTP）未做修改，保持第 6 阶段审查标记状态
*（内容由AI生成，仅供参考）*
