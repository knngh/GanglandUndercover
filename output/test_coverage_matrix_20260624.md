# Gangland Undercover — 测试覆盖矩阵

> 日期: 2026-06-24 | 版本: v0.2.1-dev
> 基于: CoreSystemTests.cs (115 EditMode tests) + 6 PlayMode 文件 (13 tests)

---

## 1. 总结

| 测试平台 | 结果 | 记录 |
| --- | --- | --- |
| EditMode | 115/115 PASS, 0 failed, 0 skipped | `ci-logs/plan12-editmode-full-results.xml` |
| PlayMode | 11/13 PASS, 0 failed, 2 ignored | `ci-logs/plan12-playmode-full-results.xml` |

Ignored 测试为 `RelayHost_PublishesCodeAndAcceptsPeer` 和 `RelayClient_JoinsHostByCode`。这两项需要通过 `GANGLAND_RELAY_ROLE=host/client` 分进程运行，不属于普通单进程 PlayMode 全量回归失败。

---

## 2. 本轮新增门禁

| PLAN | 覆盖目标 | 关键测试/文件 | 状态 |
| --- | --- | --- | --- |
| PLAN8 | PlayMode 会议、尸体报案、快照恢复、断线释放回归 | `MeetingEvents_PublishDuringPlayModeEmergencyAndBodyReportPaths`, `SnapshotRestore_RestoresGameplayStateDuringPlayModeLifecycle`, `ClientDisconnect_ReleasesTaskLocksVotesAndKeepsBodyReportable` | PASS |
| PLAN9 | 恶意 Chat/ClientProfile/Camera/CharacterCustom 覆盖 | `CustomMessages_RejectMalformedAndSpoofedMessagesOverNetcode`, `CharacterCustomPayload_RejectsMalformedAndEmptyPayloads`, `CharacterCustom_AuthorizationRejectsUnspawnedOrNonOwnerSender`, `CameraAuthorization_RequiresActionAliveRangeOrRemoteSurveillance` | PASS |
| PLAN10 | 重连/Host 状态门禁 | `HostDisconnect_ShowsVisibleRecoveryGuidance`, `SnapshotService_RestoresPlayersTasksBodiesVotesAndTimers`, `SnapshotRestore_RestoresGameplayStateDuringPlayModeLifecycle` | PASS |
| PLAN11 | 6-10 人 Alpha pacing | `AlphaPacing_ProvidesPlayableSixEightTenPlayerEnvelope`, `BeginMeeting_UsesPlayerCountScaledDiscussionTimer` | PASS |
| PLAN12 | 完整回归和文档刷新 | Full EditMode + Full PlayMode + 本文件 + `KNOWN_ISSUES.md` + `DevelopmentProgress.zh-CN.md` | PASS |

---

## 3. PlayMode 清单

| 文件 | 测试方法 | 状态 | 覆盖点 |
| --- | --- | --- | --- |
| ChatChannelPlayTests | ChatBroadcast_UpdatesRecipientCanvasHudFeedOverNetcode | PASS | 聊天广播到接收方 Canvas HUD |
| ChatChannelPlayTests | ChatChannels_RouteMeetingProximityAndGhostOverNetcode | PASS | 会议/近距/鬼魂频道路由 |
| MatchLoopPlayTests | FullMatchLoop_RunsThroughEveryPhaseAndRestarts | PASS | 完整局循环与重开 |
| MatchLoopPlayTests | Character2DAnimator_UpdatesLocalAndRemoteWalkFrames | PASS | 本地/远端行走帧同步 |
| MatchLoopPlayTests | ClientDisconnect_ReleasesTaskLocksVotesAndKeepsBodyReportable | PASS | 断线释放任务锁/投票并保留可报案尸体 |
| MatchLoopPlayTests | HostDisconnect_ShowsVisibleRecoveryGuidance | PASS | Host 断线可见恢复提示 |
| MatchLoopPlayTests | MeetingEvents_PublishDuringPlayModeEmergencyAndBodyReportPaths | PASS | 紧急会议和尸体报案事件发布 |
| MatchLoopPlayTests | SnapshotRestore_RestoresGameplayStateDuringPlayModeLifecycle | PASS | PlayMode 生命周期内快照恢复 |
| MiniGameAuthorityPlayTests | MiniGameBridge_RejectsUnopenedTask_AndCompletesServerOpenedTaskOverRpc | PASS | 小游戏授权路径 |
| MiniGameOnlineIntegrationPlayTests | OnlineTasks_OpenRichMinigames_AndCompleteThroughServerPath | PASS | 小游戏打开与 Server 完成路径 |
| NetworkCustomMessagePlayTests | CustomMessages_RejectMalformedAndSpoofedMessagesOverNetcode | PASS | 自定义消息畸形/伪造拒绝 |
| RelayTwoProcessPlayTests | RelayHost_PublishesCodeAndAcceptsPeer | Ignored | 需 `GANGLAND_RELAY_ROLE=host` |
| RelayTwoProcessPlayTests | RelayClient_JoinsHostByCode | Ignored | 需 `GANGLAND_RELAY_ROLE=client` |

---

## 4. EditMode 新增重点

| 测试方法 | 覆盖点 |
| --- | --- |
| AlphaPacing_ProvidesPlayableSixEightTenPlayerEnvelope | 6/8/10 人角色配比、任务量、证据目标、会议/投票/击杀/报案冷却和目标局长 |
| BeginMeeting_UsesPlayerCountScaledDiscussionTimer | 会议讨论时间随人数扩展 |
| CharacterCustomPayload_RejectsMalformedAndEmptyPayloads | CharacterCustom malformed/empty payload 拒绝 |
| CharacterCustom_AuthorizationRejectsUnspawnedOrNonOwnerSender | 未 spawn 或非 owner 的 CharacterCustom sender 拒绝 |
| CameraAuthorization_RequiresActionAliveRangeOrRemoteSurveillance | 监控摄像头观看需要 Action、存活、距离或远程监控能力 |
| SnapshotService_RestoresPlayersTasksBodiesVotesAndTimers | 快照恢复玩家、任务、尸体、投票和计时器 |

---

## 5. 联机安全边界

| 消息/入口 | 当前防护 | 验证 |
| --- | --- | --- |
| GanglandClientProfile | bounded UTF-8 写入/读取，超长截断，畸形 payload 忽略 | PlayMode `NetworkCustomMessagePlayTests` |
| GanglandChatSend | 结构化 payload，HTML 清洗，长度截断，频道路由校验 | EditMode + PlayMode `ChatChannelPlayTests` |
| CharacterCustom | payload 长度/格式校验，owner 校验，非法 sender 拒绝 | EditMode |
| OnlineSecurityCamera | Action phase、alive、距离或远程监控能力校验 | EditMode |
| ServerSnapshot/RoleAssign/MapSelect | 非 server sender 和非法 enum/count 拒绝 | EditMode + PlayMode custom message test |
| Task/Repair | active lock、距离、ServerRpc 完成路径校验 | EditMode + PlayMode MiniGameAuthority |

---

## 6. 剩余缺口

| 缺口 | P级 | 下一步 |
| --- | --- | --- |
| Relay 真双进程恶意 CharacterCustom/Chat/Camera 注入 | P2 | 扩展 `run-relay-twoprocess.sh` 或新增双进程测试角色 |
| 真实多客户端 Host migration election | P3 | 断开原 Host 后验证新 Host 接管、快照恢复和会议/投票继续一致 |
| 8-12 分钟完整局体验节奏 | P1 | 基于 6/8/10 人 pacing 门禁做人工或自动长局验证 |
| 正式角色动画和会议入座表现 | P1 | 接 Animator/正式 sprite 后补 PlayMode 视觉状态门禁 |
| 完整证据链 PlayMode 路径 | P1 | 新增 EvidenceChain PlayMode 场景，覆盖证据收集、会议呈现和胜负闭合 |
