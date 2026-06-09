# Gangland Undercover - 测试覆盖矩阵

> 日期: 2026-06-09 | 当前测试文件: 4 | 当前测试方法: 58

## 总览

| 类型 | 文件 | 测试数 | 最近验证状态 |
|------|------|--------|--------------|
| EditMode | `Assets/Tests/CoreSystemTests.cs` | 52 | 最近一次 52/52 通过 |
| PlayMode | `MatchLoopPlayTests.cs` | 3 | 最近一次 3/3 通过 |
| PlayMode | `MiniGameOnlineIntegrationPlayTests.cs` | 1 | 最近一次 1/1 通过 |
| PlayMode | `RelayTwoProcessPlayTests.cs` | 2 | 未设置 `GANGLAND_RELAY_ROLE` 时按设计 ignored |

最近完整验证口径：

- EditMode: 52/52 passed
- Smoke: `Gangland smoke tests passed`
- PlayMode: 4 passed / 2 Relay ignored / 0 failed

## EditMode 覆盖

| 覆盖域 | 代表测试 | 风险 |
|--------|----------|------|
| 房间/规则 | `EmergencyMeetingLimit_*`、`RoleDistribution_*`、`TotalTaskCount_*` | P0 |
| 时间和胜负 | `TimeLimit_*`、`Victory_*` | P0 |
| 聊天协议 | `DetermineChannel_*`、`Sanitize_*`、`Chat*Payload_*` | P1 |
| 角色自定义协议 | `CharacterCustomPayload_*` | P1 |
| 任务/修理越权 | `TaskCompletion_*`、`Repair*_*` | P0 |
| C->S 客户端状态边界 | `ClientState_*` | P0 |
| C->S 客户端动作边界 | `ClientAction_RejectsUndefinedActionValues` | P0 |
| S->C 快照边界 | `ServerSnapshot_*` | P0 |
| S->C 身份/地图消息边界 | `RoleAssign_*`、`MapSelect_*` | P0 |
| 新手引导/提示 | `OnboardingGuidance_*` | P2 |
| 2D 地图/资源加载 | `WorldBuilder_*`、`Sprite2DAssetCache_*` | P1 |
| 音效/UI 资源 | `AudioManager_*`、`OnlineMatchHud_*` | P1 |
| 视觉切片 | `MeetingOverlay_*`、`TaskOverlay_*`、`DownedStateCreatesKillSceneVfx` | P1 |

## PlayMode 覆盖

| 测试 | 覆盖风险点 | P级 |
|------|------------|-----|
| `FullMatchLoop_RunsThroughEveryPhaseAndRestarts` | Lobby -> Opening -> Action -> Meeting -> Voting -> Result -> Restart 全流程 | P0 |
| `Character2DAnimator_UpdatesLocalAndRemoteWalkFrames` | 本地/远端角色动画帧更新 | P1 |
| `ClientDisconnect_ReleasesTaskLocksVotesAndKeepsBodyReportable` | 断线释放任务锁/投票，尸体仍可报告 | P0 |
| `OnlineTasks_OpenRichMinigames_AndCompleteThroughServerPath` | 小游戏打开、完成、服务端路径广播 | P0 |
| `RelayHost_PublishesCodeAndAcceptsPeer` | Relay Host 发布房间码并接受对端 | P0 |
| `RelayClient_JoinsHostByCode` | Relay Client 通过房间码加入 | P0 |

## 已补强的联机消息边界

最近三个提交已覆盖：

| 消息 | 防护 | 当前测试层级 |
|------|------|--------------|
| `GanglandClientState` | 非有限位置/输入拒绝、输入 clamp、Action 阶段未知玩家拒绝、Ready 只能 Lobby 更新 | EditMode 反射路径 |
| `GanglandClientAction` | 未定义 action enum 拒绝 | EditMode 反射路径 |
| `GanglandServerSnapshot` | 只接受 Server sender、phase/count/critical task 校验、整包 staging 原子应用 | EditMode 反射路径 |
| `GanglandRoleAssign` | 只接受 Server sender、role enum 校验 | EditMode 反射路径 |
| `GanglandMapSelect` | 只接受 Server sender、map enum 校验 | EditMode 反射路径 |

## 当前最高缺口

| 缺口 | P级 | 建议文件/切片 |
|------|-----|---------------|
| 真实 NGO custom message 路径恶意消息测试 | P0 | `NetworkCustomMessagePlayTests.cs` |
| `MiniGameBridge` 运行时 AddComponent/ServerRpc 路径授权 | P0 | `MiniGameAuthorityPlayTests.cs` |
| `OnlineSecurityCamera` 运行时 prefab/spawn 路径 | P1 | `SecurityCameraNetworkTests.cs` |
| 主机迁移状态一致性 | P1 | `HostMigrationTests.cs` |
| 聊天频道路由 PlayMode 覆盖 | P1 | `ChatChannelPlayTests.cs` |
| 断线重连状态恢复 | P1 | `ReconnectTests.cs` |
| 破坏效果五类状态/视觉验证 | P1 | `SabotagePlayTests.cs` |

## 覆盖率结论

当前最强的是核心规则、资源加载、主循环和边界反射测试；最薄弱的是“真实 NGO 网络传输后状态不变/被拒绝”的 PlayMode 级验证。下一阶段应优先补一组最小 host/client custom-message PlayMode fixture，而不是继续堆反射测试。
