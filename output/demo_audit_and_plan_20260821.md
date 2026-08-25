# Gangland Undercover — Demo 计划与项目审查（2026-08-21）

> **版本**: v0.3.0-demo
> **制定日期**: 2026-08-23（D-2/D-3 剩余门禁复核）
> **作者**: WorkBuddy（项目级全量审查）
> **范围**: Unity 项目 `/Users/zhugehao/Projects/GanglandUndercover`
> **审计基础**: 源码实测 + 已有 v2.0 / next_phase_plan / test_coverage_matrix / KNOWN_ISSUES / project_status
> **性质**: 面向"可分发的可玩 Demo"的可执行路线图
> **续作入口**: `output/demo_resume_checkpoint.md`（下次直接从其中的 D-2-R3 恢复节点开始）

---

## 一、审查摘要（TL;DR）

| 维度 | 现状 | 评级 |
|------|------|------|
| 编译 | 0 compilation errors；warning 未单独清零（现有测试有 Unity API 弃用提示） | ✅ 可编译 |
| 单元/集成测试 | EditMode 236/236、PlayMode 24/31（7 skipped 为 Relay 分进程） | ✅ 强 |
| Relay 双/三进程实测 | normal 合法摄像头非空数据门禁已于 2026-08-24 双端 PASS；migration/三进程证据保持 | ✅ 强 |
| 网络安全 | 47 条 named message 全有 server 校验，越权注入均被拒 | ✅ 强 |
| Host migration | 选举 + 候选 + replacement + Relay 新分配 + 快照连续性 全部 PASS | ✅ 强 |
| 联机小游戏 | 11 种经 Client 本地 → Server 完成路径打通 | ✅ |
| OnGUI 残留 | 发布版 Canvas 已接管；OnGUI 仅保留编辑器调试回退 | 🟢 不阻塞 Demo |
| 控制器拆分 | OnlineMatchController.cs 主文件 3,660 行（partial 14 个合计 13,891） | 🟡 目标已接近，Demo 不阻塞 |
| 摄像头复制 bug | 已改为注册 NetworkPrefab 实例化；真实 Relay watcher 与非空数据已 PASS，剩余双窗口画面目视确认 | 🟢 待目视门禁 |
| 美术/动画 | 5,582 张 PNG 覆盖 + LimeZu/Modern Exteriors/Office/Interiors 4 波资产化已落 | ✅ |
| 音频 | 16 SFX + 3 BGM + 2 Ambience + 28 UI | ✅ |
| 引导 | 身份简报+目标+操作提示接入；Opening→Action PlayMode 可读性门禁已 PASS，真人短流程仍未走通 | 🟡 |
| 文档完整性 | 95+ 份 output/ 报告，覆盖 plan/test/asset/QA/launch 链路 | ✅ 极强 |
| **Demo 可玩性** | **自然 Bot 局、4/6/8/10 人规模门禁和 Relay 摄像头远端数据均通过；下一阻塞是自然节奏与真人走查证据** | **🟡 需打磨** |

**核心判断**:
- 项目**广度已足**（地图/美术/音频/联机安全/Host migration/能力框架/小游戏/证据/职业均有可运行实现）
- **Demo 化的真阻塞**只剩：①关键路径端到端实测证据；②OnGUI/截图基线/短引导；③非开发机构建
- 不需要新增大功能，**重点是把已有骨架做扎实**——这与 next_phase_plan 的"质量补强"判断一致

---

## 二、项目结构审查

### 2.1 代码组织（实测）

```
Assets/_Project/
├── Scripts/          172 个 .cs（79,079 行）
│   ├── Online/       联机核心：控制器 partial 14 个 + Services 5 个 + Bots + Camera + Map + Surveillance
│   ├── SocialDeduction/  离线原型：MiniGames 11 个 + Bean/角色/任务/建筑
│   ├── Gameplay/     Action/Event/Game/OpponentAi/Victory/PrototypeBootstrap
│   ├── UI/           Lobby/Menu/Hud/Settings/Theme/Transition/Unified
│   ├── Art/          Animator/Cache/Decorator/Palette/VFX
│   ├── Audio/        AudioManager + IAudioService
│   ├── Core/         枚举/事件总线/本地化/日志
│   ├── World/        DistrictMapView/Node/PoliceStationMap
│   ├── Editor/       编辑器工具
│   ├── Accessibility/  无障碍
│   ├── Build/        BuildScript/LaunchEngineering
│   └── CI/           CIRunner
├── Art/              2D + Animators + Review + ThirdParty
├── Audio/            Ambience + BGM + SFX + UI（含 sfx_event_mapping.json）
├── Resources/        运行时资源
├── Prefabs/          715 个
├── Scenes/           Prototype.unity + Stage1VerticalSlice.unity
├── Docs/             GameDesign.md / Direction.md / DevelopmentProgress / AssetInventory / UnitySetup
└── Tests/            EditMode + PlayMode + Relay 双/三进程
```

### 2.2 关键文件实测清单

| 文件 | 行数 | 角色 | 状态 |
|------|------|------|------|
| OnlineMatchController.cs（主 partial） | 3,660 | 联机主控 | 🟡 目标<4K 实际3,660（已逼近） |
| OnlineMatchController.*.cs（13 partial） | 10,231 | 拆分后子系统 | ✅ |
| OnlineMatchController.Network.cs | 2,465 | 47 处自定义消息收发 | ✅ 已审计 |
| OnlineMatchController.OnGUI.cs | 1,955 | 编辑器调试回退 | 🟢 发布版 Canvas 已接管 |
| OnlineMatchHud.cs | 2,273 | 临时 HUD | 🟡 未迁 uGUI |
| OnlineBotController.cs | 755 | Bot 状态机 | ✅ |
| OnlineWorldBuilder.cs | 1,583 | 程序化世界 | ✅ |
| OnlineTaskService/SabotageService/MeetingService/EvidenceService/VotingService | 各 200-500 | 子系统服务 | ✅ |
| OnlineMatchUtils（69 静态方法抽取） | — | 工具类 | ✅ |
| MatchSnapshotService | — | 快照 capture/restore | ✅ |
| HostMigrationManager | — | 选举 + Relay 路由 | ✅ |
| ChatSystem.cs | — | 三频道 + ghost | 🟡 非 NetworkBehaviour（走 custom message） |
| AudioManager | — | BGM/SFX/ducking | ✅ |
| PrototypeBootstrap | 270 | 菜单驱动入口 | ✅ |

### 2.3 联机网络栈

- **NGO 2.11.2**（无 deprecated warning）
- **Unity Transport 2.7.2**
- **Unity Services Multiplayer 2.1.3**（Lobby + Relay）
- **通信模式**: 47+ CustomMessagingManager named message + NGO 2.x RPC；`OnlineMiniGameBridge` 已注册 NetworkPrefab 并纳入远端任务路径验证
- **角色**: 7 职业（Inspector/Forensics/Tech/Undercover/Enforcer/Fixer/Driver）+ 8 阵营枚举
- **能力**: 15 个 AbilityType 枚举，实际接入 < 5（待 Phase 3.2 收口）

### 2.4 测试体系（实测）

| 平台 | 数量 | 通过率 | 说明 |
|------|------|-------|------|
| EditMode | 236 | 100% | 含反作弊/Host 选举/快照/恶意 payload |
| PlayMode（单进程） | 24 pass | 100%（另 7 个 Relay 分进程 skipped） | 聊天路由/局循环/动画帧/断线释放/Host 断线/Bot 局/截图门禁/规模/引导 |
| PlayMode（Relay 双进程） | 4 | ignored → ✅ bash run-relay-twoprocess.sh | 含恶意 Chat/Camera/CharacterCustom 注入 |
| PlayMode（Relay 三进程 migration） | 3 | ignored → ✅ GANGLAND_RELAY_SCENARIO=migration-threeclient | 远端任务 RPC + 远端投票 named-message |

### 2.5 文档/资产

- `output/` 95+ 份 Markdown 报告（plan/audit/QA/test/launch/Steam）
- `Screenshots/DemoBaseline/` 已存 6 类 `1920x1080` PNG 基线；商店高分辨率素材另按 D-4 产出
- `Builds/` macOS app + SteamPC/FriendTest 包
- `ci-logs/` 109 份历史 CI 日志
- `Packages/manifest.json` 干净，依赖最小化

---

## 三、剩余债务（实测复核）

### 3.1 仍阻塞 Demo 化的真问题

| ID | 问题 | 实测证据 | 严重度 | Demo 处置 |
|----|------|---------|-------|----------|
| D-01 | OnlineMatchController 主文件 3,660 行 | `wc -l OnlineMatchController.cs` | 🟡 | **不阻塞**，partial 已落；Demo 阶段仅追加"再迁 1-2 个职责" |
| D-06 | 摄像头远端画面尚缺人工目视确认 | NetworkPrefab 实例化、合法 watcher=1、非空 `VisiblePlayerData[]` 真实 Relay 门禁已 PASS | 🟢 | **D-2/D-3**：只剩双人视角渲染画面目视确认 |
| P1-3 | 关键截图基线/角色动画帧/UI 资产化 | `Screenshots/DemoBaseline/` 已有 6 张 1920x1080 基线 | 🟢 | **D-1+D-3 处置**：基线已刷新；继续做外部走查 + 4 个能力 VFX |
| P1-2 | 短流程身份简报真人走查未完成 | Controller 暴露；PlayMode 已验证 Opening/Action 标题、身份、目标和提示 | 🟡 | **D-3**：录制 1 个真人/Bot 局走查短流程 |
| D-2-R1 | Relay migration 旧 Host 重连存在偶发时序抖动 | 2026-08-22 双进程 1/2 采样 PASS；三进程最终 PASS | 🟡 | 每里程碑重复双进程 migration，连续失败再调重连状态机 |
| P1-1 | 角色 Animator 走帧本地+远端已 PASS | `Character2DAnimator_UpdatesLocalAndRemoteWalkFrames` | ✅ | 不阻塞 |
| P3-1 | 编辑器调试 OnGUI 回退残留 | 发布版 Canvas 已接管；回退路径仅供编辑器调试 | 🟢 | 不阻塞 Demo；Alpha 再清理调试回退 |
| E1 | Resources 体积 | `du -sh` 实测 111MB；旧 832MB 为优化前值 | ✅ | 已关闭 |

### 3.2 已关闭（不再重复劳动）

| 债务 | 状态 | 验证 |
|------|------|------|
| D-02 破坏 timer 单源 | ✅ | `OnlineTaskService` 单源 + 控制器 read-through |
| D-04 相机正交 | ✅ | 控制器多处 `Camera.main.orthographic` |
| D-05 联机小游戏 11 种 | ✅ | `OnlineTasks_OpenRichMinigames_AndCompleteThroughServerPath` |
| D-07 Vivox 移除 | ✅ | README:33 写明 |
| D-08 ChatSystem 联网化 | ✅（走 custom message） | `ChatBroadcast_UpdatesRecipientCanvasHudFeedOverNetcode` |
| D-10 测试覆盖近零 | ✅ | 236 EditMode + 31 PlayMode（24 pass + 7 skipped） |
| P0-1 构建脚本路径 | ✅ | 417MB macOS |
| P2-1 Resources 体积 | ✅ | 832MB 优化前 → 111MB 实测 |
| 恶意 Chat/ClientProfile/Camera/CharacterCustom/Snapshot 注入 | ✅ | 9 个 PlayMode + Relay 双进程实测 |
| Host migration 选举+候选+无候选降级+直连启动+Relay 新分配+Lobby 标记+重连入口 | ✅ | PLAN17-23 全 PASS |
| 迁移后会议/投票连续性 + 3 客户端任务/会议/投票连续性 + Relay 三进程迁移 | ✅ | PLAN24-25-C |

### 3.3 已收口的旧路径

- `OnlineMiniGameBridge` 已注册 NetworkPrefab，`SubmitTaskResultServerRpc` 使用 NGO 2.x RPC，并由 migration 三进程远端任务门禁实际触达；不再作为 D-2 死代码任务。

---

## 四、Demo 计划（4-6 周 · 2 阶段）

> **核心原则**: Demo 不开发新功能，只把"已有骨架"做到外部玩家进得去、玩得懂、联得稳。
> **完成定义**: ①可编译 ②可双/三进程实测 ③非开发机可运行 ④外部玩家无需文档即懂目标

### 阶段总览

```
Week 1-2   D-0 准备 ──── D-1 单局闭环 ──── D-2 联机稳定性 ────► M-Demo.0 (内测包)
Week 3-4   D-3 引导与可感知 ──── D-4 Demo 包发布 ──► M-Demo.1 (公测 Demo)
Week 5     D-5 试玩反馈 ──► M-Demo.2 (Demo 收尾)
```

| 里程碑 | 节点 | 验收 |
|--------|------|------|
| **M-Demo.0 内测 Demo** | 周末 1+2 末 | 6 类 `1920x1080` 截图基线 + Relay 双/三进程复跑证据 + Bot 局短流程走通 |
| **M-Demo.1 公开 Demo** | 周末 3+4 末 | 非开发机 30 分钟无 P0 + 引导/可感知达标 + macOS/Windows 真机构建 |
| **M-Demo.2 Demo 收尾** | 周末 5 末 | 5-8 名外部玩家反馈 + 已知问题清单 + 下一阶段路线图 |

---

## 五、D-0 准备（1-2 天）— 重新基线化

> **目的**: 任何后续 Phase 必须建立在实测基线上，不能凭记忆判断

### 5.1 任务

| 任务 | 命令/动作 | 产出 |
|------|----------|------|
| D-0a 编译基线 | `unity-batchmode` 跑 6000.4.9f1 | `unity-compile.log` 0 compilation error（warning 另行记录） |
| D-0b 测试基线 | EditMode + PlayMode 全跑 | `test_coverage_matrix` 刷新（236/236 + 24 pass / 7 skipped） |
| D-0c Relay 基线 | `bash run-relay-twoprocess.sh` + `GANGLAND_RELAY_SCENARIO=migration-threeclient` | 双/三进程 PASS 日志 |
| D-0d Bot 局基线 | PlayMode AutoHost + AI 填满 8 人 + 1 局到底 | 时长/会议数/胜负可记录 |
| D-0e 资源体积实测 | `du -sh Assets/_Project/Resources` | 解决文档 104MB vs 832MB 矛盾 |
| D-0f 已知债务核对 | 复跑 3.1 表格，逐项确认状态 | `KNOWN_ISSUES.md` 刷新 |

### 5.2 验收
- [ ] D-0a/b/c/d/e 全部有日志/数据
- [ ] 债务表每行有"实测证据"列
- [ ] 不存在"凭记忆列为未做"的条目
- [ ] 提交推送

---

## 六、D-1 单局可玩闭环（3-4 天）— M-Demo.0 前置

### 6.1 关键截图基线（6 张）

| 截图 | 场景 | 状态 | 验收 |
|------|------|------|------|
| Lobby | Lobby/Opening 状态 | ✅ `Screenshots/DemoBaseline/01_lobby_*.png` | 1920x1080 PNG、非纯黑 |
| Opening | 身份简报 | ✅ `Screenshots/DemoBaseline/02_opening_briefing_*.png` | 1920x1080 PNG、非纯黑 |
| 行动 HUD | 行动阶段 8 人 | ✅ `Screenshots/DemoBaseline/03_action_hud_*.png` | 关键能力按钮可见 |
| 会议 HUD | 会议阶段 | ✅ `Screenshots/DemoBaseline/04_meeting_*.png` | 证据墙、席位、指证按钮可见 |
| 投票 HUD | 投票阶段 | ✅ `Screenshots/DemoBaseline/05_voting_*.png` | 投票反馈可见 |
| 结算 | 胜负揭晓 | ✅ `Screenshots/DemoBaseline/06_result_*.png` | 结算文案、重开/返回房间可见 |

> **自动化**: `output/install_launch_screenshot_guide_20260610.md` + `screenshot_plan_20260609.md` 已成熟，直接套用

### 6.2 双人联机 PlayMode 走通

- 在 `run-relay-twoprocess.sh` 上跑"真人视角"流程：
  - Host 起 → Client 加 → 房主开房 → Client Ready → 双方都过身份简报 → 行动阶段各做 1 任务 → 触发 1 次会议 → 投票淘汰 → 胜负
- 记录：连接耗时/身份简报可读性/任务反馈可见性/会议证据面板是否清晰
- 产出：1 份双人 PlayMode 走查清单（pass/fail/备注）

### 6.2a D-1 自动化自然 Bot 局（已完成）

- `DemoBotMatchPlayTests.BotMatch_CompletesNaturalLoopWithinDemoBudget` 已纳入 PlayMode。
- 用例使用 1 Host + 7 Bot，真实 Update/AI 决策和真实 `EvaluateWinConditions`，不调用强制会议/强制结算钩子。
- 完整回归证据：`ci-logs/20260824_d2r2_full_graphics_playmode.xml`，共 24 passed / 0 failed / 7 skipped。
- 该用例采用确定性随机种子和测试规则时间压缩，只证明闭环行为；真实 8-12 分钟节奏仍在 D-2。

### 6.3 Bot 局端到端

- 1 Host + 7 Bot → 完整 1 局（约 8-12 分钟）→ 录像
- 重点验证：Bot 任务/破坏/投票节奏、人不介入也能收局、不死锁

### 6.4 M-Demo.0 前置验收
- [x] 6 张 1920x1080 截图基线齐全，PNG 尺寸/头/内容门禁通过
- [ ] 双人 PlayMode 走查 ≤ 2 个 P1 问题
- [x] Bot 局自然结束不卡死（自动化门禁）
- [ ] Demo 入口流程（菜单→大厅→对局→结算→重开）≤ 6 次点击

---

## 七、D-2 联机稳定性（4-5 天）— M-Demo.0 核心

### 7.1 5 局 × 4/6/8/10 人 Relay 实战

| 局数 | 人数 | 配置 | 重点 |
|------|------|------|------|
| 1 | 4 | 双 Host + 双 Client | 最小规模是否流畅 |
| 2 | 6 | 1 Host + 1 Client + 4 Bot | Bot 参与感 |
| 3 | 8 | 1 Host + 1 Client + 6 Bot | 标准局节奏 |
| 4 | 10 | 1 Host + 1 Client + 8 Bot | 上限局卡死/超时 |
| 5 | 8 | 1 Host + 1 Client + 6 Bot + 中途 Host 退出 | 触发 migration |

每局记录：连接时长 / 任务完成率 / 会议次数 / 击杀数 / 胜负 / 崩溃/卡死 / Host migration 耗时

### 7.2 摄像头远端可见性门禁（D-06 实测收口）

- 已完成 NetworkPrefab 注册和模板实例化；不再新增运行时 `NetworkObject`。
- 复跑 PlayMode/Relay 双进程，确认 Client 摄像头列表非空，且远端相机画面可观察。

### 7.3 长局超时 guard 实测

- `TimeLimit_ControllerDoesNotResolveBeforeHardLimit` 已有 EditMode
- 新增 PlayMode `LongMatch_DoesNotPrematurelyEnd`：跑 18 分钟，确认 20 分钟硬上限前不结算
- 真实长局采样（20 分钟一局）记入 `test_coverage_matrix`

### 7.4 远端任务路径回归

- `OnlineMiniGameBridge` 已注册 NetworkPrefab，并使用 NGO 2.x RPC 路径；不再把旧的“死代码”作为 D-2 任务。
- 复跑 migration 三进程时保留远端任务提交和投票连续性断言。

### 7.5 M-Demo.0 验收
- [ ] 5 局全部自然结束
- [ ] 摄像头远端可见
- [ ] 长局不提前结算
- [ ] 死代码已删 + 兜底测试绿
- [ ] 236 EditMode + 24 单进程 PlayMode + 4 Relay 双进程角色 + 3 Relay 三进程角色全绿（本轮 normal 与三进程已绿；migration 双进程需重复采样）

---

## 八、D-3 引导与可感知（3-4 天）

### 8.1 短流程身份简报+目标

- 当前 `OnlineMatchController` 暴露 `IdentityBriefing` / 目标 / 操作提示（HUD 接入）
- 新增 PlayMode `BriefingFlow_NewPlayerSeesIdentityGoalAndActionHints`：
  - 加入对局 → 弹出身份卡 → 列出本阵营目标 → 3 个新手指引任务标记 → 第一动作触发后收引导
- 录制 1 段真人 5 分钟走查录像

### 8.2 能力 VFX/SFX 补全

按 Demo 化的最小可感知集合：

| 能力 | 职业 | 当前 | 补全 |
|------|------|------|------|
| FootprintTrack | Inspector | 抽象 | 5 秒地面足迹 sprite + SFX |
| CorpseExamine | Forensics | 抽象 | 检验动画 + 弹证据卡 VFX |
| RemoteSurveillance | Tech | 抽象 | 监控画面弹出 + 频道切换音 |
| DarkVision | Enforcer | 抽象 | 短暂穿墙轮廓 outline |

每个能力加 1 段 VFX（复用 `VfxEffectProfile` + `SabotageVFX`）+ 1 段 SFX（绑定到 `SFX_BINDING_MANIFEST`）

### 8.3 文本聊天 UX 打磨

- 已是 Canvas uGUI（`ChatChannelPlayTests` 验证三频道路由）
- 新增：近距频道半径可视化（"还能 X 米听到"）+ 鬼魂频道底部小图标

### 8.4 M-Demo.0 验收
- [ ] 短流程录像可看
- [ ] 4 个关键能力 VFX/SFX 反馈可见可听
- [ ] 聊天 UX 无明显问题
- [ ] 提交推送

---

## 九、D-4 Demo 包发布（2-3 天）

### 9.1 多平台真机构建

| 平台 | 命令 | 验收 |
|------|------|------|
| macOS | `bash build_macos.sh` | 干净机器（Library 不复用）启动 < 15s |
| Windows | `bash build_windows.sh` | 同上 + 杀毒不报警 |
| Steam Deck | 可选 | Proton 兼容 + 30 分钟无 P0 |

### 9.2 7 渠道资源

复用现有 `output/` 体系：
- `steam_store_copy_draft_20260610.md` → 拷贝
- `steam_screenshot_checklist_20260610.md` → 套用
- `install_launch_screenshot_guide_20260610.md` → 套用
- 商店页高分辨率截图（数量按平台要求）+ 预告 GIF
- 玩家上手 1 页纸（PDF/MD）
- Steam 试玩 Demo 上传（用现有 `build_steampc_windows_closure.sh` 流程）

### 9.3 公测协议

- 复用 `playtest_feedback_form_20260610.md` + `playtest_triage_template_20260610.md`
- 内部试玩 → 公测

### 9.4 M-Demo.1 验收
- [ ] 2 平台真机启动 < 15s
- [ ] 30 分钟无 P0
- [ ] 7 渠道资源齐全
- [ ] Steam 试玩 Demo 上传成功

---

## 十、D-5 试玩反馈（1-2 天）

### 10.1 外部玩家组

- 5-8 人：3 个老玩家（社恐推理/Among Us）+ 3 个非社恐 + 1-2 个开发朋友
- 每人至少 3 局
- 反馈模板：`playtest_feedback_form_20260610.md`

### 10.2 反馈处理

| 类别 | 处理 |
|------|------|
| P0 崩溃/卡死 | 当天修 + 重发 |
| P1 体验/联机 | 排入下一阶段（Alpha 路线） |
| P2 平衡 | 收集数据 + Phase 2 平衡 pass |
| P3 美化/建议 | 记录到 `feature_requests.md` |

### 10.3 已知问题清单

- 刷新 `KNOWN_ISSUES.md`（实测证据，非凭记忆）
- 写入下一阶段路线图

### 10.4 M-Demo.2 验收
- [ ] 5-8 人反馈齐全
- [ ] P0 全清
- [ ] `KNOWN_ISSUES.md` 刷新
- [ ] 提交推送

---

## 十一、Demo 阶段的关键纪律

### 11.1 反虚假完成度
- **禁止** 把"git commit + 编译通过"等同于"完成"
- 每个 Demo 任务必须有"双端实测证据"或"测试名 + Passed"
- 每个里程碑复跑 D-0（重新基线化）

### 11.2 不做新功能
- 能力全量接入（Phase 3.2）→ Alpha 任务
- OnGUI → uGUI（Phase 1.4） → Alpha 任务
- 证据链平衡（Phase 3.2）→ Alpha 任务
- 卧底/内鬼深度（Phase 3.3/3.4）→ Alpha 任务
- Meta 系统（Phase 5） → Beta 任务

Demo 化只解决"已实现但未实测/未打包/未引导"的事。

### 11.3 阶段门禁（每个里程碑结束强制）
- [ ] batchmode 编译 0 error
- [ ] 236 EditMode + 24 单进程 PlayMode + 4 Relay 双 + 3 Relay 三 全绿
- [ ] 双进程/双机手动联机 ≥ 5 局
- [ ] 该里程碑 P0/P1 清零
- [ ] `KNOWN_ISSUES.md` 更新（实测证据）
- [ ] 提交推送 GitHub

---

## 十二、风险与依赖

### 12.1 风险

| 风险 | 严重度 | 缓解 |
|------|-------|------|
| Relay 云服务抖动 | 🟡 | `run-relay-twoprocess.sh` 已有 bounded retry；Demo 至少 5 局取均值 |
| macOS 公证失败 | 🟡 | D-4 前 dry run 公证流程 |
| 外部玩家错过引导 | 🟡 | D-3 短流程录像 + 1 页纸 |
| Resources 体积超大 | 🟢 | D-0e 实测，必要时把 Quaternius/Synty 残留移出 |
| 摄像头 bug 修复触发回归 | 🟡 | D-2 修复 + Relay 三进程回归 |

### 12.2 依赖

```
D-0 (基线) ──► D-1 (截图+走通) ──► D-2 (稳定性) ──► M-Demo.0
                                                          │
                                                          ▼
                              D-3 (引导+VFX) ──► D-4 (包发布) ──► M-Demo.1
                                                                       │
                                                                       ▼
                                                              D-5 (试玩) ──► M-Demo.2
```

**不可跳过**:
- D-0 必须先于 D-1（否则在错地基上做截图基线）
- D-2 必须先于 D-3（否则引导在崩溃上叠加）
- D-3 可与 D-4 并行推进

---

## 十三、Demo 完成后的下一阶段（仅记录，不实施）

```
Demo 收尾
  └─► Phase 1 真收口：主文件进一步拆分 + 移除编辑器调试 OnGUI 回退
  └─► Phase 2 核心玩法：剩余 12 能力 + 暗线/监控真做
  └─► Phase 3 差异化：证据链/卧底/内鬼做实
  └─► Phase 4 美术品质：3 张地图 Tilemap 化
  └─► Phase 5 Meta：档案/匹配/回放
  └─► Phase 6 发布工程：多平台真机构建
```

详细见 `output/next_phase_plan_20260607.md` / `master_development_plan_v2_20260605.md`。

---

## 十四、交付物清单

| 编号 | 路径 | 说明 |
|------|------|------|
| 1 | `output/demo_audit_and_plan_20260821.md` | 本文件 |
| 2 | `output/KNOWN_ISSUES.md` | 已知问题（每里程碑刷新） |
| 3 | `output/test_coverage_matrix_20260821.md` | 测试覆盖矩阵（D-0 刷新） |
| 4 | `Screenshots/DemoBaseline/` | 6 类关键场景的 1920x1080 PNG 基线 |
| 5 | `output/demo_walkthrough.md` | 真人 5 分钟走查录像说明 |
| 6 | `Builds/Demo-202608XX/` | macOS + Windows Demo 包 |
| 7 | `output/demo_assets_manifest.md` | 7 渠道资源清单 |
| 8 | `output/demo_playtest_feedback.md` | 5-8 人反馈汇总 |

---

> **总时间线**: 2-3 周（内测 Demo） + 1-2 周（公测 Demo） + 1 周（试玩收尾） = **4-6 周**
> **关键判断**: Demo 化的真阻塞已不是开发能力，而是 **联机实测、引导打磨、非开发机构建**。把这三块做扎实，Demo 即可发布。

---

## 十五、D-0 重新基线化执行记录（2026-08-21，已完成 ✅）

| 子项 | 结果 | 证据 |
|------|------|------|
| D-0a 编译基线 | 0 compilation errors（warning 未作为本轮清零门禁） | `ci-logs/20260821_222302_compile.log` |
| D-0b 测试基线 | EditMode **236/236**；PlayMode **24 pass / 0 fail / 7 skipped**（skipped=Relay 分进程角色） | `ci-logs/20260824_d2r2_full_editmode.xml` / `20260824_d2r2_full_graphics_playmode.xml` |
| D-0c Relay 双进程 | **PASS**（2026-08-24 HOST_EXIT=0, CLIENT_EXIT=0, joinCode=FKWBGK, connectedClients=2；合法摄像头非空数据 PASS） | `Logs/relay-host-results.xml` / clone XML / `output/d2_relay_run_20260822.md` |
| D-0e Resources 体积 | 111MB（Art 368MB / Audio 19MB），832MB 矛盾解除 | `du -sh` 实测 |
| D-0f 债务核对 | D-06 / A5 / P3-1(OnGUI) / P1-2(接线) / E1 均已解决或降级；P2-3 Relay 实测通过后关闭 | `KNOWN_ISSUES.md` |

**附带修复（重大）**: `ci_run.sh` CI 假阳性 bug——历史 59 份日志从未真实执行测试。根因：`-quit -executeMethod CIRunner.RunXxxTests` 在异步 TestRunnerApi 注册后立即退出。已改用官方 `-runTests -testPlatform` 同步模式，测试首次真实落地。

**D-0 结论**: 基线可信，进入 D-1（单循环可玩性：截图基线 + 双人联机走查 + Bot 局录像）。
