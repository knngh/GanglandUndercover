# Stage 10 — 联机测试准备审查报告

> 项目: GanglandUndercover | 日期: 2026-06-02
> 审查范围: 联机架构、网络配置、游戏流程完整性、测试清单

---

## 1. 架构总览

### 1.1 文件清单

| 文件 | 行数 | 职责 |
|------|------|------|
| `OnlineMatchController.cs` | 12,381 | 主控：网络连接、角色分配、游戏循环、任务/击杀/会议/投票/胜负、Bot AI、OnGUI |
| `OnlineSyncManager.cs` | 236 | 统筹 TaskSync / MeetingSync / PlayerStateSync / OnlineVictoryBridge |
| `UnityServiceBootstrap.cs` | 203 | Unity Services 初始化（Auth/Relay/Lobby），Vivox 已移除 |
| `OnlineRole.cs` | 11 | 枚举：Unassigned / Police / Undercover / Gang |
| `TaskSync.cs` | 167 | 任务池分配（Gang→破坏池，非Gang→调查池） |
| `MeetingSync.cs` | 105 | 会议生命周期：Begin → Vote → Resolve → End |
| `PlayerStateSync.cs` | 143 | 玩家状态变更检测（存活/角色/位置） |
| `OnlineVictoryBridge.cs` | 301 | 离线 VictoryEvaluator 联机适配，双重判定 |
| `VerticalSlice.cs` | 616 | 地图场景搭建（地面/房间/迷你游戏/灯光） |
| `CharacterAdapters.cs` | 157 | 角色模型 Prefab + Animator 绑定 |

### 1.2 网络技术栈

```
Unity.Netcode (Netcode for GameObjects)
  ├── Unity.Netcode.Transports.UTP (Unity Transport)
  │     └── Unity.Networking.Transport.Relay (Relay 穿透)
  ├── Unity.Services.Relay (创建分配/加入码)
  ├── Unity.Services.Lobbies (Lobby 服务，集成但 UI 未完整)
  ├── Unity.Services.Authentication (匿名登录)
  └── CustomMessagingManager (5 条命名消息，非 RPC)
```

### 1.3 同步模型

- **Host-Authoritative**：Host 拥有全部游戏状态，通过 BroadcastSnapshot（12.5Hz）下发
- **Client → Host**：ClientStateMessage（位置/就绪/名字）、ClientActionMessage（Vote/Report/Kill/Interact/Ability）
- **Host → Client**：ServerSnapshotMessage（全量状态）、RoleAssignMessage（私密角色）
- **无 Netcode RPC**：全量使用 FastBufferWriter 手动序列化，通过 CustomMessagingManager 发送

---

## 2. 联机流程审查

### 2.1 连接流程 ✅ 完整

| 阶段 | 方法 | 状态 |
|------|------|------|
| 服务初始化 | `UnityServiceBootstrap.InitializeAsync()` | ✅ |
| LAN Host | `StartHost()` → `ConfigureTransport("0.0.0.0")` → `StartHost()` | ✅ |
| LAN Client | `StartClient()` → `ConfigureTransport(address)` → `StartClient()` | ✅ |
| Relay Host | `StartRelayHost()` → `CreateAllocationAsync` → `GetJoinCodeAsync` | ✅ |
| Relay Client | `StartRelayClient()` → `JoinAllocationAsync(joinCode)` → `StartClient()` | ✅ |
| 断开清理 | `Shutdown()` → `ShutdownAsync()` → 清理状态字典 | ✅ |

### 2.2 游戏流程 ✅ 完整

| 阶段 | 转换逻辑 | 状态 |
|------|---------|------|
| Lobby → Opening | `StartOnlineMatchCore()` → `phase=Opening`，角色分配 | ✅ |
| Opening → Action | `phaseTimer` 倒计时结束（`RoleRevealSeconds`），`TickHostSimulation` 自动切换 | ✅ |
| Action → Meeting | 报告尸体 / 紧急会议 → `BeginMeeting()` | ✅ |
| Meeting → Voting | `MeetingIntroSeconds(35s)` 结束，`phase=Voting` | ✅ |
| Voting → Action/Result | `ResolveVotes()` → 若未分胜负则回 Action；否则 → Result | ✅ |
| 超时判定 | `matchElapsedSeconds >= MatchHardLimitSeconds` → `ResolveTimeLimitOutcome()` | ✅ |

### 2.3 角色分配 ✅ 合理

```
AssignRoles 算法：
  i=0 → Gang（当 players >= 2）
  i=1 → Undercover（当 players >= 5）
  i=2 → Gang（当 players >= 8）
  其余 → Police
分配方式：Shuffle 后按位置索引固定分配，非随机角色池
```

**注意**：角色分配非完全随机，而是 Shuffle 后的确定性分配，可预测性较高。

### 2.4 击杀流程 ✅ 完整

```
TryKill(sender, player):
  1. 校验 sender 为 Gang
  2. 校验 killCooldown <= 0
  3. TryFindNearestVictim(KillRange=0.9)
  4. victim.Alive=false → bodies.Add → killCooldown=34s
  5. EvaluateWinConditions → BroadcastSnapshot
```

### 2.5 会议/投票流程 ✅ 完整

```
报告尸体/紧急会议 → BeginMeeting(reason):
  - 所有存活玩家传送到会议座位（圆形排列）
  - phase=Meeting, phaseTimer=MeetingIntroSeconds(35s)
  - 清除 votes、停用黑灯/封锁

MeetingIntro 到期 → TickHostSimulation 自动切换 phase=Voting

ApplyVote(voter, target):
  - 校验投票阶段、存活状态
  - votes 字典写入
  - 全员投票 → ResolveVotes()

ResolveVotes():
  - 统计票型（最高票、平局检测）
  - 淘汰投票对象 → EvaluateWinConditions
  - 未分胜负 → phase=Action
```

### 2.6 胜负判定 ✅ 完整

| 条件 | 结果 | 路径 |
|------|------|------|
| evidenceScore ≥ evidenceTarget | 警方胜利 | OnlineVictoryBridge → NativeOnline |
| aliveGang == 0 | 警方胜利 | NativeOnline 兜底 |
| aliveGang ≥ aliveNonGang | 黑帮胜利 | NativeOnline 兜底 |
| 时间到 + evidence ≥ 82% target | 警方胜利 | ResolveTimeLimitOutcome |
| 时间到 + evidence < 82% target | 黑帮胜利 | ResolveTimeLimitOutcome |

双重判定：OnlineVictoryBridge 同时运行在线原生规则 + 离线 VictoryEvaluator（通过 GameState 映射），任一触发即返回。

---

## 3. NetworkManager 配置审查

### 3.1 现状

| 项目 | 状态 |
|------|------|
| NetworkManager Prefab | ❌ 不存在（仅 `DefaultNetworkPrefabs.asset`） |
| 创建方式 | 运行时动态 `EnsureNetworkStack()` |
| Transport | UnityTransport，运行时 `AddComponent<UnityTransport>()` |
| 自定义消息注册 | `RegisterMessages()` 注册 5 条命名消息 |
| 回调注册 | `OnClientConnectedCallback` / `OnClientDisconnectCallback` |
| NetworkPrefabs | 使用 `DefaultNetworkPrefabs.asset` 中的默认列表 |

### 3.2 问题与建议

| # | 问题 | 建议 |
|---|------|------|
| 1 | 无 NetworkManager Prefab，排查困难 | **TODO**: 创建 `NetworkManager.prefab`，预设 UnityTransport + NetworkPrefabs，便于场景引用 |
| 2 | Transport 配置硬编码 | **TODO**: 将 `ConnectTimeoutMS`、`MaxConnectAttempts`、`MaxPayloadSize` 提取为 ScriptableObject 配置 |
| 3 | DefaultNetworkPrefabs 为空或默认 | **已验证**：确保角色 Prefab 在列表中（否则 Netcode 无法 Spawn） |

---

## 4. 关键缺失项

### 4.1 🔴 严重缺失

| # | 缺失项 | 影响 | 建议 |
|---|--------|------|------|
| 1 | **Host Migration** | Host 断开 → 全部 Client 掉线，对局结束 | **TODO**: 实现 Host 切换逻辑（需 Netcode Session Management 或自定义） |
| 2 | **断线重连** | Client 掉线无法回到同一局 | **TODO**: 添加 `NetworkManager.NetworkConfig.ConnectionApproval` + Session 恢复 |

### 4.2 🟡 中等缺失

| # | 缺失项 | 影响 | 建议 |
|---|--------|------|------|
| 3 | **无语音通讯** | Vivox 已移除，联机体验降级 | 可选方案：Discord/Steam Voice SDK 集成，或标记为 TODO |
| 4 | **Lobby UI 简陋** | 仅 OnGUI 基础按钮，无玩家列表/准备/踢人 | **TODO**: 用 UGUI/UI Toolkit 重做 Lobby 界面 |
| 5 | **投票超时未严格强制** | `VotingSeconds=55s` 常量存在但强制到期自动 Skip 的逻辑需确认 | 审查 `TickHostSimulation` 中 Meeting→Voting 转换后 phaseTimer 到期的处理 |
| 6 | **NetworkVariable 缺失** | 全部手动序列化，易出错 | 建议核心状态（phase/evidenceScore）使用 NetworkVariable 辅助同步 |

### 4.3 🟢 低优先级

| # | 缺失项 | 影响 | 建议 |
|---|--------|------|------|
| 7 | 无 Ping/Latency 显示 | 联机调试困难 | **TODO**: 添加 `NetworkManager.NetworkConfig.NetworkTransport.GetCurrentRtt()` 显示 |
| 8 | OnlineMatchController 单文件 12,381 行 | 维护灾难 | **TODO**: 拆分为独立 Partial Class 文件（Lobby/Gameplay/Meeting/Voting/UI/Bot） |
| 9 | 无自动化测试 | 依赖手工联机测试 | 可为关键流程添加 Unity Test Framework 集成测试 |

---

## 5. 代码质量问题

### 5.1 序列化脆弱性

所有网络消息通过 `FastBufferWriter` 手动 WriteValueSafe/ReadValueSafe，字段顺序必须严格一致。一旦修改 `BroadcastSnapshot` 或 `ReceiveServerSnapshot` 中任一字段新增/删除/调序，对端将读取错误数据且无编译期保护。

**建议**：引入 Message 结构体 + 统一序列化器，或迁移到 Netcode 内置 RPC。

### 5.2 OnGUI 与游戏逻辑耦合

OnGUI 直接在 `OnlineMatchController` 中绘制全部 UI（Lobby 面板、会议投票屏、行动 HUD、结果屏），约 2000+ 行。UI 与游戏逻辑紧耦合。

**建议**：将 OnGUI 提取为独立 UI 组件（`OnlineLobbyUI`、`OnlineHUDUI`、`OnlineMeetingUI`）。

### 5.3 Bot 与真人混用 players 字典

Bot 和真人在同一 `players` 字典中，通过 `IsBot` 字段区分。但 `RemoveMissingPlayers` 可能误移除 Bot（Bot 的 clientId 固定为 BotClientIdBase+index）。

**建议**：Bot 列表独立管理，或在 RemoveMissingPlayers 中排除 Bot ID 范围。

---

## 6. 测试准备就绪度

| 维度 | 就绪度 | 说明 |
|------|--------|------|
| 局域网 2 人 | 🟢 就绪 | `StartHost` + `StartClient` 链路完整，OnGUI 控件可用 |
| Relay 远程 2 人 | 🟡 待 Cloud 绑定 | 代码链路完整，需 Unity Dashboard 绑定 Cloud Project |
| 角色分配 | 🟢 就绪 | 2/5/8 人分别测试 |
| 任务同步 | 🟢 就绪 | 三步校验 + BroadcastSnapshot |
| 击杀同步 | 🟢 就绪 | KillRange + KillCooldown + body 同步 |
| 报告尸体 | 🟢 就绪 | ReportRange + BeginMeeting |
| 紧急会议 | 🟢 就绪 | 次数限制 + 冷却 + 通讯干扰互斥 |
| 会议投票 | 🟢 就绪 | ApplyVote → ResolveVotes → 淘汰同步 |
| 胜负判定 | 🟢 就绪 | 证据链/阵营全灭/Gang压制/超时 |
| 断线处理 | 🟡 仅基础 | 有移除逻辑但无重连 |
| Bot AI | 🟢 就绪 | 自动补齐 + 行为 + 投票 |

---

## 7. TODO 优先级清单

### 联机测试前必须完成

| # | TODO | 预估工作量 |
|---|------|-----------|
| T1 | 确认 Unity Dashboard Cloud Project 已绑定并可用 | 30min |
| T2 | 确认 DefaultNetworkPrefabs 包含所有 NetworkObject Prefab | 15min |
| T3 | 验证 `VotingSeconds=55s` 到期后强制 Skip 的逻辑（TickHostSimulation） | 代码审查 15min |

### Stage 11 建议完成

| # | TODO | 预估工作量 |
|---|------|-----------|
| T4 | 拆分 OnlineMatchController 为 Partial Class 文件 | 2h |
| T5 | 创建 NetworkManager Prefab（预设 Transport 配置） | 1h |
| T6 | Lobby UI 升级（UGUI/UI Toolkit） | 4h |
| T7 | 添加 RTT/Ping 显示 | 30min |
| T8 | Host Migration 基础实现 | 8h（高风险） |

---

## 8. 产出文件

| 文件 | 路径 |
|------|------|
| 联机测试清单 | `/Users/zhugehao/projects/GanglandUndercover/output/online_test_plan.md` |
| 审查报告（本文件） | `/Users/zhugehao/projects/GanglandUndercover/output/stage10_online_changes.md` |