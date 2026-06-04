# Gangland Undercover 完整详细开发计划

日期: 2026-06-04  
项目路径: `/Users/zhugehao/projects/GanglandUndercover`  
引擎版本: Unity 6000.4.5f1  
目标: 将当前警匪社交推理原型推进到可封闭测试、可稳定多人开局、体验接近 Among Us 节奏但保留警匪题材差异的版本。

## 0. 当前判断

当前项目已经具备大量功能雏形，不应该再用“从零做一个狼人杀游戏”的计划。正确路线是: 先验证和稳定现有联机闭环，再拆掉阻碍迭代的巨型控制器，然后把离线已有的好玩法迁入联机，最后做服务、UI、反馈、平衡和发行。

已确认现状:

- `Assets/_Project/Scripts` 当前约 102 个 C# 文件，约 46,621 行。
- `OnlineMatchController.cs` 约 13,101 行，承担网络、地图、任务、会议、Bot、音频、UI、语音路由、快照等职责，是最大架构风险。
- 联机已有 Host/Client、Relay 调用代码、AI 补位、击杀、尸体报告、会议、投票、证据链、5 类破坏、职业技能、Canvas HUD。
- 离线/SocialDeduction 侧已有通风管、监控、紧急任务、鬼魂、警署地图和 10+ 小游戏。
- 联机任务仍以 1/2/3 校验和 Space 蓄力为主，尚未真正复用离线小游戏。
- `UnityServiceBootstrap.cs` 目前 Vivox 是 stub，语音 UI 存在但服务不可用。
- 联机侧有 `UnderworldPassage` 与 `OnlineVents` 两套机动系统，且 `OnlineVents` 存在未统一 `ScaleMapPosition` 的坐标风险。
- 当前 worktree 已有大量未提交资源和代码改动，后续任何实现任务都必须先确认改动范围，不要回滚用户已有工作。

## 1. 产品定义

### 1.1 核心体验

玩家开局进入一个 6-10 人警匪卧底局。警方、市民、卧底通过任务、监控、证据和会议找出黑帮；黑帮、线人通过击杀、破坏、暗线通道、伪装任务和会议误导阻止证据链闭合。每局 10-15 分钟，过程必须持续产生信息差、路线怀疑、任务压力和会议冲突。

### 1.2 对标 Among Us 的部分

对标的是:

- 低学习成本。
- 高可读性的角色、尸体、任务、会议、投票状态。
- 稳定开房和多人同步。
- 短局循环和快速重开。
- 死亡后仍能参与或观战。
- 任务、破坏、通风管、监控、紧急任务这些让局势持续变化的机制。

不对标的是:

- 太空主题。
- 逐字复制角色设定。
- 完全相同的任务内容。
- 必须专用服务器起步。

### 1.3 警匪差异化

必须保留和强化:

- 证据链进度，而不是纯任务条。
- 嫌疑值和线索板。
- 警署、港区、后巷、监控、证物、线人、卧底。
- 黑帮暗线机动，而不是直接叫通风管也可以，但底层要统一。
- 会议中展示可推理线索，而不是直接给答案。

## 2. 总体里程碑

| 里程碑 | 时间 | 目标 | 通过标准 |
|---|---:|---|---|
| M0 基线验证 | 2-3 天 | 确认当前工程能编译、能完整打一局、能双开同步 | Unity 0 编译错误，本地局和双开局到结算 |
| M1 联机核心瘦身 | 5-7 天 | 拆 `OnlineMatchController`，修坐标和反射风险 | 单文件降到 8k 行以下，双开烟测不退化 |
| M2 标准局闭环 | 5-7 天 | 6-10 人 10-15 分钟联机局 | 5/8/10 人局可完成，参数可配置 |
| M3 任务与破坏成品化 | 10-14 天 | 联机接入真实小游戏和破坏修复小游戏 | 至少 6 种联机小游戏，Host 权威结果 |
| M4 信息和欺骗系统 | 7-10 天 | 监控、暗线、证据板、嫌疑系统进入联机 | 会议能展示 3 类线索，监控能影响推理 |
| M5 服务和语音 | 7-14 天 | Relay/Lobby/语音/断线策略可用 | 双机 Relay 完整局，语音可用或隐藏 |
| M6 成品 UI 与反馈 | 10-14 天 | 默认玩家路径全部 Canvas 化，反馈清楚 | 新玩家不看文档能完成第一局 |
| M7 内容和平衡 | 14-28 天 | 地图、职业、Bot、胜率、局长调优 | 8 人局平均 10-15 分钟，胜率接近 45-55 |
| M8 封测发行准备 | 7-14 天 | 构建、日志、测试清单、反馈流程 | 外部测试包可分发，72 小时无 P0/P1 |

## 3. 依赖图

执行顺序必须遵守:

```text
M0 编译和双开基线
  -> M1 架构瘦身和坐标统一
    -> M2 标准局规则和鬼魂
      -> M3 任务小游戏和破坏修复
        -> M4 监控/暗线/证据板
          -> M5 Relay/Lobby/语音/断线
            -> M6 UI/动画/反馈
              -> M7 内容/平衡
                -> M8 构建/封测/发行
```

可以并行的工作:

- M0 测试记录和 M1 代码阅读可以并行。
- M3 小游戏 UI 和 M5 服务环境配置可以并行，但小游戏结果协议必须先定。
- M6 视觉反馈和 M7 平衡数据采集可以部分并行。

不应并行的工作:

- 不要在 M1 拆分之前继续往 `OnlineMatchController` 添加大型新功能。
- 不要在 M0 编译未通过时做功能开发。
- 不要在 M3 任务协议未确定时同时做多个独立小游戏接入。

## 4. 任务清单

### M0: 基线验证

#### Task 0.1: 工程编译基线

**描述:** 打开 Unity 6000.4.5f1，确认当前工程是否能无 C# 编译错误进入 Play Mode。

**验收标准:**

- Unity Console 没有 C# compile error。
- 如果有错误，输出 `output/baseline_compile_errors_YYYYMMDD.md`，每个错误包含文件、行号、复现方式。
- 不修非阻断 warning，除非它阻止 Play Mode。

**验证:**

- Unity Editor 打开项目。
- 进入 Play Mode。
- 记录 Console 状态。

**依赖:** 无。

**可能涉及文件:**

- `Assets/_Project/Scripts/**`
- `Packages/manifest.json`
- `ProjectSettings/ProjectVersion.txt`

**预计规模:** S，如果有编译错误则按错误数量拆分。

#### Task 0.2: 本地试玩完整局

**描述:** 使用本地试玩/预览路径跑一局，从房间到结算，记录当前真实体验。

**验收标准:**

- 完成 Lobby -> Opening -> Action -> Meeting/Voting -> Result。
- 记录首个阻断问题和所有高频体验问题。
- 保存至少 3 张截图或一段录屏: 大厅、行动、会议/结算。

**验证:**

- 本地 Play Mode。
- 只用默认玩家路径，不用编辑器作弊按钮完成关键流程。

**依赖:** Task 0.1。

**可能涉及文件:**

- `Assets/_Project/Scripts/Online/OnlineMatchController.cs`
- `Assets/_Project/Scripts/Online/OnlineMatchHud.cs`
- `output/baseline_local_play_YYYYMMDD.md`

**预计规模:** XS。

#### Task 0.3: 本机双开联机烟测

**描述:** 使用 Unity Multiplayer Play Mode 或两个构建实例进行 Host + Client 双开，验证核心同步。

**验收标准:**

- Host 创建房间成功。
- Client 加入成功。
- 两端玩家位置同步。
- Host 开始游戏后双方收到角色。
- 至少完成一次任务、一次击杀、一次报案、一次投票、一次结算。

**验证:**

- 执行 `output/online_test_plan.md` 的 TC-01 到 TC-12。
- 输出 `output/baseline_online_smoke_YYYYMMDD.md`。

**依赖:** Task 0.1。

**可能涉及文件:**

- `Assets/_Project/Scripts/Online/OnlineMatchController.cs`
- `Assets/_Project/Scripts/Online/GameStateSnapshot.cs`
- `Assets/_Project/Scripts/Online/MeetingSync.cs`
- `Assets/_Project/Scripts/Online/TaskSync.cs`

**预计规模:** S。

#### Task 0.4: 当前差距复核清单

**描述:** 把旧文档中的“缺失功能”逐项核对为当前状态，避免继续沿用过期判断。

**验收标准:**

- 对通风管、监控、紧急任务、小游戏、鬼魂、语音、Relay、Host Migration 给出当前状态。
- 每项标记为: 已联机可用、仅离线可用、代码存在未验证、缺失、服务阻塞。
- 输出 `output/current_gap_matrix_YYYYMMDD.md`。

**验证:**

- 以源码和 Play Mode 结果为准。
- 不只引用旧阶段报告。

**依赖:** Task 0.1 到 0.3。

**可能涉及文件:**

- `output/stage14_review.md`
- `output/among_us_roadmap.md`
- `Assets/_Project/Scripts/SocialDeduction/**`
- `Assets/_Project/Scripts/Online/**`

**预计规模:** S。

#### Checkpoint M0

- [ ] Unity 0 编译错误，或编译错误已编号。
- [ ] 本地完整局可完成，或阻断点已编号。
- [ ] Host/Client 双开核心同步可完成，或阻断点已编号。
- [ ] 当前差距矩阵已更新。

### M1: 联机核心瘦身

#### Task 1.1: 建立联机规则配置层

**描述:** 从 `OnlineMatchController` 中抽出房间规则和常量，形成 `OnlineRuleSet` 或 ScriptableObject。第一步只迁移值，不改变行为。

**验收标准:**

- 击杀范围、报案范围、会议时间、投票时间、证据目标、最小/最大人数、AI 补位、紧急会议次数都从一个配置对象读取。
- 默认值与当前行为一致。
- 房间 UI 和快照同步读取同一份规则。

**验证:**

- EditMode 测试默认规则值。
- 本地试玩和双开烟测不变。

**依赖:** M0。

**可能涉及文件:**

- `Assets/_Project/Scripts/Online/OnlineRuleSet.cs`
- `Assets/_Project/Scripts/Online/OnlineMatchController.cs`
- `Assets/_Project/Scripts/Online/OnlineMatchHud.cs`
- `Assets/_Project/Scripts/Online/GameStateSnapshot.cs`

**预计规模:** M。

#### Task 1.2: 抽出地图坐标服务

**描述:** 把 `ScaleMapPosition`、出生点、任务点、暗线点、地图区域定义抽成 `OnlineMapService` 或 `OnlineMapDefinition`。

**验收标准:**

- 任务点、出生点、会议点、暗线点全部通过同一坐标服务生成。
- 不再出现一部分点位手动 Scale、一部分点位未 Scale 的情况。
- MiniMap、世界生成、交互距离使用同一位置源。

**验证:**

- 添加坐标单元测试: 设计坐标到运行坐标只转换一次。
- Play Mode 中玩家能正确接近任务点和暗线点。

**依赖:** Task 1.1。

**可能涉及文件:**

- `Assets/_Project/Scripts/Online/OnlineMapService.cs`
- `Assets/_Project/Scripts/Online/OnlineMatchController.cs`
- `Assets/_Project/Scripts/Online/OnlineMatchHud.cs`

**预计规模:** M。

#### Task 1.3: 统一暗线/通风管系统

**描述:** 合并 `UnderworldPassage` 与 `OnlineVents`，只保留一套联机机动规则。警匪题材命名使用“暗线通道”，技术实现可以复用通风管逻辑。

**验收标准:**

- `V` 或 `F` 的使用规则清晰，不存在两个不同入口触发不同瞬移。
- 黑帮/线人可用，警方不可用。
- 冷却、目的地选择、音效、状态提示统一。
- 所有节点位置来自 Task 1.2 的地图服务。

**验证:**

- 双开中黑帮使用暗线后，所有客户端位置一致。
- 非黑帮按同一按键不会瞬移，并得到明确提示。
- 冷却在快照中同步。

**依赖:** Task 1.2。

**可能涉及文件:**

- `Assets/_Project/Scripts/Online/OnlineMatchController.cs`
- `Assets/_Project/Scripts/Online/GameStateSnapshot.cs`
- `Assets/_Project/Scripts/Online/OnlineMatchHud.cs`
- `Assets/_Project/Scripts/SocialDeduction/VentSystem.cs`，仅复用概念时参考，不强依赖

**预计规模:** M。

#### Task 1.4: 抽出任务状态服务

**描述:** 把联机任务状态、完成、破坏、修复、证据增减从 `OnlineMatchController` 移入 `OnlineTaskService`。

**验收标准:**

- 任务完成、破坏、修复逻辑不再直接散落在 `TryInteractWithTask`。
- `TaskSync` 与新服务关系清楚: 一个负责本地规则，一个负责同步/分配，或者合并为一个可测试服务。
- 任务状态变更发出事件供 HUD、音频、日志、胜负判定订阅。

**验证:**

- EditMode 测试任务完成加证据、破坏扣证据、修复清状态。
- 双开任务状态同步不退化。

**依赖:** Task 1.1。

**可能涉及文件:**

- `Assets/_Project/Scripts/Online/OnlineTaskService.cs`
- `Assets/_Project/Scripts/Online/TaskSync.cs`
- `Assets/_Project/Scripts/Online/OnlineMatchController.cs`
- `Assets/_Project/Scripts/Online/OnlineVictoryBridge.cs`

**预计规模:** M。

#### Task 1.5: 抽出会议投票服务

**描述:** 把报案、紧急会议、投票、平票、淘汰、身份公开从控制器移入 `OnlineMeetingService`。

**验收标准:**

- 报案和紧急会议触发条件有单一入口。
- 投票统计和淘汰结算可单元测试。
- `MeetingSync` 不再只是旁路记录，而是与主流程明确集成。

**验证:**

- EditMode 测试: 平票、跳过票、最高票淘汰、会议次数耗尽、通讯干扰禁止紧急会议。
- 双开投票结果一致。

**依赖:** Task 1.1。

**可能涉及文件:**

- `Assets/_Project/Scripts/Online/OnlineMeetingService.cs`
- `Assets/_Project/Scripts/Online/MeetingSync.cs`
- `Assets/_Project/Scripts/Online/OnlineMatchController.cs`

**预计规模:** M。

#### Task 1.6: 移除破坏状态反射读取

**描述:** `SabotageSync.cs` 当前反射读取控制器私有 timer 字段。改成显式状态模型或事件。

**验收标准:**

- `SabotageSync` 不使用 `System.Reflection` 读取 `OnlineMatchController` 私有字段。
- 破坏开始、倒计时、结束、修复都通过公开只读状态或事件获得。
- HUD 与日志显示不退化。

**验证:**

- 搜索 `GetField(` 和 `BindingFlags.NonPublic`，确认破坏同步不再依赖。
- 双开触发 5 类破坏，Client HUD 都收到状态。

**依赖:** Task 1.4。

**可能涉及文件:**

- `Assets/_Project/Scripts/Online/SabotageSync.cs`
- `Assets/_Project/Scripts/Online/SabotagePanel.cs`
- `Assets/_Project/Scripts/Online/OnlineTaskService.cs`
- `Assets/_Project/Scripts/Online/OnlineMatchController.cs`

**预计规模:** S-M。

#### Task 1.7: 抽出 Bot 决策

**描述:** 把 Bot 移动、任务选择、击杀、破坏、投票从控制器中抽到 `OnlineBotService`。

**验收标准:**

- Bot 行为在服务中可配置、可测试。
- 不改变当前 Bot 行为，先只移动代码。
- 后续 M7 可单独升级 Bot。

**验证:**

- 本地 AI 补位局能继续自动移动和投票。
- EditMode 测试 Bot 目标选择不返回无效任务。

**依赖:** Task 1.4、Task 1.5。

**可能涉及文件:**

- `Assets/_Project/Scripts/Online/OnlineBotService.cs`
- `Assets/_Project/Scripts/Online/OnlineMatchController.cs`

**预计规模:** M。

#### Checkpoint M1

- [ ] `OnlineMatchController.cs` 低于 8,000 行，或已完成最关键职责拆分。
- [ ] 规则、地图、任务、会议至少 3 个服务可单独测试。
- [ ] 暗线/通风管系统只有一套联机实现。
- [ ] 双开烟测通过。

### M2: 标准局闭环

#### Task 2.1: 标准房间规则同步

**描述:** 让 Host 在大厅配置的规则同步到所有 Client，包括人数、任务、证据目标、会议、投票、击杀冷却。

**验收标准:**

- Client 不能修改 Host 规则。
- Client HUD 显示 Host 当前规则。
- 开局后规则锁定，不因中途 UI 改动漂移。

**验证:**

- 双开修改规则，Client 看到一致。
- 开局后修改 UI 不影响当前局。

**依赖:** Task 1.1。

**可能涉及文件:**

- `OnlineRuleSet.cs`
- `GameStateSnapshot.cs`
- `OnlineMatchHud.cs`
- `OnlineMatchController.cs`

**预计规模:** M。

#### Task 2.2: 角色分配规则收口

**描述:** 按 5、8、10 人三档定义警、黑帮、线人、卧底数量，减少随机导致的坏局。

**验收标准:**

- 5 人: 1 黑帮，1 卧底或线人可选，剩余警方。
- 8 人: 2 黑帮或 1 黑帮 + 1 线人，1 卧底，剩余警方。
- 10 人: 2 黑帮，1 线人，1 卧底，剩余警方。
- 角色私发，公共身份仍隐藏。

**验证:**

- EditMode 测试不同人数的角色分配。
- 双开和 AI 补位局验证私发角色。

**依赖:** Task 2.1。

**可能涉及文件:**

- `OnlineMatchController.cs`
- `OnlineRole.cs`
- `OnlineRuleSet.cs`

**预计规模:** S。

#### Task 2.3: 局长和节奏调参

**描述:** 将默认局长调到 10-15 分钟，把会议总时间从当前偏长状态收紧。

**验收标准:**

- 默认讨论时间 25-35 秒。
- 默认投票时间 30-40 秒。
- 击杀冷却按人数缩放: 人少更长，人多更短。
- 证据目标与任务数量按人数缩放。

**验证:**

- 本地 5/8/10 人 AI 局各跑 3 局，记录平均局长。
- 观察是否 5 分钟内过早结束或 20 分钟仍无结论。

**依赖:** Task 2.1、Task 2.2。

**可能涉及文件:**

- `OnlineRuleSet.cs`
- `OnlineTaskService.cs`
- `OnlineVictoryBridge.cs`

**预计规模:** S。

#### Task 2.4: 在线鬼魂基础状态

**描述:** 死亡后不只是显示出局，要有在线鬼魂状态: 观战、穿墙、不能报案/击杀/开会，可继续看局势。

**验收标准:**

- 死亡玩家控制切换为观战或鬼魂移动。
- 死亡玩家不能触发报案、会议、投票前发言、任务破坏。
- HUD 明确显示鬼魂状态和可做事项。

**验证:**

- 双开击杀 Client，Client 仍能移动/观战但不能影响活人规则。
- 会议时死亡玩家不能投票。

**依赖:** Task 1.5。

**可能涉及文件:**

- `OnlineMatchController.cs`
- `OnlineMatchHud.cs`
- `GameStateSnapshot.cs`
- `Assets/_Project/Scripts/Gameplay/GhostMode.cs`，可参考

**预计规模:** M。

#### Task 2.5: 胜负判定矩阵

**描述:** 明确定义并测试所有胜负条件，避免多个系统重复判定。

**验收标准:**

- 警方胜: 证据链闭合、黑帮全灭。
- 黑帮胜: 黑帮人数压制、证据链被拖到超时失败、关键破坏失败。
- 平局或异常状态有明确处理。
- 胜负只由单一服务输出，UI 只展示。

**验证:**

- EditMode 测试每个条件。
- 双开局触发至少证据胜和人数压制胜。

**依赖:** Task 1.4、Task 1.5。

**可能涉及文件:**

- `OnlineVictoryBridge.cs`
- `VictoryEvaluator.cs`
- `OnlineMatchController.cs`

**预计规模:** M。

#### Checkpoint M2

- [ ] 5/8/10 人局都能完整进行。
- [ ] 默认规则可产生 10-15 分钟目标局长。
- [ ] 鬼魂不退出体验且不破坏规则。
- [ ] 胜负条件有测试。

### M3: 联机小游戏和破坏修复

#### Task 3.1: 设计联机小游戏运行协议

**描述:** 定义联机小游戏如何启动、提交、校验、取消、同步。不要同步每帧拖拽，只同步结果和必要状态。

**验收标准:**

- 有 `MiniGameDefinition` 或等价结构。
- 有 `MiniGameSession` 或等价运行态。
- Host 权威接收结果，Client 不能直接修改证据。
- 任务失败、取消、断线有处理。

**验证:**

- 编写协议文档 `output/minigame_runtime_contract_YYYYMMDD.md`。
- EditMode 测试合法/非法结果提交。

**依赖:** Task 1.4。

**可能涉及文件:**

- `Assets/_Project/Scripts/Online/MiniGames/OnlineMiniGameDefinition.cs`
- `Assets/_Project/Scripts/Online/MiniGames/OnlineMiniGameSession.cs`
- `Assets/_Project/Scripts/SocialDeduction/MiniGames/MiniGameBase.cs`

**预计规模:** M。

#### Task 3.2: 接入第一个真实任务 WireTask

**描述:** 将离线 WireTask 或等价 Canvas 版本接入联机任务，作为统一协议的第一个 vertical slice。

**验收标准:**

- 玩家靠近电力/货柜类任务，打开真实小游戏。
- 完成后向 Host 提交结果。
- Host 校验通过后增加任务进度/证据。
- 取消不会卡住 activeTask。

**验证:**

- 单机、本地双开分别完成 WireTask。
- Client 断开任务面板后再次进入可继续或重开。

**依赖:** Task 3.1。

**可能涉及文件:**

- `WireTask.cs`
- `OnlineMiniGameSession.cs`
- `OnlineTaskService.cs`
- `OnlineMatchHud.cs`

**预计规模:** M。

#### Task 3.3: 接入 KeypadTask 和 SwipeCardTask

**描述:** 接入门禁/证物扫描类任务，覆盖两种不同交互模式。

**验收标准:**

- Keypad 用于门禁/保险箱/锁定修复。
- SwipeCard 用于证物/档案扫描。
- 两者都走同一提交协议。

**验证:**

- 双开完成两类任务。
- 错误输入不会直接完成任务。

**依赖:** Task 3.2。

**可能涉及文件:**

- `KeypadTask.cs`
- `SwipeCardTask.cs`
- `OnlineMiniGameSession.cs`
- `OnlineTaskService.cs`

**预计规模:** M。

#### Task 3.4: 接入 MemoryTask、DownloadTask、EvidenceArchiveTask

**描述:** 增加信息类、等待类、证据归档类任务，形成至少 6 种联机任务。

**验收标准:**

- MemoryTask 用于监控回放。
- DownloadTask 用于数据上传/下载。
- EvidenceArchiveTask 用于证据归档。
- 任务面板有不同视觉，不只是同一模板换文字。

**验证:**

- 双开完成 6 种不同任务。
- 全局任务进度正确增长。

**依赖:** Task 3.3。

**可能涉及文件:**

- `MemoryTask.cs`
- `DownloadTask.cs`
- `EvidenceArchiveTask.cs`
- `MiniGameType.cs`
- `OnlineTaskService.cs`
- `OnlineMatchHud.cs`

**预计规模:** M-L，建议拆成 3 个小 PR/提交。

#### Task 3.5: 破坏修复小游戏

**描述:** 破坏不再只是状态标记，必须用小游戏修复。

**验收标准:**

- Blackout 需要 Wire/Breaker 修复。
- Communications 需要 Calibrate 修复。
- Lockdown 需要 Keypad 修复。
- EvidenceLeak 需要 EvidenceArchive 修复。
- 修复失败或取消不会立即清除破坏。

**验证:**

- 双开黑帮触发每类破坏，警方完成对应小游戏修复。
- 破坏倒计时和修复状态在 Client HUD 一致。

**依赖:** Task 3.2 到 3.4。

**可能涉及文件:**

- `SabotageType.cs`
- `OnlineTaskService.cs`
- `SabotageSync.cs`
- `OnlineMatchHud.cs`

**预计规模:** M。

#### Task 3.6: 全局任务进度和个人任务清单

**描述:** 增加 Among Us 风格全局进度，但保持警匪证据链表达。

**验收标准:**

- HUD 显示证据链和全局任务进度。
- 玩家能看到自己被分配的任务。
- 黑帮看到伪装任务或破坏目标，不直接暴露真实警方任务策略。

**验证:**

- 双开不同角色查看任务清单。
- 完成任务后全局进度一致。

**依赖:** Task 1.4、Task 3.1。

**可能涉及文件:**

- `TaskSync.cs`
- `OnlineTaskService.cs`
- `OnlineMatchHud.cs`
- `GameStateSnapshot.cs`

**预计规模:** M。

#### Checkpoint M3

- [ ] 联机至少 6 种真实小游戏。
- [ ] 破坏必须通过小游戏修复。
- [ ] Host 权威控制任务结果。
- [ ] 任务 UI 取消、断线、重进不死锁。

### M4: 信息和欺骗系统

#### Task 4.1: 联机监控基础版

**描述:** 做可用优先的监控，不一开始追求真实 RenderTexture。监控站显示低频区域/玩家轨迹，用于会议推理。

**验收标准:**

- 玩家靠近监控站可打开监控界面。
- 监控显示若干区域的最近活动或可疑移动。
- 黑帮可破坏监控，修复前监控不可用或信息延迟。

**验证:**

- 双开中一名玩家移动，另一名在监控站能看到有用信息。
- 黑帮破坏监控后信息变化。

**依赖:** Task 1.2、Task 3.5。

**可能涉及文件:**

- `SecurityCamera.cs`
- `OnlineMapService.cs`
- `OnlineMatchHud.cs`
- `OnlineTaskService.cs`

**预计规模:** M。

#### Task 4.2: 会议证据板

**描述:** 会议界面不只是投票名单，要展示可推理信息。

**验收标准:**

- 显示尸体地点、报案者、最后发现时间。
- 显示最近破坏、最近任务完成、监控线索。
- 显示玩家嫌疑摘要，但不显示真实阵营。
- 投票 UI 和证据板同屏或可切换。

**验证:**

- 触发报案进入会议，证据板展示至少 3 类线索。
- Client 和 Host 显示一致。

**依赖:** Task 1.5、Task 4.1。

**可能涉及文件:**

- `OnlineMeetingService.cs`
- `OnlineMatchHud.cs`
- `ChatSystem.cs`
- `GameStateSnapshot.cs`

**预计规模:** M。

#### Task 4.3: 嫌疑值规则

**描述:** 把嫌疑值从局部数值变成可解释系统。嫌疑变化要来自行为，不是随机。

**验收标准:**

- 靠近尸体、破坏地点、监控视野、任务失败、暗线使用痕迹会影响嫌疑。
- 警方职业技能可揭示或降低噪声。
- 会议界面只显示“线索强度”，不直接确认身份。

**验证:**

- EditMode 测试嫌疑增减。
- 双开中触发破坏和尸体，会议证据板显示变化。

**依赖:** Task 4.2。

**可能涉及文件:**

- `OnlinePlayerState`
- `OnlineMeetingService.cs`
- `OnlineVictoryBridge.cs`
- `OnlineMatchHud.cs`

**预计规模:** M。

#### Task 4.4: 暗线通道可读性

**描述:** 黑帮机动需要强体验和弱线索。黑帮知道节点，警方能通过痕迹推理。

**验收标准:**

- 黑帮 HUD 显示附近暗线和冷却。
- 使用暗线有短动画/音效。
- 警方不可直接看到所有节点，但可在监控或线索板看到“后巷活动”。

**验证:**

- 双开黑帮使用暗线，警方不会直接看到按钮，但会议有间接线索。

**依赖:** Task 1.3、Task 4.2。

**可能涉及文件:**

- `OnlineMapService.cs`
- `OnlineMatchHud.cs`
- `OnlineAudioCueService.cs`
- `OnlineMeetingService.cs`

**预计规模:** M。

#### Checkpoint M4

- [ ] 监控能产生可讨论信息。
- [ ] 会议证据板能支持推理。
- [ ] 嫌疑系统可解释。
- [ ] 暗线移动有体验和线索。

### M5: 服务、房间和语音

#### Task 5.1: Unity Cloud Project 绑定验证

**描述:** 确认 `Application.cloudProjectId` 存在，并完成 Authentication、Relay、Lobby 的真实初始化。

**验收标准:**

- `UnityServiceBootstrap` 显示 Cloud、Services、Auth、Lobby、Relay 均 OK。
- 初始化失败时 UI 给出明确原因，不只显示“待初始化”。

**验证:**

- 真机联网运行。
- Relay 创建房间码成功。

**依赖:** M0。

**可能涉及文件:**

- `UnityServiceBootstrap.cs`
- `OnlineMatchHud.cs`
- `ProjectSettings/UnityConnectSettings.asset`

**预计规模:** S，环境问题除外。

#### Task 5.2: 房间码路径成品化

**描述:** 玩家通过主菜单/大厅创建或加入 Relay 房间，不依赖 OnGUI 调试路径。

**验收标准:**

- 创建房间后展示房间码。
- 加入房间后进入玩家列表。
- Ready、AI 补位、规则设置、开始游戏都在 Canvas UI 中完成。

**验证:**

- 两台机器通过房间码完成开局。

**依赖:** Task 5.1。

**可能涉及文件:**

- `LobbyController.cs`
- `OnlineMatchHud.cs`
- `OnlineMatchController.cs`
- `MainMenuController.cs`

**预计规模:** M。

#### Task 5.3: 语音方案决策和实现

**描述:** 现在 Vivox 是 stub。必须恢复真实语音，或者从 UI 和规则中移除语音承诺。

**验收标准:**

- 若使用 Vivox: 包、初始化、登录、频道加入、位置更新、静音、离开频道全部可用。
- 若不用 Vivox: UI 不再显示“近距离语音规则”，计划改为文本聊天或第三方语音。
- 行动、会议、鬼魂三个频道规则明确。

**验证:**

- 双机测试行动近距离语音。
- 会议时所有活人同频道。
- 死亡玩家进入鬼魂频道。

**依赖:** Task 5.1。

**可能涉及文件:**

- `UnityServiceBootstrap.cs`
- `VoiceChatSystem.cs`
- `OnlineMatchController.cs`
- `OnlineMatchHud.cs`
- `Packages/manifest.json`

**预计规模:** L，建议单独完成。

#### Task 5.4: 断线和重连策略

**描述:** 明确 Client 断线、Host 断线、AI 接管、重连的产品规则。

**验收标准:**

- Client 断线 60 秒内保留状态，之后可 AI 接管或移除。
- Host Migration 要么真实可用，要么 UI 明确“Host 断线本局终止”。
- 断线不会卡住投票、任务或胜负判定。

**验证:**

- 双机中强退 Client，Host 继续。
- 强退 Host，观察迁移或终止路径。

**依赖:** Task 1.5、Task 5.2。

**可能涉及文件:**

- `HostMigrationManager.cs`
- `GameStateSnapshot.cs`
- `OnlineMatchController.cs`
- `OnlineBotService.cs`

**预计规模:** M-L。

#### Task 5.5: Host 权威和基础反作弊

**描述:** Client 只提交输入和意图，最终状态由 Host 校验。

**验收标准:**

- 移动速度超限被拒绝或纠正。
- 击杀距离、冷却、目标存活由 Host 校验。
- 任务结果由 Host 校验。
- 投票只能在会议/投票阶段提交一次。

**验证:**

- 人为发送非法操作不会改变权威状态。
- 日志记录非法尝试。

**依赖:** Task 1.4、Task 1.5、Task 3.1。

**可能涉及文件:**

- `OnlineMatchController.cs`
- `OnlineTaskService.cs`
- `OnlineMeetingService.cs`
- `PlayerStateSync.cs`

**预计规模:** M。

#### Checkpoint M5

- [ ] Relay 双机完整局可跑。
- [ ] 房间码 Canvas 路径可用。
- [ ] 语音真实可用或 UI 明确隐藏。
- [ ] 断线不会卡局。
- [ ] Host 权威覆盖关键操作。

### M6: 成品 UI、动画和反馈

#### Task 6.1: 默认关闭 OnGUI 玩家路径

**描述:** OnGUI 保留为开发开关，默认玩家只看到 Canvas UI。

**验收标准:**

- 主菜单、房间、行动、会议、结算都不需要 OnGUI。
- 开发模式可打开调试面板。
- 玩家路径没有重复 UI。

**验证:**

- 新建空配置启动，默认只显示 Canvas。
- 开启 debug flag 后可看到调试信息。

**依赖:** Task 5.2。

**可能涉及文件:**

- `OnlineMatchController.cs`
- `OnlineMatchHud.cs`
- `UIManager.cs`
- `MainMenuController.cs`
- `LobbyController.cs`

**预计规模:** M。

#### Task 6.2: 行动 HUD 收口

**描述:** 行动 HUD 必须清楚显示角色目标、任务、报案、暗线、技能、危险状态。

**验收标准:**

- 玩家 3 秒内知道下一步去哪里。
- 黑帮和警方看到不同目标文案。
- 任务/破坏/尸体/会议提示不互相遮挡。
- 小地图和案情板可切换。

**验证:**

- 1366x768、1920x1080、2560x1440 三种分辨率截图检查。
- 实机操作 10 分钟无 UI 重叠阻断。

**依赖:** Task 6.1。

**可能涉及文件:**

- `OnlineMatchHud.cs`
- `ThemeManager.cs`
- `SettingsData.cs`

**预计规模:** M。

#### Task 6.3: 会议和投票 UI 成品化

**描述:** 会议是社交推理核心，必须比当前信息面板更清楚。

**验收标准:**

- 玩家座位、存活/死亡、投票状态清晰。
- 证据板、聊天/语音状态、投票按钮可见。
- 投票结果动画能解释平票、跳过、淘汰、身份是否公开。

**验证:**

- 双开会议至少 3 次，含平票和淘汰。
- 会议倒计时和投票倒计时准确。

**依赖:** Task 4.2、Task 6.1。

**可能涉及文件:**

- `OnlineMatchHud.cs`
- `OnlineMeetingService.cs`
- `GameOverController.cs`

**预计规模:** M-L。

#### Task 6.4: 击杀、报案、淘汰、胜负反馈

**描述:** 给关键事件加动画、音效、相机和短提示。

**验收标准:**

- 击杀有明显反馈但不暴露凶手给非目击者。
- 尸体可读，报案按钮明确。
- 淘汰动画能显示是否公开身份。
- 结算解释胜负原因。

**验证:**

- 双开击杀、报案、投票淘汰、结算各触发一次。
- 无音频资源时 fallback tone 不刺耳或可关闭。

**依赖:** Task 2.5、Task 6.3。

**可能涉及文件:**

- `KillSystem.cs`
- `BodyVisual.cs`
- `OnlineAudioCueService.cs`
- `GameOverController.cs`
- `OnlineMatchHud.cs`

**预计规模:** M。

#### Task 6.5: 设置菜单和可访问性

**描述:** 增加玩家必需设置。

**验收标准:**

- 音量、分辨率、画质、按键、语音开关可保存。
- 色盲辅助至少支持阵营/任务/破坏状态的非颜色标识。
- 设置在游戏中可打开但不破坏输入。

**验证:**

- 改设置、重启、确认持久化。
- 不同分辨率 UI 不溢出。

**依赖:** Task 6.1。

**可能涉及文件:**

- `SettingsManager.cs`
- `SettingsData.cs`
- `SettingsUIHelper.cs`
- `OnlineMatchHud.cs`

**预计规模:** M。

#### Checkpoint M6

- [ ] 默认玩家路径全 Canvas。
- [ ] 新玩家能理解目标、任务、报案、会议、投票。
- [ ] 核心事件有反馈。
- [ ] 设置可保存。

### M7: 内容、平衡和复玩性

#### Task 7.1: 港区地图收口

**描述:** 第一张地图要减少纯装饰噪声，强化路线、视野、任务点、暗线、监控、会议点。

**验收标准:**

- 主要路线 3 条以上，且存在绕路风险。
- 任务点分布能制造分散和目击机会。
- 暗线节点不直接贴近所有关键任务。
- 监控覆盖关键路口但有盲区。

**验证:**

- 8 人局观察 5 局，记录击杀地点和报案时间。
- 玩家不会频繁迷路。

**依赖:** Task 4.1、Task 4.4。

**可能涉及文件:**

- `OnlineMapService.cs`
- `OnlineMatchController.cs` 的世界生成部分，若尚未完全拆出
- 场景/Prefab 资源

**预计规模:** M-L。

#### Task 7.2: 警署地图联机化

**描述:** 把离线警署图作为第二张联机地图，不只是离线场景。

**验收标准:**

- 房间可选择港区/警署。
- 警署有独立任务点、暗线点、监控点、会议点。
- 任务和破坏配置适配警署。

**验证:**

- 双开警署图完成一局。
- 地图选择同步到 Client。

**依赖:** Task 1.2、Task 3.6。

**可能涉及文件:**

- `PoliceStationMap.cs`
- `PoliceStationTasks.cs`
- `OnlineMapService.cs`
- `OnlineMatchHud.cs`

**预计规模:** L，建议拆为地图选择、点位配置、联机验证三步。

#### Task 7.3: 职业能力收敛

**描述:** 当前职业多，但每个必须容易理解。先保留 5-7 个高价值职业。

**验收标准:**

- 每个职业一句话能说明。
- 每个职业只有一个主技能。
- 技能有明确冷却、反馈和反制。
- 弱差异职业合并或移除。

**验证:**

- 8 人局中每个职业至少使用一次技能。
- 玩家能复述职业作用。

**依赖:** Task 2.1。

**可能涉及文件:**

- `OnlineRole.cs`
- `OnlineMatchController.cs` 或拆出的能力服务
- `OnlineMatchHud.cs`

**预计规模:** M。

#### Task 7.4: Bot 行为升级

**描述:** Bot 不能只随机行动。它需要支撑少人测试和 AI 补位体验。

**验收标准:**

- 黑帮 Bot 优先找落单目标、避开监控、在高价值任务点破坏。
- 警方 Bot 优先做任务、修复、报案、根据嫌疑投票。
- Bot 不应该明显作弊知道真实身份，除非难度配置允许。

**验证:**

- 2 真人 + 6 Bot 跑 5 局。
- Bot 行为不阻断局势。

**依赖:** Task 1.7、Task 4.3。

**可能涉及文件:**

- `OnlineBotService.cs`
- `OpponentAi.cs`，可参考
- `OnlineMeetingService.cs`

**预计规模:** M-L。

#### Task 7.5: 平衡数据采集

**描述:** 没有数据就无法调到 10-15 分钟和 45-55 胜率。

**验收标准:**

- 每局记录: 人数、地图、角色配置、局长、胜方、首杀时间、会议次数、任务完成率、破坏次数、玩家断线。
- 输出本地 CSV 或 JSON 日志。
- 不上传隐私数据。

**验证:**

- 跑 10 局后生成可分析文件。

**依赖:** Task 2.5。

**可能涉及文件:**

- `MatchAnalytics.cs`
- `OnlineVictoryBridge.cs`
- `GameOverController.cs`

**预计规模:** M。

#### Task 7.6: 平衡迭代

**描述:** 基于数据调整规则，不凭感觉。

**验收标准:**

- 8 人局平均 10-15 分钟。
- 警方/黑帮胜率 45-55，封测早期可接受 40-60。
- 平均会议次数 2-4。
- 首杀时间大多在 1.5-4 分钟。

**验证:**

- 至少 20 局样本，含真人局。

**依赖:** Task 7.5。

**可能涉及文件:**

- `OnlineRuleSet.cs`
- 地图配置
- 任务配置

**预计规模:** 持续任务。

#### Checkpoint M7

- [ ] 第一张地图路线清晰。
- [ ] 第二张图可联机。
- [ ] 职业减少到可理解集合。
- [ ] Bot 能补位。
- [ ] 有平衡数据。

### M8: 封测和发行准备

#### Task 8.1: 自动化编译和烟测

**描述:** 建立最小 CI 或本地脚本，避免每次改动都靠手工发现编译错误。

**验收标准:**

- 一条命令能做 Unity batchmode 编译或打开工程检测脚本编译。
- 核心 EditMode/PlayMode 测试可运行。
- 失败输出日志路径。

**验证:**

- 本地运行脚本，故意制造一个测试失败能看到失败。

**依赖:** M1 至少完成核心服务测试。

**可能涉及文件:**

- `Assets/_Project/Editor/PrototypeSmokeTests.cs`
- `Assets/_Project/Editor/*Tests.cs`
- `scripts/`

**预计规模:** M。

#### Task 8.2: 构建产物

**描述:** 输出至少一个外部玩家可运行的包。

**验收标准:**

- macOS 或 Windows 至少一个平台构建成功。
- 构建包含必要资源。
- 启动后能进入主菜单、创建/加入房间。

**验证:**

- 在非 Editor 环境运行。
- 双机或双实例测试。

**依赖:** Task 8.1、Task 5.2。

**可能涉及文件:**

- `ProjectSettings/EditorBuildSettings.asset`
- `ProjectSettings/ProjectSettings.asset`
- 构建脚本

**预计规模:** M。

#### Task 8.3: 崩溃和日志

**描述:** 封测需要知道玩家为什么卡住。

**验收标准:**

- 本地日志包含房间、阶段、角色、错误、断线原因。
- 一键导出日志包。
- 不包含敏感 token 或个人隐私。

**验证:**

- 模拟断线和异常，导出日志。

**依赖:** Task 5.4。

**可能涉及文件:**

- `RuntimeLogCollector.cs`
- `OnlineMatchController.cs`
- `UnityServiceBootstrap.cs`

**预计规模:** S-M。

#### Task 8.4: 封闭测试流程

**描述:** 准备 6-10 人封测的操作说明和反馈模板。

**验收标准:**

- 玩家只需看 1 页说明即可开局。
- 反馈表收集: 是否能开房、是否听得懂目标、最困惑点、最有趣点、bug、卡顿、局长。
- 每轮封测后输出问题优先级列表。

**验证:**

- 内部 2 人先读说明，确认能进入房间。

**依赖:** Task 8.2。

**可能涉及文件:**

- `output/playtest_guide_YYYYMMDD.md`
- `output/playtest_feedback_template_YYYYMMDD.md`

**预计规模:** S。

#### Task 8.5: 发布门槛

**描述:** 明确 Alpha、Beta、RC 不同阶段的门槛。

**验收标准:**

- Alpha: 熟人 6-10 人稳定玩 5 局，P0 为 0。
- Beta: 外部玩家不看开发者说明能玩完 3 局，P1 可控。
- RC: 72 小时无 P0/P1，开房和加入成功率高于 95%。

**验证:**

- 使用封测数据评估，不主观宣布。

**依赖:** Task 8.4。

**可能涉及文件:**

- `output/release_gate_YYYYMMDD.md`

**预计规模:** XS。

#### Checkpoint M8

- [ ] 可构建。
- [ ] 可分发。
- [ ] 可收集日志。
- [ ] 可组织封测。
- [ ] 发布门槛清楚。

## 5. 地图、美术、2D 表现、UI 专项推进计划

### 5.1 核心原则

地图不是背景，地图是社交推理的棋盘。美术、2D 地图表现、UI 都必须服务同一个目标: 玩家能快速判断自己在哪里、别人可能从哪里来、尸体在哪里被发现、任务和破坏在哪里发生、会议中哪些路线可以被推理。

本计划采用 **2D 优先**。正式地图推荐使用 top-down/正交 2D 表现，现有 3D 资源只作为概念参考、临时灰盒或必要时预渲染为 sprite，不再作为第一版正式地图的主要生产路线。

这样做的原因:

- 2D 更接近 Among Us 的核心优势: 读图清楚、遮挡少、任务点和尸体容易看见。
- 2D 美术成本低于完整 3D 城市场景，迭代速度快。
- 地图可以用 Tilemap/Sprite 快速调整，不必每次改路线都重摆 3D 模型。
- UI、小地图、会议证据板更容易和世界坐标保持一致。
- 现有联机逻辑可以保留，玩家位置仍可用 `Vector3(x, y, 0)`，主要替换渲染和碰撞表现。

所以地图推进顺序必须是:

```text
玩法地图定义
  -> 2D 灰盒关卡
    -> 任务/暗线/监控/破坏点布局
      -> 多人测试
        -> 2D Tile/Sprite 资产包
          -> 灰盒替换为第一版 2D 美术
            -> 色彩/光效/性能
              -> UI 地图和提示系统
                -> 真人测试和调整
```

不要反过来先画大量建筑、道路、摊位、灯牌。那会得到一个“看起来有城市”的画面，但不是一个好玩的社交推理地图。

### 5.1A 2D 技术路线

**保留:**

- 联机 Host/Client、快照同步、任务、会议、胜负、Bot、证据链。
- 地图坐标和玩家移动的逻辑层，可以继续用 `Vector3`，只固定 z 为 0。
- `OnlineMapService` 作为地图点位唯一来源。

**替换:**

- 运行时 3D 建筑/道路/装饰生成 -> 2D Tilemap 或 Sprite prefab。
- 3D 模型任务台 -> 2D 任务点 sprite + 交互高亮。
- 3D 暗线入口 -> 2D 井盖/后巷入口 sprite。
- 3D 监控模型 -> 2D 监控台 sprite + UI 面板。
- 3D 碰撞体 -> 2D 碰撞或继续使用逻辑 Rect 碰撞，但视觉必须和碰撞一致。

**不立即替换:**

- 网络协议不为 2D 重写。
- 任务/会议/投票系统不因 2D 改动推倒重来。
- 如果已有角色 3D 可正常显示，可以短期用 billboard/简化模型过渡；正式版建议改为 2D 角色 sprite 或 2D 骨骼动画。

**推荐渲染方案:**

- Unity Orthographic Camera。
- Tilemap 分层: Floor、Walls、Props、Interactables、Overlay。
- SpriteRenderer 分层: Players、Bodies、TaskHighlights、Effects。
- UI Canvas 分层: HUD、MapOverlay、Meeting、MiniGame、Result。

### 5.2 地图推进阶段

#### Map Phase A: 玩法地图定义

**目标:** 先定义地图的玩法骨架，不讨论具体美术素材。

**港区第一张正式图建议规格:**

- 8-10 个核心区域，不超过 12 个。
- 3 条主路: 上路、中路、下路。
- 2-3 条高风险支路: 后巷、仓库侧门、夜市窄路。
- 1 个公共会议点。
- 6-8 个任务簇，每簇 2-4 个任务点。
- 4 个暗线节点，形成 2 组长距离机动。
- 3-4 个监控覆盖点，覆盖主路但留盲区。
- 2 个强破坏区域: 电力/通讯。
- 1-2 个高风险击杀区域，必须有绕路和目击可能。

**产出物:**

- `output/map_design_harbor_v1.md`
- 顶视图草图，标注区域、路线、任务、暗线、监控、破坏。
- 每个区域一句话说明它的玩法目的。

**验收标准:**

- 不看美术也能理解路线。
- 每个区域都有明确用途: 任务、目击、绕路、破坏、会议、监控、暗线。
- 任务点不会全部集中在安全区。
- 黑帮有机动空间，警方有推理信息来源。

#### Map Phase B: 2D 灰盒关卡

**目标:** 用 Tilemap、简单 Sprite 或纯色矩形做 2D 灰盒地图，只验证走路、视野、碰撞、路线，不做最终美术。

**规则:**

- 只用简单地面块、墙块、门洞、障碍块。
- 不画装饰，不上最终建筑 sprite。
- 所有可走区域、阻挡、门洞、窄路都必须清楚。
- 灰盒阶段允许丑，但不允许路线含糊。

**产出物:**

- `Assets/_Project/Scenes/HarborGreybox2D.unity` 或对应 2D runtime map config。
- `OnlineMapService` 中的港区点位配置。
- 灰盒截图: 全图、小地图、行动视角、会议视角。

**验收标准:**

- 玩家从任意出生点到任意任务点有至少 2 条路线。
- 从常见击杀点到报案点存在 5-12 秒的发现窗口。
- 角色、尸体、任务点在正交行动视角中不被墙体或装饰遮挡。
- 双开测试中黑帮能绕路，警方能追踪路线。

#### Map Phase C: 互动点布局

**目标:** 在灰盒上放置任务、暗线、监控、破坏、会议按钮、出生点。

**布局规则:**

- 任务点分布要制造分散，不要都在主路。
- 暗线出口不能直接贴着所有高价值任务，否则黑帮过强。
- 监控要覆盖关键路口，但必须有盲区。
- 破坏点应迫使玩家跨区移动。
- 尸体常见点附近要有报案路线，不要太隐蔽。

**产出物:**

- `output/map_interaction_layout_harbor_v1.md`
- `OnlineMapService` 或地图配置中的点位表。
- 小地图点位截图。

**验收标准:**

- 8 人 AI 局中任务分布不会让所有玩家长期聚集。
- 黑帮从暗线出来后至少有 2 种路线选择。
- 监控能产生会议线索，但不能直接锁死黑帮。
- 破坏修复会让玩家离开安全区。

#### Map Phase D: 多人玩法测试

**目标:** 灰盒必须先玩得通，再进入正式 2D 美术。

**测试方法:**

- 2 真人 + 6 Bot 跑 5 局。
- 6-8 真人跑 3 局。
- 每局记录: 首杀地点、报案地点、会议次数、玩家迷路点、任务完成率、黑帮胜负、玩家觉得最危险区域。

**验收标准:**

- 平均局长接近 10-15 分钟。
- 玩家能记住区域名称和路线。
- 至少有 2 个区域经常成为争论焦点。
- 没有一个区域长期无人经过。

### 5.3 2D 美术推进阶段

#### Art Phase A: 美术风格圣经

**目标:** 先统一风格，再画 tile 和 sprite。

**内容必须包括:**

- 题材关键词: 香港港区、夜间巡查、警署档案、后巷暗线、码头货柜、夜市灯牌。
- 色彩层级:
  - 玩家和尸体最高可读性。
  - 任务和破坏次高。
  - 路线和墙体中等。
  - 纯装饰最低。
- 2D 笔触/材质感规范: 道路、混凝土、金属、玻璃、霓虹、警署室内。
- 像素/单位比例: 角色尺寸、门宽、墙厚、柜台、任务台、尸体轮廓。
- 资产使用规则: 哪些 2D 素材可用，哪些 3D 资源只做参考或预渲染，哪些风格不再混用。

**产出物:**

- `output/art_direction_harbor_v1.md`
- 10-20 张参考图或截图板。
- 颜色、图标、tile、sprite 尺寸 token 表。

**验收标准:**

- 新增 tile/sprite 能判断是否符合风格。
- 不再出现同一地图内 sprite 比例、视角和明暗明显冲突。
- 任务点、危险点、暗线点在颜色和轮廓上可读。

#### Art Phase B: 2D Tile/Sprite 资产包

**目标:** 不逐张画独立大图，而是先做可复用 2D tile 和 sprite。

**港区 2D 资产建议:**

- Floor tiles: 主路、窄巷、码头地面、警署地面、夜市场地。
- Wall tiles: 低墙、建筑外沿、仓库墙、警署内墙、围栏。
- Door/Passage sprites: 普通入口、窄入口、暗线入口、封锁门。
- Interactable sprites: 任务台、电箱、通讯柜、证物柜、监控台、会议桌。
- Landmark sprites: 货柜、警车、摊位、招牌、路障。
- Readability sprites: 区域牌、地面箭头、危险标线、任务灯、尸体高亮。

**2D 资产规范:**

- Tile 尺寸统一，建议先固定为一个 Unity 单位网格。
- 所有 sprite pivot 统一，便于与地图坐标对齐。
- 阻挡层和视觉层分离。
- 交互物必须有 normal、hover、disabled、sabotaged 四类状态。
- 角色、尸体、任务、破坏、暗线的视觉优先级高于装饰。

**产出物:**

- `Assets/_Project/Art/2D/Harbor/Tiles/`
- `Assets/_Project/Art/2D/Harbor/Sprites/`
- `Assets/_Project/Prefabs/Map2D/Harbor/`
- `output/tile_sprite_inventory_harbor_v1.md`

**验收标准:**

- 灰盒主要区域能用 tile/sprite 替换 80%。
- 替换后碰撞不改变路线。
- 正交行动视角和小地图视角都清楚。

#### Art Phase C: 灰盒替换为第一版 2D 美术

**目标:** 用 2D tile/sprite 替换灰盒，但不改玩法布局。

**规则:**

- 先替换地面、墙、主要阻挡 tile。
- 再替换任务台、暗线、监控、破坏点 sprite。
- 最后补装饰。
- 每次替换后都跑路线和碰撞 smoke。

**验收标准:**

- 玩家不会因为新 sprite 误判可走/不可走。
- 任务点没有被装饰遮挡。
- 尸体在常见地面 tile 上仍然醒目。
- 小地图位置和世界位置一致。

#### Art Phase D: 色彩、光效、气氛

**目标:** 做警匪氛围，但不能牺牲读图。

**规则:**

- 危险气氛用局部光效、边缘 vignette 和音效，不要全图过暗。
- 任务点要有明确 icon、轮廓或局部发光。
- 破坏状态要改变 2D overlay: 黑灯、断讯、封锁。
- 会议区比行动区更安静、更清楚。

**验收标准:**

- 黑灯状态下仍能看见角色轮廓和近距离交互点。
- 不同区域有辨识度。
- 截图缩小到 50% 仍能看出任务、尸体、玩家。

#### Art Phase E: 性能和资源整理

**目标:** 美术替换不能让联机帧率和加载崩掉。

**验收标准:**

- 高频 tile/sprite 使用 atlas。
- 删除或隔离未使用资源。
- 运行时生成对象数量可控。
- 常见机器上行动阶段稳定目标帧率。

**验证:**

- 8 人局跑 10 分钟，记录帧率、GC、加载、卡顿。
- 检查 sprite atlas、collider 数量和 draw call。

### 5.4 UI 与地图的结合

UI 不是独立皮肤。UI 要解决地图读不清、任务不知道在哪、会议不知道说什么的问题。

#### UI Phase A: 信息架构

**目标:** 明确每个阶段玩家需要的信息。

**行动阶段需要:**

- 我的身份和目标。
- 最近任务/推荐路线。
- 报案、击杀、技能、暗线、互动按钮状态。
- 小地图和危险状态。
- 证据链/任务进度。

**会议阶段需要:**

- 谁死了、在哪里发现、谁报案。
- 最近线索: 监控、破坏、任务、嫌疑变化。
- 玩家列表、发言/语音状态、投票状态。
- 投票结果和淘汰解释。

**大厅阶段需要:**

- 房间码、玩家、Ready、规则、地图选择、AI 补位。

**结算阶段需要:**

- 胜负原因、关键事件、角色公开、重开按钮。

#### UI Phase B: 地图 UI

**目标:** 小地图和大地图必须来自同一份地图数据。

**要求:**

- 小地图显示区域、玩家、任务、尸体、破坏、暗线提示。
- 大地图显示任务详情和推荐路线。
- 黑帮和警方看到的信息不同。
- 会议证据板能引用地图区域名称。

**验收标准:**

- 世界任务点移动后，小地图不需要手动改第二份坐标。
- 玩家能用小地图找到任务点。
- 黑帮能看到暗线提示，警方只看到间接线索。

**可能涉及文件:**

- `OnlineMapService.cs`
- `OnlineMatchHud.cs`
- `OnlineTaskService.cs`
- `GameStateSnapshot.cs`

#### UI Phase C: 交互提示

**目标:** 玩家站到任务、尸体、暗线、监控、会议按钮旁时，提示必须明确且不遮挡。

**要求:**

- 同一位置有多个可交互对象时，按优先级显示: 尸体 > 危机修复 > 任务 > 暗线 > 监控 > 普通提示。
- 黑帮和警方提示不同。
- 冷却和不可用原因要显示。
- 不再用大段说明文字堆在屏幕上。

**验收标准:**

- 玩家能在 1 秒内知道当前能按什么键。
- 不会出现任务提示遮住尸体报案提示。

#### UI Phase D: 任务小游戏 UI

**目标:** 任务面板成为稳定组件，不是每个任务临时画一套。

**要求:**

- 统一外壳: 标题、目标、退出、进度、错误、提交。
- 任务内部交互可不同。
- 支持键鼠和未来触控扩展。
- 联机结果由 Host 校验。

**验收标准:**

- 至少 6 种小游戏使用同一外壳。
- 不同分辨率下按钮和文字不溢出。

#### UI Phase E: 会议证据板 UI

**目标:** 会议 UI 要把地图事件变成可讨论证据。

**要求:**

- 尸体地点在小地图上高亮。
- 显示最近 3-5 条关键事件。
- 显示每个玩家的公开线索，不显示真实身份。
- 投票状态和倒计时清楚。

**验收标准:**

- 玩家能根据 UI 复述“为什么怀疑某人”。
- 平票、跳过、淘汰结果清楚。

### 5.5 地图/UI 专项任务

#### Task A.1: 港区地图玩法文档

**描述:** 写出第一张正式港区图的区域、路线、任务、暗线、监控、破坏布局。

**验收标准:**

- 8-10 个核心区域。
- 3 条主路线，2-3 条支线。
- 每个区域有玩法目的。
- 点位表包含任务、暗线、监控、破坏、会议、出生点。

**验证:** 和 `OnlineMapService` 点位一一对应。

**依赖:** Task 1.2。

**可能涉及文件:**

- `output/map_design_harbor_v1.md`
- `OnlineMapService.cs`

**预计规模:** S。

#### Task A.2: 港区灰盒

**描述:** 用 Tilemap、简单 Sprite 或纯色矩形做正式港区 2D 灰盒，不做最终美术。

**验收标准:**

- 可走、不可走、门洞、窄路明确。
- 任务点、暗线点、监控点都能到达。
- 双开能完整跑局。

**验证:** 2 真人 + 6 Bot 跑 5 局。

**依赖:** Task A.1。

**可能涉及文件:**

- `Assets/_Project/Scenes/HarborGreybox2D.unity`
- `OnlineMapService.cs`

**预计规模:** M。

#### Task A.3: 港区 2D Tile/Sprite 美术包

**描述:** 建立可复用港区 2D tile/sprite 资产，先做功能资产，不先做大量装饰。

**验收标准:**

- 地面、墙体、门洞、任务台、监控台、暗线入口、破坏点都有 2D 资产。
- tile 尺寸、sprite pivot、collider 规则统一。
- 资产视角、轮廓、明暗风格一致。

**验证:** 使用 tile/sprite 替换灰盒 80% 后路线不变。

**依赖:** Task A.2、Art Phase A。

**可能涉及文件:**

- `Assets/_Project/Art/2D/Harbor/Tiles/`
- `Assets/_Project/Art/2D/Harbor/Sprites/`
- `Assets/_Project/Prefabs/Map2D/Harbor/`

**预计规模:** L。

#### Task A.4: 小地图和世界地图统一

**描述:** 小地图和大地图不再手写第二份点位，统一从地图服务读取。

**验收标准:**

- 世界任务点、暗线点、监控点变动后 UI 自动同步。
- 支持按角色过滤信息。
- 会议证据板能高亮对应区域。

**验证:** 移动一个任务点，小地图位置自动改变。

**依赖:** Task 1.2、Task 6.2。

**可能涉及文件:**

- `OnlineMapService.cs`
- `OnlineMatchHud.cs`
- `GameStateSnapshot.cs`

**预计规模:** M。

#### Task A.5: UI 设计系统

**描述:** 给警匪题材建立统一 UI 组件和视觉规则。

**验收标准:**

- 有按钮、状态标签、倒计时、任务卡、玩家卡、证据条、小地图图标、投票按钮组件。
- 颜色、字体大小、间距、图标状态统一。
- 支持 1366x768 到 2560x1440。

**验证:** 三种分辨率截图无溢出和遮挡。

**依赖:** Task 6.1。

**可能涉及文件:**

- `OnlineMatchHud.cs`
- `ThemeManager.cs`
- `UIManager.cs`
- `SettingsUIHelper.cs`

**预计规模:** M-L。

#### Task A.6: 地图可读性 QA

**描述:** 每次地图或 UI 大改后做可读性检查。

**验收标准:**

- 缩略图能看出玩家、尸体、任务、破坏。
- 行动视角无遮挡核心交互。
- 色盲模式下任务/破坏/尸体仍可区分。
- 黑灯状态不导致不可玩。

**验证:** 输出 `output/map_readability_review_YYYYMMDD.md`，包含截图和问题清单。

**依赖:** Task A.3、Task A.5。

**可能涉及文件:**

- `output/map_readability_review_YYYYMMDD.md`
- `OnlineMatchHud.cs`
- 地图资源

**预计规模:** 持续任务。

### 5.6 地图差距的真实原因和 2D 修正路线

当前地图差距大的原因不是单纯“模型不精细”。真实原因是:

- 现在地图更像程序生成的城市展示，不像经过多人测试收口的社交推理棋盘。
- 多套地图来源并存，导致点位、比例、碰撞、UI 和玩法不能完全一致。
- 混用资源包使风格、比例、材质和视角不统一。
- 3D/2.5D 港区比 Among Us 的 2D 飞船更难读图。
- 装饰密度高，但任务、尸体、暗线、监控这些关键信息层级不够。

修正路线:

1. 先做灰盒和玩法测试。
2. 再做 2D tile/sprite 资产包。
3. 再替换灰盒，不改变玩法布局。
4. 再加色彩、光效、动效、装饰。
5. 最后做 UI 地图、提示和证据板联动。

这条路线比继续堆 3D 模型慢一两天，但能避免返工数周。2D 不是降级，它是为社交推理读图和快速迭代做的更保守选择。

## 6. 测试矩阵

| 测试 | 频率 | 目标 |
|---|---|---|
| Unity 编译 | 每次任务完成 | 无 C# 编译错误 |
| EditMode 规则测试 | 每次改规则服务 | 胜负、投票、任务、破坏无回归 |
| 本地试玩局 | 每日 | 单机完整局不阻断 |
| Host/Client 双开 | 每个联机任务完成 | 快照、任务、会议、投票同步 |
| Relay 双机 | 每周或服务改动后 | 外网房间可用 |
| 6-10 人真人局 | M5 后每周 | 真实体验和稳定性 |
| 分辨率 UI 检查 | UI 改动后 | 无遮挡、无溢出 |
| 地图可读性检查 | 地图/美术/UI 大改后 | 玩家、尸体、任务、破坏、暗线可读 |
| 灰盒多人测试 | 每张地图进入美术前 | 路线、视野、击杀/报案窗口成立 |
| 2D atlas/Tilemap 检查 | 2D 资产接入后 | sprite atlas、tile collider、sorting layer 正常 |
| 构建运行 | M8 后每次发布 | 非 Editor 可运行 |

## 7. Bug 优先级定义

P0:

- 无法编译。
- 无法开局。
- 无法加入房间。
- 对局无法结算。
- 投票、任务、击杀导致所有玩家卡死。

P1:

- Client 与 Host 状态明显不一致。
- 任务或破坏可被作弊直接完成。
- 会议信息错误影响胜负。
- 断线导致其他玩家无法继续。
- UI 阻挡核心操作。

P2:

- 平衡明显偏一方。
- Bot 行为愚蠢但不阻断。
- 动画、音频、提示缺失。
- 局部 UI 难读但可操作。

P3:

- 文案、装饰、轻微视觉问题。
- 非核心设置缺失。
- 低频边缘体验问题。

## 8. 文件和模块目标结构

目标结构建议:

```text
Assets/_Project/Scripts/Online/
  OnlineMatchController.cs          # 薄控制器，生命周期和协调
  OnlineRuleSet.cs                  # 房间规则
  OnlineMapService.cs               # 地图和坐标
  OnlineTaskService.cs              # 任务、破坏、修复
  OnlineMeetingService.cs           # 会议、投票、淘汰
  OnlineBotService.cs               # Bot 行为
  OnlineAudioCueService.cs          # 事件音效
  OnlineSecurityService.cs          # Host 权威校验
  MiniGames/
    OnlineMiniGameDefinition.cs
    OnlineMiniGameSession.cs
    OnlineMiniGameResult.cs
```

不要追求一次性全部完成。M1 只抽最能降低风险的规则、地图、任务、会议。

地图、美术和 UI 目标结构建议:

```text
Assets/_Project/Art/2D/
  Harbor/
    Floors/
    Walls/
    Interactables/
    Props/
    Characters/
    Effects/
    UIIcons/
  PoliceStation/

Assets/_Project/Prefabs/Map2D/
  Harbor/
  PoliceStation/

Assets/_Project/Scripts/Online/UI/
  MapHudController.cs
  InteractionPromptView.cs
  MeetingEvidenceBoard.cs
  TaskMiniGameShell.cs
```

## 9. 第一周执行表

Day 1:

- Task 0.1 工程编译。
- Task 0.2 本地试玩完整局。
- 输出编译和本地试玩报告。

Day 2:

- Task 0.3 双开烟测。
- Task 0.4 当前差距矩阵。
- 只修 P0，不修体验小问题。

Day 3:

- Task 1.1 抽 `OnlineRuleSet`。
- 添加默认规则测试。
- 双开回归。

Day 4:

- Task 1.2 抽 `OnlineMapService`。
- 把出生点、任务点、暗线点切到统一坐标服务。
- 双开回归。

Day 5:

- Task 1.3 统一暗线/通风管。
- 删除或封存重复入口。
- 双开验证黑帮机动。

Day 6-7:

- 修 M0/M1 遗留 P0/P1。
- 更新计划状态。
- 决定是否进入 M2。

## 10. 资源和人员建议

最小团队:

- 1 名 Unity gameplay/network 工程师: M0-M5 主责。
- 1 名 UI/UX 工程师: M3/M6 主责。
- 1 名美术/技术美术: M6/M7 主责。
- 1 名测试/制作: 从 M0 开始维护测试清单和封测反馈。

单人执行时的原则:

- 每天只推进一个垂直切片。
- 每个任务结束都必须跑一次对应 smoke。
- 每次只拆一个模块，不边拆边加新玩法。
- 文档里的任务编号就是提交/PR 的边界。

地图/UI 专项人员建议:

- 关卡设计: 先由 gameplay 工程师和制作共同负责灰盒，不要直接交给美术自由发挥。
- 2D 美术/技术美术: 在灰盒通过后做 tile/sprite 资产包，严格按 tile 尺寸、pivot、sorting layer、collider 规范。
- UI/UX: 从 M3 开始介入任务外壳、小地图、会议证据板，不等美术完工。
- QA/测试: 每张地图进入美术前必须跑灰盒多人测试。

## 11. 明确不做

M0-M5 期间不做:

- 商城、皮肤、账号系统。
- 第三和第四地图。
- 专用服务器。
- 移动端适配。
- 大规模资产替换。
- 非必要重写。

这些会拖慢最关键的验证: 多人局是否好玩、是否稳定、是否能复玩。

地图/UI 方面 M0-M5 期间也不做:

- 不做大规模精修装饰。
- 不做第三、第四张地图。
- 不做写实级 3D 建筑资产。
- 不在灰盒未通过前替换整张地图 2D 美术。
- 不让 UI 和世界地图维护两份坐标。

## 12. 最终成功标准

项目达到封测可用时，应满足:

- 6-10 人能通过房间码稳定进入同一局。
- 8 人局平均 10-15 分钟。
- 每局至少发生任务推进、破坏/修复、击杀/报案、会议投票。
- 任务不是单一按键校验，至少 6 种真实小游戏。
- 死亡玩家不会立刻退出体验。
- 会议中有证据可讨论，不只是瞎猜。
- 黑帮有暗线和破坏，但可被推理反制。
- UI 默认路径不依赖 OnGUI。
- 港区地图已经通过 2D 灰盒多人测试并完成第一版 tile/sprite 美术替换。
- 小地图、大地图、会议证据板和世界点位使用同一份地图数据。
- 72 小时封测无 P0/P1。
