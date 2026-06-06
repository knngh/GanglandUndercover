# Gangland Undercover — 完整开发计划 v2.0

> **制定日期**: 2026-06-05  
> **审计范围**: ~135 C# 源文件 + 26 设计文档 + GameDesign.md  
> **计划类型**: 长期完整流程（Pre-production → Ship，~38 周）  
> **前置**: 代码实测审计报告（5 大关键问题已识别）  
> **权威性**: 本文为项目唯一权威路线图，取代此前所有分散的阶段计划文档

> **修订记录 v2.1 (2026-06-05)**: 对"当前基线/已知债务"进行实测复核（编译 + PlayMode + 源码核对）。多项原列为"未做"的债务实际已完成，原表会导致重复劳动并低估真正瓶颈。债务表已按实测重写，并据此调整 Phase 0/1/2 工作项。**结论：真正的关键路径是 Phase 1.1 控制器拆分（D-01），而非小游戏移植。**

---

## 产品定位

**对标**: Among Us 社交推理  
**差异化**: 警匪卧底题材 + 职业能力 + 证据链推理 + 双身份博弈  
**目标平台**: PC (macOS + Windows) 首发  
**目标人数**: 6-10 人联机  
**核心承诺**: "比 Among Us 可玩性更多"——每个差异化维度必须可玩、可测、可感知

---

## 设计支柱（不可妥协）

1. **欺骗与推理** — 每一局都是社交博弈，信息不是给定的、是被发现的
2. **职业即玩法** — 不同职业能做的事根本不同，不是数值换皮
3. **港味 noir** — 霓虹、雨夜、信任崩塌的美学基调
4. **高压时刻** — 紧急任务、破坏、会议指证制造让人心跳加速的节点
5. **可重复性** — 每局不同的角色、证据分布、玩家互动产生不同的故事

---

## 当前基线（2026-06-05）

### 已完成 ✅
- NGO 联机基础架构（Host/Client + CustomMessagingManager）
- 击杀/报告/会议/投票/淘汰完整循环
- 5 种破坏类型定义（Blackout/Lockdown/Communications/EvidenceLeak/PatrolAlert）
- 7 职业枚举 + 15 种 AbilityType 定义（OnlineRuleSet）
- 2 张程序化 greybox 地图（HarbourDistrict 12 房间 / PoliceStation 6 房间）
- 离线模式：13 种小游戏 + VentSystem(365行) + SecurityCamera(384行) + GhostMode(228行)
- F 阶段：教程/设置/本地化/聊天/可访问性 框架
- G 阶段：构建脚本/CI 管道
- 美术管线：CC0 素材采集/角色精灵导入

### 已知债务（v2.1 实测复核）

#### A. 仍待处理 — 经实测确认，真实工作 ❌

| 编号 | 问题 | 严重度 | 实测证据 |
|------|------|--------|---------|
| D-01 | OnlineMatchController God Object | 🔴 | **13,035 行**（实测），Phase 1.1 关键路径 |
| D-08 | ChatSystem 非 NetworkBehaviour，联机不可用 | 🔴 | `public class ChatSystem`（无继承），ChatSystem.cs:41 |
| D-12 | 离线/联机双循环重叠 | 🟡 | SocialPrototypeController **3898 行** + OnlineMatchController **13035 行**（实测） |
| D-03 | 世界构建仍用 PrimitiveType 3D 图元（与 2D 方向不一致） | 🟡 | OnlineWorldBuilder.cs 中 **23 处** `PrimitiveType.` |
| D-09 | Host Migration 未充分联调 | 🟢→🟡 | `HostMigrationManager.cs` 已存在；联调/回归覆盖仍不足，降级为"验证"而非"新建" |

#### B. 已解决 / 已基本完成 — 原表过时，不得重复立项 ✅

| 原编号 | 原描述 | 实测结论 | 残余工作 |
|--------|--------|---------|---------|
| ~~D-05~~ | 联机小游戏目录不存在 | **已基本完成（11 种联机可玩）**：实际走**客户端本地技能闸**——`BeginActiveTask` 经工厂 `OnlineMiniGameBridge.CreateDefaultMinigame(taskId)`(13 桶→11 种) 开本地小游戏，完成走 `CompleteActiveTask` 服务器提交。PlayMode 集成测试 `OnlineTasks_OpenRichMinigames_AndCompleteThroughServerPath` **Passed**（断言 ≥6 种、实测 11 种全开全完成）。**注意：`OnlineMiniGameBridge` 的 ServerRpc 服务器驱动路径是死代码**（其 GameObject=controller 无 NetworkObject，`IsSpawned` 永远 false），可后续删除或正式接 NetworkObject | 仅剩第二波打磨 + 边界用例 + 死代码清理 → 见 Phase 2.1（已降级） |
| ~~D-02~~ | 破坏 timer 在控制器和 TaskService 重复持有 | **已解决**：timer 单源于 `OnlineTaskService`，控制器仅 read-through 属性（`LockdownTimer => taskService.LockdownTimer` …）与 `ApplySabotageTimer → taskService.ApplySabotageEffect`（注释标注 "B3: 替代反射"）。另有 `SabotageSync.cs`(326行) 负责网络同步 | 无（Phase 1.1f "SabotageManager 新建"已被 OnlineTaskService+SabotageSync 覆盖，删除该子任务） |
| ~~D-04~~ | 行动相机仍透视，2D 应为正交 | **已解决**：控制器多处 `Camera.main.orthographic`，注释 "M3: camera is always orthographic now" | 无 |
| ~~D-06~~ | 监控摄像头仅离线可用 | **部分完成**：`Online/Surveillance/OnlineSecurityCamera.cs`(169行, NetworkBehaviour) 已存在。⚠️ **已知未修 bug**：以运行时 `AddComponent<NetworkObject>().Spawn()`(globalObjectIdHash=0) 创建，无法向远端 Client 复制（"NetworkPrefab could not be found"）。连通性不受影响（对局走 CustomMessagingManager 自定义消息/快照），但远端看不到该摄像头 | 改注册为 NetworkPrefab + 多路监控室 UI → 见 Phase 2.3（已重定义为修复+UI） |
| ~~D-07~~ | README 仍承诺 Vivox 语音 | **已解决**：README:33 已写明 "Proximity voice (Vivox) has been removed — text chat replaces it" | 无（Phase 1.5 删除该项） |
| ~~D-10~~ | 单元测试近零（仅 1 smoke 文件） | **不准确**：实有 **5 个**测试（CoreSystemTests / MatchLoopPlayTests / RelayTwoProcessPlayTests / MiniGameOnlineIntegrationPlayTests / PrototypeSmokeTests），PlayMode 集成测试在跑且通过 | 测试覆盖仍可扩展，但应作为**回归护栏**登记，而非"从零补测" |

#### C. 计划中拟"新建"但已存在的文件（避免重复创建）

| 计划位置 | 文件 | 实测 |
|---------|------|------|
| Phase 1.1b "OnlineCameraRig.cs（新建）" | `Online/Camera/OnlineCameraRig.cs` | **已存在（212行）** → 改为"迁移控制器内残余相机逻辑" |
| Phase 1.1d "KillSystem.cs（已存在，扩展）" | `Online/KillSystem.cs` | ✅ 存在，描述准确 |
| Phase 1.1e "MatchSnapshotService.cs（新建）" | — / `Online/GameStateSnapshot.cs` | 数据结构 `GameStateSnapshot.cs` 已存在，控制器已有 `BroadcastSnapshot`/快照计时器；为**抽取**而非"从零" |
| Phase 1.1c "OnlineBotController.cs（扩展）" | `Online/Bots/OnlineBotController.cs` | ✅ 存在（755行），描述准确 |

**真正需"从零新建"的，仅 `MatchSnapshotService`（抽取）一项；其余 Phase 1.1 文件均已存在，工作性质是"迁移/收口"。**

---

## Phase 0: 设计对齐（Design Alignment）

**时长**: 3-5 天 | **里程碑**: M0
**目标**: 让 GDD、代码、路线图三者描述的是同一个游戏

### 0.0 重新基线化（强制前置）
**动机**: v2.1 复核已证明记忆中的债务表会漂移（D-02/04/05/06/07/10 均已过时）。任何后续 Phase 都必须建立在**实测**而非记忆的基线上。

| 步骤 | 命令 / 动作 | 产出 |
|------|------------|------|
| 0.0a | batchmode 编译 | 确认 0 error，记录 warning 数 |
| 0.0b | 全套 EditMode + PlayMode 测试 | 当前 5 个测试的 pass/fail/skip 快照 |
| 0.0c | 源码债务核对 | 用测试结果重写"已知债务"表（本文 v2.1 已完成首轮，每里程碑复跑一次） |
| 0.0d | 登记回归护栏 | 现有 5 个测试纳入 CI 门禁，Phase 1 拆分期间必须保持绿 |

**验收**: 债务表每一项都有"实测证据"列；不存在"凭记忆列为未做"的条目。

### 0.1 重写 GameDesign.md
**现状**: 当前 GDD 描述回合制区域控制策略游戏；实际代码是实时社交推理游戏。

**新 GDD v2.0 必须定义**:
- **游戏类型**: 实时多人社交推理 (Real-time Multiplayer Social Deduction)
- **核心循环**: Moment-to-moment (走位/交互) → Session (对局 10-15min) → Meta (声誉/解锁)
- **阵营矩阵**: 警察 / 黑帮 / 卧底 / 内鬼 — 每阵营目标、胜利条件、可知信息
- **信息不对称表**: 谁知道什么、什么时候知道
- **验收**: 工程师读 GDD 能直接写出实现，不需要口头补充

### 0.2 设计支柱确认
从现有系统中提炼 5 条不可妥协的设计支柱（见上文）。

### 0.3 清理过时产物
- 归档旧 GameDesign.md（回合制版本）到 `output/archive/`
- 标记所有"文档承诺但代码未实现"的功能，登记为 Phase 2-3 任务

---

## Phase 1: 架构收口（Architecture Reclamation）

**时长**: 7-10 天 | **里程碑**: M1  
**目标**: OnlineMatchController 从 13,000 行拆到 <4,000 行；每个子系统可独立测试

### 1.1 拆分 OnlineMatchController（D-01 修复）

| 子任务 | 迁移目标 | 迁移内容 |
|--------|---------|---------|
| 1.1a | OnlineWorldBuilder.cs（已存在，1583行） | CreateVerticalSlice*() / CreateShapeProp() / RegisterWalkableArea() 系列 |
| 1.1b | OnlineCameraRig.cs（**已存在 212行**，迁入） | 把控制器内残余的 fieldOfView / orthographicSize / 相机跟随 / 缩放逻辑迁入已有 rig |
| 1.1c | OnlineBotController.cs（已存在 755行，扩展） | botThinkTimers / botVoteTimers / botTargets / Bot Update 循环 |
| 1.1d | KillSystem.cs（已存在，扩展） | 击杀冷却字典 / 击杀范围 / 报告冷却 — 控制器不再持有这些状态 |
| 1.1e | MatchSnapshotService.cs（抽取，复用 `GameStateSnapshot.cs`） | 从控制器抽取 `BroadcastSnapshot`/快照计时器 → CaptureSnapshot / RestoreFromSnapshot |
| ~~1.1f~~ | ~~SabotageManager.cs（新建）~~ | **删除**：破坏 timer 已单源于 `OnlineTaskService` + `SabotageSync.cs`（见债务表 B 区 D-02）。改为：把控制器内对 taskService 的零散调用收敛为清晰接口 |

**验收**: OnlineMatchController.cs < 4,000 行 · batchmode 0 error · PlayMode 双进程一局不崩

### 1.2 ~~消除状态重复（D-02 修复）~~ — 已完成，改为验证
**实测：破坏 timer 已单源于 `OnlineTaskService`**（控制器仅 read-through 属性）。本节降级为：补 1 个 PlayMode 测试，断言破坏施放/Tick/快照恢复后控制器与 taskService 读数一致，作为回归护栏。

### 1.3 ChatSystem 联网化（D-08 修复）
改为 NetworkBehaviour，ClientRpc 广播，UI 从 OnGUI 改为 uGUI InputField + ScrollView。

### 1.4 OnGUI 全量迁移到 uGUI Canvas
| Prefab | 原 OnGUI 文件 | 内容 |
|--------|-------------|------|
| MatchHUD | OnlineMatchHud.cs (2201行) | 任务列表、击杀冷却、破坏状态、玩家列表 |
| LobbyUI | LobbyController.cs (622行) | 房间列表、房间设置、准备按钮、玩家头像 |
| MeetingUI | 控制器内 OnGUI 段 | 讨论计时器、投票面板、玩家头像、聊天面板 |
| GameOverUI | GameOverController.cs (760行) | 胜负动画、阵营揭晓、统计数据 |

**验收**: 全流程无 OnGUI 调用（编辑器工具除外）

### 1.5 ~~README 对齐~~ — Vivox 已移除，仅需收尾
**实测：README:33 已声明 Vivox 移除、文本聊天替代。** 残余仅"已知限制"小节补充（host migration 联调状态、平台支持），10 分钟工作量。

---

## Phase 2: 核心玩法闭环（Core Loop Completion）

**时长**: 14-18 天 | **里程碑**: M2  
**目标**: 联机模式能打**完整的、内容丰富的**对局

### 2.1 联机小游戏：第一波已完成，补第二波（D-05 已基本修复）

**第一波（实测 11 种）— 已完成 ✅**：经 `BeginActiveTask` 客户端本地技能闸触发，完成走 `CompleteActiveTask` 服务器提交；集成测试 `OnlineTasks_OpenRichMinigames_AndCompleteThroughServerPath` **Passed**（断言 ≥6 种、实测 11 种全开全完成、28 任务站点）。**不重新移植**，仅作回归保护。<br>⚠️ 顺带清理：`OnlineMiniGameBridge` 的 ServerRpc 服务器驱动路径是死代码（无 NetworkObject，`IsSpawned` 恒 false），决定删除或正式接 NetworkObject。

**本节真实工作 = 第二波 + 边界用例**：

| 小游戏 | 原文件（离线，共 11 个 Task） | 联机同步策略 |
|--------|--------|------------|
| SortTask | SortTask.cs | Client 本地 → ServerRpc 完成 |
| TapTask | TapTask.cs | Client 本地计数 → ServerRpc 完成 |
| AsteroidTask | AsteroidTask.cs | Client 本地 → ServerRpc 完成 |
| CalibrateTask | CalibrateTask.cs | Client 本地 → ServerRpc 完成 |
| EvidenceArchiveTask | EvidenceArchiveTask.cs | Server 计时 → ClientRpc 进度（接证据链） |

**边界用例**：玩家中途断线/被杀 → 服务器 ReleaseTask；并发完成同一任务点；破坏期间任务禁用。

**验收**: 11 种离线小游戏全部经 bridge 联机可触发可完成；集成测试断言种类数 ≥10；断线/并发边界有测试覆盖。

### 2.2 暗线通道系统（Vent 等效）
基于离线 VentSystem.cs (365行)，Gang + Undercover 阵营专属快速移动。

| 组件 | 说明 |
|------|------|
| UnderworldPassage.cs (NetworkBehaviour) | 节点图导航、冷却、ServerRpc 穿越 |
| UnderworldNode.cs | 地图 4 节点配置，连接房间 |
| UnderworldVisual.cs | 靠近高亮，非 Gang 阵营不可见/不可交互 |

### 2.3 监控摄像头系统（D-06 部分完成，先修复制 bug + 补 UI）
**实测：`Online/Surveillance/OnlineSecurityCamera.cs`(169行, NetworkBehaviour) 已存在，但有未修复制 bug。** 本节工作（按优先级）：
1. **修复制 bug**：运行时 `AddComponent<NetworkObject>().Spawn()`(globalObjectIdHash=0) 无法向远端复制 → 改注册为 NetworkPrefab（远端 Client 当前看不到摄像头）。
2. RenderTexture 多路摄像头 + 监控室 UI 切换。
3. 与 Tech 职业 `RemoteSurveillance` 能力联动（Phase 3.2）。
不从离线版重写。

### 2.4 紧急任务系统

| 任务类型 | 触发条件 | 机制 | 失败后果 |
|---------|---------|------|---------|
| 证据销毁 | 证据链达 75% | 全图 2 处同时修复，60s 倒计时 | 证据分 -40% |
| 警方增援 | 黑帮 ≤ 警察 50% | 黑帮需破坏通讯塔，45s 倒计时 | 黑帮暴露位置 30s |

### 2.5 破坏系统深化

| 破坏 | 当前 | 补充 |
|------|------|------|
| Blackout | 仅屏幕变暗 | + 视野缩小 + 交互范围减半 |
| Lockdown | 仅状态标记 | + 门封锁视觉 + 修复小游戏 |
| Communications | 仅状态标记 | + 小地图禁用 + 修复小游戏 |
| EvidenceLeak | 仅状态标记 | + 证据分下降 + 修复小游戏 |
| PatrolAlert | 仅音效 | + NPC 巡逻重绘 + 黑帮掩护 |

---

## Phase 3: 差异化兑现（Differentiator Realization）

**时长**: 14-18 天 | **里程碑**: M3  
**目标**: "比 Among Us 可玩性更多"不只是口号

### 3.1 证据链系统（核心差异化 #1）

```
证据链 v2.0:
收集线索 → 关联推理 → 会议指证
(任务产出)   (CaseLog)   (投票权重)
```

| 组件 | 说明 |
|------|------|
| EvidenceType 枚举 | Footprint / Bloodstain / WeaponTrace / AlibiBreak / TransactionRecord / SurveillanceFootage |
| EvidenceNode | 类型、位置、时间、发现者、关联 ID |
| EvidenceChain.cs | 关联矩阵：同类型 +1，跨类型 +2，发现者链 +1 |
| CaseLogUI.cs | 会议指证面板：证据链展示 + 指证目标选择 |
| AccusationSystem.cs | 有链指证 → 被指证者投票权重 +2；无链 → 无效指证 |

### 3.2 职业能力全量接入（核心差异化 #2）
OnlineRuleSet 定义了 15 种 AbilityType，实际接入代码的 < 5 种。

| 能力 | 职业 | 状态 | 需实现 |
|------|------|------|--------|
| FootprintTrack | Inspector | ❌ | 地面 5 秒可见足迹 |
| CorpseExamine | Forensics | ❌ | 检验尸体弹出额外线索 |
| TaskSpeedBonus | Tech | ❌ | 小游戏计时器加速 |
| RemoteSurveillance | Tech | ❌ | 远程查看任意摄像头 |
| EvidenceChainBonus | Tech | ❌ | 证据关联加成 |
| DarkVision | Enforcer | ❌ | 短暂穿墙轮廓 |
| BodyDrag | Fixer | ❌ | 拖动尸体 |
| SabotageCooldownReduce | Fixer/Mole | ❌ | 破坏冷却缩短 |
| SecretVote | Undercover/Mole | ❌ | 投票不可见 |
| VentSpeedBonus | Driver | ❌ | 暗线加速 |
| MoveSpeedBonus | Driver | ❌ | 基础移速加成 |
| MoleIntel | Mole | ❌ | 窃取敌方任务进度 |

**验收**: 每个职业 ≥2 个能力接入，PlayMode 可触发可验证

### 3.3 卧底双身份深度（核心差异化 #3）

```
卧底生命周期:
潜伏阶段 → 信息收集 → 背叛时刻 → 翻盘/覆灭
(表面Gang)  (窃取情报)  (选择阵营)  (胜负判定)
```

| 机制 | 说明 |
|------|------|
| 伪装任务 | 完成 X 个 Gang 阵营任务 |
| 情报窃取 | 每次完成 Gang 任务积累 intel |
| 背叛时机 | 会议前/紧急任务中/击杀后 三种窗口 |
| 双结局 | 背叛→Police胜+加分；潜伏→Gang胜时独赢 |

### 3.4 内鬼机制（核心差异化 #4）

| 机制 | 说明 |
|------|------|
| MoleIntel | Police 任务完成时窃取 intel |
| 暗杀指令 | Intel 满 5 → 获得目标名单 |
| 翻盘条件 | Intel 达标 + 关键警察淘汰 → 独立获胜 |
| 暴露风险 | 被 Inspector 发现 → 永久标记 |

---

## Phase 4: 内容广度与品质（Content & Polish）

**时长**: 21-28 天 | **里程碑**: M4 (Alpha)  
**目标**: 3 张完整地图 + 2D 美术化 + 音频完善 + UI 品质

### 4.1 地图内容

| 任务 | 说明 |
|------|------|
| D1: 现有 2 图校验 | MapValidator 跑 HarbourDistrict + PoliceStation |
| D2: 九龙城寨 | 新图，12 房间、6 监控点、独特暗线网络 |
| D3: 地图选择 UI | 大厅房主选图 → 多端同步 |
| D4: Balance Pass | 3 图 × 6/8/10 人节奏测试 |

### 4.2 2D 美术化
- **E1**: 美术 Bible（港夜霓虹色板、角色尺寸、UI 风格、字体）
- **E2**: 7 职业 sprite 完整动画帧（idle/walk/交互/死亡）
- **E3**: 3 张地图 Tilemap 化（替换 greybox Primitive）
- **E4**: VFX（暗杀闪光、停电暗化、紧急任务红光）
- **E5**: BGM 切换 + 完整音效库

### 4.3 UI/UX 升级
- 所有 OnGUI → uGUI 收尾
- 响应式布局（16:9/16:10/21:9）
- 色盲模式实跑验证
- 中英双语完整覆盖

---

## Phase 5: 留存与 Meta 系统（Meta & Retention）

**时长**: 14-21 天 | **里程碑**: M5 (Beta)  
**目标**: 玩家有理由反复回来玩

### 5.1 玩家档案
- ProfileData: 总对局数/胜率/最常用职业/信誉分
- 称号系统: 神探/冷血杀手/千面卧底...
- 本地持久化 + 云端同步

### 5.2 匹配系统
- 信誉分匹配：高信誉优先同局
- 举报/封禁基础框架

### 5.3 对局回放
- 会议后 30 秒"关键时刻回放"
- 全对局数据序列化（可选）

### 5.4 赛季/通行证框架
- 每月赛季：新地图变体/新职业/限时模式
- 免费+付费通行证：皮肤/称号/角色外观

---

## Phase 6: 发布工程（Launch Engineering）

**时长**: 14-21 天 | **里程碑**: M6 (Ship)  
**目标**: 可上架 Steam 的质量

### 6.1 多平台构建
| 平台 | 说明 |
|------|------|
| macOS | 签名 + 公证 |
| Windows | 真机构建验证 |
| Steam Deck | Proton 兼容测试（可选） |

### 6.2 网络与服务器
- Unity Lobby + Relay 服务正式配置
- 专用服务器评估
- 区域 ping 测试（亚洲/北美/欧洲）

### 6.3 性能基线
| 指标 | 目标 |
|------|------|
| 帧率 | 60 FPS @ 1080p |
| 内存 | < 2GB |
| 网络 | < 50 KB/s per client |
| 加载 | < 15s 进大厅 |

### 6.4 合规与商店
- Steamworks SDK 接入
- 商店页面 + 预告片
- 年龄分级 + EULA

### 6.5 测试流程
| 阶段 | 人数 | 目标 |
|------|------|------|
| 内部 Alpha | 4-6 人 | 崩溃/卡死 |
| 封闭 Beta | 10-20 人 | 平衡/留存 |
| 开放 Demo | 50+ 人 | 服务器压力 |
| EA 上线 | — | Steam 发布 |

---

## Phase 7: 运营迭代（Live Ops）

上线后持续：
- 每 2 周平衡补丁
- 每月内容更新（地图变体/新小游戏/新职业）
- 每季度大版本（新地图/新机制）
- 社区反馈闭环

---

## 总时间线与里程碑

```
Week 1-2   ████████ Phase 0: 设计对齐
Week 3-5   ██████████████ Phase 1: 架构收口
Week 6-10  ████████████████████████ Phase 2: 核心玩法闭环
Week 11-15 ████████████████████████ Phase 3: 差异化兑现
Week 16-24 ████████████████████████████████████████ Phase 4: 内容与品质
Week 25-30 ██████████████████████████████ Phase 5: Meta 系统
Week 31-38 ████████████████████████████████████████ Phase 6: 发布工程
Week 39+   ████████████████████ Phase 7: 运营迭代
```

| 里程碑 | 周 | 验收标准 |
|--------|---|---------|
| **M0: 设计对齐** | 2 | GDD v2.0 与代码一致 |
| **M1: 可维护** | 5 | OnlineMatchController < 4K 行，全 uGUI |
| **M2: 可玩** | 10 | 10 人联机完整一局（小游戏✅基线已过+暗线+监控✅基线已有+紧急任务）；工期可较原估缩短，因 2.1/2.3 大部已完成 |
| **M3: 有差异** | 15 | 证据链+职业能力+卧底/内鬼可感知 |
| **M4: Alpha** | 24 | 3 张美术化地图 + 完整音效 + UI 品质 |
| **M5: Beta** | 30 | 玩家档案 + 匹配 + 回放 |
| **M6: Ship** | 38 | Steam EA 上线 |

---

## 关键风险

| 风险 | 严重度 | 缓解 |
|------|--------|------|
| 控制器拆分引入回归 | 🔴 | 每步后 PlayMode 自动化回归 |
| 联机小游戏性能 | 🟡 | RPC 频率限制、Client 预测 |
| 证据链设计实现差距 | 🟡 | Phase 0 先设计明确 |
| 2D 美术资源瓶颈 | 🟡 | CC0 素材先跑通管线 |
| 测试玩家不足 | 🟢 | Bot 补齐、本地多开 |

---

## 系统依赖链

```
Phase 0 (GDD) 
  └→ Phase 1 (拆分控制器) 
       └→ Phase 2 (小游戏/暗线/监控) 
            └→ Phase 3 (证据链/职业能力) — 依赖小游戏和控制器拆分完成
                 └→ Phase 4 (美术/品质) — 依赖核心玩法定型
                      └→ Phase 5 (Meta) — 依赖对局数据完整
                           └→ Phase 6 (发布) — 依赖全部内容就绪
```

**不可跳过的依赖**: Phase 1 必须在 Phase 2-3 之前完成（否则每加一个功能都在 13K 行控制器上堆代码）。Phase 0 可在 Phase 1 并行推进。

---

> **原则**: 每个 Phase 有明确的文件、行数、API 出口。每完成一个里程碑做 batchmode 编译 + PlayMode 回归。计划可调顺序，不可跳过依赖项。
