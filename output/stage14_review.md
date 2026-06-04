---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9470d846c7eff9b24afb94a99a2cb3f0_d9c706b05f0811f1a4f35254002afed2
    ReservedCode1: jjteEo0oUaC5uuSM0Iwee5zaI2y5k7/ifUg+X1p/HD52UjK25kuznyDGP+POUFsslrHxnpV/paQHdM4HhaCJRYesDL3YAtRNbDREBfxQNrsDSQMxN60OaOEWMv4fwW3Zu5BomlWUyU2OcS7vMzeIW9ZcO5GZfS8tntll+pN9twhwmDc1pQWmytKnqIo=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9470d846c7eff9b24afb94a99a2cb3f0_d9c706b05f0811f1a4f35254002afed2
    ReservedCode2: jjteEo0oUaC5uuSM0Iwee5zaI2y5k7/ifUg+X1p/HD52UjK25kuznyDGP+POUFsslrHxnpV/paQHdM4HhaCJRYesDL3YAtRNbDREBfxQNrsDSQMxN60OaOEWMv4fwW3Zu5BomlWUyU2OcS7vMzeIW9ZcO5GZfS8tntll+pN9twhwmDc1pQWmytKnqIo=
---

# Stage 14 全项目审查 + Among Us 差距分析

> 审查日期：2026-06-03 | 项目路径：`/Users/zhugehao/projects/GanglandUndercover`

---

## 1. 代码统计

### 1.1 总览

| 指标 | 数值 |
|---|---|
| .cs 文件总数 | **88** |
| 总行数 | **39,464** |
| 类型定义（class/struct/enum/interface） | **141** |
| public/protected 方法 | **366** |

### 1.2 按模块分布

| 模块目录 | 行数 | 占比 | 说明 |
|---|---|---|---|
| `Online/` | 20,541 | 52.0% | 联机核心（OnlineMatchController 独占 13,099 行） |
| `SocialDeduction/` | 10,489 | 26.6% | 推理玩法 + 小游戏（SocialPrototypeController 3,828 行） |
| `UI/` | 3,466 | 8.8% | 界面系统 |
| `Gameplay/` | 1,982 | 5.0% | 游戏逻辑 |
| `Core/` | 551 | 1.4% | 状态/枚举/本地化 |
| `World/` | 357 | 0.9% | 地图节点 |
| `Audio/` | 348 | 0.9% | 音频管理 |
| `Editor/` | ~1,730 | 4.4% | 编辑器工具/测试 |

### 1.3 Top 10 最大文件

| 文件 | 行数 | 风险 |
|---|---|---|
| `OnlineMatchController.cs` | 13,099 | **God Object** — 严重违反 SRP，需拆分 |
| `SocialPrototypeController.cs` | 3,828 | 较大，涉及任务/会议/投票全流程 |
| `OnlineMatchHud.cs` | 2,064 | 联机 HUD 全覆盖，复杂度合理 |
| `PrototypeSmokeTests.cs` | 864 | 编辑器测试 |
| `GameOverController.cs` | 760 | 结算界面 |
| `OnlineMatchController.VerticalSlice.cs` | 616 | 垂直线切片适配 |
| `KillSystem_20260602_150052_224.cs` | 544 | **旧版本备份文件，应删除** |

---

## 2. 死代码检测

### 2.1 Public 方法死代码

检测到 **97 个** public/protected 方法在全代码库中无外部调用方（占总数 366 的 26.5%）。

**高优先级（可疑死代码）**：
| 文件 | 方法数 | 说明 |
|---|---|---|
| `UIManager.cs` | 10 | RegisterPanel / ShowOnly / ShowPanel / HidePanel / HideAll / CreatePanel / CreateTextWithStyle / CreateButton / CreateImageButton / CreateInputField / AddVerticalLayout — 大部分为静态工厂方法，可能仅在 Unity Inspector 反射调用 |
| `TransitionEffect.cs` | 4 | FadeToBlack / FadeFromBlack / SwitchScene / CompleteImmediately |
| `AudioManager.cs` | 6 | PlaySFX / PlaySFXAtPoint / PlayBGM / StopBGM / PauseBGM / ResumeBGM — 均通过 `AudioManager.Instance` 调用，静态分析无法追踪 |
| `SocialPrototypeController.cs` | 5 | ResolveAutoVote / GetCriticalTaskSystem / ExecuteTurnAction / StartTurnMeeting / CastTurnVote / SkipTurnVote — 可能通过 UnityEvent/反射触发 |
| `GhostMode.cs` | 4 | ExitGhostMode / CanSeeGhost / CanInteract / CanReport / CanCallMeeting |
| `OnlineSyncManager.cs` | 8 | OnMatchStarted / OnTaskCompletedLocally / OnMeetingBegan 等事件回调 |
| `TaskSync.cs` | 6 | CanComplete / OnTaskRepaired / CompletedCount 等 |

**重要说明**：静态分析对 Unity 项目存在大量假阳性——UnityEvent 绑定、`GetComponent` 反射、`Invoke` 动态调用均无法被正则匹配。实际建议仅关注第 3.3 节中"新增类型未被引用"部分。

### 2.2 旧版本文件

| 文件 | 行数 | 建议 |
|---|---|---|
| `KillSystem_20260602_150052_224.cs` | 544 | **立即删除** — 明确为时间戳备份 |

---

## 3. 类型定义一致性

### 3.1 总览

- 定义类型：**141**（class/struct/enum/interface）
- 未被其他文件引用的类型：**38**（27.0%）
- 其中 MonoBehaviour：5（可能通过 Unity 场景引用）

### 3.2 高置信度未引用类型（非 MonoBehaviour）

| 类型 | 定义位置 | 类型 | 评估 |
|---|---|---|---|
| `ParticleData` | UIParticleEffect.cs | struct | UI 内部辅助类型，仅同文件使用 |
| `PlayerRecord` | GameOverController.cs | struct | 仅同文件使用 |
| `TransitionType` | TransitionEffect.cs | enum | 仅同文件使用 |
| `TaskArea` | PoliceStationTasks.cs | enum | 内部枚举 |
| `CameraNode` | SecurityCamera.cs | struct | 内部数据结构 |
| `Asteroid` | AsteroidTask.cs | class | 小游戏内部类 |
| `DraggableEvidence` / `CaseSlotView` | EvidenceArchiveTask.cs | class | 小游戏内部类 |
| `SortItem` / `SortSlot` | SortTask.cs | class | 小游戏内部类 |
| `MemoryCell` | MemoryTask.cs | class | 小游戏内部类 |
| `Localization` | Localization.cs | class | 静态工具类，通过 `Localization.Text()` 静态方法调用 |
| `MusicTrack` | AudioManager.cs | enum | 同文件使用 |
| `GameMode` | PrototypeBootstrap.cs | enum | 同文件使用 |
| `EvaluateSource` | OnlineVictoryBridge.cs | enum | 同文件使用 |
| `SabotageButtonConfig` | SabotagePanel.cs | struct | 内部数据 |
| `NotebookTab` | OnlineMatchHud.cs | enum | 同文件使用 |
| `MapEntrance` / `ShipRoomSpec` / `OnlineVentNode` | OnlineMatchController.cs | enum/struct | 巨型文件内部类型 |
| `PrototypeSceneMenu` / `OnlineDemoPlayMenu` 等 Editor 类 | Editor/*.cs | class | 编辑器菜单项，通过 Unity MenuItem 调用 |

### 3.3 可疑项

| 类型 | 位置 | 风险 |
|---|---|---|
| `LoadingScreen` | LoadingScreen.cs | **MonoBehaviour，无其他 .cs 引用** — 可能仅在场景中挂载 |
| `TransitionEffect` | TransitionEffect.cs | **MonoBehaviour，无其他 .cs 引用** |
| `RoomDecoration` | RoomDecoration.cs | **MonoBehaviour，仅 SocialPrototypeController 引用但无实际调用链** |
| `SabotageButton` | SabotagePanel.cs | **MonoBehaviour，仅同文件使用** |
| `SabotageSync` | SabotageSync.cs | **class，仅 OnlineSyncManager 和它自己引用** |
| `WorldSpaceUI` | KillSystem_20260602_150052_224.cs | **在旧备份文件中定义，应随备份删除** |

---

## 4. MonoBehaviour 初始化检查

MonoBehaviour 的 Awake/Start/Update 初始化依赖于 Unity 场景绑定，静态分析无法完整检测依赖注入。代码库中已识别到以下模式：

| 文件 | 初始化方式 | 评估 |
|---|---|---|
| `MainMenuController.cs` | `Initialize(PrototypeBootstrap)` 手动注入 | 安全 |
| `LobbyController.cs` | `Awake()` 中创建 UI + 通过 PrototypeBootstrap 注入 | 安全 |
| `GameOverController.cs` | `Initialize(PrototypeBootstrap)` 手动注入 | 安全 |
| `SocialPrototypeController.cs` | `Awake()` + 手动初始化所有子系统 | 有 `_initialized` 标志位保护 |
| `OnlineMatchHud.cs` | 由 OnlineMatchController 注入上下文 | 需确保注入在 Awake 之前 |
| MiniGame 各 Task | `Show()` / `Hide()` 由 PickMiniGameType 创建后立即调用 | 安全 |

**风险点**：`SocialPrototypeController.Awake()` 创建大量子系统（150KB 文件），若其中任何一环初始化失败，缺少优雅降级。建议将子系统拆分为独立 MonoBehaviour 并通过 Inspector 引用注入。

---

## 5. 功能完整性检查

### 5.1 离线模式（SocialPrototypeController + GameController）

| 阶段 | 实现 | 状态 |
|---|---|---|
| 角色选择 | `MainMenuController` → `PrototypeBootstrap.StartOfflineGame()` | ✅ 完成 |
| 回合循环 | `GameController`：PlayerTurn ↔ AiTurn，每 3 天触发 Meeting | ✅ 完成 |
| 任务系统 | `TaskStation` → `PickMiniGameType()` → 10 种小游戏 | ✅ 完成 |
| 紧急任务 | `CriticalTaskSystem`（Oxygen/Reactor 类） | ✅ 完成 |
| 会议 | `GameController.RunMeeting()` / `PlayerCastVote()` | ✅ 完成 |
| 投票淘汰 | `GameState.EliminateRole()` | ✅ 完成 |
| 胜利判定 | `VictoryEvaluator.TryEvaluate()` — 双向渗透模型 | ✅ 完成 |
| 聊天 | `ChatSystem`（离线模式 AI 自动发送预设消息） | ✅ 完成 |
| 尸体报告 | `BodyVisual` + `BodyMarker` | ✅ 完成 |
| 通风管 | `VentSystem` | ✅ 完成 |
| 摄像头 | `SecurityCamera` | ✅ 完成 |
| 幽灵模式 | `GhostMode` | ✅ 完成 |
| 第二地图 | `PoliceStationMap` + `PoliceStationTasks` | ✅ 完成 |
| 紧急按钮 | `EmergencyButton` 定义存在 | ✅ 有声明 |

### 5.2 联机模式（OnlineMatchController + 子系统）

| 阶段 | 实现 | 状态 |
|---|---|---|
| 房间创建 | `OnlineMatchController.CreateRoom()` | ✅ 完成 |
| 房间加入 | `LobbyController` + 房间码输入 | ✅ 完成 |
| 角色分配 | `OnlineMatchController`：Host 分配 OnlineRole | ✅ 完成 |
| 同步 | `OnlineSyncManager` + `PlayerStateSync` + `TaskSync` | ✅ 完成 |
| 自由行动 | `OnlineMatchPhase.Action` — 任务/击杀/报告 | ✅ 完成 |
| 任务系统 | `OnlineMatchController` 自有任务体系（BuildTaskList / DrawActiveTaskPanel / DrawTaskMiniGameWidget） | ⚠️ 部分完成 |
| 会议 | `MeetingSync`：ReportBody → BeginMeeting → Vote → Resolve → EndMeeting | ✅ 完成 |
| 投票 | `OnlineMatchPhase.Voting` + 投票计数 | ✅ 完成 |
| 胜利判定 | `OnlineVictoryBridge` → `VictoryEvaluator` | ✅ 完成 |
| 聊天 | `ChatSystem`（联机全玩家 + 同阵营私聊） | ✅ 完成 |
| Host Migration | `HostMigrationManager`（心跳 + 快照迁移） | ✅ 完成 |
| 击杀同步 | `KillSystem`（可报告尸体） | ✅ 完成 |
| 破坏 | `SabotagePanel` + `SabotageSync` | ✅ 完成 |

### 5.3 UI 流程

| 界面 | 实现 | 状态 |
|---|---|---|
| 主菜单 | `MainMenuController`：角色选择 + 地图选择 + 开始按钮 | ✅ 完成 |
| 联机大厅 | `LobbyController`：创建/加入房间 + 玩家列表 | ✅ 完成 |
| 游戏 HUD | `PrototypeHud`（离线）/ `OnlineMatchHud`（联机） | ✅ 完成 |
| 社交 HUD | `SocialPrototypeHud`（任务/角色/地图信息） | ✅ 完成 |
| 会议界面 | `SocialPrototypeController` 内建会议 UI | ✅ 完成 |
| 结算界面 | `GameOverController`：胜利/失败 + 详细统计 | ✅ 完成 |
| 转场效果 | `TransitionEffect` + `LoadingScreen` | ✅ 完成 |

### 5.4 小游戏接入检查

**MiniGameType 枚举（10 种）**：

| # | MiniGameType | 实现文件 | 行数 | PickMiniGameType 关键词 | 状态 |
|---|---|---|---|---|---|
| 1 | WireTask | `WireTask.cs` | 268 | "货柜" / "电闸" | ✅ |
| 2 | MemoryTask | `MemoryTask.cs` | 298 | "监控" | ✅ |
| 3 | SwipeCardTask | `SwipeCardTask.cs` | 222 | "证物" / "档案" | ✅ |
| 4 | KeypadTask | `KeypadTask.cs` | 325 | "密码" / "保险箱" / "门禁" | ✅ |
| 5 | SortTask | `SortTask.cs` | 386 | "分类" / "垃圾" / "归档" / "整理" | ✅ |
| 6 | ScanTask | `ScanTask.cs` | 270 | "扫描" / "体检" / "化验" | ✅ |
| 7 | TapTask | `TapTask.cs` | 319 | "点击" / "反应" / "射击" | ✅ |
| 8 | CalibrateTask | `CalibrateTask.cs` | 277 | "航向" / "校准" / "校准仪" | ✅ |
| 9 | AsteroidTask | `AsteroidTask.cs` | 332 | "陨石" / "太空" / "碎片" | ✅ |
| 10 | DownloadTask | `DownloadTask.cs` | 327 | "下载" / "上传" / "数据" | ✅ |

**PickMiniGameType 覆盖分析**：
- 全部 10 种类型均被 `PickMiniGameType` 方法覆盖（关键词匹配 + 默认 hash 随机兜底）
- **但 PickMiniGameType 仅在离线模式（SocialPrototypeController）中存在**
- **联机模式（OnlineMatchController）没有集成 PickMiniGameType**，联机任务系统为独立实现（`BuildTaskList` / `DrawTaskMiniGameWidget` 等），两套体系完全分离

**额外存在但未入枚举的任务**：
- `EvidenceArchiveTask`（406 行）：拖拽证据归档，存在于 `MiniGames/` 目录下但**不在 MiniGameType 枚举中**。PoliceStationTasks 将其映射为 `MiniGameType.SortTask` 占位，注释标注"后续替换为 EvidenceArchiveTask"

---

## 6. Among Us 差距分析

### 6.1 功能对比矩阵

| Among Us 功能 | GanglandUndercover | 状态 | 详情 |
|---|---|---|---|
| **核心循环** | | | |
| 自由行动 + 任务 | ✅ | 完成 | 离线 10 种小游戏；联机独立任务系统 |
| 击杀 + 尸体报告 | ✅ | 完成 | KillSystem + BodyVisual |
| 紧急会议 | ⚠️ | 部分 | EmergencyButton 有声明定义，联机模式已接入 MeetingSync |
| 投票淘汰 | ✅ | 完成 | 离线/联机均支持投票 |
| 胜利判定 | ✅ | 完成 | 双向渗透模型 + 全淘汰判定 |
| **联机功能** | | | |
| 房间创建/加入 | ✅ | 完成 | LobbyController + OnlineMatchController |
| 主机迁移 | ✅ | 完成 | HostMigrationManager（心跳 + 快照） |
| 聊天（会议+自由阶段） | ✅ | 完成 | ChatSystem 跨模式支持 |
| 语音聊天 | ❌ | 缺失 | UnityServiceBootstrap 有 Vivox 初始化但无语音 UI 集成 |
| 好友系统 | ❌ | 缺失 | 无好友列表/邀请 |
| 跨平台 | ❌ | 未验证 | 仅 Unity Editor/macOS 测试 |
| **玩法系统** | | | |
| 通风管 | ✅ | 完成 | VentSystem |
| 破坏（Sabotage） | ✅ | 完成 | SabotagePanel + SabotageSync |
| 摄像头监控 | ✅ | 完成 | SecurityCamera |
| 幽灵模式 | ✅ | 完成 | GhostMode（可继续做任务，不可发言） |
| 紧急任务（O2/Reactor） | ✅ | 完成 | CriticalTaskSystem |
| 指纹/足迹 | ❌ | 缺失 | Among Us 的足迹追踪系统 |
| **地图** | | | |
| 第一地图 | ✅ | 完成 | GanglandDistrict（九龙港区） |
| 第二地图 | ✅ | 完成 | PoliceStation（警察局） |
| 第三地图 | ❌ | 未开始 | —— |
| 第四地图 | ❌ | 未开始 | —— |
| **角色/自定义** | | | |
| 多角色（Crewmate/Impostor） | ✅ | 完成 | 4 角色：Undercover/Gang/Police/Mole |
| 角色皮肤/颜色 | ❌ | 缺失 | 无角色自定义 |
| 帽子/宠物 | ❌ | 缺失 | —— |
| **设置** | | | |
| 游戏参数自定义 | ❌ | 缺失 | 击杀冷却/任务数量/投票时间等不可配 |
| 语言切换 | ⚠️ | 部分 | GameLanguage 枚举存在，Locale 文本部分 |
| **UI** | | | |
| 任务地图（小地图） | ⚠️ | 部分 | DistrictMapView 存在，联机模式接入状态不明 |
| 玩家列表（Tab 键） | ❌ | 缺失 | 联机模式下无可查看存活玩家的快捷面板 |
| 设置菜单 | ❌ | 缺失 | 无游戏内设置界面 |

### 6.2 差距评估总结

| 分类 | 已实现 | 部分实现 | 未实现 | 完成度 |
|---|---|---|---|---|
| 核心循环 | 5 | 0 | 0 | **100%** |
| 联机功能 | 4 | 1 | 3 | **63%** |
| 玩法系统 | 5 | 0 | 1 | **83%** |
| 地图 | 2 | 0 | 2 | **50%** |
| 角色/自定义 | 1 | 0 | 2 | **33%** |
| 设置 | 0 | 1 | 2 | **17%** |
| UI 辅助 | 0 | 1 | 2 | **17%** |
| **综合** | **17** | **3** | **12** | **~60%** |

---

## 7. 架构问题与风险

### 7.1 严重问题

| # | 问题 | 影响 | 建议 |
|---|---|---|---|
| 1 | **OnlineMatchController 13,099 行 God Object** | 维护困难、测试困难、Merge 冲突高 | 拆分为 RoomManager / TaskManager / MeetingManager / PlayerManager |
| 2 | **离线/联机任务系统完全分离** | 双倍维护成本、行为不一致 | 联机模式复用 PickMiniGameType，统一任务调度层 |
| 3 | **KillSystem 旧备份文件残留** | 代码库污染、混淆引用 | 删除 `KillSystem_20260602_150052_224.cs` |
| 4 | **EvidenceArchiveTask 未接入枚举** | 已写 406 行代码但无法被调度 | 加入 MiniGameType 枚举 + PickMiniGameType 映射 |

### 7.2 中等问题

| # | 问题 | 建议 |
|---|---|---|
| 5 | `UIManager` 10 个死方法（工厂/面板管理） | 确认是否完全被 `MainMenuController`/`LobbyController` 替代，若替代则删除 |
| 6 | `TransitionEffect` / `LoadingScreen` 无 .cs 引用 | 检查场景中是否绑定，若未使用考虑清理 |
| 7 | 编辑器类 `StageTwoCharacterAnimationSetup` 等无交叉引用 | 确认 MenuItem 绑定有效 |
| 8 | 无自动测试覆盖非 Editor 代码 | 建议添加核心逻辑的 PlayMode 测试 |

### 7.3 低优先级

| # | 问题 | 建议 |
|---|---|---|
| 9 | 小游戏内部辅助类（SortItem/MemoryCell/Asteroid 等）被标记为未引用 | 正常现象，仅同文件使用 |
| 10 | `PoliceStationTasks.GetTaskName()` 无调用 | 若证据归档任务最终接入时再激活 |

---

## 8. 差距缩小优先级建议

### P0 — 立即执行（本阶段）

1. **删除旧备份** `KillSystem_20260602_150052_224.cs`
2. **EvidenceArchiveTask 接入枚举**：加入 `MiniGameType` + `PickMiniGameType`
3. **联机任务系统统一**：OnlineMatchController 的 `DrawTaskMiniGameWidget` 对接 `PickMiniGameType`

### P1 — 联机聊天完善

4. **语音聊天 UI 集成**：Vivox 已初始化，补 UI（对 Among Us 体验影响大）
5. **联机小游戏同步**：离线任务系统（PickMiniGameType）接入 OnlineMatchController
6. **Host Migration 联调测试**：迁移快照完整性验证

### P2 — 体验补全

7. **游戏参数配置面板**：击杀冷却/任务数量/投票时间可调
8. **玩家列表（Tab 键）**：联机模式下查看存活玩家
9. **第三地图设计**：新地图区域 + 任务定义
10. **角色外观自定义**：颜色至少

### P3 — 后续迭代

11. 好友系统 / 跨平台验证
12. 足迹系统
13. 设置菜单

---

## 9. 产出物

本报告为最终产出，路径：
*（内容由AI生成，仅供参考）*
