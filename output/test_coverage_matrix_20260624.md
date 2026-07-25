# Gangland Undercover — 测试覆盖矩阵

> 日期: 2026-06-24 | 版本: v0.2.1-dev
> 更新: 2026-07-13 | 基于: CoreSystemTests.cs (130 EditMode tests) + 6 PlayMode 文件 (22 tests)

---

## 1. 总结

| 测试平台 | 结果 | 记录 |
| --- | --- | --- |
| EditMode | 130/130 PASS, 0 failed, 0 skipped | `Logs/codex-plan25a-core-editmode.xml` |
| PlayMode | 15/22 PASS, 0 failed, 7 ignored | `Logs/codex-plan25c-full-playmode.xml` |
| Relay 双进程 | Host PASS + Client PASS | `Logs/relay-host-results.xml` / `../GanglandUndercover_relayclient/Logs/relay-client-results.xml` |
| Relay migration 双进程 | Host PASS + Client PASS；旧 Host 重连新 Relay，候选 Client 接管为 replacement Host | `Logs/relay-host-results.xml` / `../GanglandUndercover_relayclient/Logs/relay-client-results.xml` |
| Relay migration 三进程 | Old Host PASS + Candidate PASS + Observer PASS；旧 Host 和 observer 均重连新 Relay，replacement Host 验证迁移后远端任务 RPC、会议和远端投票 named-message 连续性 | `Logs/relay-host-results.xml` / `../GanglandUndercover_relayclient/Logs/relay-client-results.xml` / `../GanglandUndercover_relayobserver/Logs/relay-observer-results.xml` |

Ignored 测试为 `RelayHost_PublishesCodeAndAcceptsPeer`、`RelayClient_JoinsHostByCode`、`RelayMigration_OldHostReconnectsToReplacementRelay`、`RelayMigration_ClientPromotesToReplacementRelayHost`、`RelayMigration_ThreeClientOldHostReconnectsToReplacementRelay`、`RelayMigration_ThreeClientCandidatePromotesAndRunsPostRestoreFlow` 和 `RelayMigration_ThreeClientObserverFollowsReplacementRelay`。这些需要通过 `GANGLAND_RELAY_ROLE=host/client/migration-host/migration-client/migration-host-threeclient/migration-candidate-threeclient/migration-observer-threeclient` 分进程运行，不属于普通单进程 PlayMode 全量回归失败。
2026-07-12 已通过 `bash run-relay-twoprocess.sh` 单独验证真实 Relay 双进程连接，并覆盖恶意 `RoleAssign` / `MapSelect` / `ServerSnapshot` / `ChatBroadcast` / lobby `ChatSend` 注入不改变 Host 状态；同轮覆盖 Client 通过真实 spawned `OnlineSecurityCamera.StartWatchingServerRpc` 发起越权观看请求，Host watcher 集合保持为空；并覆盖 Client 通过真实 server-owned `CharacterCustomizer` clone 伪造 `GanglandCharacterCustom`，Host 外观选择保持不变。
2026-07-13 已新增 `GANGLAND_RELAY_SCENARIO=migration bash run-relay-twoprocess.sh` 编排入口和两端 PlayMode 用例；脚本等待 Host 写出旧 Relay 码后再启动 Client，规避两进程同时抢 Unity Licensing/UPM。授权残留进程清理后实跑通过：旧 Host 以 `oldCode=HJNBQF` 创建旧 Relay，候选 Client 生成 `migrationCode=QNTMDW` 并接管为 replacement Host，旧 Host 断开旧 Relay 后成功重连新 Relay，replacement Host 端最终连接数为 2。
2026-07-13 追加 Host migration 对局连续性门禁：`SnapshotRestore_PreservesMeetingServiceAndAllowsVotingContinuation` 覆盖迁移快照恢复后保留 MeetingService 会议次数、当前会议原因、已投票状态，并允许剩余玩家继续投票结算；同时复跑 PlayMode `SnapshotRestore_RestoresGameplayStateDuringPlayModeLifecycle` 通过。
2026-07-13 追加 Host migration 3 客户端迁移后连续性门禁：`SnapshotRestore_ThreeClientPostMigrationTaskMeetingAndVotingFlow` 覆盖迁移恢复后 3 名玩家、半完成任务进度和证据链状态保留，并验证任务继续完成、发起会议、三人投票到结算的确定性流程；完整 `CoreSystemTests` 复跑 130/130 通过。
2026-07-13 追加真实 Relay 三进程 migration 采样：`GANGLAND_RELAY_SCENARIO=migration-threeclient bash run-relay-twoprocess.sh` 串行编排 old Host、candidate replacement Host、observer 三个独立 Unity 工程进程；通过 `oldCode=CQGM97`、`migrationCode=T6RJTJ` 验证旧 Host 和 observer 均从旧 Relay 迁到新 Relay，replacement Host 端连接数达到 3，并完成迁移后任务、会议、投票连续性断言。为抵抗云 Relay Join 抖动，observer 端 Join 旧/新 Relay 均带有界重试。
2026-07-13 追加 PLAN25-C 真实远端提交采样：三进程 migration 用例在 replacement Host 恢复快照后，由旧 Host 通过真实 `OnlineMiniGameBridge.SubmitTaskResultServerRpc` 完成任务 9，由旧 Host 和 observer 通过公开 `RequestVote` 进入 `GanglandClientAction` named-message 投票路径；实跑通过 `oldCode=KJDR9J`、`migrationCode=RPB99Q`，三端结果均 PASS。

---

## 2. 本轮新增门禁

| PLAN | 覆盖目标 | 关键测试/文件 | 状态 |
| --- | --- | --- | --- |
| PLAN8 | PlayMode 会议、尸体报案、快照恢复、断线释放回归 | `MeetingEvents_PublishDuringPlayModeEmergencyAndBodyReportPaths`, `SnapshotRestore_RestoresGameplayStateDuringPlayModeLifecycle`, `ClientDisconnect_ReleasesTaskLocksVotesAndKeepsBodyReportable` | PASS |
| PLAN9 | 恶意 Chat/ClientProfile/Camera/CharacterCustom 覆盖 | `CustomMessages_RejectMalformedAndSpoofedMessagesOverNetcode`, `CharacterCustomPayload_RejectsMalformedAndEmptyPayloads`, `CharacterCustom_AuthorizationRejectsUnspawnedOrNonOwnerSender`, `CameraAuthorization_RequiresActionAliveRangeOrRemoteSurveillance` | PASS |
| PLAN10 | 重连/Host 状态门禁 | `HostDisconnect_ShowsVisibleRecoveryGuidance`, `SnapshotService_RestoresPlayersTasksBodiesVotesAndTimers`, `SnapshotRestore_RestoresGameplayStateDuringPlayModeLifecycle` | PASS |
| PLAN11 | 6-10 人 Alpha pacing | `AlphaPacing_ProvidesPlayableSixEightTenPlayerEnvelope`, `BeginMeeting_UsesPlayerCountScaledDiscussionTimer` | PASS |
| PLAN12 | 完整回归和文档刷新 | Full EditMode + Full PlayMode + 本文件 + `KNOWN_ISSUES.md` + `DevelopmentProgress.zh-CN.md` | PASS |
| PLAN13 | 证据链 PlayMode 闭环 + Relay 恶意消息门禁 + 长局硬上限 guard | `EvidenceChain_TaskEvidenceFeedsMeetingDigestAndVoteClosure`, `TimeLimit_ControllerDoesNotResolveBeforeHardLimit`, `RelayHost_PublishesCodeAndAcceptsPeer`, `RelayClient_JoinsHostByCode` | PASS |
| PLAN14 | CharacterCustom 远端 sender/装扮 ID 真实性门禁 + 摄像头 request handler 门禁 | `CharacterCustom_RemoteAuthorizationPolicyAcceptsOnlyOwnerOnServerAndServerOnClient`, `CharacterCustom_ApplyCustomDataRejectsWrongPartIds`, `SecurityCamera_StartWatchingRequestMaintainsAuthorizedWatcherSet` | PASS |
| PLAN15 | Relay 真实双进程摄像头越权观看请求门禁 | `RelayHost_PublishesCodeAndAcceptsPeer`, `RelayClient_JoinsHostByCode` | PASS |
| PLAN16 | CharacterCustomizer 真实 NetworkPrefab 生成 + Relay 越权外观消息门禁 | `CharacterCustomizer_SpawnsOwnerObjectsAndRejectsNonOwnerPayload`, `RelayHost_PublishesCodeAndAcceptsPeer`, `RelayClient_JoinsHostByCode` | PASS |
| PLAN17 | Host migration 选举策略 + 无可接管 peer 降级结算 | `HostMigration_ElectsLowestRemainingClientAndExcludesOldHost`, `HostMigration_ClientHostDisconnectFallsBackWhenNoRemainingPeers` | PASS |
| PLAN18 | Host migration 候选存在性判定 guard | `HostMigration_TryElectionKeepsCandidateZeroDistinctFromNoCandidate`, Full EditMode, Full PlayMode | PASS |
| PLAN19 | Host migration replacement Host 启动门禁 + 直连接管启动 | `HostMigration_ReplacementHostStartPolicyBlocksUnsafePromotion`, `HostMigration_DirectReplacementHostStartsNetworkManager`, Full EditMode, Full PlayMode | PASS |
| PLAN20 | Relay Host migration 新 allocation 路由 | `HostMigration_RelayReplacementRouteDetectsOldRelayCode`, Full EditMode, Full PlayMode | PASS |
| PLAN21 | Relay Host migration discovery marker + 同房间候选选择 | `RelayMigrationLobbySessionOptions_CarryHostMigrationDiscoveryMarker`, `HostMigrationRelayCandidate_MatchesOnlyMarkedJoinableSameRoom`, Full EditMode, Full PlayMode | PASS |
| PLAN22 | Host migration Relay 候选显式重连入口 | `HostMigrationRelayRoomJoinIntent_AllowsOnlyDisconnectedMarkedSameRoom`, Full EditMode, Full PlayMode | PASS |
| PLAN23 | Relay migration 双进程编排入口 + 串行启动 guard | `RelayMigration_OldHostReconnectsToReplacementRelay`, `RelayMigration_ClientPromotesToReplacementRelayHost`, `GANGLAND_RELAY_SCENARIO=migration bash run-relay-twoprocess.sh` | PASS |
| PLAN24 | Host migration 快照恢复后的会议/投票连续性 | `SnapshotRestore_PreservesMeetingServiceAndAllowsVotingContinuation`, `SnapshotRestore_RestoresGameplayStateDuringPlayModeLifecycle` | PASS |
| PLAN25-A | Host migration 3 客户端任务/会议/投票连续性 | `SnapshotRestore_ThreeClientPostMigrationTaskMeetingAndVotingFlow`, Full EditMode | PASS |
| PLAN25-B | 真实 Relay 三进程 Host migration + 迁移后任务/会议/投票连续性 | `RelayMigration_ThreeClientOldHostReconnectsToReplacementRelay`, `RelayMigration_ThreeClientCandidatePromotesAndRunsPostRestoreFlow`, `RelayMigration_ThreeClientObserverFollowsReplacementRelay`, `GANGLAND_RELAY_SCENARIO=migration-threeclient bash run-relay-twoprocess.sh`, Full PlayMode | PASS |
| PLAN25-C | 真实 Relay 三进程 Host migration 后远端任务 RPC + 远端投票 named-message | `RelayMigration_ThreeClientOldHostReconnectsToReplacementRelay`, `RelayMigration_ThreeClientCandidatePromotesAndRunsPostRestoreFlow`, `RelayMigration_ThreeClientObserverFollowsReplacementRelay`, `GANGLAND_RELAY_SCENARIO=migration-threeclient GANGLAND_RELAY_TIMEOUT_SECONDS=900 bash run-relay-twoprocess.sh`, Full PlayMode | PASS |

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
| MatchLoopPlayTests | HostMigration_ClientHostDisconnectFallsBackWhenNoRemainingPeers | PASS | 非 Host 客户端检测 Host 断线且无剩余 peer 时降级结算 |
| MatchLoopPlayTests | HostMigration_DirectReplacementHostStartsNetworkManager | PASS | 直连旧连接已关闭时，replacement Host 真实启动 NetworkManager 并补齐核心 NetworkObjects |
| MatchLoopPlayTests | MeetingEvents_PublishDuringPlayModeEmergencyAndBodyReportPaths | PASS | 紧急会议和尸体报案事件发布 |
| MatchLoopPlayTests | SnapshotRestore_RestoresGameplayStateDuringPlayModeLifecycle | PASS | PlayMode 生命周期内快照恢复 |
| MatchLoopPlayTests | EvidenceChain_TaskEvidenceFeedsMeetingDigestAndVoteClosure | PASS | 任务取证、会议证据摘要、指证投票权重和胜负闭合 |
| MiniGameAuthorityPlayTests | MiniGameBridge_RejectsUnopenedTask_AndCompletesServerOpenedTaskOverRpc | PASS | 小游戏授权路径 |
| MiniGameOnlineIntegrationPlayTests | OnlineTasks_OpenRichMinigames_AndCompleteThroughServerPath | PASS | 小游戏打开与 Server 完成路径 |
| NetworkCustomMessagePlayTests | CustomMessages_RejectMalformedAndSpoofedMessagesOverNetcode | PASS | 自定义消息畸形/伪造拒绝 |
| NetworkCustomMessagePlayTests | CharacterCustomizer_SpawnsOwnerObjectsAndRejectsNonOwnerPayload | PASS | CharacterCustomizer NetworkPrefab 生成、owner clone 复制、非 owner 外观消息拒绝 |
| RelayTwoProcessPlayTests | RelayHost_PublishesCodeAndAcceptsPeer | Ignored | 需 `GANGLAND_RELAY_ROLE=host` |
| RelayTwoProcessPlayTests | RelayClient_JoinsHostByCode | Ignored | 需 `GANGLAND_RELAY_ROLE=client` |
| RelayTwoProcessPlayTests | RelayMigration_OldHostReconnectsToReplacementRelay | Ignored | 需 `GANGLAND_RELAY_ROLE=migration-host`，旧 Host 创建旧 Relay 后按 migration 新码重连 |
| RelayTwoProcessPlayTests | RelayMigration_ClientPromotesToReplacementRelayHost | Ignored | 需 `GANGLAND_RELAY_ROLE=migration-client`，旧 Client 接管并创建新 Relay allocation |
| RelayTwoProcessPlayTests | RelayMigration_ThreeClientOldHostReconnectsToReplacementRelay | Ignored | 需 `GANGLAND_RELAY_ROLE=migration-host-threeclient`，旧 Host 等候 candidate + observer 后重连新 Relay，并按 marker 通过真实 MiniGameBridge ServerRpc 提交任务、通过 RequestVote 提交投票 |
| RelayTwoProcessPlayTests | RelayMigration_ThreeClientCandidatePromotesAndRunsPostRestoreFlow | Ignored | 需 `GANGLAND_RELAY_ROLE=migration-candidate-threeclient`，candidate 接管新 Relay 并验证迁移后远端任务 RPC、会议、远端投票 named-message 连续性 |
| RelayTwoProcessPlayTests | RelayMigration_ThreeClientObserverFollowsReplacementRelay | Ignored | 需 `GANGLAND_RELAY_ROLE=migration-observer-threeclient`，observer 从旧 Relay 跟随迁移到新 Relay，Join 带有界重试，并通过 RequestVote 提交迁移后投票 |

---

## 4. EditMode 新增重点

| 测试方法 | 覆盖点 |
| --- | --- |
| AlphaPacing_ProvidesPlayableSixEightTenPlayerEnvelope | 6/8/10 人角色配比、任务量、证据目标、会议/投票/击杀/报案冷却和目标局长 |
| BeginMeeting_UsesPlayerCountScaledDiscussionTimer | 会议讨论时间随人数扩展 |
| TimeLimit_ControllerDoesNotResolveBeforeHardLimit | 控制器内部超时入口不会在 20 分钟硬上限前提前结算长局 |
| CharacterCustomPayload_RejectsMalformedAndEmptyPayloads | CharacterCustom malformed/empty payload 拒绝 |
| CharacterCustom_AuthorizationRejectsUnspawnedOrNonOwnerSender | 未 spawn 或非 owner 的 CharacterCustom sender 拒绝 |
| CharacterCustom_RemoteAuthorizationPolicyAcceptsOnlyOwnerOnServerAndServerOnClient | Server 只收 owner 提交，Client 只收 Server 广播，拒绝 peer 伪造 owner 广播 |
| CharacterCustom_ApplyCustomDataRejectsWrongPartIds | JSON 中装扮 ID 必须存在且匹配目标部位，防止错部位/伪造 ID 覆盖选择 |
| HostMigration_ElectsLowestRemainingClientAndExcludesOldHost | Host migration 选举策略排除旧 Host，并从剩余 clientId 中稳定选择最小值；无候选时返回无新 Host |
| HostMigration_TryElectionKeepsCandidateZeroDistinctFromNoCandidate | 选举 API 用 bool 区分“有候选”和候选 clientId，避免合法 clientId 0 被误当作无候选 |
| HostMigration_ReplacementHostStartPolicyBlocksUnsafePromotion | replacement Host 启动策略阻止复用 Relay 旧房间码和旧连接仍监听时的同步接管；已是 server/host 时允许收尾 |
| HostMigration_RelayReplacementRouteDetectsOldRelayCode | 旧 Relay 房间码存在时进入新 Relay allocation replacement Host 路由，不复用旧房间码直连接管 |
| RelayMigrationLobbySessionOptions_CarryHostMigrationDiscoveryMarker | migration Relay Lobby Session 携带 public `hostMigration=relay-replacement` 标记，供旧客户端发现新 Relay 房 |
| HostMigrationRelayCandidate_MatchesOnlyMarkedJoinableSameRoom | 旧客户端只接受带标记、可加入、同房间、非满员、非锁定、无密码的 Relay migration 候选 |
| HostMigrationRelayRoomJoinIntent_AllowsOnlyDisconnectedMarkedSameRoom | 旧客户端只有在断线恢复状态、选中同房间 Host migration 标记房时才构造重连意图，并清洗新 Relay code |
| SnapshotRestore_PreservesMeetingServiceAndAllowsVotingContinuation | Host migration 快照恢复后保留会议次数、会议原因、已投票状态，并允许剩余玩家继续投票结算 |
| SnapshotRestore_ThreeClientPostMigrationTaskMeetingAndVotingFlow | Host migration 3 客户端快照恢复后保留玩家、半完成任务和证据链，并允许任务、会议、投票连续推进到结算 |
| CameraAuthorization_RequiresActionAliveRangeOrRemoteSurveillance | 监控摄像头观看需要 Action、存活、距离或远程监控能力 |
| SecurityCamera_StartWatchingRequestMaintainsAuthorizedWatcherSet | 摄像头观看请求只维护已授权 watcher，越权或阶段变化会移除 watcher |
| SnapshotService_RestoresPlayersTasksBodiesVotesAndTimers | 快照恢复玩家、任务、尸体、投票和计时器 |

---

## 5. 联机安全边界

| 消息/入口 | 当前防护 | 验证 |
| --- | --- | --- |
| GanglandClientProfile | bounded UTF-8 写入/读取，超长截断，畸形 payload 忽略 | PlayMode `NetworkCustomMessagePlayTests` |
| GanglandChatSend | 结构化 payload，HTML 清洗，长度截断，频道路由校验 | EditMode + PlayMode `ChatChannelPlayTests` |
| CharacterCustom | payload 长度/格式校验，Server owner 校验，Client 仅接受 Server 广播，装扮 ID/部位匹配校验；OnlineMatchController 为每个 client 生成 owner CharacterCustomizer NetworkObject | EditMode + PlayMode + Relay 双进程真实 `GanglandCharacterCustom` |
| OnlineSecurityCamera | Action phase、alive、距离或远程监控能力校验，StartWatching request handler 只保留授权 watcher | EditMode + Relay 双进程真实 `StartWatchingServerRpc` |
| ServerSnapshot/RoleAssign/MapSelect | 非 server sender 和非法 enum/count 拒绝 | EditMode + PlayMode custom message test + Relay 双进程恶意注入 |
| ChatBroadcast/Lobby ChatSend | Client 伪造广播不污染 Host，Lobby 聊天发送不越权进入对局 | Relay 双进程恶意注入 |
| Task/Repair | active lock、距离、ServerRpc 完成路径校验；Host migration 后远端客户端仍可通过真实 MiniGameBridge ServerRpc 完成恢复任务 | EditMode + PlayMode MiniGameAuthority + Relay migration 三进程 |

---

## 6. 剩余缺口

| 缺口 | P级 | 下一步 |
| --- | --- | --- |
| Host migration 真实多客户端长流程 | P3 | 已覆盖确定性选举、候选存在性 guard、无剩余 peer 降级、直连 replacement Host 启动、Relay 新 allocation 路由、migration Lobby 标记、同房间候选选择、客户端半自动加入入口、真实 Relay 双进程接管/重连、快照恢复后继续会议/投票一致、确定性 3 客户端迁移后任务/会议/投票连续性、真实 Relay 三进程迁移后连续性采样，以及迁移后远端任务 RPC/远端投票 named-message；下一步扩成多任务、多轮会议和中途断线恢复的长流程采样 |
| 8-12 分钟完整局体验节奏 | P1 | 已有配置和硬上限 guard；下一步补真实人工/自动长局采样，记录任务完成率、会议次数和平均淘汰时间 |
| 正式角色动画和会议入座表现 | P1 | 接 Animator/正式 sprite 后补 PlayMode 视觉状态门禁 |
