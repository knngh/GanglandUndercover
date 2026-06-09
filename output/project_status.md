# Gangland Undercover — 项目整体状态报告

**产出日期**: 2026-06-01
**项目路径**: /Users/zhugehao/projects/GanglandUndercover
**Unity 版本**: 6000.4.5f1

---

## 一、各阶段完成内容概览

| 阶段 | 日期 | 状态 | 核心交付 |
|------|------|------|---------|
| Stage 1 垂直切片场景 | 2026-06-01 | ✅ 完成 | Stage1VerticalSlice.unity + prefab，5872 世界对象，22 可编辑锚点 |
| Stage 2 角色与动画 | 2026-06-01 | ✅ 完成 | 11 个 prefab Animator Controller 挂载，Stage2 预制体嵌套角色 |
| Stage 3 离线模式 3D 化 | 2026-06-01 | ✅ 完成 | SocialPrototypeController 接入 DenysAlmaral 角色，Synty 任务道具，墙面装饰 |
| Stage 4 离线游戏循环 | 2026-06-01 | ✅ 完成 | PrototypeHud + DistrictMapView 桥接，GameController 实例化 |
| Stage 5 对手 AI 与胜负 | 2026-06-01 | ✅ 完成 | OpponentAi 差异化决策，VictoryEvaluator 中文化重写，会议投票淘汰 |
| Stage 6 联机模式启动 | 2026-06-01 | ✅ 完成 | GameMode 枚举双模式互斥启动，网络层审查（15+ 问题标注） |
| Stage 7 联机同步设施 | 2026-06-01 | ✅ 完成 | 5 个同步文件，OnlineSyncManager 管理器，8 处集成点接线 |
| Stage 8 主菜单与流程 | 2026-06-01 | ✅ 完成 | MainMenuController + GameOverController + LobbyController，完整菜单驱动架构 |

### Stage 1：垂直切片场景重搭

- 场景文件 `Stage1VerticalSlice.unity`，`Stage1VerticalSliceWorld.prefab`
- 作者化地图区域：中央集合点、监控室、茶餐厅、夜市、后巷、电房入口
- 会议专用场景（圆桌、语音席位、证据墙）、断电专用场景（红色应急灯、修复 pad）
- 镜头锚点：OpeningFirstScreen、ActionFollow、BlackoutRoute、MeetingTable、ResultStage
- 编辑器工具：`Gangland/Build Stage1 Vertical Slice Scene`、`Gangland/Capture Stage1 Vertical Slice Screenshot`

### Stage 2：角色与动画替换

- DenysAlmaral 5 个角色 prefab（police_Female_A、casual_Male_G/K、casual_Female_G/K）挂载 GanglandCharacter.controller
- Synty SM_Chr_Male_01 / SM_Chr_Female_01 新增 Animator 组件
- 4 个 Stage2 角色 prefab（Undercover/Gang/Police/Civilian）嵌套 DenysAlmaral 源预制体

### Stage 3：离线模式 3D 资源化

- SocialPrototypeController：Police 映射从胶囊体改为 `police_Female_A`，新增任务道具映射（查封货柜→Crate_02 等 5 个）
- CreateCharacter()：Animator 配置 + 自适应缩放（Bounds 驱动，clamp [0.04,0.32]）+ Tint 着色
- CreateTask()：任务站 3D 道具实例化（Synty PolygonGeneric Props）
- CreateRoom()：南北墙装饰（Synty SM_Bld_Base_Wall_Half_02）

### Stage 4：离线游戏循环打通

- SocialPrototypeController 内部实例化 GameController，双 UI（SocialPrototypeHud + PrototypeHud）共存
- 桥接策略：OnTurnStateChanged 回调同步 GameController 状态到实时模式
- 3D 区域映射：DistrictMapView 的 6 个可点击 DistrictNode 与 BuildWorld 区域一一对应

### Stage 5：对手 AI 与胜负判定

- OpponentAi.cs：按 SocialRole 差异化决策（Gang → 高风险区、Undercover → 信息区、Police → 巡逻）
- I-AI 会议投票：CastMeetingVote() 按阵营策略投票淘汰
- VictoryEvaluator.cs 重写：中文化 + 按阵营拆分 + 会议淘汰胜利条件
- 游戏循环：玩家行动 → AI 行动 → 会议投票（每 3 天一次）→ 淘汰 → 胜负检查

### Stage 6：联机模式架构

- PrototypeBootstrap.cs 新增 `GameMode` 枚举（Offline / Online），通过 `_mode` 下拉框互斥启动
- SocialPrototypeController 新增 `AutoStartOnAwake` 标志和 `StartOfflineMode(role)` 入口
- 网络层审查：标注 15+ 待修复问题（无加密、无重连、无预测插值、无反作弊等），未修改网络代码

### Stage 7：联机同步基础设施

- 5 新文件：TaskSync.cs、MeetingSync.cs、PlayerStateSync.cs、OnlineVictoryBridge.cs、OnlineSyncManager.cs
- OnlineVictoryBridge：双判定策略（原生在线规则 + 离线 VictoryEvaluator 映射）
- 8 处集成点接入 OnlineMatchController：任务、击杀、会议、投票、胜负判定

### Stage 8：主菜单与完整流程

- 新建：MainMenuController.cs（263 行）、GameOverController.cs（283 行）、LobbyController.cs（374 行）
- PrototypeBootstrap.cs 重写（270 行）：菜单驱动架构，Awake → CreateMainMenu → 离线/联机分支
- 游戏循环闭环：主菜单 → 离线模式（选身份→游戏→结算→返回） / 联机模式（大厅→连接→房间→游戏→结算→返回）

---

## 二、GameDesign.md 垂直切片需求对照

| 需求 | GameDesign.md 描述 | 实现状态 | 对应代码/资产 |
|------|-------------------|---------|-------------|
| 生成场景 | One generated scene | ✅ | Stage1VerticalSlice.unity + Prototype.unity（双场景） |
| 可点击区域/行动 UI | Clickable district/action UI | ✅ | DistrictMapView.cs + PrototypeHud.cs + GameController.cs |
| 可点击地图节点（阵营色） | Clickable map nodes with controller colors | ✅ | DistrictNode.cs，6 区域（Dockyard / WarehouseRow / NightMarket / PolicePrecinct / Clinic / TenementBlock） |
| 角色选择 | Role select | ✅ | MainMenuController.cs（离线模式选择卧底/黑帮/警察） |
| 回合日志 | Turn log | ✅ | PrototypeHud.UpdateRoundLog() |
| 简单 AI | Simple AI | ✅ | OpponentAi.cs（按 SocialRole 差异化决策 + 会议投票） |
| 胜负结果画面 | Win/loss result screen | ✅ | GameOverController.cs + VictoryEvaluator.cs |

### 设计文档中的核心机制实现情况

| 机制 | 离线模式 | 联机模式 |
|------|---------|---------|
| 6 区域地图（Dockyard 等） | ✅ SocialPrototypeController.BuildWorld() | ✅ OnlineMatchController 程序化港区地图 |
| 阵营资源（Gang Influence / Police Heat / Evidence / Cover / Suspicion） | ✅ GameState.cs 定义 | ✅ OnlineVictoryBridge 映射 |
| 回合制结构（选区域→行动→AI→结算） | ✅ GameController.cs | ✅ 实时制（联机不同回合） |
| 故事事件（Dockyard Witness 等 4 个） | ✅ StoryEvent.cs + EventResolver.cs | ❌ 联机未接入 |
| 三方胜利条件 | ✅ VictoryEvaluator.cs | ✅ OnlineVictoryBridge 双重判定 |
| 会议投票淘汰 | ✅ GameController.RunMeeting() | ✅ OnlineMatchController 内置会议+投票 |

---

## 三、代码统计

### 项目总览

| 指标 | 数值 |
|------|------|
| C# 源文件（活跃） | 48 个 |
| C# 总行数 | 25,400 行 |
| Unity 场景文件 | 2 个（Prototype.unity / Stage1VerticalSlice.unity） |
| Prefab 数量 | 904 个 |
| 项目体积（不含 Library/obj/Temp） | 6.0 GB |
| 备份/旧版本文件 | 2 个（OpponentAi_20260601_193309_857.cs / OpponentAi_20260601_203106_895.cs） |

### 按模块代码分布

| 模块 | 文件数 | 总行数 | 核心文件 |
|------|--------|--------|---------|
| 联机核心 | 11 | ~16,400 | OnlineMatchController.cs (12,376)、OnlineMatchHud.cs (2,061) |
| 社交推理（离线） | 6 | ~3,700 | SocialPrototypeController.cs (2,750)、SocialPrototypeHud.cs (522) |
| 游戏玩法 | 7 | ~1,700 | ActionResolver.cs、OpponentAi.cs、GameController.cs、VictoryEvaluator.cs、PrototypeBootstrap.cs |
| UI | 3 | ~920 | MainMenuController.cs、GameOverController.cs、LobbyController.cs |
| 核心数据 | 6 | ~440 | GameState.cs、DistrictState.cs、Localization.cs 等 |
| 世界/地图 | 2 | ~210 | DistrictMapView.cs、DistrictNode.cs |
| Editor 工具 | 7 | ~1,930 | PrototypeSmokeTests.cs (863)、StageTwoCharacterAssetBuilder.cs 等 |

### 括号平衡验证

```
所有 48 个 .cs 文件括号完全平衡 — 0 处不匹配
```

---

## 四、已知遗留问题清单

### P0 — 阻断级

| 序号 | 问题 | 影响 | 建议 |
|------|------|------|------|
| 1 | Unity Editor 编译验证未执行 | 无法确认所有 .cs 文件在 Unity 中编译通过 | 在 Unity Editor 中打开项目，确认 0 编译错误 |

### P1 — 高优先级

| 序号 | 问题 | 影响范围 | 建议 |
|------|------|---------|------|
| 2 | 联机模式未在真实网络环境测试 | OnlineMatchController 的 Relay/UTP 逻辑仅代码审查，未跑真实联机 | 搭建 2 客户端联机环境验证 |
| 3 | Animator Controller GUID 引用可能需重新生成 | 动画剪辑 GUID 在 Unity Editor 外修改 .prefab 后可能存在引用漂移 | 通过 Unity Editor 重新生成 Controller |
| 4 | 离线模式 3D 资源回退路径未测试 | Resources.Load 失败时有兜底方块，但未验证所有回退路径 | 模拟资源缺失场景验证 |
| 5 | SocialPrototypeController 与 OnlineMatchController 共享场景行为未测试 | 两个控制器都有 Camera/Light 等基础设施创建，可能冲突 | 测试从主菜单切换模式多次确认无残留 |

### P2 — 中优先级

| 序号 | 问题 | 影响范围 | 建议 |
|------|------|---------|------|
| 6 | 联机网络层 15+ 问题（Stage6 审查标注） | 无加密、无重连、无 Host 迁移、12.5Hz 偏高 | 待网络环境就绪后逐项修复 |
| 7 | 离线模式 GameController 回合制与联机模式实时制不统一 | 离线回合制逻辑无法直接复用到联机 | 考虑抽象统一接口 |
| 8 | StoryEvent 未接入联机模式 | 联机模式缺少叙事事件驱动 | 联机稳定后补充事件系统同步 |
| 9 | Vivox 语音已移除（文本聊天替代） | 联机模式下语音频道不可用，仅文本聊天 | 不再规划语音方案 |
| 10 | Lobby/Relay 外网服务未配置 | 当前仅支持本地 IP 直连 | 绑定 Unity Cloud Project，配置 Authentication/Lobby/Relay |

### P3 — 低优先级

| 序号 | 问题 | 影响范围 | 建议 |
|------|------|---------|------|
| 11 | 备份文件 OpponentAi_*.cs（2 个）残留在源码目录 | 不影响编译但增加维护困惑 | 移入 TempAssets 或删除 |
| 12 | SocialPrototypeHud 与 PrototypeHud 双 Canvas 共存 | UI 逻辑复杂，面板切换可能遗漏 | 统一 UI 管理器后用 Canvas Group 切换 |
| 13 | MainMenuController、LobbyController、GameOverController 均为 OnGUI 实现 | 外观不如 uGUI Canvas | 后续替换为正式 Canvas UI |
| 14 | 离线角色缩放因子硬编码 clamp [0.04, 0.32] | 新角色模型可能需要调整 | 改为可配置参数 |

---

## 五、后续开发建议

### 短期（1-2 周）：验证与收口

1. **Unity Editor 编译验证**：打开项目，确认所有 .cs 文件编译通过，修复编译错误。
2. **离线模式完整可玩性测试**：从 MainMenu → 选择身份 → 进入游戏 → 完整一局 → GameOver → 返回主菜单，验证无阻断错误。
3. **联机模式本地双开测试**：Host + Client 本机双开，验证连接、角色同步、任务、会议、投票基本流程。
4. **清理备份文件**：将 `OpponentAi_20260601_*.cs` 移出源码目录。

### 中期（2-4 周）：联机完善

5. ~~Vivox 语音配置~~：Vivox 已移除，文本聊天替代。不再规划语音方案。
6. **Lobby/Relay 外网联机**：绑定 Authentication，配置 Lobby 房间码和 Relay，支持外网联机测试。
7. **网络层加固**：按 Stage6 审查清单逐项修复（加密、重连、反作弊校验）。
8. **联机事件系统**：将 StoryEvent 接入联机模式，丰富叙事节奏。

### 长期：向商业发行推进

9. **正式 UI 替换**：将 OnGUI 临时面板替换为 Canvas uGUI 正式界面。
10. **角色动画完善**：将程序化状态层替换为正式 Animator + 动画片段。
11. **任务小游戏正式化**：将数字校验任务升级为 4 个正式 Canvas 小游戏。
12. **外部测试**：组织 6-10 人封闭测试，收集平衡数据和反馈。

---

## 六、架构全景图

```
                          PrototypeBootstrap (启动入口)
                                   |
                    +--------------+--------------+
                    |                             |
              MainMenuController              (菜单显示)
                    |
        +-----------+-----------+
        |                       |
  StartOfflineGame()      StartOnlineGame()
        |                       |
  SocialPrototypeController  OnlineMatchController
        |                       |
  +-----+-----+            +----+----+
  |           |            |         |
GameController  DistrictMapView  OnlineSyncManager  OnlineMatchHud
  |           |            |         |
OpponentAi  PrototypeHud  TaskSync  MeetingSync  PlayerStateSync  OnlineVictoryBridge
  |
VictoryEvaluator ← GameOverController
```

**数据流**：
- 离线：PlayerAction → GameController → OpponentAi → VictoryEvaluator → GameOver
- 联机：Client Input → Host Authority → OnlineMatchController → OnlineSyncManager → OnlineVictoryBridge → GameOver
- 共享：GameState（离线直接读写；联机通过 OnlineVictoryBridge 映射）

---

## 七、编译与资产健康度

| 检查项 | 结果 |
|--------|------|
| .cs 文件括号平衡 | ✅ 全部 48 文件通过 |
| 损坏素材隔离 | ✅ LowpolyStreetPack 已移至 TempAssets/Quarantined |
| 资源清单完整 | ✅ AssetInventory.zh-CN.md |
| 烟测通过（上次） | ✅ 2026-05-04 20:25:52 PASS |
| Unity Editor 编译 | ⚠️ 待验证 |
| 联机网络环境测试 | ⚠️ 待验证 |
