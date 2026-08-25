# Gangland Undercover — 测试覆盖矩阵（D-0 基线刷新版）

> **日期**: 2026-08-24 10:12 | **版本**: v0.3.0-demo | **触发**: Demo 计划 D-2/D-3 剩余门禁推进
> **基于**: EditMode 236 用例 + PlayMode 31 用例（本轮新增 4/6/8/10 人规模与身份简报门禁；按 `-runTests` 真实执行）
> **取代**: `test_coverage_matrix_20260624.md`（其 130/22 计数已过时）

---

## 1. D-0 基线实测总结（2026-08-21）

| 测试平台 | 结果 | 记录 |
| --- | --- | --- |
| 编译 | 0 compilation errors；warning 未作为本轮清零门禁 | `ci-logs/20260821_222302_compile.log` |
| EditMode | **236/236 PASS, 0 failed, 0 skipped** | `ci-logs/20260821_222302_editmode.xml` |
| PlayMode（单进程） | **24 passed / 0 failed / 7 skipped**；含新增规模、身份简报与截图门禁（Relay 分进程角色仍按设计 skipped） | `ci-logs/20260824_d2r2_full_graphics_playmode.xml` |
| Relay 双进程 | **PASS（2026-08-24）** HOST_EXIT=0 CLIENT_EXIT=0, joinCode=FKWBGK, connectedClients=2；恶意注入拒绝；合法 watcher=1；摄像头非空回调 `updates=1` | `Logs/relay-host-results.xml` / clone `relay-client-results.xml` / camera-data marker |
| Relay migration 双进程 | **1 PASS / 1 时序抖动**（本轮复跑） | `Logs/relay-migration-host-results.xml` / clone `relay-migration-client-results.xml` |
| Relay migration 三进程 | **PASS（本轮最终复跑）** | `Logs/relay-migration-threeclient-host-results.xml` / 两个 clone XML |

### 与 2026-06-24 矩阵的差异

| 项 | 2026-06-24 | 2026-08-21 实测 | 说明 |
| --- | --- | --- | --- |
| EditMode 数量 | 130 | **236** | 套件增长 +106 用例 |
| PlayMode 数量 | 22（15 pass + 7 ignored） | **31（24 pass + 7 skipped）** | 新增自然 Bot 局、截图门禁、规模/引导和角色/摄像头相关用例 |
| CI 可信度 | `ci_run.sh` 假阳性（从未真实执行） | **已修复**（`-runTests` 同步模式） | 详见 KNOWN_ISSUES ⚠️ 节 |

> **7 个 skipped** 为 `RelayTwoProcessPlayTests` 的分进程角色用例，需要
> `GANGLAND_RELAY_ROLE=host/client/migration-host/migration-client/migration-host-threeclient/migration-candidate-threeclient/migration-observer-threeclient`
> 分进程运行，不属于单进程回归失败。

---

## 2. PlayMode 单进程明细（2026-08-24 实测全 PASS）

| 文件 | 测试方法 | 结果 | 时长(s) |
| --- | --- | --- | --- |
| ChatChannelPlayTests | ChatBroadcast_UpdatesRecipientCanvasHudFeedOverNetcode | PASS | 1.52 |
| ChatChannelPlayTests | ChatChannels_RouteMeetingProximityAndGhostOverNetcode | PASS | 0.22 |
| MatchLoopPlayTests | Character2DAnimator_UpdatesLocalAndRemoteWalkFrames | PASS | 0.84 |
| MatchLoopPlayTests | ClientDisconnect_ReleasesTaskLocksVotesAndKeepsBodyReportable | PASS | 0.13 |
| MatchLoopPlayTests | EvidenceChain_TaskEvidenceFeedsMeetingDigestAndVoteClosure | PASS | 0.15 |
| MatchLoopPlayTests | FullMatchLoop_RunsThroughEveryPhaseAndRestarts | PASS | 0.24 |
| MatchLoopPlayTests | HostDisconnect_ShowsVisibleRecoveryGuidance | PASS | 0.12 |
| MatchLoopPlayTests | HostMigration_ClientHostDisconnectFallsBackWhenNoRemainingPeers | PASS | 0.12 |
| MatchLoopPlayTests | HostMigration_DirectReplacementHostStartsNetworkManager | PASS | 0.13 |
| MatchLoopPlayTests | MeetingEvents_PublishDuringPlayModeEmergencyAndBodyReportPaths | PASS | 0.13 |
| MatchLoopPlayTests | SnapshotRestore_RestoresGameplayStateDuringPlayModeLifecycle | PASS | 0.12 |
| MiniGameAuthorityPlayTests | MiniGameBridge_RejectsUnopenedTask_AndCompletesServerOpenedTaskOverRpc | PASS | 0.16 |
| MiniGameOnlineIntegrationPlayTests | OnlineTasks_OpenRichMinigames_AndCompleteThroughServerPath | PASS | (套件内) |
| NetworkCustomMessagePlayTests | CustomMessages_RejectMalformedAndSpoofedMessagesOverNetcode | PASS | (套件内) |
| NetworkCustomMessagePlayTests | CharacterCustomizer_SpawnsOwnerObjectsAndRejectsNonOwnerPayload | PASS | (套件内) |
| (另 2 用例) | 见 XML | PASS | 0.0x 级 |

### D-1 自然 Bot 局门禁

| 用例 | 结果 | 证据 |
| --- | --- | --- |
| `DemoBotMatchPlayTests.BotMatch_CompletesNaturalLoopWithinDemoBudget` | **PASS** | `ci-logs/d1-final-play-results.xml` |
| 阶段链 | `Opening → Action → Meeting → Voting → Result`（完整回归中出现两轮会议变体） | 日志 `[DemoBot] phaseTrace=...` |
| 行为采样 | 1 Host + 7 Bot；自然会议 1+；自然击杀 1+；尸体可见；自然 Result | 同上日志 |
| 真实性边界 | 未调用强制会议/强制结算钩子；测试规则仅压缩时钟、提高 Bot 报案/会议概率和移动倍率 | `Assets/Tests/PlayMode/DemoBotMatchPlayTests.cs` |

> 该门禁使用时间加速和确定性种子证明行为闭环，不等价于真实 8-12 分钟节奏采样；后者仍属于 D-2。

> 注：测试使用时间加速模拟，单局循环用例 0.24s 完成不代表真实局时长；真实节奏采样属 Demo D-2 任务。

### D-1/D-3 规模与身份简报门禁

| 用例 | 结果 | 证据 |
| --- | --- | --- |
| `BotRoster_SupportsFourPlayerDemoScale` | **PASS** | `ci-logs/20260823_demo_all.xml` |
| `BotRoster_SupportsSixPlayerDemoScale` | **PASS** | `ci-logs/20260823_demo_all.xml` |
| `BotRoster_SupportsEightPlayerDemoScale` | **PASS** | `ci-logs/20260823_demo_all.xml` |
| `BotRoster_SupportsTenPlayerDemoScale` | **PASS** | `ci-logs/20260823_demo_all.xml` |
| `OnboardingBriefing_ExposesIdentityObjectiveAndActionPrompt` | **PASS** | `ci-logs/20260823_demo_all.xml` |

规模门禁使用真实 `EditorSimulateLocalMatch`、`EnsureMinimumBots` 和角色分配表，验证
4/6/8/10 人的 roster 与阵营数量；不等价于真人 Relay 节奏采样。期间修正了
`SetRoomMinPlayers` 在从 8 人切到 10 人时被旧最大人数错误截断的问题。

---

## 3. 联机安全边界（继承 2026-06-24 版，无回退）

| 消息/入口 | 当前防护 | 验证 |
| --- | --- | --- |
| GanglandClientProfile | bounded UTF-8，超长截断，畸形忽略 | PlayMode `NetworkCustomMessagePlayTests` |
| GanglandChatSend | 结构化 payload，HTML 清洗，长度截断，频道路由校验 | EditMode + PlayMode `ChatChannelPlayTests` |
| CharacterCustom | 长度/格式校验，Server owner 校验，装扮 ID/部位匹配 | EditMode + PlayMode + Relay 双进程 |
| OnlineSecurityCamera | Action/alive/距离/远程监控能力校验，watcher 集合 | EditMode + Relay 双进程 `StartWatchingServerRpc` |
| ServerSnapshot/RoleAssign/MapSelect | 非 server sender 和非法 enum/count 拒绝 | EditMode + PlayMode + Relay 双进程 |
| Task/Repair | active lock、距离、ServerRpc 完成路径校验 | EditMode + PlayMode MiniGameAuthority + Relay 三进程 |

---

## 4. 剩余缺口（Demo 视角）

| 缺口 | P级 | Demo 处置 |
| --- | --- | --- |
| 8-12 分钟完整局体验节奏真实采样 | P1 | D-2：5 局 × 4/6/8/10 人实战记录；规模配置门禁已 PASS，真实节奏仍未采样 |
| 关键截图基线（会议/结算缺） | P1 | D-1：6 张 1920x1080 基线已产出，仍需外部走查 |
| 短流程引导端到端走查 | P1 | 身份/目标/操作提示 PlayMode 门禁已 PASS；真人录像仍待 D-3 |
| 摄像头远端画面目视确认 | P2 | 合法 watcher + 非空 `VisiblePlayerData` 自动化 Relay 门禁已 PASS；仍需双窗口人工确认渲染画面 |
| Host migration 多任务/多轮会议长流程 | P3 | Alpha 阶段 |

### D-2 回归执行状态（2026-08-24）

| 场景 | 本轮结果 | 说明 |
| --- | --- | --- |
| Relay normal 双进程 | **PASS** | Host/Client exit 0，`connectedClients=2`，恶意 Chat/Camera/CharacterCustom 注入均拒绝；合法摄像头 watcher=1，Client 收到非空数据；最终样本 joinCode=`FKWBGK` |
| Relay migration 双进程 | **1 PASS / 1 FLAKY FAIL** | 一次旧 Host 重连后短暂断开，一次完整 PASS（`GMRLJP → PJBGCP`）；后续里程碑继续重复采样 |
| Relay migration 三进程 | **PASS（复跑 2/2）** | 两次最终三端 exit 0；旧 Host/observer 重连、远端任务 RPC、远端投票和 observer 稳定性均 PASS（独立 XML：`M6DGGT → QRWQQP`） |

> D-2 自动化 Relay 门禁（含合法摄像头非空数据）已完成；4/6/8/10 人真实节奏局、摄像头远端画面目视确认和双人真人视角走查仍待执行。

详细命令、日志、一次性 Licensing 阻塞和测试稳定性修正：`output/d2_relay_run_20260822.md`。

---

## 5. 阶段门禁（Demo 每里程碑强制）

- [ ] `bash ci_run.sh --skip-tests` 编译 0/0
- [x] `bash ci_run.sh --skip-build` 等价回归：EditMode 236/236 + 图形 PlayMode 24/31（7 Relay 分进程角色按设计 skipped）
- [x] `bash run-relay-twoprocess.sh`（normal）双端 PASS，含合法摄像头非空数据
- [ ] 该里程碑 P0/P1 清零，`KNOWN_ISSUES.md` 更新
- [ ] 提交推送 GitHub
