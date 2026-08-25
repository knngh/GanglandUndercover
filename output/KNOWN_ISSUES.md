# Gangland Undercover — KNOWN_ISSUES.md

> **最后更新**: 2026-08-24 10:16 | **版本**: v0.3.0-demo | **更新人**: Demo 计划 D-2/D-3 剩余门禁推进

---

## 本次基线化摘要（2026-08-21 D-0 实测）

| 项目 | 实测结果 | 证据 |
|------|---------|------|
| 编译 | 0 compilation errors；warning 数量未作为本轮门禁（现有测试仍有 Unity API 弃用提示） | `ci-logs/20260821_222302_compile.log` |
| EditMode | **236/236 PASS, 0 failed, 0 skipped**（文档旧值 130，套件已增长） | `ci-logs/20260821_222302_editmode.xml` |
| PlayMode | **24 passed, 0 failed, 7 skipped**（skipped 均为 Relay 分进程角色，需 `GANGLAND_RELAY_ROLE` 单独跑） | `ci-logs/20260824_d2r2_full_graphics_playmode.xml` |
| Resources 体积 | **111MB**（Art 368MB / Audio 19MB）；104MB 为旧文档近似值，832MB 为优化前旧值，当前以实测为准 | `du -sh` 实测 |
| Relay 双进程 | **PASS（2026-08-21 22:26）** 双端 exit 0，恶意注入三项断言全部拒绝（见 P2-3） | `run-relay-twoprocess.sh` |

| D-1 Bot 局 | **PASS**：1 Host + 7 Bot 自然跑过 `Opening → Action → Meeting → Voting → Result`，完整回归稳定 | `ci-logs/d1-final-play-results.xml` |

### D-2 本轮执行状态（2026-08-24）

- Relay `normal` 双进程最终 PASS：Host/Client exit 0，`connectedClients=2`，恶意 Chat/Camera/CharacterCustom 注入均被拒。
- migration 双进程 2 次采样为 **1 PASS / 1 时序抖动**；失败样本是旧 Host 重连后 60 帧内短暂断开，下一次完整通过，后续里程碑需继续复跑取样本。
- migration 三进程最终 PASS：三端 exit 0，旧 Host 重连、远端任务 RPC、远端投票和 observer 跟随均通过。期间修正了测试对 clientId 排序和正常远端退出的脆弱假设。
- 4/6/8/10 人真实节奏局、摄像头远端画面目视确认和双人真人视角走查仍未完成，不能以自动化 Relay 门禁替代。
- 4/6/8/10 人本地 Bot 规模矩阵已补齐并通过（4 个独立 PlayMode case）；身份简报 Opening→Action 可读性门禁也已通过。两者仍不等价于真人 Relay 节奏或录像走查。
- D-2-R3 自然采样入口已增加专用 Bot `Gang` 保障和开局 roster 记录，避免唯一真人闲置造成不可结算样本；该保障尚未产生新的 `Result` 证据，仍需许可证恢复后重跑 4/6/8/10 人。
- 摄像头合法观看门禁已在 2026-08-24 真实 Relay 通过：Host 发布 Action 和精确 `cameraNetworkObjectId`，远端加入 watcher，Client 收到非空 `VisiblePlayerData[]`，Host 核对 watcher=1。
- D-2-R2 已关闭：清理孤立 Licensing Client 后最小 batchmode 与 normal Relay 均恢复；最终 Host/Client exit 0，joinCode=`FKWBGK`。

### ⚠️ 已修复的 CI 假阳性 bug（重要）

- **症状**: `ci_run.sh` 25 秒报 Compile/EditMode/PlayMode 全 ✅。
- **根因**: `CIRunner.RunXxxTests` 走 `TestRunnerApi.Execute()`（异步）+ `EditorApplication.update` Watchdog，但 `-quit` 在 executeMethod 返回后立即退出 → 测试从未执行、退出码恒 0。**历史 59 份 ci-logs 无一包含测试结果行。**
- **修复** (2026-08-21): `ci_run.sh` 新增 `run_tests()` 改用 Unity Test Framework 官方 `-runTests -testPlatform <mode> -testResults <xml>` 同步模式，并从 NUnit XML 提取汇总行。
- **教训**: 此前所有"CI PIPELINE PASSED"均不可作为测试通过证据；今后以 `-runTests` 的 NUnit XML 为准。

---

## 发布阻断 (P0) — 已清零

> ✅ 全部 P0 已修复并验证

---

## 债务实测状态更新（2026-08-21 复核）

### 已解决 ✅（历史文档仍列为未修，不得重复立项）

| 原编号 | 原描述 | 实测结论 | 证据 |
|--------|--------|---------|------|
| ~~D-06~~ | 监控摄像头复制 bug（globalObjectIdHash=0） | **已修复**：`OnlineMatchController.Visuals.cs:864` 注释 "A1 修复"，改用 `NetworkObject.InstantiateAndSpawn(surveillanceCameraTemplate)` 从注册 NetworkPrefab 实例化；prefab 在 `Resources/Network/OnlineSecurityCamera.prefab`；注册在 `Network.cs:104` | 源码实测 + PlayMode `CameraAuthorization_*` PASS |
| ~~A5/死代码~~ | OnlineMiniGameBridge ServerRpc 死代码 | **已解决**：bridge 已注册 NetworkPrefab（`Resources/Network/OnlineMiniGameBridge.prefab`），`SubmitTaskResultServerRpc` 改用 NGO 2.x `[Rpc(SendTo.Server)]`，Relay 三进程迁移测试 PLAN25-C 实测可达（旧 Host 经真实 ServerRpc 完成任务 9） | `test_coverage_matrix_20260624.md` PLAN25-C |
| ~~P3-1~~ | OnGUI 全量迁移 uGUI | **发布版已解决**：`OnlineMatchHud.cs`（2,273 行）纯 Canvas uGUI（0 处 OnGUI）；`OnlineMatchController.OnGUI.cs` 发布版直接 `return`（"M7.3: Canvas 模式已接管全部 UI，OnGUI 保留仅为编辑器调试回退"）。**next_phase_plan 中 "OnlineMatchHud 是 OnGUI" 的说法过时。** | 源码实测 grep |
| ~~P1-2 接线~~ | 新玩家引导 | **代码已接线**：Canvas HUD 读 `controller.OnboardingBriefingTitle/Body/ActionPrompt`（`OnlineMatchHud.cs:1449+`）。剩余为端到端走查验证（Demo D-3 任务） | 源码实测 |
| ~~E1~~ | Resources 体积矛盾 | **实测 111MB**，与文档 104MB 基本一致 | `du -sh` |

### 仍开放 🔄

| 编号 | 问题 | 严重度 | 实测证据 | Demo 处置 |
|------|------|--------|---------|----------|
| D-01 | OnlineMatchController 主文件 3,660 行（partial 14 个合计 13,891） | 🟡 | `wc -l` 实测 | 不阻塞 Demo；v2 目标 <4K 已基本达成（主文件 3,660 < 4,000） |
| P1-3 | 地图美术资产化继续扩展（角色动画帧、会议/任务/战术地图 UI、截图基线） | 🟢 | `art_readiness_current.md` + `Screenshots/DemoBaseline/` | Demo D-1 已产出 6 张 1920x1080 基线；外部走查与 D-3 能力反馈仍待完成 |
| P2-2 | Bot 不使用暗线通道 | 🟢 | KNOWN_ISSUES 旧记录 | 不阻塞 Demo（Alpha 任务） |
| D-2-R1 | Relay migration 旧 Host 重连时序偶发抖动 | 🟡 | 2026-08-22 双进程 2 次采样：1 次 60 帧内短暂断开、1 次 PASS；三进程最终 PASS | D-2 每里程碑重复采样；若连续失败再调整重连状态机/重试窗口 |
| ~~D-2-R2~~ | normal Relay 合法摄像头数据证据 | ✅ 已关闭 | 2026-08-24 Host/Client 1/1 PASS；`updates=1 nonEmpty=true`；watcher=1；joinCode=`FKWBGK` | 保留为每里程碑 normal Relay 门禁，不再作为开放问题 |
| ~~P2-3~~ | Relay 真双进程恶意 Client 注入实测 | **PASS（2026-08-21 22:26）**: HOST_EXIT=0 CLIENT_EXIT=0, joinCode=H9BMMF, connectedClients=2, maliciousMessagesRejected=true, cameraWatchRejected=true, characterCustomRejected=true | 见下方 |
| P3-2 | Host migration election 真实多客户端长流程自动化 | 🟢 | 已有三进程 + 迁移后连续性采样 | 不阻塞 Demo |
| P3-3 | CharacterCustom Relay 转发行为双端确认 | 🟢 | 已有 PLAN14/16 门禁 | 不阻塞 Demo |

---

## 高优先级 (P1)

### P1-3: 地图美术资产化仍需继续扩展 🔁
- **已完成切片**: 关键地标 + 任务事件反馈 + Modern Exteriors/Office/Interiors 四波 room props（门槛 75）+ VFX 命名层 + EditMode/PrototypeSmoke 覆盖
- **剩余影响**: 角色动画帧、会议/任务/战术地图 UI、关键截图基线和更细粒度动画 VFX 仍需继续资产化扩展
- **已完成**: Demo D-1 已生成 6 个场景类别的 `1920x1080` 截图基线；下一步是双人视角走查与 D-3 能力反馈，不再重复刷新同一批截图

---

## 中优先级 (P2)

### P2-3: Relay 真双进程验证 ✅（2026-08-21 关闭）
- **2026-08-21 22:26 结果**: `run-relay-twoprocess.sh`（normal 场景）双端 PASS —— HOST_EXIT=0, CLIENT_EXIT=0, joinCode=H9BMMF, host 侧 connectedClients(含自身)=2；恶意注入断言全部生效：maliciousMessagesRejected=true（畸形/伪造消息被拒）、cameraWatchRejected=true（越权摄像头观看被拒）、characterCustomRejected=true（非 owner 装扮被拒）。
- **已关闭部分**: Chat/ClientProfile/Camera/CharacterCustom/RoleAssign/MapSelect/ServerSnapshot 恶意注入均有单进程或双进程门禁；Host migration 三进程连续性已采样。
- **剩余动作**: 非问题，转为 Demo 每里程碑门禁项复跑（见 test_coverage_matrix 第 5 节）。

---

## 低优先级 (P3)

### P3-2: 网络 Host 迁移 election 测试不足
- **已关闭部分**: 选举策略 + 候选存在性 + 无候选降级 + 直连接管 + Relay 新 allocation 路由 + Lobby discovery marker + 快照恢复连续性 + 三进程任务/会议/投票 + 迁移后远端 RPC/投票，全部 PASS。
- **剩余影响**: 多任务、多轮会议和中途断线恢复的长流程采样。
- **下一步**: Alpha 阶段扩展。

### P3-3: CharacterCustom Relay 转发行为仍需双端实测确认
- 已有 PLAN14/16 EditMode + PlayMode + Relay 双进程门禁覆盖，残余风险低。

---

## 已修复 (v0.1.0 → v0.3.0) — 仅列关键项

- ✅ CI 假阳性 bug（2026-08-21）: `ci_run.sh` 改用 `-runTests` 同步模式，测试首次真实执行
- ✅ EditMode 236/236（全部通过）
- ✅ PlayMode 24 passed / 0 failed / 7 skipped（7 skipped 为 Relay 分进程角色；完整图形回归 2026-08-24）
- ✅ D-06 摄像头 NetworkPrefab 化（A1 修复）
- ✅ OnlineMiniGameBridge ServerRpc 活化（NetworkPrefab + NGO 2.x Rpc API + Relay 三进程实测）
- ✅ 发布版 OnGUI 清零（Canvas 全接管，OnGUI 仅编辑器调试回退）
- ✅ P0-1: BuildScript 场景路径修复 + macOS 构建 417MB
- ✅ P2-1: Resources 832MB 优化前 → 111MB 实测
- ✅ NGO deprecated warning 清零
- ✅ Bot 卡死检测 + 证据系统统一 + UI 行动 HUD 基准
- ✅ Chat payload 结构化 + Task/Repair 服务器校验 + Camera 授权 + CharacterCustom owner 校验
- ✅ Host migration 全链路（PLAN17-25-C）
