# Gangland Undercover — 测试覆盖矩阵

> 日期: 2026-06-09 | 最后更新: 2026-06-09 v3 | 版本: v0.2.0-dev
> 基于: CoreSystemTests.cs (76 [Test]) + 5 PlayMode 文件 (8 tests)

---

## 1. EditMode 测试清单（CoreSystemTests.cs）

> 总计: **76/76 passed**, 0 failed, 0 skipped

### 1.1 房间规则与角色分发（7）

| # | 测试方法 | 覆盖域 | P级 |
|---|---------|--------|-----|
| 1 | EmergencyMeetingLimit_ClampsWithinRange | 会议次数上限 | P0 |
| 2 | EmergencyMeetingLimit_FloorIsOne | 会议次数下限 | P0 |
| 3 | RoleDistribution_5Players_Returns1Gang1Undercover | 5 人角色 | P0 |
| 4 | RoleDistribution_8Players_Returns2Gang1Undercover1Mole | 8 人角色 | P0 |
| 5 | RoleDistribution_10Players_Returns3Gang2Undercover1Mole | 10 人角色 | P0 |
| 6 | RoleDistribution_OutOfRange_UsesNearestPreset | 超范围角色 | P0 |
| 7 | TotalTaskCount_AndEvidenceTarget_ScaleByPlayerCount | 任务+证据缩放 | P0 |

### 1.2 时间与胜负（10）

| # | 测试方法 | 覆盖域 | P级 |
|---|---------|--------|-----|
| 8 | TimeLimit_NotReached_ReturnsFalse | 超时未触发 | P0 |
| 9 | TimeLimit_EvidenceHigh_PoliceWins | 超时证据高 | P0 |
| 10 | TimeLimit_EvidenceLow_GangWins | 超时证据低 | P0 |
| 11 | TimeLimit_TasksHigh_PoliceWins | 超时任务高 | P0 |
| 12 | Victory_EvidenceClosure_PoliceWins | 证据闭合胜 | P0 |
| 13 | Victory_AllGangDead_PoliceWins | 黑帮全灭胜 | P0 |
| 14 | Victory_GangOutnumber_GangWins | 黑帮占多胜 | P0 |
| 15 | Victory_NoChange_WhenBalanced | 平衡无胜 | P0 |
| 16 | Victory_UndercoverSoloWins | 卧底独胜 | P0 |
| 17 | Victory_NotStarted_ReturnsNoChange | 未开局无胜 | P1 |

### 1.3 聊天频道路由与安全（7）

| # | 测试方法 | 覆盖域 | P级 |
|---|---------|--------|-----|
| 18 | DetermineChannel_DeadPlayer_Ghost | 死亡→鬼魂频道 | P0 |
| 19 | DetermineChannel_AliveMeetingAndAction | 存活→会议/行动频道 | P0 |
| 20 | Sanitize_RemovesTagsAndTruncates | 聊天清洗 | P0 |
| 21 | ChatSystem_BlocksMessagesFromMutedSender | 静音发消息被阻 | P1 |
| 22 | ChatSystem_UnblockSenderRestoresMessages | 解静音恢复消息 | P1 |
| 23 | ChatSystem_BlockLatestSenderBlocksFollowUpMessages | 封堵最近发送者 | P1 |
| 24 | ChatSystem_ReportLatestMessageStoresSanitizedSnapshot | 举报消息存储清洗快照 | P1 |

### 1.4 主菜单与会话初始化（3）🆕

| # | 测试方法 | 覆盖域 | P级 |
|---|---------|--------|-----|
| 25 | MainMenuLoginStatus_NoServiceExplainsAnonymousInitialization | 无服务时的匿名初始化说明 | P1 |
| 26 | MainMenuSettingsStatus_UsesCurrentSettingsValues | 设置状态行反映当前值 | P1 |
| 27 | MainMenuPlayerNameInput_TrimsFallbackAndCapsLength | 玩家代号输入限制+回退 | P1 |

### 1.5 Relay/Lobby/房间浏览器（17）

| # | 测试方法 | 覆盖域 | P级 |
|---|---------|--------|-----|
| 28 | RelayLobbySummary_EmptyStateGuidesCreateOrJoin | 空状态引导 | P0 |
| 29 | RelayLobbySummary_InputGuidesRelayJoin | 输入引导加入 | P0 |
| 30 | RelayLobbySummary_HostShowsShareCodeAndConnectedCount | Host 码+人数 | P0 |
| 31 | RelayLobbySummary_OperationInProgressShowsJoinTarget | 操作中提示 | P0 |
| 32 | LobbyBrowserSummary_RefreshInProgressMentionsRoomCount | 刷新中计数 | P0 |
| 33 | LobbyBrowserSummary_EmptyStateGuidesRelayFallback | 空列表 fallback | P0 |
| 34 | LobbyRoomLine_ShowsJoinableRelayCodeAndRules | 房间行显示 | P0 |
| 35 | LobbyRoomLine_MissingRelayCodeIsVisibleButNotJoinable | 无码房间可见不可加 | P0 |
| 36 | LobbySessionProperties_MatchBrowserQueryIndex | 属性匹配索引 | P0 |
| 37 | RelayLobbySessionOptions_ArePublicSearchableAndClamped | 选项公共可搜 | P0 |
| 38 | LobbyPublishStatus_ShowsProgressAndSessionCode | 发布状态 | P0 |
| 39 | LobbyRoomSessionJoin_CanUseSessionWhenIdAndRelayCodeExist | Session+码加入 | P0 |
| 40 | LobbyRoomSessionJoin_FallsBackForLocalPreviewOrMissingSessionId | 本地预览 fallback | P0 |
| 41 | LobbyRoomSessionJoin_BlocksLockedRooms | **锁定房间拒绝** | P0 |
| 42 | LobbyRoomSessionJoin_BlocksPasswordRooms | **密码房间拒绝** | P0 |
| 43 | LobbyRoomSessionJoin_BlocksFullRooms | **满员房间拒绝** | P0 |
| 44 | LobbyRoomSessionJoin_BlocksMissingRelayCode | **无码房间拒绝** | P0 |

### 1.6 消息序列化协议（4）

| # | 测试方法 | 覆盖域 | P级 |
|---|---------|--------|-----|
| 45 | ChatSendPayload_RoundTripsContentWithPipes | 聊天发送序列化 | P1 |
| 46 | ChatBroadcastPayload_RoundTripsContentWithPipes | 聊天广播序列化 | P1 |
| 47 | CharacterCustomPayload_RoundTripsObjectIdAndJson | 角色自定义序列化 | P1 |
| 48 | CharacterCustomPayload_RejectsOversizedJson | 超大 JSON 拒绝 | P1 |

### 1.7 任务与修理授权（4）

| # | 测试方法 | 覆盖域 | P级 |
|---|---------|--------|-----|
| 49 | TaskCompletion_RejectsDirectSubmitWithoutActiveLock | 无锁提交拒绝 | P0 |
| 50 | TaskCompletion_RejectsDirectSubmitOutsideRange | 超范围提交拒绝 | P0 |
| 51 | RepairStart_RejectsSabotagedTaskOutsideRange | 超范围修理拒绝 | P0 |
| 52 | RepairCompletion_RejectsDirectSubmitWithoutActiveLock | 无锁修理完成拒绝 | P0 |

### 1.8 联机消息边界验证（9）

| # | 测试方法 | 覆盖域 | P级 |
|---|---------|--------|-----|
| 53 | ClientState_RejectsNonFinitePositionAndInput | 非有限位置/输入拒绝 | P0 |
| 54 | ClientState_ClampsActionInputAndIgnoresReadyChangesDuringMatch | 输入 clamp + Action 阶段忽略 Ready | P0 |
| 55 | ClientState_DoesNotSpawnUnknownPlayersAfterActionStarts | Action 后不新增未知玩家 | P0 |
| 56 | ClientAction_RejectsUndefinedActionValues | 未定义 action 拒绝 | P0 |
| 57 | ServerSnapshot_IgnoresNonServerSender | 非 Server 发送者忽略 | P0 |
| 58 | ServerSnapshot_RejectsInvalidPhaseAndCounts | 无效 phase/计数拒绝 | P0 |
| 59 | ServerSnapshot_DoesNotPartiallyApplyWhenLaterCountsAreInvalid | 后续无效时不部分应用 | P0 |
| 60 | RoleAssign_IgnoresNonServerSenderAndUndefinedRoles | 非 Server + 未定义 role 拒绝 | P0 |
| 61 | MapSelect_IgnoresUndefinedMapType | 未定义 map 拒绝 | P0 |

### 1.9 新手引导（2）

| # | 测试方法 | 覆盖域 | P级 |
|---|---------|--------|-----|
| 62 | OnboardingGuidance_LobbyShowsIdentityObjectiveAndActionPrompt | Lobby 引导 | P1 |
| 63 | OnboardingGuidance_GangActionPromptsSabotageAndVotingCover | 黑帮行动提示 | P1 |

### 1.10 世界构建与资产管线（9）

| # | 测试方法 | 覆盖域 | P级 |
|---|---------|--------|-----|
| 64 | DistrictMap_UsesOperationalLightingInsteadOfRandomNeonSpots | 区域照明 | P1 |
| 65 | WorldBuilder_LoadsCuratedLimeZuTilesBeforeFallbackTiles | LimeZu tile 优先 | P1 |
| 66 | WorldBuilder_FirstScreenUsesLimeZuRuntimeSprites | 首屏 LimeZu 精灵 | P1 |
| 67 | WorldBuilder_TaskStationsUseLimeZuRuntimeSprites | 任务站 LimeZu 精灵 | P1 |
| 68 | WorldBuilder_AddsReadableLandmarksAndTaskEventFeedback | 地标+任务反馈 | P1 |
| 69 | WorldBuilder_UsesCuratedLimeZuRoomPropsAndBlackoutVfx | 房间 props+断电 Vfx | P1 |
| 70 | Sprite2DAssetCache_LoadsCuratedLimeZuSpritesByExplicitPath | 精灵缓存路径加载 | P1 |
| 71 | AudioManager_HasCuratedKenneyRuntimeSfxForEveryGameplayCue | 音效 cue 全覆盖 | P1 |
| 72 | OnlineMatchController_MapsGameplayCuesToKenneySfx | cue→音效映射 | P1 |

### 1.11 HUD 与视觉切片（4）

| # | 测试方法 | 覆盖域 | P级 |
|---|---------|--------|-----|
| 73 | OnlineMatchHud_AttachesHoverSfxToRuntimeButtons | 悬停音效 | P2 |
| 74 | OnlineMatchController_DownedStateCreatesKillSceneVfx | 击倒场景 Vfx | P1 |
| 75 | MeetingOverlay_Uses2DAssetSkinnedVisualSlice | 会议视觉切片 | P1 |
| 76 | TaskOverlay_Uses2DAssetSkinnedVisualSlice | 任务视觉切片 | P1 |

> EditMode: **76/76 passed**, 0 failed, 0 skipped

---

## 2. PlayMode 测试清单

> 总计: **6 passed, 2 ignored, 0 failed**

| # | 文件 | 测试方法 | 状态 | 覆盖风险点 | P级 |
|---|------|---------|------|-----------|-----|
| 1 | MatchLoopPlayTests | FullMatchLoop_RunsThroughEveryPhaseAndRestarts | ✅ | 全流程循环 | P0 |
| 2 | MatchLoopPlayTests | Character2DAnimator_UpdatesLocalAndRemoteWalkFrames | ✅ | 动画帧同步 | P1 |
| 3 | MatchLoopPlayTests | ClientDisconnect_ReleasesTaskLocksVotesAndKeepsBodyReportable | ✅ | 断线释放锁+尸体保留 | P0 |
| 4 | MiniGameOnlineIntegrationPlayTests | OnlineTasks_OpenRichMinigames_AndCompleteThroughServerPath | ✅ | 小游戏多样+Server 完成路径 | P0 |
| 5 | MiniGameAuthorityPlayTests | MiniGameBridge_RejectsUnopenedTask_AndCompletesServerOpenedTaskOverRpc | ✅ | ServerRpc 授权路径+未开任务拒绝 | P0 |
| 6 | NetworkCustomMessagePlayTests | CustomMessages_RejectMalformedAndSpoofedMessagesOverNetcode | ✅ | 恶意消息全路径拒绝 | P0 |
| 7 | RelayTwoProcessPlayTests | RelayHost_PublishesCodeAndAcceptsPeer | ⚠️ Ignored | Relay 双进程端到端 | P0 |
| 8 | RelayTwoProcessPlayTests | RelayClient_JoinsHostByCode | ⚠️ Ignored | Relay 双进程端到端 | P0 |

---

## 3. Ignored 测试详情

| 测试 | 忽略条件 | 运行方式 |
|------|---------|---------|
| RelayHost_PublishesCodeAndAcceptsPeer | `GANGLAND_RELAY_ROLE != "host"` | `bash run-relay-twoprocess.sh` |
| RelayClient_JoinsHostByCode | `GANGLAND_RELAY_ROLE != "client"` | 同上 |

---

## 4. EditMode 覆盖域映射（76 测试）

| 覆盖域 | 测试数量 | 测试编号 | P级 |
|--------|---------|---------|-----|
| 房间规则+角色分发 | 7 | #1-7 | P0 |
| 时间+胜负 | 10 | #8-17 | P0 |
| 聊天频道路由+安全 | 7 | #18-24 | P0-P1 |
| 主菜单与会话初始化 🆕 | 3 | #25-27 | P1 |
| Relay/Lobby/房间浏览器 | 17 | #28-44 | P0 |
| 消息序列化协议 | 4 | #45-48 | P1 |
| 任务+修理授权 | 4 | #49-52 | P0 |
| 联机消息边界验证 | 9 | #53-61 | P0 |
| 新手引导 | 2 | #62-63 | P1 |
| 世界构建+资产管线 | 9 | #64-72 | P1 |
| HUD+视觉切片 | 4 | #73-76 | P1-P2 |

---

## 5. 联机消息边界覆盖

| 消息 | 防护 | EditMode 测试 | PlayMode 测试 |
|------|------|-------------|-------------|
| `GanglandClientState` | 非有限位置/输入拒绝、clamp、Action 阶段忽略 Ready、未知玩家不 spawn | #53-55 | `NetworkCustomMessagePlayTests` |
| `GanglandClientAction` | 未定义 action 拒绝 | #56 | 同上 |
| `GanglandServerSnapshot` | 非 Server 拒绝、无效 phase/计数、不部分应用 | #57-59 | 同上 |
| `GanglandRoleAssign` | 非 Server 拒绝、未定义 role | #60 | 同上 |
| `GanglandMapSelect` | 未定义 map 拒绝 | #61 | 同上 |
| `GanglandChatSend` | HTML 清洗+截断 | #20 | 同上 |
| MiniGameBridge ServerRpc | 无锁拒绝、超范围拒绝、ServerRpc 完成路径 | #49-52 | `MiniGameAuthorityPlayTests` |
| 房间锁定/密码/满员 | 锁定→拒绝、密码→拒绝、满员→拒绝、无码→拒绝 | #41-44 | — (需 Relay 双进程) |

---

## 6. 覆盖缺口与建议

| 缺口 | P级 | 建议 |
|------|-----|------|
| `OnlineSecurityCamera` 运行时 prefab/spawn 路径 | P1 | 新建 `SecurityCameraNetworkTests.cs` |
| 主机迁移状态一致性 | P1 | 新建 `HostMigrationTests.cs` |
| 聊天频道路由 PlayMode 覆盖（多客户端） | P1 | 新建 `ChatChannelPlayTests.cs` |
| 断线重连状态恢复 | P1 | 新建 `ReconnectTests.cs` |
| 破坏效果五类状态/视觉验证 | P1 | 新建 `SabotagePlayTests.cs` |
| 证据链完整 PlayMode 覆盖 | P1 | 新建 `EvidenceChainPlayTests.cs` |
| 击倒授权路径 PlayMode | P0 | 新建 `KillAuthorityPlayTests.cs` |
| 主菜单 UI 交互 PlayMode | P2 | 新建 `MainMenuPlayTests.cs` |
| 设置持久化跨会话 | P2 | 新建 `SettingsPersistencePlayTests.cs` |
| 设置覆盖层 UI 交互(5滑块+8按钮) | P2 | 新建 `SettingsOverlayPlayTests.cs` |
| Canvas 举报/屏蔽按钮交互 PlayMode | P1 | 新建 `ChatSafetyCanvasPlayTests.cs` |
| 匿名登录自动/手动两条路径 | P2 | 现有 `MainMenuLoginStatus` 覆盖 EditMode，缺 PlayMode |

---

## 7. 覆盖率结论

**EditMode**: 76/76 passed — 核心规则、聊天安全、主菜单、房间浏览器、消息边界、资产管线、新手引导、HUD 全覆盖。新增 3 条主菜单与会话初始化测试（#25-27）。Canvas 举报/屏蔽按钮和设置覆盖层通过 `OnlineMatchHud.CountChatSafetyCanvasActions()` 和 `MainMenuSettingsStatus` 已有部分反射覆盖。

**PlayMode**:
- ✅ 已覆盖：主循环全流程、动画帧、断线释放、小游戏多样性+授权路径、恶意消息拒绝（6 passed）
- ⚠️ 需手动触发：Relay 云服务双进程端到端（2 ignored，需 `run-relay-twoprocess.sh`）
- ❌ 缺口：击倒授权、证据链、主机迁移、断线重连、破坏效果、安全摄像头、聊天多客户端、主菜单 UI、设置持久化、设置覆盖层交互、Canvas 举报/屏蔽按钮交互

当前最强覆盖：核心规则 + 房间浏览器(17 测试) + 反射边界 + 真实 NGO 网络路径恶意消息拒绝。
当前最弱覆盖：多客户端状态同步类测试和击倒/证据链 PlayMode 路径。
