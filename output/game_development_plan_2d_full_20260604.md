# Gangland Undercover 完整开发计划（2D 重做 · 全系统版）

- 日期: 2026-06-04
- 项目路径: `/Users/zhugehao/projects/GanglandUndercover`
- 引擎: Unity 6000.4.5f1（Netcode for GameObjects + Unity Transport）
- 题材: 港区潜线 / Harbor Undercover —— 香港港区警匪卧底社交推理
- 本文取代: `game_development_plan_full_20260604.md` 与 `current_gap_matrix_20260604.md`、`game_development_plan_2d_20260604.md`
- 目标: 把现有警匪社交推理联机原型，在**地图与世界表现全面 2D 化**的方向上，推进到可封闭测试、6-10 人可稳定开局、单局 10-15 分钟、节奏接近 Among Us 且保留警匪差异化的发行候选版本。本计划覆盖**整个程序的全部系统**，不只地图。

> 警告：本项目代码在持续高速变动。本文 0 节基线是 2026-06-04 源码核对结果，但执行任何任务前，必须以**当时的源码**重新核对该任务涉及文件的现状，不得直接照搬本文或任何旧 stage 报告的结论。

---

## 目录

- 0 真实基线（源码实测）
- 1 产品定义
- 2 系统总览与责任边界
- 3 总体里程碑 M0-M10
- 4 详细任务清单（按里程碑）
- 5 2D 世界表现专项（相机/渲染/资产/性能）
- 6 2D 地图专项（玩法→灰盒→布局→测试→美术）
- 7 2D UI 专项（信息架构/地图UI/提示/小游戏外壳/会议证据板）
- 8 联机架构专项（控制器瘦身/同步/权威/反作弊）
- 9 非核心但必须收口的系统（教程/设置/本地化/聊天/音频/角色外观）
- 10 测试矩阵
- 11 Bug 优先级
- 12 目标模块结构
- 13 风险登记册
- 14 执行节奏（第一/二/三周）
- 15 明确不做
- 16 最终成功标准

---

## 0. 真实基线（源码实测，2026-06-04）

### 0.1 工程规模

- `Assets/_Project/Scripts` 约 104 个 C# 文件，约 46,500+ 行（不含编辑器工具）。
- 按目录行数（核对当日）：
  - `Online/` 约 20,775 行（含 `OnlineMatchController.cs` 12,673 行 + 2 partial；`OnlineMatchHud.cs` 2,060；`OnlineTaskService.cs` 905）
  - `SocialDeduction/` 约 13,051 行（含 `SocialPrototypeController.cs` 3,848）
  - `SocialDeduction/MiniGames/` 约 3,487 行（13 个小游戏）
  - `UI/` 约 4,580 行
  - `Gameplay/` 约 1,973 行；`Tutorial/` 1,300；`Core/` 544；`World/` 354；`Audio/` 347；`Environment/` 1,001
- 测试: 仅 `Assets/_Project/Editor/PrototypeSmokeTests.cs`，单元测试覆盖几乎为零。
- Editor 工具: `PrototypeSceneMenu` / `OnlineDemoPlayMenu` / `VerticalSliceStageOneBuilder` / `StageTwoCharacterAssetBuilder` / `StageTwoCharacterAnimationSetup` / `QuaterniusRuntimeResourceMirror` / `PrototypeSmokeTests`。

### 0.2 已经做好的（旧文档仍当待办，实际已完成，不要重做）

| 能力 | 证据 | 含义 |
|---|---|---|
| 规则配置层 | `OnlineRuleSet.cs`(108) 被控制器/HUD/快照引用 | 旧 Task1.1 已完成 |
| 地图坐标服务 | `OnlineMapService.cs`(219)，`ScaleMapPosition`/`ScaleMapSize`/`UnderworldPassagePosition` 为点位唯一来源 | 旧 Task1.2 已完成 |
| 暗线/通风管统一 | `OnlineMatchController.cs:4451` 注释"合并原 OnlineVents/TryVent" | 旧 Task1.3 已完成，无双系统 |
| 任务服务抽出 | `OnlineTaskService.cs`(905)，含事件 `OnTaskCompleted/OnSabotaged/OnEvidenceChanged/OnSabotageEffectApplied` 等 + 公开只读状态 | 旧 Task1.4 已大部完成 |
| SabotageSync 去反射 | `SabotageSync.cs:144-148` 改用 `controller.BlackoutTimer` 等公开属性，`:296 controller.LocalRole`，全文已无 `Reflection/GetField/BindingFlags` | 旧 Task1.6 已完成 |
| Relay 业务代码 | `OnlineMatchController.cs:1371-1444` `CreateAllocationAsync/GetJoinCodeAsync/JoinAllocationAsync/SetRelayServerData` | 联机房间码底层已具备 |
| 鬼魂联机可用 | `Gameplay/GhostMode.cs`(227)，控制器淘汰/驱逐时激活 | 死亡观战已有 |
| 物理碰撞已 2D | `OnlineMatchController.cs:191 PhysicsColliderCount` 统计 `Collider2D`；玩家位置 `Vector3(x,y,0)` | 2D 化逻辑层零改动 |
| 双模式架构 | `PrototypeBootstrap.cs:11` `GameMode{Offline,Online}`，默认 Online，菜单驱动 | 离线/联机两套循环并存 |
| 本地化 | `Localization.cs` 中/英双语表 | 已有 i18n 骨架 |

### 0.3 真实待修/待做（含新发现的债务）

| # | 项 | 真实状态 | 证据 |
|---|---|---|---|
| 1 | **控制器超重** | 12,673 行，仍含世界生成/相机/Bot/会议/快照/OnGUI | `OnlineMatchController.cs` |
| 2 | **破坏 timer 状态重复** | 控制器自有 `blackoutTimer` 等字段并倒计时+序列化，`OnlineTaskService` 也有一份 | 控制器 `:407/:463/:2185-2197/:2361/:2480`，服务 `:80-84` |
| 3 | **渲染层仍 3D 图元** | `PrimitiveType.Cube/Sphere/Cylinder` + 带 z 深度 `CreatePropChild` | 控制器 `:7301+`/`:7406+` |
| 4 | **行动相机透视** | 预览态 orthographic，行动态 `fieldOfView` | 控制器 `:7029-7060`、`:374-467` |
| 5 | **联机小游戏缺失** | 13 个小游戏仅在 `SocialDeduction/MiniGames/`，无 `Online/MiniGames/` | 目录缺失 |
| 6 | **监控仅离线** | `SocialDeduction/SecurityCamera.cs`(383)，Online 无引用 | — |
| 7 | **语音 stub 但 UI 仍承诺** | `UnityServiceBootstrap.cs:31-35` Vivox 已移除；README 仍写近距离语音；`VoiceChatSystem.cs`(1080) 纯本地无 NetworkBehaviour | — |
| 8 | **ChatSystem 非网络类** | `ChatSystem.cs:19 class ChatSystem`（无 NetworkBehaviour/RPC） | — |
| 9 | **Host Migration 未联调** | `HostMigrationManager.cs`(452) 结构完整但无联机验证 | — |
| 10 | **单元测试近零** | 仅 1 smoke 文件 | — |
| 11 | **仓库卫生** | 约 19,500+ 未提交改动（多为 Kenney CityKit 二进制）+ 根目录 `temp_fix_calls.py`/`temp_fix2.py` | git status |
| 12 | **离线/联机双循环重叠** | `SocialPrototypeController`(3848) 与 `OnlineMatchController`(12673) 各有完整循环；2D 化需明确是否两套都改 | — |

### 0.4 与旧文档的偏差教训

- `current_gap_matrix_20260604.md` 误判"Relay 零代码""SabotageSync 无反射"，**不可信**。
- `game_development_plan_full_20260604.md` 方法论好但"现状判断"过期。
- 真相是动态的：审查当天 `OnlineTaskService` 抽出、SabotageSync 去反射都在持续提交中完成。**每个任务执行前必须复核源码。**

---

## 1. 产品定义

### 1.1 核心体验

6-10 人警匪卧底局。三大阵营：

- **警方/市民阵营**（Inspector、Forensics、Tech 等职业）：跑证据任务、看监控、开会议，靠证据链闭合或票出黑帮取胜。
- **黑帮阵营**（Enforcer、Fixer）：击杀、破坏、走暗线通道、伪装任务、会议误导，靠压制人数或拖垮证据链取胜。
- **卧底/线人**（Undercover、Mole、Driver）：双面身份，制造信息噪声。

单局 10-15 分钟，过程持续产生：信息差、路线怀疑、任务压力、会议冲突。

### 1.2 视角与表现：2D 优先（本版核心方向）

正式地图与世界表现采用 **top-down 正交 2D**。现有 3D 资源（Synty / DenysAlmaral / Kenney CityKit / Quaternius）降级为概念参考或必要时预渲染 sprite，不再作为正式生产路线。

为什么 2D：
- 读图清楚、遮挡少，任务点/尸体/玩家易见 —— 这是 Among Us 的核心优势。
- 美术成本低、迭代快，Tilemap/Sprite 改路线不必重摆 3D。
- 小地图、大地图、会议证据板更易与世界坐标一致。

为什么现在可行（成本低于想象）：
- 玩家位置已是 `Vector3(x,y,0)`，z 恒为 0。
- 碰撞已是 `Collider2D`。
- 点位已统一走 `OnlineMapService`。
- 因此 2D 化 = **替换"渲染 + 相机投影 + 美术资产"，逻辑/网络层几乎不动**。

### 1.3 警匪差异化（保留并强化）

- 证据链进度（非纯任务条）+ 嫌疑值 + 线索板。
- 警署、港区、后巷、监控、证物、线人、卧底等题材元素。
- 黑帮"暗线通道"机动（底层与通风管统一，命名差异化）。
- 会议展示可推理线索，而非直接给答案。

### 1.4 平台与范围

- 首发 PC（macOS/Windows），横屏，键鼠。
- 联机：Host/Client + Relay 房间码，6-10 人。
- 不在范围（见第 15 节）：移动端、专用服务器、商城账号、写实 3D。

---

## 2. 系统总览与责任边界

> 全程序系统清单，每项给出现状与本计划处理方式。这是"完整开发计划"的索引。

### 2.1 联机核心（Online/）

| 系统 | 文件 | 现状 | 计划处理 |
|---|---|---|---|
| 对局总控 | `OnlineMatchController.cs`(12673) | 超重 | M2/M8 增量抽服务、瘦身 |
| 规则 | `OnlineRuleSet.cs`(108) | 已用 | M8 调参 |
| 地图坐标 | `OnlineMapService.cs`(219) | 已用 | M6 地图 UI 同源 |
| 任务/破坏 | `OnlineTaskService.cs`(905) | 已抽出 | M2 消除与控制器的 timer 重复；M5 接小游戏 |
| 快照同步 | `GameStateSnapshot.cs`(471)、`OnlineSyncManager.cs`(236) | 已用 | M8 反作弊收口 |
| 分项同步 | `PlayerStateSync`(142)/`TaskSync`(167)/`MeetingSync`(104)/`SabotageSync`(351) | 已用，SabotageSync 已去反射 | M5/M7 与服务对齐 |
| 击杀/尸体 | `KillSystem.cs`(507) | 3D 表现 | M3 改 2D 表现 |
| 破坏面板 | `SabotagePanel.cs`(481) | 已用 | M5 接修复小游戏 |
| 胜负桥 | `OnlineVictoryBridge.cs`(300) | 已用 | M4 胜负矩阵测试 |
| 服务引导 | `UnityServiceBootstrap.cs`(202) | Vivox 移除 | M1 语音定调、M7 Relay 成品 |
| Host 迁移 | `HostMigrationManager.cs`(452) | 未联调 | M7 验证或降级 |
| 聊天 | `ChatSystem.cs`(281)/`ChatMessage.cs`(26) | 非网络类 | M1 语音方案 B 时改文本聊天联机 |
| HUD | `OnlineMatchHud.cs`(2060) | 含 OnGUI | M6 全 Canvas 化 |

### 2.2 离线社交推理（SocialDeduction/）

| 系统 | 文件 | 现状 | 计划处理 |
|---|---|---|---|
| 离线总控 | `SocialPrototypeController.cs`(3848) | 完整离线循环 | M0 决策：保留为单机练习/Bot 试验场，**不投入 2D 美术** |
| 离线 HUD | `SocialPrototypeHud.cs`(527) | 离线 | 同上 |
| 小游戏×13 | `MiniGames/*`(3487) | 仅离线，`MiniGameBase` 用 OnComplete/OnCancel 回调 | **M5 复用接入联机** |
| 通风管 | `VentSystem.cs`(364) | 离线，节点图 | 联机暗线已自有实现，仅概念参考 |
| 监控 | `SecurityCamera.cs`(383) | 离线 | **M5 做联机版** |
| 紧急按钮 | `EmergencyButton.cs`(19) | 仅 UI 占位 | M4 接报案/会议 |
| 关键任务 | `CriticalTaskSystem.cs`(309) | 离线 | M5 参考 |
| 角色 | `SocialCharacter`/`CharacterCustomizer`(637)/`Wardrobe`/`Bean*` | 见 9.6 | M3 决策角色 2D 表现 |
| 语音 | `VoiceChatSystem.cs`(1080) | 纯本地 | M1 定调 |
| 环境美术生成器 | `EnvironmentManager`(492)/`BuildingBuilder`(711)/`StreetFurniture`(468)/`StreetProps`(352)/`DetailScatter`(366)/`BillboardSystem`(338)/`LightingMaster`(260)/`WeatherController`(209)/`ProceduralTexture`(283)/`MaterialFactory`(322)/`RoomDecoration`(277) | 3D 运行时生成 | **M3 2D 化主战场：被 2D Tilemap/Sprite 取代或预渲染** |

### 2.3 通用/玩法/UI/工具

| 系统 | 文件 | 现状 | 计划处理 |
|---|---|---|---|
| 游戏状态/枚举 | `Core/*`(544) | 已用 | 保留 |
| 本地化 | `Localization.cs`(141) 中英 | 已用 | M9 文案补全 |
| 区域地图数据 | `World/DistrictNode`/`DistrictMapView`(124)/`PoliceStationMap`(144) | 离线为主 | M8 警署 2D 图参考 |
| 警署内饰生成 | `Environment/PoliceStationInteriorBuilder.cs`(1001) | 3D 生成 | M8 2D 化 |
| 离线玩法 | `Gameplay/GameController`/`ActionResolver`(381)/`OpponentAi`(534)/`VictoryEvaluator`(153) | 离线 | OpponentAi 供 M8 Bot 参考 |
| 启动引导 | `Gameplay/PrototypeBootstrap.cs`(323) | 菜单驱动 | 保留 |
| 教程 | `Tutorial/*`(1300) | 见 9.1 | M9 接入新手引导 |
| 设置 | `UI/Settings*`(SettingsManager 348/Data 293/Helper 473) | 见 9.2 | M6 收口持久化 |
| 菜单/大厅/结算 | `UI/MainMenuController`(537)/`LobbyController`(321)/`GameOverController`(760) | 已用 | M6/M7 Canvas 化 |
| 主题/过渡/加载/粒子 | `UI/ThemeManager`(169)/`TransitionEffect`(326)/`LoadingScreen`(368)/`UIParticleEffect`(167) | 已用 | M7 统一设计系统 |
| 音频 | `Audio/AudioManager.cs`(347) + 运行时音效 | 已用 | M7 事件反馈 |

---

## 3. 总体里程碑 M0-M10

| 里程碑 | 目标 | 关键通过标准 |
|---|---|---|
| **M0 真实基线** | 编译/本地局/双开局跑通并记录；确定离线循环去留 | 0 编译错误；本地+双开到结算；基线报告以源码为准；离线循环定位明确 |
| **M1 债务清理与方向定调** | worktree 保护、消除 timer 重复、语音二选一、补最小测试 | worktree 已提交；破坏状态单一来源；README 与代码一致；规则/胜负有测试 |
| **M2 联机架构稳健化** | 控制器增量瘦身、Host 权威基础、快照健壮 | 抽出 Bot/相机/世界生成至少 1 个；快照往返测试；双开烟测不退化 |
| **M3 2D 世界表现地基** | 相机正交化、渲染后端 2D 化、角色/尸体/任务 2D | 行动相机 orthographic；2D 渲染后端可切换；双开烟测不退化 |
| **M4 标准局闭环** | 角色分配/节奏/鬼魂/胜负矩阵成品 | 5/8/10 人局可完整跑；默认 10-15 分钟；胜负有测试 |
| **M5 联机小游戏与信息系统** | 13 小游戏接入联机、破坏修复、监控、证据板、嫌疑 | ≥6 联机小游戏；破坏须修复；会议 3 类线索；监控影响推理 |
| **M6 2D 港区玩法地图** | 灰盒→布局→多人测试→tile/sprite→替换 | 灰盒可跑局；多人测试达标；80% 灰盒被替换且路线不变 |
| **M7 服务、房间与 UI 成品** | Relay 房间码、断线策略、全 Canvas、2D 地图 UI | 双机 Relay 完整局；房间码 Canvas 路径；新玩家不看文档完成第一局 |
| **M8 内容与平衡** | 警署第二张 2D 图、职业收敛、Bot 升级、平衡数据 | 两张 2D 图可联机；8 人局 10-15 分钟、胜率 45-55 |
| **M9 收口系统** | 教程、设置、本地化、音频反馈、可访问性 | 新手引导可完成；设置持久化；中英完整；色盲/黑灯可玩 |
| **M10 封测发行准备** | 构建、日志、测试自动化、发布门槛 | 外部包可分发；72 小时无 P0/P1 |

### 3.1 依赖图

```text
M0 真实基线
 -> M1 债务清理与方向定调
   -> M2 联机架构稳健化
     -> M3 2D 世界表现地基
       -> M4 标准局闭环
         -> M5 联机小游戏与信息系统
           -> M6 2D 港区玩法地图
             -> M7 服务/房间/UI 成品
               -> M8 内容与平衡
                 -> M9 收口系统
                   -> M10 封测发行
```

可并行：M2 架构抽服务与 M3 相机正交化；M5 小游戏 UI 与 M7 Relay 环境配置（协议须先定）；M9 教程/本地化与 M8 平衡数据采集。
不可并行：M3 渲染未稳前不做 M6 大规模美术替换；M5 协议未定前不并行接多个小游戏；编译未过不做功能开发；M1 timer 重复未消前不在控制器加新破坏类型。

---

## 4. 详细任务清单（按里程碑）

> 每个任务统一格式：**描述 / 验收标准 / 验证方式 / 依赖 / 涉及文件 / 规模 / 边界情况**。执行前必须以**当时源码**复核涉及文件，本文行号为 2026-06-04 快照，可能已漂移。

### M0 真实基线

#### Task 0.1 编译与本地双开烟测基线
- **描述**: 在干净 checkout 上完成一次完整编译，记录编译告警；用 `OnlineDemoPlayMenu` 起 Host，再用第二实例 Join，跑到结算，记录每步可达性。
- **验收**: 0 编译错误；Host+Client 双开能进入对局并跑到 GameOver；产出一份「基线可达性」记录（哪些菜单/流程可达、哪些断点）。
- **验证**: 控制台 0 error；录屏或逐步日志；`PrototypeSmokeTests` 全绿。
- **依赖**: 无。
- **涉及文件**: `Editor/OnlineDemoPlayMenu`、`PrototypeBootstrap.cs`、`OnlineMatchController.cs`、`Editor/PrototypeSmokeTests.cs`。
- **规模**: S（半天，主要是跑与记录）。
- **边界情况**: 第二实例端口/Relay 不可用时退化为本地 loopback；Unity 域重载导致首帧异常需区分真实 bug 与编辑器噪声。

#### Task 0.2 离线循环去留决策
- **描述**: 评估 `SocialPrototypeController`(3848) 与 `OnlineMatchController`(12673) 的功能重叠，明确离线循环在本计划中的定位（建议：保留为单机 Bot 试验场/教程沙盒，**不投入 2D 美术与玩法对齐**，避免双线维护）。
- **验收**: 一份决策记录写入计划与 README；离线入口在菜单中标注「练习/实验」；明确 2D 美术只服务联机循环。
- **验证**: README 与菜单文案一致；后续里程碑不再为离线循环排期美术任务。
- **依赖**: Task 0.1。
- **涉及文件**: `SocialPrototypeController.cs`、`SocialPrototypeHud.cs`、`PrototypeBootstrap.cs`、`README.md`。
- **规模**: S。
- **边界情况**: 若离线循环含联机缺失的可复用逻辑（如某些任务判定），需登记为「可移植清单」而非直接弃用。

#### Task 0.3 基线报告与债务登记
- **描述**: 把 0.3 节的 12 项真实待修，逐项在 issue/任务系统建条目，附源码证据行号；明确每项归属里程碑。
- **验收**: 12 条债务全部有归属里程碑与证据；本计划 0.3 表为唯一事实源。
- **验证**: 交叉核对：每条债务能在源码定位到证据行。
- **依赖**: Task 0.1。
- **涉及文件**: 全 0.3 表所列文件。
- **规模**: S。
- **边界情况**: 执行时若发现债务已被消除（如某 timer 已合并），就地降级并记录「复核日 + 已解决」。

### M1 债务清理与方向定调

#### Task 1.1 worktree 卫生与提交保护
- **描述**: 处理约 19,500+ 未提交改动（多为 Kenney CityKit 二进制）。把第三方美术二进制纳入 `.gitignore` 或 Git LFS；删除根目录 `temp_fix_calls.py`/`temp_fix2.py`；提交一个干净基线。
- **验收**: `git status` 干净或仅剩有意保留项；临时脚本移除；二进制策略落地（LFS 或 ignore）。
- **验证**: `git status`；`git log` 有一条「基线整理」提交；克隆后体积合理。
- **依赖**: Task 0.1。
- **涉及文件**: `.gitignore`、`.gitattributes`、根目录临时脚本、`Assets/.../CityKit*` 二进制。
- **规模**: M。
- **边界情况**: 若二进制已被引用进场景/prefab，迁移 LFS 前需确认 meta 不丢；切勿误删被引用资源。

#### Task 1.2 消除破坏 timer 状态重复（关键债务 #2）
- **描述**: 控制器自有 `blackoutTimer/lockdownTimer/...` 字段（`:407/463/2185-2197/2361/2480`）与 `OnlineTaskService`(`:80-84`) 重复并各自倒计时。统一为 `OnlineTaskService` 单一来源；控制器只读 `taskService.BlackoutTimer` 等。
- **验收**: 控制器不再持有破坏 timer 私有字段与倒计时逻辑；序列化/快照只从服务取值；`SabotageSync` 仍读公开属性正常。
- **验证**: EditMode 测试：服务 tick 后控制器读数一致；双开烟测破坏全流程（触发→倒计时→修复）不退化；`SabotageSync` 客户端表现正确。
- **依赖**: Task 0.3。
- **涉及文件**: `OnlineMatchController.cs`(`:253-257/407/463/2185-2197/2361/2480`)、`OnlineTaskService.cs`(`:80-84`)、`SabotageSync.cs`(`:144-148`)、`GameStateSnapshot.cs`。
- **规模**: L。
- **边界情况**: 快照恢复/Host 迁移时 timer 续算；同帧多破坏并发；服务未就绪时控制器读默认值不报 null。

#### Task 1.3 语音方案二选一定调
- **描述**: Vivox 已移除（`UnityServiceBootstrap.cs:31-35`）。在「方案 A：暂不做语音、删除 UI 承诺与 README 文案」与「方案 B：做联机文本聊天替代语音、保留近距离概念」中二选一定调。建议方案 B（成本可控、信息量大）。
- **验收**: README 不再承诺近距离语音；UI 中语音控件按所选方案处理（移除或改文本聊天入口）；`VoiceChatSystem.cs`(1080) 标注为「本地占位，不联机」或移除。
- **验证**: README 与代码一致；UI 无失效语音按钮。
- **依赖**: Task 0.2。
- **涉及文件**: `README.md`、`UnityServiceBootstrap.cs`、`VoiceChatSystem.cs`、`OnlineMatchHud.cs`、`ChatSystem.cs`。
- **规模**: S（决策）+ 后续 M1 Task1.4 落地。
- **边界情况**: 若选 B，则文本聊天必须真正联机（见 1.4），不能再是本地类。

#### Task 1.4 文本聊天联机化（若选方案 B）
- **描述**: `ChatSystem.cs:19` 当前是非网络类。改造为 `NetworkBehaviour`，会议期/全局/近距离三通道用 RPC 同步，死亡玩家进鬼魂频道。
- **验收**: 双开下一方发言另一方实时可见；会议期与行动期频道规则正确；鬼魂只与鬼魂可见。
- **验证**: 双开手测三通道；EditMode 测消息序列化；防刷屏限流。
- **依赖**: Task 1.3 选 B。
- **涉及文件**: `ChatSystem.cs`、`ChatMessage.cs`、`OnlineMatchController.cs`（会议状态钩子）、`OnlineMatchHud.cs`（聊天 UI）。
- **规模**: M。
- **边界情况**: 长消息截断；表情/特殊字符；客户端时钟与服务器会议状态不同步时的频道归属；防 XSS/注入（纯文本渲染）。

#### Task 1.5 最小测试网建立
- **描述**: 单元测试近零（仅 1 smoke）。为「规则/胜负/破坏 timer/坐标服务」补 EditMode 测试，建立后续回归网。
- **验收**: 至少覆盖 `OnlineRuleSet` 角色分配、`OnlineVictoryBridge` 胜负判定、`OnlineTaskService` timer、`OnlineMapService` 坐标映射；CI 可跑。
- **验证**: `dotnet test`/Unity Test Runner 全绿；引入回归时能红。
- **依赖**: Task 1.2（timer 单源后才测得准）。
- **涉及文件**: 新增 `Editor/Tests/*`，被测 `OnlineRuleSet.cs`、`OnlineVictoryBridge.cs`、`OnlineTaskService.cs`、`OnlineMapService.cs`。
- **规模**: M。
- **边界情况**: 依赖 `FindAnyObjectByType`/场景的逻辑需可注入或抽纯函数后测；避免测试依赖真实网络。

### M2 联机架构稳健化

#### Task 2.1 控制器增量瘦身：抽出世界生成
- **描述**: 控制器 `:7301+/7406+` 含世界/道具生成（3D 图元 + z 深度）。抽到 `Online/World/OnlineWorldBuilder`（先平移不改行为），为 M3 2D 渲染切换留接口。
- **验收**: 控制器不再直接 new PrimitiveType；世界生成走新类；行为与抽出前一致。
- **验证**: 双开烟测世界外观一致；diff 仅为搬移；新类有最小测试或冒烟。
- **依赖**: Task 1.5。
- **涉及文件**: `OnlineMatchController.cs`(`:7301+/7406+`)、新增 `Online/World/OnlineWorldBuilder.cs`、`OnlineMapService.cs`。
- **规模**: L。
- **边界情况**: 生成顺序/父子层级/对象池；NetworkObject spawn 时机不能因搬移而改变。

#### Task 2.2 控制器增量瘦身：抽出 Bot
- **描述**: 控制器内 Bot 逻辑抽到 `Online/Bots/OnlineBotController`，参考 `Gameplay/OpponentAi`(534)。先平移。
- **验收**: Bot 行为不变；控制器去除 Bot 私有状态；Bot 可独立开关。
- **验证**: 双开 + Bot 填充烟测；Bot 路径/任务/投票行为与前一致。
- **依赖**: Task 2.1。
- **涉及文件**: `OnlineMatchController.cs`（Bot 段）、新增 `Online/Bots/OnlineBotController.cs`、参考 `OpponentAi.cs`。
- **规模**: L。
- **边界情况**: Bot 只在 Host 决策（权威）；客户端不得本地模拟 Bot；填充/移除人数变化时不崩。

#### Task 2.3 控制器增量瘦身：抽出相机
- **描述**: 控制器 `:374-467/7029-7060` 相机配置（预览 orthographic / 行动 perspective）抽到 `Online/Camera/OnlineCameraRig`，为 M3 全正交化做准备。
- **验收**: 相机行为暂不变但集中可控；控制器不再直接改 `fieldOfView`。
- **验证**: 双开视角与前一致；切换预览/行动态正常。
- **依赖**: Task 2.1。
- **涉及文件**: `OnlineMatchController.cs`(`:374-467/7029-7060`)、新增 `Online/Camera/OnlineCameraRig.cs`。
- **规模**: M。
- **边界情况**: 死亡观战/鬼魂相机；会议态相机；分辨率/宽高比自适应。

#### Task 2.4 快照健壮性
- **描述**: 加固 `GameStateSnapshot`(471)/`OnlineSyncManager`(236) 往返：捕获→序列化→恢复无状态丢失，为 Host 迁移与反作弊打底。
- **验收**: 捕获后立即恢复，状态等价（玩家位置/角色/任务/破坏/会议/票数）；版本不匹配有降级。
- **验证**: EditMode 往返测试；双开中途强制快照恢复不崩。
- **依赖**: Task 1.2（timer 单源）、Task 2.1。
- **涉及文件**: `GameStateSnapshot.cs`、`OnlineSyncManager.cs`、`OnlineMatchController.cs`（Capture/Restore）。
- **规模**: M。
- **边界情况**: 死亡/鬼魂玩家；进行中的小游戏；破坏倒计时续算；新加入客户端的初始快照。

### M3 2D 世界表现地基

#### Task 3.1 行动相机正交化
- **描述**: 把行动态从 `fieldOfView` 透视改为 top-down `orthographicSize`，与预览态统一。
- **验收**: 全程正交；缩放/跟随平滑；不同分辨率视野一致。
- **验证**: 双开烟测无透视畸变；尸体/任务点在视野内位置正确。
- **依赖**: Task 2.3。
- **涉及文件**: `Online/Camera/OnlineCameraRig.cs`、`OnlineMatchController.cs`。
- **规模**: M。
- **边界情况**: 超宽屏裁切；会议/鬼魂态相机；地图边界 clamp。

#### Task 3.2 渲染后端 2D 化（可切换）
- **描述**: 世界生成从 `PrimitiveType.Cube/Sphere/Cylinder`(+z 深度) 改为 `SpriteRenderer`/Tilemap，z 恒 0，靠 sortingLayer/Order 排序。提供「2D 渲染后端」开关以便回退对比。
- **验收**: 2D 后端下世界可读、排序正确、遮挡符合 top-down；逻辑/碰撞零改动。
- **验证**: 双开烟测；与 3D 后端切换对比；性能不退化。
- **依赖**: Task 2.1（世界生成已抽出）、Task 3.1。
- **涉及文件**: `Online/World/OnlineWorldBuilder.cs`、新增 `Online/World/Sprite2DBackend`、`OnlineMapService.cs`。
- **规模**: L。
- **边界情况**: sorting 抖动；动态道具与玩家穿插排序；灰盒占位 sprite 与真实美术切换；批合并/drawcall。

#### Task 3.3 角色 2D 表现决策与实现
- **描述**: 决策角色 2D 表现（建议 8 向或 4 向 sprite，或简化为顶视圆形 + 朝向）。把 `SocialCharacter`/`CharacterCustomizer`(637) 的 3D 模型路径降级，接 2D 表现。
- **验收**: 角色 top-down 可辨、有朝向、可上色区分阵营/自定义；移动动画基本。
- **验证**: 双开看多角色辨识；自定义颜色生效。
- **依赖**: Task 3.2。
- **涉及文件**: `SocialCharacter`、`CharacterCustomizer.cs`、`OnlineMatchController.CharacterAdapters.cs`(157)。
- **规模**: L。
- **边界情况**: 鬼魂半透明；死亡尸体朝向；自定义外观在 2D 下的映射；网络同步朝向。

#### Task 3.4 尸体与任务点 2D 表现
- **描述**: `KillSystem.cs`(507) 尸体改 2D sprite + 地面贴花；任务点/破坏点用 2D 图标 + 可交互高亮。
- **验收**: 尸体可被发现/上报；任务点图标清晰、范围提示正确。
- **验证**: 双开击杀→发现→上报流程；任务点高亮与触发范围一致。
- **依赖**: Task 3.2、Task 3.3。
- **涉及文件**: `KillSystem.cs`、`OnlineWorldBuilder.cs`、`OnlineMatchHud.cs`（图标）。
- **规模**: M。
- **边界情况**: 多尸体重叠；尸体在暗线口；任务点被破坏态下的视觉。

### M4 标准局闭环

#### Task 4.1 角色分配与人数适配
- **描述**: `OnlineRuleSet`(108) 按 5/8/10 人配置三阵营比例与职业；保证开局分配确定且可测。
- **验收**: 5/8/10 人各有合理阵营比；分配可复现（种子）；卧底/线人数量随人数缩放。
- **验证**: EditMode 测多组人数分配；双开实测开局角色面板正确。
- **依赖**: M3 完成、Task 1.5。
- **涉及文件**: `OnlineRuleSet.cs`、`OnlineMatchController.cs`（开局分配）。
- **规模**: M。
- **边界情况**: 中途掉线人数变化；Bot 占位的阵营计入；最小人数不足时禁止开局。

#### Task 4.2 节奏与时长收口
- **描述**: 默认单局 10-15 分钟。配置任务总量、破坏冷却、会议冷却、击杀冷却，使节奏接近 Among Us 且保留警匪差异（证据链）。
- **验收**: 默认配置下 8 人局落在 10-15 分钟；各冷却可在 `OnlineRuleSet` 调。
- **验证**: 多场实测时长分布；极端打法（速推/拖延）边界内。
- **依赖**: Task 4.1。
- **涉及文件**: `OnlineRuleSet.cs`、`OnlineTaskService.cs`、`OnlineMatchController.cs`。
- **规模**: M。
- **边界情况**: 全员挂机的超时收尾；任务提前完成的提前结算。

#### Task 4.3 鬼魂与紧急按钮闭环
- **描述**: `GhostMode.cs`(227) 已联机；接 `EmergencyButton.cs`(19) 报案/会议触发，死亡进鬼魂可继续做任务但不可交互活人。
- **验收**: 紧急按钮触发会议有冷却；鬼魂可做任务推进进度但不参与投票发言（按设计）；尸体上报触发会议。
- **验证**: 双开报案→会议→投票→结算闭环。
- **依赖**: Task 4.1、Task 3.4。
- **涉及文件**: `EmergencyButton.cs`、`GhostMode.cs`、`OnlineMatchController.cs`、`MeetingSync.cs`(104)。
- **规模**: M。
- **边界情况**: 同时多人按按钮；会议中击杀禁用；鬼魂任务是否计入胜利条件需明确。

#### Task 4.4 胜负矩阵成品与测试
- **描述**: `OnlineVictoryBridge`(300) 覆盖全部胜负路径：警方证据链闭合/票光黑帮；黑帮人数压制/拖垮证据；卧底特殊条件。
- **验收**: 每条胜负路径可触发且只触发一次；结算画面正确归因。
- **验证**: EditMode 胜负判定表驱动测试；双开复现关键路径。
- **依赖**: Task 4.1-4.3。
- **涉及文件**: `OnlineVictoryBridge.cs`、`UI/GameOverController.cs`(760)、`OnlineMatchController.cs`。
- **规模**: M。
- **边界情况**: 同帧双方达成条件的优先级；最后一人掉线；平票处理。

### M5 联机小游戏与信息系统

#### Task 5.1 小游戏联机协议定义
- **描述**: 13 个离线小游戏继承 `MiniGameBase`(OnComplete/OnCancel 回调)。定义统一联机协议：开始(ServerRpc 校验可做)→本地交互→完成(ServerRpc 上报)→服务器确认计入进度(ClientRpc)。防作弊：完成由服务器最终裁定。
- **验收**: 一份协议文档 + `Online/MiniGames/OnlineMiniGameBridge` 接口；至少 1 个小游戏走通端到端。
- **验证**: 双开下完成小游戏，进度服务器侧 +1；伪造完成被服务器拒绝。
- **依赖**: M4 完成、Task 1.2（任务服务单源）。
- **涉及文件**: 新增 `Online/MiniGames/OnlineMiniGameBridge.cs`、`MiniGames/MiniGameBase.cs`、`OnlineTaskService.cs`、`TaskSync.cs`(167)。
- **规模**: L。
- **边界情况**: 中途掉线/取消；同一任务多人误触；服务器超时未确认；防止客户端直接改进度。

#### Task 5.2 批量接入 ≥6 个小游戏
- **描述**: 按协议接入 WireTask/KeypadTask/SwipeCardTask/ScanTask/DownloadTask/MemoryTask 等至少 6 个到联机。
- **验收**: ≥6 个小游戏联机可完成并计入进度；UI 外壳统一。
- **验证**: 双开逐个跑通；进度同步正确。
- **依赖**: Task 5.1。
- **涉及文件**: `MiniGames/*`、`OnlineMiniGameBridge.cs`、`OnlineMatchHud.cs`（小游戏外壳）。
- **规模**: L。
- **边界情况**: 各小游戏输入差异；移动端无关（PC 键鼠）；取消返回行动态相机/控制权。

#### Task 5.3 破坏须修复机制
- **描述**: `SabotagePanel.cs`(481) + 破坏 timer。破坏触发后必须由市民阵营在限时内完成修复小游戏，否则触发败北条件（如断电/反应堆式）。
- **验收**: 破坏有倒计时与修复点；修复完成清除效果；超时触发对应后果。
- **验证**: 双开触发→修复成功/失败两条路径。
- **依赖**: Task 5.1、Task 1.2。
- **涉及文件**: `SabotagePanel.cs`、`OnlineTaskService.cs`、`SabotageSync.cs`、`OnlineMiniGameBridge.cs`。
- **规模**: M。
- **边界情况**: 多破坏并发；修复中再次被破坏；Host 迁移时倒计时续算。

#### Task 5.4 联机监控系统
- **描述**: `SecurityCamera.cs`(383) 仅离线。做联机版：监控站可查看若干摄像头画面（2D 顶视区域裁切或玩家位置缩略），黑帮经过亮红灯提示。
- **验收**: 监控站可联机查看；画面反映真实玩家位置（服务器权威）；不泄露超出摄像头范围的信息。
- **验证**: 双开一人看监控、一人走动，位置一致且仅限可视区。
- **依赖**: Task 3.2（2D 渲染）。
- **涉及文件**: 新增 `Online/Surveillance/OnlineSecurityCamera.cs`、参考 `SecurityCamera.cs`、`OnlineMatchHud.cs`。
- **规模**: L。
- **边界情况**: 监控被破坏（断电）时黑屏；带宽（用位置而非视频流）；防止客户端拿到全图玩家位置。

#### Task 5.5 会议证据板与嫌疑系统
- **描述**: 会议展示 3 类线索（任务完成轨迹/监控目击/证据链片段），呈现可推理信息而非答案；嫌疑值随行为变化。
- **验收**: 会议 UI 呈现 3 类线索；嫌疑值有来源可追溯；线索不直接点名凶手。
- **验证**: 双开会议中线索正确反映对局事件；嫌疑值随击杀/破坏/任务变化。
- **依赖**: Task 4.4、Task 5.4。
- **涉及文件**: `OnlineMatchHud.cs`（证据板）、`OnlineTaskService.cs`（证据事件）、`MeetingSync.cs`、`World/DistrictMapView.cs`。
- **规模**: L。
- **边界情况**: 信息过载的取舍；鬼魂可见信息范围；防止线索泄露未公开身份。

### M6 2D 港区玩法地图

#### Task 6.1 灰盒地图（玩法优先）
- **描述**: 按玩法需求（任务点分布、暗线口、监控覆盖、会议点、视线遮挡）做可跑的 2D 灰盒地图，坐标全走 `OnlineMapService`。
- **验收**: 灰盒可完整跑一局；任务点/暗线/监控/会议点齐全；路线有取舍。
- **验证**: 多人灰盒局；路线热力与卡点观察。
- **依赖**: M5 完成。
- **涉及文件**: `OnlineMapService.cs`、`OnlineWorldBuilder.cs`、新增灰盒 Tilemap 资产。
- **规模**: L。
- **边界情况**: 死角/卡墙；暗线两端可达性；任务点不可达。

#### Task 6.2 多人测试与布局迭代
- **描述**: 6-10 人灰盒测试，收集路线/卡点/平衡数据，迭代布局。
- **验收**: 多人测试达标（无致命卡点、节奏在 10-15 分钟、胜率不极端）。
- **验证**: ≥3 场多人测试记录；问题清单闭环。
- **依赖**: Task 6.1。
- **涉及文件**: 同 6.1 + `OnlineRuleSet.cs`（配合调参）。
- **规模**: M。
- **边界情况**: 人数不足的填充；测试中掉线；地图过大导致空旷。

#### Task 6.3 Tile/Sprite 美术替换
- **描述**: 用 2D tile/sprite 替换灰盒，路线/碰撞不变（逻辑层不动）。
- **验收**: ≥80% 灰盒被美术替换且路线不变；可读性优于灰盒。
- **验证**: 替换前后路线/碰撞对比一致；多人观感测试。
- **依赖**: Task 6.2、Task 3.2。
- **涉及文件**: Tilemap/Sprite 资产、`OnlineWorldBuilder.cs`、`OnlineMapService.cs`。
- **规模**: L。
- **边界情况**: 美术与碰撞错位；sorting 与玩家穿插；性能（合图/drawcall）。

### M7 服务、房间与 UI 成品

#### Task 7.1 Relay 房间码完整链路
- **描述**: 底层已具备（`OnlineMatchController.cs:1371-1444`）。补完「创建房间→显示房间码→输码加入→大厅→开局」全 Canvas 链路。
- **验收**: 双机经 Relay 完整跑一局；房间码可复制/输入；错误码有友好提示。
- **验证**: 真双机（非 loopback）跑通；错误（满员/失效码）有提示。
- **依赖**: M6 完成。
- **涉及文件**: `OnlineMatchController.cs`(`:1371-1444`)、`UnityServiceBootstrap.cs`、`UI/LobbyController.cs`(321)、`UI/MainMenuController.cs`(537)。
- **规模**: L。
- **边界情况**: Relay 服务不可用降级；房间码大小写/格式；满员/重复加入。

#### Task 7.2 断线与 Host 迁移策略
- **描述**: `HostMigrationManager.cs`(452) 未联调。联机验证心跳/选举/快照恢复；若不稳则降级为「Host 掉线即结算 + 友好提示」。
- **验收**: Host 掉线后要么成功迁移续局，要么干净结算不卡死；客户端掉线可重连或干净退出。
- **验证**: 双/三机故意杀 Host 测试；客户端断线测试。
- **依赖**: Task 7.1、Task 2.4（快照健壮）。
- **涉及文件**: `HostMigrationManager.cs`、`OnlineMatchController.cs`、`OnlineSyncManager.cs`。
- **规模**: L。
- **边界情况**: 迁移瞬间的 RPC 丢失；新 Host 选举并列；迁移中会议/小游戏态。

#### Task 7.3 全 Canvas 化（移除 OnGUI）
- **描述**: `OnlineMatchHud.cs`(2060) 与 `HostMigrationManager` 含 OnGUI。全部迁移到 Canvas/UGUI，统一设计系统。
- **验收**: 运行时无 OnGUI；HUD/提示/会议/小游戏外壳均 Canvas；适配多分辨率。
- **验证**: 关掉 OnGUI 仍完整可玩；不同分辨率布局正确。
- **依赖**: Task 7.1。
- **涉及文件**: `OnlineMatchHud.cs`、`HostMigrationManager.cs`、`UI/ThemeManager.cs`(169)、`UI/TransitionEffect.cs`(326)。
- **规模**: L。
- **边界情况**: OnGUI 承担的调试信息需保留为可开关调试面板；UI 缩放策略。

#### Task 7.4 2D 地图 UI（小地图/大地图）同源
- **描述**: 小地图与大地图坐标全走 `OnlineMapService`，与世界一致；标记任务点/玩家/尸体/暗线/破坏点。
- **验收**: 地图 UI 与世界坐标一致；标记按身份过滤（如黑帮看暗线）。
- **验证**: 双开对照世界与地图位置；身份过滤正确。
- **依赖**: Task 7.3、Task 6.3。
- **涉及文件**: `OnlineMapService.cs`、`World/DistrictMapView.cs`(124)、`OnlineMatchHud.cs`。
- **规模**: M。
- **边界情况**: 缩放/拖动；标记重叠聚合；信息泄露（不给市民看黑帮暗线）。

#### Task 7.5 首局可用性（新手不看文档）
- **描述**: 整合菜单/大厅/HUD/提示，使新玩家不看文档能完成第一局。
- **验收**: 无引导文档下，新玩家能创建/加入房间、理解目标、完成任务、参与会议。
- **验证**: 找未接触者实测第一局可完成。
- **依赖**: Task 7.1-7.4。
- **涉及文件**: `MainMenuController.cs`、`LobbyController.cs`、`OnlineMatchHud.cs`、`Tutorial/*`。
- **规模**: M。
- **边界情况**: 文案歧义；首次进入的空状态提示。

### M8 内容与平衡

#### Task 8.1 警署第二张 2D 地图
- **描述**: 基于 `PoliceStationInteriorBuilder.cs`(1001) 与 `World/PoliceStationMap`(144) 做第二张 2D 警署图（不同节奏/布局）。
- **验收**: 警署图可联机完整跑；与港区图节奏区分。
- **验证**: 多人跑通；时长/胜率落在目标区间。
- **依赖**: M7 完成。
- **涉及文件**: `PoliceStationInteriorBuilder.cs`、`PoliceStationMap.cs`、`OnlineMapService.cs`、`OnlineWorldBuilder.cs`。
- **规模**: L。
- **边界情况**: 两图共用逻辑的差异化参数；地图选择 UI。

#### Task 8.2 职业收敛与平衡
- **描述**: 收敛职业列表到可平衡的集合（Inspector/Forensics/Tech/Enforcer/Fixer/Undercover/Mole/Driver 等），定义各职业能力与冷却。
- **验收**: 职业能力明确、互不重叠、可平衡；UI 展示职业说明。
- **验证**: 多场实测各职业体验；无明显 OP/废柴。
- **依赖**: Task 8.1。
- **涉及文件**: `OnlineRuleSet.cs`、`OnlineMatchController.cs`、`OnlineMatchHud.cs`。
- **规模**: M。
- **边界情况**: 职业与人数缩放；能力与破坏/监控交互。

#### Task 8.3 Bot 升级
- **描述**: 把 `OnlineBotController`（M2 抽出）升级为可填充测试的有用 Bot：会做任务、会移动、基础会议行为，参考 `OpponentAi`(534)。
- **验收**: Bot 能填满到测试人数并推进对局；不卡死、不暴露作弊视野。
- **验证**: Bot 填充局可跑到结算；Bot 行为合理。
- **依赖**: Task 8.2。
- **涉及文件**: `Online/Bots/OnlineBotController.cs`、`OpponentAi.cs`（参考）。
- **规模**: L。
- **边界情况**: Bot 权威只在 Host；Bot 投票/被票；Bot 做小游戏的简化裁定。

#### Task 8.4 平衡数据采集与调参
- **描述**: 采集多场对局数据（时长/胜率/各阵营胜率/任务完成率），调 `OnlineRuleSet` 至 8 人局 10-15 分钟、胜率 45-55%。
- **验收**: 达成时长与胜率目标；调参有数据支撑。
- **验证**: ≥20 场数据；胜率/时长分布达标。
- **依赖**: Task 8.1-8.3。
- **涉及文件**: `OnlineRuleSet.cs`、新增轻量对局日志。
- **规模**: M。
- **边界情况**: 小样本偏差；Bot 局与真人局区分统计。

### M9 收口系统

#### Task 9.1 新手引导（教程）
- **描述**: `Tutorial/*`(1300) 接入新手引导：移动/任务/会议/破坏/暗线/监控的最小教学。
- **验收**: 新手引导可完成；覆盖核心交互。
- **验证**: 未接触者跟引导能学会基本操作。
- **依赖**: M8 完成。
- **涉及文件**: `Tutorial/*`、`OnlineMatchHud.cs`、`PrototypeBootstrap.cs`。
- **规模**: M。
- **边界情况**: 引导与真实联机的隔离（建议离线沙盒）；可跳过/重看。

#### Task 9.2 设置持久化
- **描述**: `UI/Settings*`(SettingsManager 348/Data 293/Helper 473) 收口：音量/画质/按键/语言/可访问性持久化。
- **验收**: 设置改动持久化且即时生效；重启保留。
- **验证**: 改设置→重启→保留；各项生效。
- **依赖**: M8。
- **涉及文件**: `UI/SettingsManager.cs`、`SettingsData.cs`、`SettingsHelper.cs`。
- **规模**: M。
- **边界情况**: 非法配置回退默认；分辨率切换失败恢复。

#### Task 9.3 本地化补全
- **描述**: `Localization.cs`(141) 中英双语补全所有新增 UI/提示文案。
- **验收**: 中英完整无漏 key；运行时切换语言生效。
- **验证**: 切语言全 UI 检查无 key 缺失。
- **依赖**: Task 7.3（UI 定型）。
- **涉及文件**: `Localization.cs`、全 UI 文案引用点。
- **规模**: M。
- **边界情况**: 长文案排版溢出；占位符顺序。

#### Task 9.4 音频事件反馈
- **描述**: `Audio/AudioManager.cs`(347) 补关键事件音效（任务完成/破坏/会议/击杀/胜负/UI）。
- **验收**: 关键事件有音效；音量受设置控制。
- **验证**: 各事件触发音效正确；静音设置生效。
- **依赖**: Task 9.2。
- **涉及文件**: `AudioManager.cs`、各事件触发点。
- **规模**: S-M。
- **边界情况**: 音效堆叠/打断；远近衰减（若做）。

#### Task 9.5 可访问性（色盲/黑灯）
- **描述**: 阵营/状态不仅靠颜色区分（加图标/形状）；断电（黑灯）态保留必要可读性。
- **验收**: 色盲模式可玩；黑灯态关键信息仍可辨。
- **验证**: 色盲模拟器检查；黑灯态走查。
- **依赖**: Task 9.2。
- **涉及文件**: `OnlineMatchHud.cs`、`ThemeManager.cs`、`SabotagePanel.cs`。
- **规模**: M。
- **边界情况**: 黑灯不应泄露过多信息（与黑帮优势平衡）。

#### Task 9.6 角色外观系统收口
- **描述**: `CharacterCustomizer`(637)/`Wardrobe`/`Bean*` 在 2D 下收口：可选外观/颜色，网络同步，会议/世界一致。
- **验收**: 自定义外观联机一致；不影响辨识阵营所需的中立呈现。
- **验证**: 双开自定义外观两端一致。
- **依赖**: Task 3.3。
- **涉及文件**: `CharacterCustomizer.cs`、`Wardrobe`、`Bean*`、`OnlineMatchController.CharacterAdapters.cs`。
- **规模**: M。
- **边界情况**: 外观同步带宽；重名/同色辨识。

### M10 封测发行准备

#### Task 10.1 构建与分发
- **描述**: 出 macOS/Windows 可分发构建；签名/打包/版本号。
- **验收**: 外部包可在干净机器运行联机。
- **验证**: 干净机器安装运行联机一局。
- **依赖**: M9 完成。
- **涉及文件**: 构建配置、`PrototypeBootstrap.cs`、CI。
- **规模**: M。
- **边界情况**: 平台权限（网络/麦克风）；首次运行依赖。

#### Task 10.2 日志与崩溃上报
- **描述**: 加轻量运行日志与崩溃/异常捕获，便于封测定位。
- **验收**: 异常有日志落地；可收集封测反馈。
- **验证**: 故意触发异常有日志；封测可回收。
- **依赖**: Task 10.1。
- **涉及文件**: 新增日志模块、`OnlineMatchController.cs` 关键钩子。
- **规模**: S-M。
- **边界情况**: 隐私（不记敏感信息）；日志体积。

#### Task 10.3 测试自动化与发布门槛
- **描述**: 把 M1 起累积的 EditMode/PlayMode 测试纳入 CI，定义发布门槛：72 小时封测无 P0/P1。
- **验收**: CI 全绿才可发布；72 小时无 P0/P1。
- **验证**: CI 流水线；封测 bug 看板。
- **依赖**: Task 10.1-10.2。
- **涉及文件**: CI 配置、`Editor/Tests/*`、`PrototypeSmokeTests.cs`。
- **规模**: M。
- **边界情况**: 偶发性 flaky 测试隔离；封测样本不足。

---

## 5. 2D 世界表现专项（相机 / 渲染 / 资产 / 性能）

> 本节聚焦「逻辑不动、只改表现」的 2D 化技术细节，是 M3 的展开与 M6 美术替换的技术约束来源。

### 5.1 相机
- 全程 **orthographic top-down**。预览态已正交（`:7029-7060`），行动态从 `fieldOfView` 改 `orthographicSize`（Task 3.1）。
- 跟随：平滑 lerp 到本地玩家；边界 clamp 到地图 bounds（走 `OnlineMapService` 提供的地图尺寸）。
- 多分辨率：固定可视世界高度（`orthographicSize`），宽度随宽高比扩展；超宽屏加遮幅或扩展视野，二选一并记录。
- 特殊相机：会议态切独立构图；鬼魂/观战态可自由跟随存活玩家或自由移动；监控站为独立渲染区域（见 5.5）。

### 5.2 渲染后端
- 玩家/道具/世界一律 `SpriteRenderer` 或 Tilemap，**z 恒 0**，排序靠 `sortingLayerName` + `sortingOrder`。
- Sorting Layers 建议：`Floor < Decal(尸体贴花/任务地标) < Props(可遮挡道具) < Actors(玩家/尸体) < Overhead(屋顶/遮罩) < FX < UIWorld`。
- Actors 层内按 y 坐标动态 order（y 越小越靠前），实现 top-down 伪深度。
- 提供「2D 渲染后端」开关（Task 3.2），保留 3D 后端用于对照回退，稳定后移除 3D 路径。
- 摒弃 `PrimitiveType.Cube/Sphere/Cylinder` 与 `CreatePropChild` 的 z 深度（`:7301+/7406+`）。

### 5.3 资产策略
- 灰盒期用纯色/占位 sprite + 简单 tile，保证可读与可迭代。
- 正式美术：港区主题（码头/集装箱/后巷/警署），tile 合图减少 drawcall；玩家 sprite 4/8 向或顶视圆形+朝向（Task 3.3 决策）。
- 现有 3D 资源（Synty/Kenney CityKit/Quaternius/DenysAlmaral）降级为概念参考；**必要时**预渲染为 top-down sprite，不作为正式生产路线。
- 资产命名/目录规范：`Art/2D/{Map,Actors,Props,UI}`；与 `OnlineMapService` 点位 key 对应。

### 5.4 性能预算（10 人局）
- 目标：中端机 60fps。drawcall 控制（合图/SpriteAtlas）；对象池复用道具/尸体/特效。
- 网络：监控用「玩家位置 + 区域裁切」而非视频流（见 5.5）；同步频率分层（玩家位置高频、任务/破坏状态事件驱动）。
- 内存：tile/atlas 按地图加载卸载；切图时不泄漏旧地图对象。

### 5.5 监控的 2D 实现约束
- 服务器权威：客户端不得拿到全图玩家位置；监控站只接收「该摄像头可视区域内」的位置（服务器裁切后下发）。
- 表现：在监控 UI 渲染对应区域的 2D 缩略（可用第二相机渲染到 RenderTexture，或纯 2D 标记图）。
- 断电（破坏）时监控黑屏；黑帮经过亮红灯（服务器判定身份，客户端只收到「红灯事件」不收身份）。

---

## 6. 2D 地图专项（玩法 → 灰盒 → 布局 → 测试 → tile/sprite）

> 对应 M6，强调「玩法优先、坐标同源、逻辑不动」。

### 6.1 设计输入（玩法需求）
- 任务点分布：覆盖全图、迫使移动、制造路线交叉（增加目击机会）。
- 暗线口（黑帮）：成对，跨区，单独 sortingLayer，仅黑帮可见入口提示。
- 监控覆盖：留盲区给黑帮、留可观察走廊给市民。
- 会议点/紧急按钮：中心或多点；尸体上报触发会议。
- 视线遮挡：top-down 用「屋顶遮罩层」或区域迷雾制造信息差（可选）。

### 6.2 灰盒（Task 6.1）
- 纯 Tilemap + 占位 sprite，坐标全走 `OnlineMapService.ScaleMapPosition/ScaleMapSize/UnderworldPassagePosition`。
- 碰撞 `Collider2D` 摆放与逻辑层一致；可跑完整局。

### 6.3 布局迭代（Task 6.2）
- 6-10 人多人测试，记录：路线热力、卡点、平均跑图时间、各阵营胜率、任务完成率。
- 迭代标准：无致命卡点、节奏 10-15 分钟、胜率不极端、暗线不过强/过弱。

### 6.4 美术替换（Task 6.3）
- ≥80% 灰盒被 tile/sprite 替换且**路线/碰撞不变**（逻辑层零改动是硬约束）。
- 替换后回归：路线/碰撞对比、sorting 与玩家穿插、性能。

### 6.5 第二张图（警署，M8）
- 基于 `PoliceStationInteriorBuilder`(1001)/`PoliceStationMap`(144) 做 2D 警署图，节奏与港区区分；共享逻辑、差异化参数与布局。

---

## 7. 2D UI 专项（信息架构 / 地图 UI / 提示 / 小游戏外壳 / 会议证据板）

> 对应 M5/M6/M7/M9 的 UI 面，目标全 Canvas、信息清晰、新手可用。

### 7.1 信息架构
- HUD 三区：左下任务/进度，右下能力/交互，顶部会议/破坏倒计时与状态。
- 身份信息只对本人可见；阵营颜色辅以图标/形状（可访问性，Task 9.5）。

### 7.2 地图 UI（小地图 / 大地图，Task 7.4）
- 坐标与世界同源（`OnlineMapService`）。标记：任务点/玩家(本人)/尸体(已发现)/破坏点/暗线(仅黑帮)。
- 大地图可缩放/拖动；标记重叠聚合；身份过滤防信息泄露。

### 7.3 提示与反馈
- 交互范围高亮、可交互图标、冷却环、破坏告警 toast、胜负归因结算（`GameOverController` 760）。
- 全 Canvas（移除 OnGUI，Task 7.3），统一 `ThemeManager`(169)/`TransitionEffect`(326)/`LoadingScreen`(368)。

### 7.4 小游戏外壳（Task 5.2）
- 统一弹窗外壳：标题/任务说明/取消/完成态；进入时锁玩家移动、退出还控制权与相机。
- 13 个小游戏复用同一外壳，差异只在内容区。

### 7.5 会议证据板（Task 5.5）
- 三类线索分区呈现（任务轨迹/监控目击/证据链片段）；嫌疑值可视化但不点名凶手。
- 投票区、计时、发言/文本聊天（若方案 B）；鬼魂可见范围受限。

---

## 8. 联机架构专项（控制器瘦身 / 同步 / 权威 / 反作弊）

> 对应 M2/M5/M7/M8，是稳定性与公平性的根基。

### 8.1 控制器瘦身路线（增量、可回退）
- 目标：把 `OnlineMatchController`(12673) 从「世界生成/相机/Bot/会议/快照/OnGUI」减负，主控只保留对局编排。
- 顺序：世界生成(Task 2.1) → Bot(Task 2.2) → 相机(Task 2.3) → OnGUI→Canvas(Task 7.3)。每步先平移不改行为，烟测不退化再继续。
- 红线：编译未过/烟测退化不进下一步；M1 timer 重复未消前不在控制器加新破坏类型。

### 8.2 同步分层
- 高频：玩家位置/朝向（`PlayerStateSync` 142）。
- 事件驱动：任务完成/破坏/修复/会议/投票（`TaskSync` 167/`MeetingSync` 104/`SabotageSync` 351）。
- 状态快照：加入/恢复/迁移（`GameStateSnapshot` 471/`OnlineSyncManager` 236）。

### 8.3 权威模型
- Host 权威：角色分配、任务完成裁定、击杀合法性、破坏与修复、投票计票、胜负判定、Bot 决策。
- 客户端只发意图（ServerRpc），表现由 ClientRpc/NetworkVariable 下发。
- 信息最小化下发：监控/暗线/身份按可见性裁切，杜绝客户端拿到全量隐私信息。

### 8.4 反作弊收口
- 小游戏完成服务器最终裁定（Task 5.1），拒绝伪造完成。
- 位置/移动服务器校验（速度/瞬移上限）。
- 监控/地图只下发该客户端有权看到的数据（Task 5.4/7.4）。
- 快照/迁移不携带不该暴露的身份信息给非应得客户端。

### 8.5 timer 单一来源（关键债务 #2，Task 1.2）
- 所有破坏 timer 归 `OnlineTaskService`(`:80-84`)；控制器只读公开属性；`SabotageSync` 读公开属性（`:144-148`，已去反射）。
- 快照/迁移时 timer 续算由服务负责。

### 8.6 Relay / 房间 / 迁移
- Relay 业务已具备(`:1371-1444`)，M7 补完 UI 链路(Task 7.1)。
- Host 迁移(`HostMigrationManager` 452)先联调，不稳则降级为干净结算(Task 7.2)。

---

## 9. 非核心但必须收口的系统

> 对应 M9，缺这些无法封测发行。

### 9.1 教程（`Tutorial/*` 1300）
- 离线沙盒教学：移动/任务/会议/破坏/暗线/监控；可跳过/重看（Task 9.1）。

### 9.2 设置（`SettingsManager` 348/`SettingsData` 293/`SettingsHelper` 473）
- 音量/画质/按键/语言/可访问性持久化即时生效，重启保留（Task 9.2）。

### 9.3 本地化（`Localization.cs` 141）
- 中英全覆盖，运行时切换无缺 key（Task 9.3）。

### 9.4 聊天（`ChatSystem.cs` 281/`ChatMessage.cs` 26）
- 方案 B：改 NetworkBehaviour 联机文本聊天（Task 1.4），三通道 + 鬼魂频道，纯文本防注入。

### 9.5 音频（`AudioManager.cs` 347）
- 关键事件音效，受设置控制（Task 9.4）。

### 9.6 角色外观（`CharacterCustomizer` 637/`Wardrobe`/`Bean*`）
- 2D 下可选外观/颜色，网络同步，会议/世界一致（Task 9.6）；不破坏阵营辨识中立性。

### 9.7 语音（`VoiceChatSystem.cs` 1080）
- 方案 B 下标注「本地占位，不联机」或移除；README 不再承诺近距离语音（Task 1.3）。

---

## 10. 测试矩阵

| 层级 | 范围 | 工具 | 触发 | 对应里程碑 |
|---|---|---|---|---|
| EditMode 单测 | 角色分配/胜负/破坏 timer/坐标映射/快照往返 | Unity Test Runner | CI 每次提交 | M1 起持续 |
| PlayMode 集成 | 开局→任务→破坏→会议→结算单机流程 | Unity Test Runner | CI/手动 | M4 起 |
| 双开烟测 | Host+Client loopback 跑到结算 | `OnlineDemoPlayMenu` | 每个里程碑 | M0 起每步 |
| 真双机 Relay | 房间码→大厅→完整局→迁移/断线 | 两台机器 | M7 起 | M7/M10 |
| 多人测试 | 6-10 人灰盒/正式图、节奏/胜率/卡点 | 组织测试 | M6/M8 | M6/M8 |
| 性能 | 10 人局 60fps、drawcall、内存 | Profiler | M3/M6/M10 | M3/M6/M10 |
| 可访问性 | 色盲/黑灯走查 | 模拟器+人工 | M9 | M9 |
| 封测稳定性 | 72 小时无 P0/P1 | bug 看板 | M10 | M10 |

回归基线：每个里程碑结束跑「EditMode 全绿 + 双开烟测到结算」，作为进入下一里程碑的门槛。

---

## 11. Bug 优先级

| 级别 | 定义 | 例子 | 处理 |
|---|---|---|---|
| **P0** | 阻断/崩溃/无法开局/数据破坏 | 开局崩溃、Host 迁移卡死、快照恢复丢状态 | 立即停下修复，阻塞发布 |
| **P1** | 核心玩法不可用/严重不公平 | 小游戏无法完成、破坏无法修复、监控泄露全图、票数错算 | 当里程碑内必修，阻塞发布 |
| **P2** | 体验明显受损但可绕过 | UI 错位、提示缺失、偶发同步抖动 | 排期修复，不阻塞里程碑通过但需登记 |
| **P3** | 细节/打磨 | 文案、音效缺失、轻微视觉 | 收口期统一处理 |

发布门槛：72 小时封测无 P0/P1（Task 10.3）。

---

## 12. 目标模块结构

```text
Assets/_Project/Scripts/
  Online/
    OnlineMatchController.cs        # 瘦身后只做对局编排
    OnlineMatchController.*.cs      # partial（按域拆分）
    OnlineRuleSet.cs                # 规则/配置（已用）
    OnlineMapService.cs             # 坐标唯一来源（已用）
    OnlineTaskService.cs            # 任务/破坏/timer 单一来源（已用）
    GameStateSnapshot.cs / OnlineSyncManager.cs
    PlayerStateSync / TaskSync / MeetingSync / SabotageSync
    KillSystem.cs / SabotagePanel.cs / OnlineVictoryBridge.cs
    UnityServiceBootstrap.cs / HostMigrationManager.cs
    ChatSystem.cs                   # 方案B后为 NetworkBehaviour
    OnlineMatchHud.cs               # 全 Canvas（移除 OnGUI）
    World/   OnlineWorldBuilder.cs + Sprite2DBackend   # M2 抽出 + M3 2D
    Camera/  OnlineCameraRig.cs                        # M2 抽出 + M3 正交
    Bots/    OnlineBotController.cs                    # M2 抽出 + M8 升级
    MiniGames/ OnlineMiniGameBridge.cs                 # M5 新增（联机协议）
    Surveillance/ OnlineSecurityCamera.cs              # M5 新增（联机监控）
  SocialDeduction/
    SocialPrototypeController.cs    # 离线练习/沙盒（不投 2D 美术）
    MiniGames/*                     # 13 个小游戏（被联机复用）
    SecurityCamera.cs / VentSystem.cs / VoiceChatSystem.cs   # 参考/占位
  Core/ / World/ / Gameplay/ / Tutorial/ / UI/ / Audio/ / Environment/
  Art/2D/{Map,Actors,Props,UI}      # 2D 资产新增
  Editor/
    Tests/*                         # M1 起的 EditMode/PlayMode 测试
    OnlineDemoPlayMenu / PrototypeSmokeTests / ...
```

原则：联机为主干，离线为沙盒；表现层（World/Camera/Bots/Sprite2D）从主控中析出可独立替换；所有坐标走 `OnlineMapService`，所有破坏 timer 走 `OnlineTaskService`。

---

## 13. 风险登记册

| # | 风险 | 影响 | 概率 | 缓解 | 触发应对 |
|---|---|---|---|---|---|
| R1 | 控制器瘦身引入回归 | 高 | 中 | 增量平移+每步烟测+EditMode 网 | 退回上一步，二分定位 |
| R2 | timer 单源改造影响破坏/快照 | 高 | 中 | 先补测试再改（Task 1.5→1.2） | 回退字段，分步迁移 |
| R3 | 2D 渲染 sorting 抖动/穿插 | 中 | 中 | y 排序规则+开关回退 3D 对照 | 固定关键层 order，加调试可视化 |
| R4 | Host 迁移不稳 | 高 | 中 | 先联调，不稳即降级干净结算 | 启用降级路径（Task 7.2） |
| R5 | 监控/地图信息泄露作弊 | 高 | 中 | 服务器裁切下发，权威判定 | 收紧下发，加服务器校验 |
| R6 | 小游戏联机作弊（伪造完成） | 高 | 中 | 服务器最终裁定 | 拒绝并记录异常，封测上报 |
| R7 | 双循环（离线/联机）维护成本 | 中 | 高 | M0 决策离线为沙盒不投美术 | 冻结离线玩法对齐排期 |
| R8 | 第三方美术二进制污染仓库 | 中 | 高 | LFS/ignore + 干净基线 | 历史清理（谨慎，需备份） |
| R9 | 语音承诺与实现不符 | 中 | 高 | 方案二选一并改 README | 立即去除失效 UI 承诺 |
| R10 | 平衡不达标（胜率/时长） | 中 | 中 | 数据驱动调参（Task 8.4） | 加采样场次，针对性调 RuleSet |
| R11 | 性能不达 60fps | 中 | 中 | 合图/对象池/位置同步分层 | Profiler 定位热点，降特效 |
| R12 | 源码高速变动致计划过期 | 中 | 高 | 每任务执行前复核源码 | 就地更新计划与行号 |

---

## 14. 执行节奏

> 节奏按里程碑推进，不给绝对日期（源码变动快）。以「门槛达成」为推进信号。

### 第一阶段（地基）：M0 → M1 → M2
- 跑通基线、清仓库、定语音、补最小测试网；增量抽出世界生成/Bot/相机；快照健壮。
- 门槛：编译 0 error；双开到结算；timer 单源；EditMode 网建立；至少抽出 1 个服务。

### 第二阶段（2D 与闭环）：M3 → M4 → M5
- 相机正交 + 2D 渲染后端 + 角色/尸体/任务 2D；标准局闭环（分配/节奏/鬼魂/胜负）；联机小游戏协议 + ≥6 接入 + 破坏修复 + 监控 + 证据板。
- 门槛：全正交、2D 后端不退化；5/8/10 人局可跑、10-15 分钟；≥6 小游戏联机、破坏须修复、会议 3 类线索。

### 第三阶段（地图/服务/内容/收口/发行）：M6 → M7 → M8 → M9 → M10
- 2D 港区图灰盒→测试→美术替换；Relay 房间码 + 全 Canvas + 2D 地图 UI + 首局可用；警署第二图 + 职业收敛 + Bot 升级 + 平衡；教程/设置/本地化/音频/可访问性；构建/日志/CI/发布门槛。
- 门槛：80% 灰盒被替换路线不变；双机 Relay 完整局；两图 8 人 10-15 分钟胜率 45-55；新手不看文档完成第一局；72 小时无 P0/P1。

可并行：M2 抽服务 ∥ M3 相机正交；M5 小游戏 UI ∥ M7 Relay 环境配置（协议先定）；M9 教程/本地化 ∥ M8 平衡采集。
不可并行：M3 渲染未稳不做 M6 大规模美术；M5 协议未定不并接多个小游戏；编译未过不做功能；timer 重复未消不加新破坏类型。

---

## 15. 明确不做（本版范围外）

- 移动端 / 触屏适配。
- 专用服务器（dedicated server）；维持 Host/Client + Relay。
- 商城 / 账号体系 / 付费内容。
- 写实 3D 美术与 3D 正式生产路线（降级为参考）。
- 联机语音（除非 R9 后续重新立项；本版方案 B 用文本聊天替代）。
- 离线循环的 2D 美术与玩法对齐（保留为沙盒/练习场）。
- 大规模新职业/新机制扩张（先把核心收敛平衡，Task 8.2）。

---

## 16. 最终成功标准

1. **可发行候选**：macOS/Windows 外部包可分发，干净机器经 Relay 房间码完成完整联机局。
2. **稳定**：72 小时封测无 P0/P1；EditMode 全绿；双开/双机烟测不退化。
3. **节奏达标**：6-10 人可稳定开局，默认 8 人局 10-15 分钟，节奏接近 Among Us。
4. **平衡达标**：两张 2D 图均可联机，8 人局胜率 45-55%，无明显 OP/废柴职业。
5. **2D 表现到位**：全程正交 top-down，世界/角色/尸体/任务/地图 UI 坐标同源、可读性优于灰盒，≥80% 灰盒被美术替换且路线不变，10 人局 60fps。
6. **系统完整**：联机小游戏(≥6)/破坏须修复/联机监控/会议三类线索/鬼魂/胜负矩阵齐全；教程/设置/本地化(中英)/音频/可访问性收口。
7. **架构健康**：破坏 timer 单一来源；控制器减负（世界/相机/Bot/UI 析出）；服务器权威 + 信息最小化下发，关键反作弊到位。
8. **新手可用**：未接触者不看文档可完成第一局。

> 收尾提醒：本计划任何「现状/行号」均为 2026-06-04 源码快照，源码持续变动。执行每个任务前，必须以**当时源码**复核该任务涉及文件，发现偏差就地更新本计划，不得照搬旧结论。
