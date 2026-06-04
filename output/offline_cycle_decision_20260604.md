# 离线循环去留决策记录

- 日期: 2026-06-04
- 决策人: File Agent（基于源码对比分析）
- 涉及文件: `SocialPrototypeController.cs` (3848行) vs `OnlineMatchController.cs` (12673行)

---

## 1. 功能重叠对比

### 1.1 架构对比

| 维度 | SocialPrototypeController (离线) | OnlineMatchController (联机) |
|---|---|---|
| 总行数 | 3,848 | 12,673（+2 partial） |
| 架构模式 | 单体 MonoBehaviour | 单体 + partial + 抽出服务 |
| 网络层 | 无 | Netcode for GameObjects + Relay |
| 玩家数量 | 1真人 + 4 Bot（固定5人） | 1-10人 + Bot填充 |
| 渲染 | 3D PrimitiveType / DenysAlmaral / Synty | 3D Quaternius / Synty / Kenney CityKit |
| 地图 | 2张（港区 / 警察局） | 1张大型九龙港区（含 VerticalSlice 多层） |

### 1.2 功能域重叠矩阵

| 功能域 | 离线 | 联机 | 重叠度 | 说明 |
|---|---|---|---|---|
| 世界生成 | PrimitiveType + EnvironmentManager + BuildingBuilder等11个生成器 | OnlineMapService + OnlineWorldBuilder + Quaternius/Synty/Kenney 模型 | 高 | 均生成3D地图，方式不同但目标相同 |
| 角色系统 | 5角色（Police/Undercover/Gang/Mole），3D预制体 | 10人，7职业（Inspector/Forensics/Tech/Undercover/Enforcer/Fixer/Driver），角色适配器 | 高 | 角色分配逻辑相似，离线角色是联机的子集 |
| 任务系统 | 5个固定任务站 + 问答小游戏 | 28个任务 + 多步骤小游戏框架（OnlineTaskService） | 高 | 离线是联机的简化版 |
| Bot AI | 路径/任务/击杀/破坏/断电/投票 | 路径/任务/击杀/破坏/投票（更完整） | 高 | 两套Bot逻辑高度重复 |
| 会议/投票 | 简易投票/跳过 | 完整会议流程（投票计票/角色揭示） | 中 | 离线是联机的极度简化版 |
| 击杀/尸体 | Q键击杀 + 尸体发现/R上报 | KillSystem + 尸体同步 + 上报流程 | 中 | 离线逻辑基本被联机覆盖 |
| 破坏/断电 | 仅断电一种 | 断电/封锁/通讯干扰/证据泄露/巡逻警报（OnlineTaskService） | 低 | 联机已超越离线 |
| 监控系统 | SecurityCamera (383行) 完整实现 | 无联机版（计划M5做） | **离线独有** | 🔑 需移植到联机 |
| 通风/暗线 | VentSystem (364行) 节点图 | 暗线已自有实现 | 低 | 各自独立，联机版已替代 |
| 证据追踪 | FootprintTrail + RouteEntry + 路线记忆 | 无 | **离线独有** | 可参考移植 |
| 关键任务 | CriticalTaskSystem (309行) | 无（OnlineTaskService覆盖） | **离线独有** | 参考价值 |
| 回合制层 | turnController + turnHud + turnMap | 无 | **离线独有** | 仅离线实验 |
| 小游戏(13个) | MiniGames/ 目录 (3,487行) | 无联机版（计划M5复用） | **离线独有** | 🔑 需移植到联机 |
| HUD | SocialPrototypeHud (527行, OnGUI) | OnlineMatchHud (2,060行, OnGUI) | 中 | 各自独立，联机版更强 |
| 聊天 | 离线聊天系统（ChatMessage/ChatSystem本地） | ChatSystem（非网络类，待联机化） | 中 | 均待改造 |

### 1.3 重叠度总评

**整体重叠度约 65%**。离线循环本质上是联机循环的「单机简化原型」：同样的社交推理核心玩法（角色分配→任务→击杀/破坏→报案→会议→投票→结算），但架构更紧凑、系统更少、没有网络层。

离线循环**独有的、联机尚未具备的功能**包括：
1. **监控系统** (`SecurityCamera.cs` 383行) — 计划 M5 移植
2. **13个小游戏** (`MiniGames/` 3,487行) — 计划 M5 复用接入
3. **证据足迹追踪** (FootprintTrail/RouteEntry) — 参考价值
4. **关键任务系统** (`CriticalTaskSystem.cs` 309行) — 参考价值
5. **回合制策略层** — 离线实验性质，不移植

---

## 2. 离线循环保留价值评估

### 2.1 作为单机 Bot 试验场

| 评估维度 | 评分 | 说明 |
|---|---|---|
| Bot 行为试验 | ★★★★☆ | 离线Bot可快速验证击杀/破坏/投票逻辑，无需起双实例 |
| 玩法快速迭代 | ★★★☆☆ | 无网络层负担，改一行即跑，但已有`OnlineDemoPlayMenu`烟测 |
| 教程沙盒 | ★★★★☆ | 新玩家可在离线模式学会基本操作后再进联机 |
| 美术预览 | ★★☆☆☆ | 离线用3D预制体渲染，与2D重做方向不一致 |

### 2.2 维护成本

| 成本项 | 估算 |
|---|---|
| 两套世界生成并存 | 离线11个生成器 vs 联机 OnlineWorldBuilder，总代码量约 5,000+ 行 |
| 两套 Bot AI 并存 | OpponentAi(534) + SocialPrototypeController内部Bot(约800行) vs OnlineBotController(约1,200行) |
| 两套任务/破坏并存 | 离线简易版 vs OnlineTaskService(905行) |
| 2D化时两套都改的风险 | 若两套循环都投入2D美术，工作量翻倍且行为一致性风险 |

---

## 3. 决策

### 3.1 离线循环定位

> **保留为「单机练习沙盒 / Bot 试验场」，不投入 2D 美术与正式玩法对齐。**

- 离线模式仅用于：快速原型验证、Bot行为调试、新手教学沙盒
- 菜单标注「练习/实验」
- 不在离线循环上排期任何美术任务（2D/3D 均不投入）
- 离线循环的渲染层维持现状（3D PrimitiveType），不做 2D 化

### 3.2 2D 美术投入决策

> **2D 美术仅服务联机循环。**

理由：
1. 联机是产品主线（6-10人联机对局），离线是辅助工具
2. 双线维护2D美术成本过高，且离线用户量预计远小于联机
3. 离线3D渲染已能满足「能跑就行」的沙盒需求
4. 计划 M3-M6 的2D化工作量已饱和，不应被离线分流

### 3.3 可移植到联机的逻辑清单

以下离线独有逻辑需在后续里程碑移植到联机：

| # | 逻辑 | 来源文件 | 行数 | 归属里程碑 | 说明 |
|---|---|---|---|---|---|
| 1 | 13个小游戏 | `SocialDeduction/MiniGames/*` | 3,487 | **M5** | 联机版本复用 MiniGameBase，通过 OnlineMiniGameBridge 接入 |
| 2 | 监控系统 | `SecurityCamera.cs` | 383 | **M5** | 联机版需服务器裁切下发，防信息泄露 |
| 3 | 证据足迹追踪 | `SocialPrototypeController.cs` FootprintTrail/RouteEntry | ~300 | M8（参考） | 可在联机中加入痕迹系统丰富推理 |
| 4 | 关键任务系统 | `CriticalTaskSystem.cs` | 309 | M5（参考） | 可为 OnlineTaskService 的关键任务提供设计参考 |
| 5 | Bot行为参考 | `Gameplay/OpponentAi.cs` | 534 | **M8** | 联机 Bot 升级时可参考离线 Bot 的行为模式 |

---

## 4. 执行确认

- [x] 离线循环定位明确：单机练习沙盒 / Bot 试验场
- [x] 2D 美术不投入离线循环
- [x] 可移植逻辑清单已登记
- [ ] README.md 中离线入口描述已更新（见下节）

---

## 5. README.md 更新

离线入口描述需更新为：

> 离线模式（练习/实验）：单人 + Bot，用于快速测试玩法或熟悉操作。不包含完整联机功能，不投入 2D 美术。

当前 README.md 相关段落位于 `PrototypeBootstrap.cs:11 GameMode{Offline,Online}` 的引用。菜单中的离线入口需同步标注「练习/实验」。
