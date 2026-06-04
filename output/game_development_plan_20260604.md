# Gangland Undercover 游戏开发计划

日期: 2026-06-04  
项目: `/Users/zhugehao/projects/GanglandUndercover`  
引擎: Unity 6000.4.5f1  
当前代码规模: `Assets/_Project/Scripts` 下 102 个 C# 文件，约 46,621 行

## 1. 结论

这个项目已经不是早期空壳。它有警匪社交推理的完整雏形: Host/Client 联机、Relay 代码、AI 补位、击杀、报案、会议、投票、证据链、破坏、任务面板、Canvas HUD、两张地图方向、离线小游戏、离线通风管、监控、紧急任务等。

但它和 Among Us 的差距不再是“功能名没有写出来”，而是四个更关键的问题:

1. 联机主循环没有达到真实多人局稳定度。Relay/Lobby 依赖 Cloud Project，Vivox 目前是 stub，真实 2-8 人局没有形成可重复测试基线。
2. 联机任务还不是成品小游戏。`OnlineMatchController` 的任务仍以 1/2/3 校验和 Space 蓄力为核心，离线侧的 `MiniGames` 没有成为联机任务的统一实现。
3. 代码结构已经阻碍继续做大。`OnlineMatchController.cs` 约 13,101 行，是最大风险。继续往里面加系统会让调试、测试、合并和修 bug 变慢。
4. 体验层还不够成品化。视觉反馈、击杀/出局动画、会议表达、语音、设置、房间发现、断线恢复、移动端适配和测试流程都还没达到可发行标准。

所以后续开发不能再按“继续补功能列表”的方式推进。先稳定联机核心和架构，再把离线已有的好东西迁入联机，最后做商业化品质和发行准备。

## 2. 产品目标

不要做太空狼人杀的换皮。目标应是“警匪卧底版短局社交推理”。

核心规格:

- 6-10 人标准局，AI 可补位，目标局长 10-15 分钟。
- 两大阵营: 警方/市民侧通过任务和投票闭合证据链；黑帮/线人侧通过击杀、破坏、误导会议拖垮证据链。
- 每局必须有四个高频张力点: 做任务、发现尸体、开会互辩、黑帮机动/破坏。
- 警匪差异化必须保留: 证据链、嫌疑值、警署/港区题材、卧底/线人身份张力。
- Among Us 对标的是可读性、节奏、多人稳定性和复玩性，不是科幻题材。

## 3. 当前资产和代码判断

已经存在的基础:

- 联机: `OnlineMatchController.cs`、`OnlineMatchHud.cs`、`TaskSync.cs`、`MeetingSync.cs`、`PlayerStateSync.cs`、`OnlineVictoryBridge.cs`、`HostMigrationManager.cs`。
- 核心玩法: 击杀、尸体、报告、紧急会议、投票、证据链、5 类破坏、职业技能、AI Bot。
- 离线推理侧: `SocialPrototypeController.cs`、`VentSystem.cs`、`SecurityCamera.cs`、`CriticalTaskSystem.cs`、`GhostMode.cs`。
- 小游戏: `SocialDeduction/MiniGames` 下已有 Wire、Memory、SwipeCard、Keypad、Sort、Scan、Tap、Calibrate、Asteroid、Download、EvidenceArchive。
- UI: 联机已有 Canvas HUD，但 `OnlineMatchController` 内仍保留大量 OnGUI 调试/备用界面。
- 服务: `UnityServiceBootstrap.cs` 能初始化 Unity Services，但 Vivox 被移除并返回 false。

关键技术债:

- `OnlineMatchController.cs` 承担网络、规则、地图生成、UI、任务、破坏、音频、AI、快照、语音路由等职责，必须拆。
- 联机侧存在 `UnderworldPassage` 和 `OnlineVents` 两套机动系统。项目坐标大多使用 `ScaleMapPosition`，但 `OnlineVents` 节点直接用设计坐标比较，存在交互偏移风险。
- `SabotageSync.cs` 通过反射读取 `OnlineMatchController` 私有 timer 字段，应该改为事件或公开只读状态模型。
- 联机任务和离线 MiniGame 体系分裂，导致做一份任务玩法需要维护两套体验。
- Relay/Lobby 与 Vivox 的文案已经出现在 HUD，但真实服务能力未闭环。

## 4. 架构决策

短期保持 NGO Host-Authoritative，不立即上专用服务器。原因是当前最缺的是可玩闭环和验证数据，专用服务器会放大范围。

把联机模式作为主产品，离线模式作为教程、AI 练习和开发沙盒。后续所有新玩法先保证联机可同步，再考虑离线兼容。

把规则数据从巨型控制器里移出来。地图点位、任务定义、破坏配置、角色参数、房间规则都应进入 ScriptableObject 或纯 C# config。

共享任务小游戏框架。联机任务不再手写一套 1/2/3 输入，而是通过 `MiniGameBase` 或新的 `IMiniGameRuntime` 启动同一批小游戏。

把 UI 从调试面板转为正式 Canvas 流程。OnGUI 只保留开发开关，默认玩家路径不再看到 OnGUI。

## 5. 阶段路线

### Phase 0: 建立可验证基线，2-3 天

目标: 确定现在到底能不能编译、能不能本地完整打一局、能不能双客户端同步。

任务:

- 打开 Unity，完成 Editor 编译，记录 0 编译错误或错误清单。
- 跑本地试玩局: Lobby -> Opening -> Action -> Meeting/Voting -> Result。
- 跑局域网双开: Host + Client 创建、加入、Ready、开局、移动、任务、击杀、报案、投票、结算。
- 跑一次 Relay 流程。如果 Cloud Project 未绑定，则记录为环境阻塞，不当作代码阻塞。
- 生成一份 `output/baseline_test_YYYYMMDD.md`，记录可复现问题。

验收:

- Unity Console 无 C# 编译错误。
- 本地试玩能完整结算一局。
- 双开至少完成: 连接、角色私发、移动同步、任务同步、击杀同步、会议同步、投票同步。
- 所有 P0/P1 bug 有编号、复现步骤和归属模块。

### Phase 1: 联机核心瘦身和稳定，5-7 天

目标: 不改变玩法，先降低继续开发的维护风险。

任务:

- 从 `OnlineMatchController` 拆出以下职责，保持行为不变:
  - `OnlineSessionService`: Host/Client/Relay 启停、消息注册。
  - `OnlineRuleSet`: 常量和房间规则。
  - `OnlineTaskService`: 任务状态、进度、破坏、修复。
  - `OnlineMeetingService`: 报案、紧急会议、投票、淘汰。
  - `OnlineMapService`: 坐标、出生点、任务点、暗线节点。
  - `OnlineBotService`: Bot 决策。
  - `OnlineAudioCueService`: 音频 cue。
- 统一 `UnderworldPassage` 和 `OnlineVents`。只保留一个“暗线通道”规则模型，所有节点必须使用同一坐标系统。
- 移除 `SabotageSync` 对私有字段的反射读取，改为从 `OnlineMatchController` 或新状态对象读取只读 sabotage state。
- 为纯逻辑增加 EditMode 测试: 任务完成、破坏修复、投票平票、胜负判断、暗线冷却。

验收:

- 每次拆分后双开烟测仍通过。
- `OnlineMatchController.cs` 从 13k 行下降到 8k 行以下，且新增服务每个小于 1,500 行。
- 暗线/通风管只有一套交互入口和一套冷却逻辑。
- 不新增玩法，只降低风险。

### Phase 2: 标准局规则对齐，5-7 天

目标: 做出一局 10-15 分钟真正有节奏的联机局。

任务:

- 房间规则可配置并同步: 击杀冷却、讨论时间、投票时间、紧急会议次数、任务数量、证据目标、是否公开身份。
- 调整默认参数:
  - 讨论 25-35 秒。
  - 投票 30-40 秒。
  - 击杀冷却 25-40 秒，按人数缩放。
  - 每人 3-5 个任务，按地图规模缩放。
  - 每人 1 次紧急会议或全局限次，取决于玩家数。
- 实现在线鬼魂基础体验: 死亡后可观战、可穿越阻挡、不可报案/投票前发言、可继续做任务或提供弱贡献。
- 梳理胜负条件: 证据链胜、黑帮人数压制胜、黑帮全灭败、超时胜负。

验收:

- 5 人、8 人、10 人三档都能完成标准局。
- 每局至少触发一次会议，每局任务完成率和击杀压力都能影响结局。
- 死亡玩家不会被迫退出体验。

### Phase 3: 联机任务小游戏和破坏修复，10-14 天

目标: 把任务从“按键校验”升级为真正小游戏，这是目前和 Among Us 手感差距最大的点。

任务:

- 设计统一接口:
  - `MiniGameDefinition`: id、标题、地点、时长、难度、证据值、可被破坏类型。
  - `MiniGameSession`: Start、Cancel、Complete、Fail、SerializeProgress。
  - `MiniGameResult`: completed、mistakes、duration、evidenceGain。
- 将离线 MiniGames 接入联机 Canvas:
  - 第一批: WireTask、KeypadTask、SwipeCardTask、MemoryTask、DownloadTask、EvidenceArchiveTask。
  - 第二批: ScanTask、SortTask、CalibrateTask、TapTask。
- 破坏修复改为小游戏:
  - Blackout -> WireTask 或 BreakerTask。
  - Communications -> CalibrateTask。
  - Lockdown -> KeypadTask。
  - EvidenceLeak -> EvidenceArchiveTask。
- 增加全局任务进度条和本地任务清单，区分“已分配给我”和“全局进度”。
- 联机同步只同步结果和必要进度，不同步每帧 UI。

验收:

- 非黑帮玩家完成至少 6 种不同小游戏。
- 破坏必须通过对应修复小游戏清除。
- 客户端断开任务面板不会造成任务卡死。
- Host 权威校验任务结果，Client 不能直接加证据分。

### Phase 4: 信息与欺骗系统，7-10 天

目标: 让警匪题材形成独立深度，而不只是追逐和投票。

任务:

- 联机监控系统:
  - 监控室打开 Canvas 监控界面。
  - 以低频位置快照或区域标记显示可疑移动，不需要真实 RenderTexture 起步。
  - 黑帮可破坏监控，修复后恢复。
- 暗线通道正式化:
  - 地图可见性按阵营区分。
  - 黑帮/线人可用，警方只能通过证据或监控间接发现。
  - 使用动画、音效和冷却提示。
- 证据与嫌疑:
  - 摄像头、尸体距离、破坏地点、任务失败都能改变嫌疑。
  - 会议界面展示“线索”，但不直接给答案。
- 聊天/语音规则:
  - 行动阶段近距离语音。
  - 会议阶段全员语音。
  - 死亡后鬼魂频道。

验收:

- 监控至少能帮助玩家判断路线。
- 黑帮暗线移动可被玩家推理，但不直接暴露。
- 会议界面能展示至少 3 类证据: 尸体地点、最后目击、任务/破坏记录。

### Phase 5: 服务、房间和语音，7-14 天

目标: 从“能连”推进到“玩家能稳定开房玩”。

任务:

- 绑定 Unity Cloud Project，确认 Authentication、Relay、Lobby 可用。
- 完成房间码创建/加入的玩家路径，不依赖调试按钮。
- 增加大厅浏览或 Quick Join。短期可先做“私人房间码 + AI 补位”。
- 语音方案二选一:
  - 恢复 Vivox 包并实现登录、频道加入、位置更新、静音。
  - 或明确替换为其他语音服务，避免 HUD 继续显示不可用语音。
- 断线策略:
  - Client 断线: 保留位置 60 秒，支持重连或 AI 接管。
  - Host 断线: 验证 `HostMigrationManager`，不可靠则先明确“Host 断线终止”。
- 基础安全:
  - Host 校验移动速度、击杀距离、任务完成结果、投票阶段。
  - Client 只提交意图，不提交最终权威状态。

验收:

- 两台不同机器可通过 Relay 完成一局。
- 语音状态真实可用或 UI 明确隐藏。
- Client 断线不会让其他玩家卡死。
- Host 权威校验至少覆盖移动、击杀、任务、投票。

### Phase 6: 成品 UI、动画和反馈，10-14 天

目标: 让第一眼和第一局体验接近商业游戏，而不是调试 Demo。

任务:

- 默认玩家路径全部切到 Canvas UI:
  - 主菜单、创建房间、加入房间、规则设置。
  - 游戏 HUD、任务面板、小地图、人员/案情页。
  - 会议、投票、结算、重新开局。
- 击杀、报案、会议、淘汰、胜负增加动画和音频反馈。
- 角色视觉:
  - 阵营不直接暴露，但玩家颜色/外观可区分。
  - 死亡、鬼魂、会议座位、任务互动状态可读。
- 设置菜单:
  - 音量、分辨率、画质、按键、语音开关、色盲辅助。
- 新手引导:
  - 3 分钟教程: 做任务、报案、投票、黑帮破坏、暗线使用。

验收:

- 新玩家不读文档也能完成第一局基本操作。
- OnGUI 默认关闭，只作为开发调试开关。
- 会议和结算能清楚解释为什么赢/输。

### Phase 7: 内容、平衡和复玩性，14-28 天

目标: 让玩家愿意开第二局。

任务:

- 地图 1 收口为正式港区图，减少纯装饰噪声，强化路线和视野。
- 地图 2 警署图进入联机模式，任务和暗线重新配置。
- 职业/角色收敛:
  - 先保留 5-7 个明确职业，删掉或合并弱差异职业。
  - 每个职业只保留一个容易理解的技能。
- Bot 升级:
  - 黑帮 Bot 会找落单目标、避开监控、开破坏。
  - 警方 Bot 会报案、修复、根据嫌疑投票。
- 平衡测试:
  - 记录每局时长、胜率、任务完成率、平均会议次数、首杀时间、弃局率。
  - 根据数据调整证据目标、冷却、任务数量、地图尺寸。

验收:

- 8 人局平均时长 10-15 分钟。
- 黑帮/警方胜率在 45%-55% 之间，允许早期 40%-60%。
- 每局至少 2 个可复述的“事件”: 击杀、误投、关键修复、监控发现等。

### Phase 8: 发行准备，7-14 天

目标: 做出可交给外部玩家测试的版本。

任务:

- 建立构建流程: macOS/Windows 至少一个可分发包。
- 增加自动化检查:
  - Unity batchmode 编译。
  - EditMode/PlayMode 核心测试。
  - 资源缺失扫描。
- 增加运行日志和崩溃收集。
- 准备封闭测试包:
  - 说明最少化，只保留开房、加入、操作表。
  - 收集表单: bug、局长、卡顿、最困惑点、最有趣点。
- 定义版本门槛:
  - Alpha: 熟人 6-10 人能稳定玩 5 局。
  - Beta: 外部玩家能无引导玩完 3 局。
  - RC: 72 小时内无 P0/P1。

验收:

- 有可分发构建。
- 有测试清单和反馈入口。
- 已知阻断问题全部关闭或明确降级范围。

## 6. 优先级总表

P0 必须先做:

- Unity 编译和双开基线。
- 拆 `OnlineMatchController` 的关键职责。
- 修正暗线/通风管坐标和重复系统。
- Relay/Lobby 环境状态明确。
- 真实多人局测试。

P1 做出 Among Us 手感:

- 联机真实小游戏。
- 破坏修复小游戏。
- 鬼魂体验。
- 会议证据板。
- 成品 Canvas UI。
- 击杀/报案/投票/淘汰反馈。

P2 做出警匪差异:

- 监控/嫌疑/证据线索。
- 警署图。
- 职业能力收敛。
- Bot 行为升级。

P3 发行增强:

- Quick Join/大厅浏览。
- 移动端适配。
- 皮肤/外观。
- 更多地图。
- 账号和好友。

## 7. 下一步 72 小时

1. 打开 Unity，记录编译结果。没有 0 编译错误之前不要继续加玩法。
2. 本地试玩跑完整一局，截图/录屏，记录所有卡点。
3. 双开 Host/Client 跑 `online_test_plan.md` 的 TC-01 到 TC-12。
4. 修第一个阻断联机闭环的问题，不修装饰性问题。
5. 开始拆 `OnlineMatchController`: 先抽 `OnlineRuleSet` 和 `OnlineMapService`，因为它们对行为影响最低但能立刻减少坐标/规则混乱。

## 8. 风险与缓解

| 风险 | 影响 | 缓解 |
|---|---|---|
| 巨型控制器继续膨胀 | 开发速度越来越慢，bug 难定位 | Phase 1 先拆职责，不加新玩法 |
| 联机功能只在本机可用 | 外部玩家无法测试 | Phase 0/5 建立双机 Relay 验证 |
| 任务小游戏只存在离线 | 联机手感仍像 Demo | Phase 3 统一 MiniGame Runtime |
| 语音 UI 显示但服务不可用 | 玩家误解产品能力 | Phase 5 真实接入或隐藏 |
| 美术资源量大但玩法读不清 | 画面复杂，信息噪声高 | Phase 6 先做可读性和状态反馈 |
| 直接追求专用服务器 | 周期拉长，无法验证玩法 | 先 Host-Authoritative，测试数据证明需求后再升级 |

## 9. 不建议做的事

- 不要继续在 `OnlineMatchController.cs` 里堆功能。
- 不要先做第三、第四张地图。
- 不要先做皮肤、账号、商城。
- 不要把太空题材机制逐字复制过来。警匪题材需要保留证据链和嫌疑推理。
- 不要用 AI Bot 通过来代替真人双开测试。

## 10. 成功标准

第一阶段成功不是“功能表全打勾”，而是下面这些结果:

- 一个新玩家能在 3 分钟内理解自己该做什么。
- 6-8 人能稳定完成 5 局，平均局长 10-15 分钟。
- 每局有任务压力、击杀压力、会议信息和黑帮欺骗。
- 玩家死亡后仍愿意留下看到结算。
- 开房、加入、语音/聊天、投票不会成为主要抱怨点。
- 代码结构允许继续迭代，而不是每次改动都冒出联机回归。

