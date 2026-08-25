# Gangland Undercover Demo 续作检查点

> 最后更新：2026-08-25
> 项目：`/Users/zhugehao/projects/GanglandUndercover`
> 用途：下次继续 Demo 开发时先读本文件，直接从“恢复节点”开始，不重新盘点已完成工作。

## 当前结论

本轮已完成 4/6/8/10 人本地 Bot 规模门禁、10 人大厅设置修复、身份简报
`Opening -> Action` 门禁，以及 Relay 合法摄像头 watcher/非空数据回调真实双进程门禁。

Unity Licensing 已恢复，D-2-R2 已关闭。D-2-R3 的 4/6/8/10 人生产时钟采样均已完成：
四局真实进入 `Result` 并生成 MatchStats；完整字段和胜负结果见
`output/d2_r3_pacing_samples_20260824.md`。历史 Licensing/UPM 阻塞保留在采样记录中，
不影响本轮最终 PASS 结论。

图形 PlayMode 身份简报/六阶段截图门禁最新一轮 `1/1 PASS`，生成六张 1920×1080 基线图；
Action 世界、LimeZu atlas 单格裁剪和程序化底图分层均有回归证据。D-3 仍只缺真人窗口
走查和 5 分钟录像，自动化证据不冒充人工验收。

## 已完成，不要重复开发

- `DemoBotMatchPlayTests.BotMatch_CompletesNaturalLoopWithinDemoBudget`：自然 Bot 闭环 PASS。
- 4/6/8/10 人规模门禁：4 个独立 PlayMode case 全部 PASS。
- 各规模 roster、Bot 数量及 Gang/Undercover/Mole 分配已验证。
- `SetRoomMinPlayers` 已允许最少人数上调时同步抬高最大人数，8 -> 10 人不再被旧最大值截断。
- 身份简报标题、身份/目标正文和操作提示已验证；提示能在 `Opening -> Action` 后切换。
- Relay normal 测试已加入合法摄像头观看路径：
  - Host 进入 Action 并把真实远端玩家放入首个摄像头区域。
  - Client 订阅 `OnCameraDataReceived` 并请求观看。
  - Client 必须收到非空 `VisiblePlayerData[]` 才写 marker。
  - Host 必须核对合法 watcher 数量为 1。
- Relay 脚本已清理 `.camera-legal-ready` 和 `.camera-data-received` marker。
- Host/Client 通过精确 `cameraNetworkObjectId` 协调同一摄像头 clone，避免多摄像头无序查询造成假失败。
- 覆盖矩阵、已知问题、Demo 审计和 D-2 Relay 记录已同步。

## 已验证证据

| 项目 | 结果 | 证据 |
| --- | --- | --- |
| Demo Bot/规模/身份简报针对性回归 | 6/6 PASS | `ci-logs/20260823_demo_all.xml` |
| 4/6/8/10 人规模单独回归 | 4/4 PASS | `ci-logs/20260823_scale3.xml` |
| 身份简报单独回归 | 1/1 PASS | `ci-logs/20260823_onboarding.xml` |
| Relay 测试程序集编译/发现 | 0 failed，7 个角色测试按设计 skipped | `ci-logs/20260823_relay_compile.xml` |
| Relay normal 合法摄像头数据 | Host/Client 1/1 PASS，exit 0；watcher=1；`updates=1 nonEmpty=true` | `Logs/relay-host-results.xml`、clone `relay-client-results.xml`、camera-data marker |
| 完整回归 | EditMode 236/236；图形 PlayMode 24/31（7 个 Relay 角色按设计 skipped） | `ci-logs/20260824_d2r2_full_editmode.xml`、`ci-logs/20260824_d2r2_full_graphics_playmode.xml` |
| 最终 EditMode 回归（含 atlas/sorting 契约） | 241/241 PASS | `ci-logs/20260824_final_editmode_v2.xml` |
| 最终图形 PlayMode 回归 | 24 PASS / 0 FAIL / 11 skipped（Relay 依赖按设计跳过） | `ci-logs/20260824_final_graphics_playmode.xml` |
| LimeZu atlas 单格裁剪回归 | 1/1 PASS | `ci-logs/20260824_limezu_crop_test.xml` |
| 图形 Action 世界/六阶段截图收口 | 1/1 PASS；worldRenderers=5464，visible=2456，Sprite rect=16×16/PPU=16 | `ci-logs/20260824_action_shape_split.xml`、`Screenshots/DemoBaseline/03_action_hud_153149368.png` |
| D-3 最新六阶段截图门禁 | 1/1 PASS；六张 1920×1080 RGBA PNG，覆盖 Lobby/Opening/Action/Meeting/Voting/Result | `ci-logs/20260825_d3_graphics_screenshot.xml`、`Screenshots/DemoBaseline/03_action_hud_143914073.png` |
| 静态检查 | PASS | `git diff --check`、`bash -n run-relay-twoprocess.sh` |

2026-08-24 最小 Unity batchmode 已恢复并 exit 0；D-2-R2 最终样本 joinCode=`FKWBGK`。

## 恢复节点：D-3 真人证据

D-2-R3 已完成，不再重复采样。2026-08-24/25 恢复回归完成：采样专用启动入口改为独立
方法名，避免破坏既有反射测试；完整 EditMode `241/241 PASS`
（`ci-logs/20260824_resume_full_editmode_final.xml`）。macOS HostName 已设置为
`zhugehaodeAir.local`，Unity Licensing/UPM IPC 恢复。下一节点只处理真实窗口走查和录像，
若当前环境没有可控图形窗口则记录 `DEFERRED`。

### 采样要求

- 至少覆盖 4/6/8/10 人配置，优先 1 Host + 1 Client + Bot 补位；4 人局若有足够窗口可增加真人 Client。
- 每局记录连接时长、实际局时、会议次数、击杀数、任务完成率、胜方、崩溃/卡死和异常日志。
- 不使用测试时间压缩或强制结算；每局必须自然进入 Result。
- 先建立统一记录表，再跑首个 4 人样本，避免事后补数据。

本轮新增证据见 `output/d2_r3_pacing_samples_20260824.md` 和
`output/d3_identity_and_visual_walkthrough_20260824.md`。

## 后续开发队列

按以下顺序继续：

1. 执行双人真人窗口走查，重点确认摄像头远端画面、身份简报可读性、任务/会议/结算流程；无窗口时记录 `DEFERRED`。
2. 录制 D-3 身份简报 5 分钟端到端短流程；PlayMode 内容门禁已完成，只缺真人视角证据。
3. 四个职业能力已接入可感知 VFX/SFX 和行为门禁；继续做真人窗口确认：`FootprintTrack`、`CorpseExamine`、`RemoteSurveillance`、`DarkVision`。
4. 收口聊天 UX，并重新执行完整 EditMode、图形 PlayMode 和 Relay migration 回归。

## 工作树保护

工作树包含大量用户既有资源、代码和文档修改。继续时必须保留它们：

- 不运行 `git reset --hard` 或 `git checkout --`。
- 不整体清理未跟踪文件。
- 只编辑当前节点需要的文件。
- 本轮直接相关文件包括：
  - `Assets/Tests/PlayMode/DemoBotMatchPlayTests.cs`
  - `Assets/Tests/PlayMode/RelayTwoProcessPlayTests.cs`
  - `Assets/_Project/Scripts/Online/OnlineMatchController.cs`
  - `run-relay-twoprocess.sh`
  - `output/d2_relay_run_20260822.md`
  - `output/demo_audit_and_plan_20260821.md`
  - `output/test_coverage_matrix_20260821.md`
  - `output/KNOWN_ISSUES.md`

## 快速恢复提示

下次可直接使用以下指令开始：

> 阅读 `output/demo_resume_checkpoint.md`，从 D-2-R3 恢复节点继续；先建立真实节奏记录表，再执行 4/6/8/10 人自然对局采样，不重复已经 PASS 的规模矩阵、身份简报和 Relay 摄像头数据门禁。
