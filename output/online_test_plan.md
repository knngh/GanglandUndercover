# GanglandUndercover 联机测试清单

> 生成日期：2026-06-02 | 对应代码：OnlineMatchController.cs (12381行) + 6个子系统

---

## 一、测试环境

| 项目 | 配置 |
|------|------|
| 引擎 | Unity 6 + Netcode for GameObjects |
| Transport | Unity Transport (UTP) |
| Relay | Unity Relay Services（匿名认证） |
| 测试拓扑 | 场景A：局域网 2台 | 场景B：Relay 远程 2台 |
| Host 角色 | 由先启动/创建房间的客户端担任（Host-Authoritative） |
| 同步频率 | 12.5Hz（BroadcastSnapshot），CustomMessagingManager 命名消息 |

---

## 二、测试用例

### TC-01：Unity Services 初始化

| 项目 | 内容 |
|------|------|
| **前置** | 启动游戏，等待 Awake 完成 |
| **步骤** | 1. 打开 Unity 启动游戏 2. 观察 OnGUI 顶部状态栏 |
| **预期** | 显示 "Cloud OK | Services OK | Auth OK | Lobby OK | Relay OK \| Vivox 已移除" |
| **验证** | 检查 Console 日志，确认 UnityServiceBootstrap.InitializeAsync 无异常；若 Cloud Project 未绑定则预期显示警告 |
| **风险** | Cloud Project ID 未配置会导致 Relay/Lobby 不可用，仅能局域网直连 |

---

### TC-02：局域网 Host 创建房间

| 项目 | 内容 |
|------|------|
| **前置** | TC-01 通过 |
| **步骤** | 1. Host 端点击 OnGUI 中的 "Start Host" 按钮（或输入房间名后启动） 2. 检查 NetworkManager 日志 |
| **预期** | NetworkManager.StartHost() 成功；OnGUI 显示 "Host — Lobby"；房间名显示在状态栏 |
| **验证** | Console 中确认 `NetworkManager.StartHost()` 无报错；OnGUI 玩家列表中出现 Host 自身（clientId=0） |
| **代码路径** | `StartHost()` → `ConfigureTransport("0.0.0.0")` → `networkManager.StartHost()` |

---

### TC-03：局域网 Client 加入房间

| 项目 | 内容 |
|------|------|
| **前置** | TC-02 Host 已运行，Client 与 Host 在同一局域网 |
| **步骤** | 1. Client 端在 OnGUI 输入 Host 局域网 IP 地址 2. 点击 "Start Client" |
| **预期** | Client 连接成功；OnGUI 玩家列表中出现两个玩家；Client 状态同步 Host 的快照 |
| **验证** | Host 端 Console 显示 `HandleClientConnected` 日志；Client 端 OnGUI 显示 "同步在线局：Lobby。" |
| **代码路径** | `StartClient()` → `ConfigureTransport(address)` → `networkManager.StartClient()` → `HandleClientConnected` 创建 OnlinePlayerState |

---

### TC-04：Relay Host 创建房间

| 项目 | 内容 |
|------|------|
| **前置** | TC-01 通过，Cloud Project 已绑定 |
| **步骤** | 1. Host 端点击 "Start Relay Host" 2. 获取加入码（Join Code） |
| **预期** | Relay 分配成功，OnGUI 显示加入码；NetworkManager.StartHost() 基于 Relay 启动 |
| **验证** | Console 确认 `RelayService.Instance.CreateAllocationAsync` 成功；加入码不为空 |
| **代码路径** | `StartRelayHost()` → `RelayService.Instance.CreateAllocationAsync()` → `GetJoinCodeAsync()` → `SetRelayServerData` → `StartHost()` |

---

### TC-05：Relay Client 加入房间

| 项目 | 内容 |
|------|------|
| **前置** | TC-04 Relay Host 已运行 |
| **步骤** | 1. Client 端输入 Join Code 2. 点击 "Join Relay" |
| **预期** | Client 通过 Relay 连接到 Host；OnGUI 玩家列表更新 |
| **验证** | Console 确认 `RelayService.Instance.JoinAllocationAsync(joinCode)` 成功 |
| **代码路径** | `StartRelayClient()` → `JoinAllocationAsync(joinCode)` → `SetRelayServerData` → `networkManager.StartClient()` |

---

### TC-06：角色分配

| 项目 | 内容 |
|------|------|
| **前置** | 至少 2 名玩家已连接（不含 Bot），Host 点击开始 |
| **步骤** | 1. Host 点击 OnGUI 的 "Start Match" 2. 等待 Opening 阶段结束 |
| **预期** | 2人时：1 Gang + 1 Police；5人时：1 Gang + 1 Undercover + 3 Police；8人时：2 Gang + 1 Undercover + 5 Police。每位客户端通过命名消息 `RoleAssignMessage` 收到私密角色 |
| **验证** | Host Console 确认 `AssignRoles` 被调用；各客户端 OnGUI 显示 "收到身份：Police/Undercover/Gang"；公众角色（PublicRole）在 Opening 阶段为 Unassigned |
| **代码路径** | `StartOnlineMatchCore()` → `AssignRoles(ids)` → Shuffle → 按位置分配 → `SendRole(clientId, role)` → `ReceiveRoleAssign` |

---

### TC-07：任务同步

| 项目 | 内容 |
|------|------|
| **前置** | 游戏进入 Action 阶段 |
| **步骤** | 1. 非 Gang 玩家移动到任务点 2. 按 E 键进入任务面板 3. 按 1/2/3 组合键完成三步校验 4. 按 Space 蓄力 |
| **预期** | 任务进度在所有客户端实时同步（通过 BroadcastSnapshot）；完成后 evidenceScore+1、caseLog 更新 |
| **验证** | 两个客户端同时查看同一任务状态（completed/progress）；caseLog 中记录 "XXX 完成，证据链推进" |
| **代码路径** | `ReadActiveTaskInput()` → `CompleteActiveTask()` → `SendClientAction(Interact)` → `TryInteractWithTask()` → `BroadcastSnapshot()` |

---

### TC-08：击杀同步

| 项目 | 内容 |
|------|------|
| **前置** | Gang 玩家与目标距离 ≤ KillRange(0.9)，击杀冷却结束 |
| **步骤** | 1. Gang 玩家移动到非 Gang 玩家附近 2. 按 Q 键 |
| **预期** | 目标 Aliva→false；bodies 列表新增尸体；killCooldown 设为 34s；caseLog 记录 "黑帮击倒了 XXX"；BroadcastSnapshot 同步到所有客户端 |
| **验证** | Client 端 OnGUI 中受害者标记为死亡（灰色/红色）；公共角色仍为 Unassigned |
| **代码路径** | `TryKill()` → `victim.Alive=false` → `bodies.Add(...)` → `killCooldowns[sender]=34` → `EvaluateWinConditions()` → `BroadcastSnapshot()` |

---

### TC-09：报告尸体

| 项目 | 内容 |
|------|------|
| **前置** | 地图上存在未被报告的尸体（Reported=false） |
| **步骤** | 1. 存活玩家移动到尸体旁（距离 ≤ ReportRange=1.25） 2. 按 R 键 |
| **预期** | 尸体标记为 Reported=true；会议开始（phase=Meeting）；会议原因显示 "XXX 发现尸体并报案"；所有存活玩家移动到会议座位 |
| **验证** | 两个客户端同时进入 Meeting 界面；投票面板出现；caseLog 记录 |
| **代码路径** | `TryReportOrEmergency()` → `TryFindNearestBody()` → `BeginMeeting(reason)` → `BroadcastSnapshot()` → `ReceiveServerSnapshot()` 同步 phase=Meeting |

---

### TC-10：紧急会议

| 项目 | 内容 |
|------|------|
| **前置** | 行动阶段，emergencyMeetingsLeft > 0，无通讯干扰，紧急冷却结束 |
| **步骤** | 1. 存活玩家移动到地图零点（指挥区） 2. 按 R 键 |
| **预期** | emergencyMeetingsLeft-1；会议开始，原因显示 "XXX 按下警署紧急铃" |
| **验证** | 同 TC-09 验证方式；确认紧急会议次数递减 |
| **代码路径** | `TryReportOrEmergency()` → 无尸体分支 → 距离检测 → `BeginMeeting()` |

---

### TC-11：会议投票

| 项目 | 内容 |
|------|------|
| **前置** | 会议阶段，至少 2 名存活玩家 |
| **步骤** | 1. 等待 MeetingIntro(35s) 结束进入 Voting 2. 各玩家在 OnGUI 投票面板选择目标（或 Skip） |
| **预期** | 投票在两种场景同步：① Host 本地投票直接 ApplyVote；② Client 通过 SendClientAction(Vote) 发送到 Host → ApplyVote → BroadcastSnapshot；全部存活玩家投票后自动转入 ResolveVotes |
| **验证** | OnGUI 投票计数实时更新；票型在所有客户端一致 |
| **代码路径** | `ApplyVote()` → `votes[voter]=target` → 全员投票 → `ResolveVotes()` |

---

### TC-12：淘汰同步

| 项目 | 内容 |
|------|------|
| **前置** | 投票完成，最高票者被选出（非平局、非 Skip） |
| **步骤** | 1. 观察被淘汰玩家的 OnGUI 状态 |
| **预期** | 被淘汰者 Alive=false、位置重置、Input=zero；若 revealRoleOnEject=true，则 PublicRole 公开；淘汰者 Client 端显示角色并转为幽灵视角 |
| **验证** | 被淘汰客户端 OnGUI 显示 "已出局"；存活客户端玩家列表标记为死亡 |
| **代码路径** | `ResolveVotes()` → `ejected.Alive=false` → `RemoveReportedBodies()` → `EvaluateWinConditions()` → `BroadcastSnapshot()` |

---

### TC-13：胜负判定 — 证据链闭合

| 项目 | 内容 |
|------|------|
| **前置** | 警方玩家完成足够任务，evidenceScore ≥ evidenceTarget |
| **步骤** | 1. 持续完成任务直至 evidenceScore 达标 |
| **预期** | phase=Result；status="警方胜利：证据链闭合。"；所有玩家 PublicRole 公开；resultSummary 生成 |
| **验证** | 两个客户端同时显示结果屏幕；caseLog 记录胜利信息 |
| **代码路径** | `EvaluateWinConditions()` → `evidenceScore >= evidenceTarget` → `SetResult("警方胜利：证据链闭合。")` → `BroadcastSnapshot()` |

---

### TC-14：胜负判定 — 阵营全灭

| 项目 | 内容 |
|------|------|
| **前置** | 所有 Gang 玩家死亡（aliveGang==0 且 players≥2） |
| **步骤** | 1. 通过会议投票淘汰所有 Gang 成员 |
| **预期** | phase=Result；status="警方胜利：黑帮全部出局。" |
| **验证** | 同 TC-13 |
| **代码路径** | `EvaluateWinConditions()` → `aliveGang==0` → `SetResult(...)` |

---

### TC-15：胜负判定 — Gang 人数压制

| 项目 | 内容 |
|------|------|
| **前置** | aliveGang > 0 且 (aliveNonGang==0 或 Gang ≥ NonGang) |
| **步骤** | Gang 击杀足够多非 Gang 玩家 |
| **预期** | phase=Result；status="黑帮胜利：港区控制权失守。" |
| **验证** | 同 TC-13 |
| **代码路径** | `EvaluateWinConditions()` → `aliveGang>=aliveNonGang` → `SetResult(...)` |

---

### TC-16：超时判定

| 项目 | 内容 |
|------|------|
| **前置** | 游戏时长达到 MatchHardLimitSeconds(1200s=20min)，胜负未分 |
| **步骤** | 1. 持续游戏至 20 分钟（可调低常量加速测试） |
| **预期** | evidenceScore ≥ 82% target → 警方胜利；否则 → 黑帮胜利 |
| **验证** | 同 TC-13 |
| **代码路径** | `TickHostSimulation()` → `matchElapsedSeconds >= MatchHardLimitSeconds` → `ResolveTimeLimitOutcome()` |

---

### TC-17：客户端断线

| 项目 | 内容 |
|------|------|
| **前置** | 游戏中某 Client 强制关闭 |
| **步骤** | 1. 强制退出 Client 进程 2. Host 继续运行 10 秒 |
| **预期** | Host Console 记录 OnClientDisconnectCallback；该玩家在下一个 BroadcastSnapshot 中被 RemoveMissingPlayers 移除 |
| **验证** | Host 玩家列表不再显示断线玩家；游戏继续（断线玩家被视为永久离开） |
| **风险** | ⚠️ 无重连机制，断线玩家无法重新加入当前对局 |

---

### TC-18：破坏系统同步

| 项目 | 内容 |
|------|------|
| **前置** | Gang 玩家接近任务点 |
| **步骤** | 1. Gang 玩家按 E 破坏任务 2. 观察破坏效果 |
| **预期** | Blackout(28s) → 所有客户端屏幕变暗；Lockdown(32s) → 状态栏提示封锁；Communications → 紧急会议禁用 |
| **验证** | 两个客户端同时观察 blackoutTimer/lockdownTimer/communicationJamTimer 变化（通过 BroadcastSnapshot 同步） |
| **代码路径** | `TryInteractWithTask(Gang)` → `ApplySabotageEffect(type)` → `BroadcastSnapshot()` |

---

### TC-19：职业技能同步

| 项目 | 内容 |
|------|------|
| **前置** | 不同职业的玩家，能力冷却结束 |
| **步骤** | 1. 按 F 键使用职业技能 |
| **预期** | Inspector → 标记最高嫌疑；Forensics → evidenceScore+1；Tech → 修复一次破坏；Enforcer → 击杀冷却缩短；Fixer → 修复两次破坏+证据受损；Driver → 换位 |
| **验证** | HUD 显示技能冷却（abilityCooldown）；evidenceScore 变化同步 |
| **代码路径** | `TryUseProfessionAbility()` → 按 Profession 分发 → `BroadcastSnapshot()` |

---

### TC-20：AI Bot 在联机中的行为

| 项目 | 内容 |
|------|------|
| **前置** | roomAutoFillAi=true，人数不足时自动补齐 |
| **步骤** | 1. 2 名真人 + roomMinPlayers=4 → Host 启动后自动补齐 2 Bot |
| **预期** | Bot 显示 "AI-xxx" 名称；Bot 自动移动、做任务/破坏、投票（随机延迟 1.2-4.5s）；Bot 可能在冷却结束自动击杀附近目标 |
| **验证** | Host Console 中 Bot 的 clientId ≥ BotClientIdBase；Bot 投票出现在 ResolveVotes 票型中 |
| **代码路径** | `EnsureMinimumBots()` → `TickBotAction()` → `TickBotVoting()` |

---

## 三、已知风险与缺失项

| # | 风险项 | 严重度 | 说明 |
|---|--------|--------|------|
| 1 | **无 Host Migration** | 🔴 高 | Host 断开后游戏直接终止，所有 Client 掉线 |
| 2 | **无断线重连** | 🔴 高 | Client 断线后无法重新加入同一局 |
| 3 | **无 NetworkManager Prefab** | 🟡 中 | NetworkManager 在 EnsureNetworkStack() 中动态创建，场景中无预设，排查困难 |
| 4 | **Vivox 语音已移除** | 🟡 中 | 所有语音路由代码为 Stub，联机无语音交流 |
| 5 | **Custom Messaging 无类型安全** | 🟡 中 | 全部使用 FastBufferWriter 手动序列化，字段顺序错误会导致数据错乱（无编译期保护） |
| 6 | **无延迟/Ping 显示** | 🟢 低 | 联机体验缺乏网络诊断，排查丢包困难 |
| 7 | **单文件 12000+ 行** | 🟢 低 | OnlineMatchController.cs 极度臃肿，Review/调试困难 |
| 8 | **无 Lobby UI** | 🟡 中 | 房间列表、踢人、准备状态等全部依赖 OnGUI 基础控件 |
| 9 | **Relay 依赖 Cloud Project** | 🟡 中 | 局域网测试正常但 Relay 需要 Unity Dashboard 配置项目绑定 |

---

## 四、测试执行记录模板

| 用例 | 场景 | 结果 | 备注 | 日期 |
|------|------|------|------|------|
| TC-01 | LAN/Relay | ⬜ 待测 | | |
| TC-02 | LAN | ⬜ 待测 | | |
| TC-03 | LAN | ⬜ 待测 | | |
| TC-04 | Relay | ⬜ 待测 | | |
| TC-05 | Relay | ⬜ 待测 | | |
| TC-06 | LAN | ⬜ 待测 | | |
| TC-07 | LAN | ⬜ 待测 | | |
| TC-08 | LAN | ⬜ 待测 | | |
| TC-09 | LAN | ⬜ 待测 | | |
| TC-10 | LAN | ⬜ 待测 | | |
| TC-11 | LAN | ⬜ 待测 | | |
| TC-12 | LAN | ⬜ 待测 | | |
| TC-13 | LAN | ⬜ 待测 | | |
| TC-14 | LAN | ⬜ 待测 | | |
| TC-15 | LAN | ⬜ 待测 | | |
| TC-16 | LAN | ⬜ 待测 | | |
| TC-17 | LAN | ⬜ 待测 | | |
| TC-18 | LAN | ⬜ 待测 | | |
| TC-19 | LAN | ⬜ 待测 | | |
| TC-20 | LAN | ⬜ 待测 | | |

> 图例：⬜ 待测 | ✅ 通过 | ❌ 失败 | ⚠️ 部分通过