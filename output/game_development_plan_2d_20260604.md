# Gangland Undercover 完整开发计划（2D 地图重做版）

日期: 2026-06-04
项目路径: `/Users/zhugehao/projects/GanglandUndercover`
引擎版本: Unity 6000.4.5f1（Netcode for GameObjects + Unity Transport）
本文取代: `output/game_development_plan_full_20260604.md` 与 `output/current_gap_matrix_20260604.md` 的“现状/计划”部分。

目标: 把当前警匪社交推理联机原型，在**地图全面 2D 化**的方向上，推进到可封闭测试、6-10 人可稳定开局、单局 10-15 分钟、体验接近 Among Us 节奏且保留警匪题材差异的版本。

---

## 0. 基线（以源码实测为准，2026-06-04 重新核对）

> 重要：本节是用源码核对过的真实现状，已修正旧两份文档的事实错误。后续任何排期都以本节为准。

### 0.1 工程规模

- `Assets/_Project/Scripts` 约 104 个 C# 文件，约 46,524 行。
- `OnlineMatchController.cs` 实测 **12,690 行**（旧文档误写 13,101）。
  - partial 拆分: `OnlineMatchController.VerticalSlice.cs`(615) + `OnlineMatchController.CharacterAdapters.cs`(157)。
- 仅有 1 个测试文件 `Assets/_Project/Editor/PrototypeSmokeTests.cs`，单元测试覆盖几乎为零。

### 0.2 已经做好的（旧计划仍当“待办”，实际已完成，不要重做）

- **规则配置层已存在**: `OnlineRuleSet.cs`(108 行)，被控制器、HUD、快照全局引用。
- **地图坐标服务已存在**: `OnlineMapService.cs`(219 行)，`ScaleMapPosition` / `ScaleMapSize` / `UnderworldPassagePosition` 是点位唯一来源。
- **暗线/通风管已统一**: 只有单一 `TryUseUnderworldPassage`（`OnlineMatchController.cs:4451`，注释明确“合并原 OnlineVents / TryVent 系统”）。不存在两套机动系统，坐标已统一走 mapService。
- **Relay 业务代码已存在**: `OnlineMatchController.cs:1371-1444`，含 `CreateAllocationAsync` / `GetJoinCodeAsync` / `JoinAllocationAsync` / `SetRelayServerData`。
- **鬼魂联机可用**: `Gameplay/GhostMode.cs`(228)，控制器淘汰/驱逐时激活。
- **物理碰撞已是 2D**: `PhysicsColliderCount` 统计的是 `Collider2D`（`OnlineMatchController.cs:191`）。玩家位置已是 `Vector3(x, y, 0)`，z 固定 0。

### 0.3 真实待修/待做的

| 项 | 真实状态 | 证据 |
|---|---|---|
| 渲染层 | **仍是 3D 图元** | 世界用 `PrimitiveType.Cube/Sphere/Cylinder` + 带 z 深度的 `CreatePropChild`（`:7301+`） |
| 行动相机 | **透视(perspective)** | 预览态 orthographic，行动态用 `fieldOfView`（`:7051-7060`） |
| SabotageSync | **在用反射读私有字段** | `:109/:156/:314` 反射 `blackoutTimer/lockdownTimer/communicationJamTimer/evidenceLeakTimer/patrolAlertTimer` + `localRole` |
| 语音 | **Vivox 已移除但 UI 仍承诺** | `UnityServiceBootstrap.cs:31-35` `VivoxReady=>false`，README 仍写“近距离语音规则” |
| 联机小游戏 | **缺失** | 13 个小游戏只在 `SocialDeduction/MiniGames/`，无 `Online/MiniGames/` |
| 监控 | **仅离线** | `SocialDeduction/SecurityCamera.cs`，Online 侧无引用 |
| Host Migration | **代码存在未联调** | `HostMigrationManager.cs`(452) |
| 单元测试 | **几乎为零** | 仅 1 个 smoke 文件 |
| 仓库卫生 | **约 19,522 个未提交改动** | 多为 Kenney CityKit 第三方美术二进制 |
| 临时脚本 | **遗留在根目录** | `temp_fix_calls.py` / `temp_fix2.py` |

### 0.4 与旧文档的关键差异（避免重蹈覆辙）

- `current_gap_matrix_20260604.md` 误判“Relay 零代码”“SabotageSync 无反射”，**不可作决策依据**。
- `game_development_plan_full_20260604.md` 的方法论可用，但“现状判断”过期：M1 的规则层/地图服务/暗线统一其实已完成。
- 教训：本项目任何“现状”结论都必须以源码核对，不信旧 stage 报告。

---

## 1. 产品定义

### 1.1 核心体验

6-10 人警匪卧底局。警方/市民/卧底通过任务、监控、证据链和会议找出黑帮；黑帮/线人通过击杀、破坏、暗线机动、伪装任务和会议误导阻止证据链闭合。单局 10-15 分钟，过程持续产生信息差、路线怀疑、任务压力和会议冲突。

### 1.2 视角与表现：2D 优先（本版核心方向变更）

正式地图采用 **top-down 正交 2D**。现有 3D 资源（Synty / DenysAlmaral / Kenney CityKit）只作为概念参考或必要时预渲染为 sprite，不再作为正式地图主要生产路线。理由：

- 2D 读图清楚、遮挡少，任务点和尸体易见，贴合 Among Us 核心优势。
- 2D 美术成本低、迭代快，Tilemap/Sprite 改路线不必重摆 3D 模型。
- 小地图、会议证据板更容易与世界坐标一致。
- **逻辑层几乎零改动**：玩家位置已是 `Vector3(x,y,0)`，碰撞已是 `Collider2D`，只需替换渲染与相机投影。

### 1.3 警匪差异化（保留并强化）

- 证据链进度，而非纯任务条。
- 嫌疑值与线索板。
- 警署、港区、后巷、监控、证物、线人、卧底。
- 黑帮“暗线通道”机动（底层统一，命名差异化）。
- 会议展示可推理线索，而非直接给答案。

---

## 2. 总体里程碑

执行顺序遵守依赖，但因 0.2 已完成部分，重心从“拆控制器”转向“2D 地图 + 联机玩法成品化”。

| 里程碑 | 目标 | 通过标准 |
|---|---|---|
| **M0 真实基线** | 编译、本地局、双开局跑通并记录 | 0 编译错误；本地局到结算；双开核心同步；基线报告以源码为准 |
| **M1 关键债务清理** | 修反射、语音方向定调、保护 worktree、补最小测试 | SabotageSync 去反射；语音方案二选一落地；worktree 已提交保护；规则/胜负有 EditMode 测试 |
| **M2 2D 渲染地基** | 行动相机正交化，世界渲染从 3D 图元切到 2D Sprite | 行动相机 orthographic；玩家/尸体/任务/墙体为 2D 表现；双开烟测不退化 |
| **M3 港区 2D 玩法地图** | 灰盒 → 互动点布局 → 多人测试 → 第一版 tile/sprite | 港区灰盒可完整跑局；2 真人+6 Bot×5 局；80% 灰盒被 tile/sprite 替换且路线不变 |
| **M4 联机小游戏与破坏修复** | 离线小游戏接入联机，破坏需小游戏修复 | ≥6 种联机小游戏；破坏须修复小游戏；Host 权威结果 |
| **M5 信息与欺骗系统** | 监控、证据板、嫌疑、暗线可读性进入联机 | 会议能展示 3 类线索；监控影响推理；暗线有体验+间接线索 |
| **M6 服务与房间** | Relay 房间码成品化、断线策略、Host 权威反作弊 | 双机 Relay 完整局；房间码 Canvas 路径；断线不卡局 |
| **M7 成品 UI 与反馈** | 默认路径全 Canvas，2D 地图 UI/提示/证据板联动 | 新玩家不看文档完成第一局；小地图/大地图/世界点位同源 |
| **M8 内容与平衡** | 第二张 2D 图、职业收敛、Bot 升级、平衡数据 | 8 人局平均 10-15 分钟，胜率 45-55；两张 2D 图可联机 |
| **M9 封测发行准备** | 构建、日志、测试清单、发布门槛 | 外部包可分发；72 小时无 P0/P1 |

### 2.1 依赖图

```text
M0 真实基线
  -> M1 关键债务清理（反射/语音/worktree/测试）
    -> M2 2D 渲染地基（相机正交 + Sprite 渲染）
      -> M3 港区 2D 玩法地图（灰盒->布局->测试->美术）
        -> M4 联机小游戏与破坏修复
          -> M5 信息与欺骗（监控/证据板/嫌疑/暗线）
            -> M6 服务与房间（Relay/断线/反作弊）
              -> M7 成品 UI 与反馈（2D 地图 UI）
                -> M8 内容与平衡（第二张图/职业/Bot/数据）
                  -> M9 封测与发行
```

可并行: M2 相机正交化与 M1 测试补齐；M4 小游戏 UI 与 M6 服务环境配置（但小游戏结果协议须先定）；M7 视觉反馈与 M8 平衡数据采集。
不可并行: M2 渲染未稳前不做大规模 M3 美术替换；M4 协议未定前不并行接多个小游戏；编译未过不做功能开发。

---

## 3. 任务清单

### M0：真实基线

#### Task 0.1 工程编译基线
- **描述**: 打开 Unity 6000.4.5f1，确认无 C# 编译错误进 Play Mode。
- **验收**: Console 0 compile error；有错则输出 `output/baseline_compile_errors_20260604.md`（文件+行号+复现）；不修非阻断 warning。
- **依赖**: 无。
- **涉及**: `Assets/_Project/Scripts/**`、`Packages/manifest.json`、`ProjectSettings/ProjectVersion.txt`。
- **规模**: S。

#### Task 0.2 本地试玩完整局
- **描述**: 默认玩家路径跑一局 Lobby→Opening→Action→Meeting/Voting→Result。
- **验收**: 完整走完；记录首个阻断和高频体验问题；≥3 张截图（大厅/行动/会议结算）。
- **依赖**: 0.1。
- **涉及**: `Online/OnlineMatchController.cs`、`Online/OnlineMatchHud.cs`、`output/baseline_local_play_20260604.md`。
- **规模**: XS。

#### Task 0.3 本机双开联机烟测
- **描述**: Multiplayer Play Mode 或双构建实例 Host+Client，验证核心同步。
- **验收**: 建房/加入/位置同步/角色私发/各 1 次任务·击杀·报案·投票·结算；执行 `output/online_test_plan.md` TC-01~TC-12；输出 `output/baseline_online_smoke_20260604.md`。
- **依赖**: 0.1。
- **涉及**: `OnlineMatchController.cs`、`GameStateSnapshot.cs`、`MeetingSync.cs`、`TaskSync.cs`。
- **规模**: S。

#### Checkpoint M0
- [ ] 0 编译错误或错误已编号。
- [ ] 本地完整局可完成或阻断点已编号。
- [ ] 双开核心同步可完成或阻断点已编号。
- [ ] 基线报告以源码为准，不沿用旧 gap matrix。

---

### M1：关键债务清理

> 旧计划 M1 的“抽规则/抽地图/统一暗线”已完成（见 0.2），故本 M1 聚焦真正待修债务。

#### Task 1.1 保护现有 worktree
- **描述**: 当前约 19,522 个未提交改动（多为第三方美术二进制）有丢失风险。先安全提交/分支保护，再开始改动。
- **验收**: 在新分支提交现有改动或确认归档；`.gitignore` 复核（第三方大二进制是否应入库或用 Git LFS）；清理根目录 `temp_fix_calls.py` / `temp_fix2.py`。
- **依赖**: M0。
- **涉及**: `.gitignore`、仓库根。
- **规模**: S。**优先级最高，先做。**

#### Task 1.2 SabotageSync 去反射
- **描述**: `SabotageSync.cs:109/156/314` 每帧反射读 5 个私有 timer + `localRole`。改为公开只读状态或事件。
- **验收**: `SabotageSync` 不再使用 `System.Reflection`/`GetField`/`BindingFlags`；破坏开始/倒计时/结束/修复通过公开只读状态或事件获取；HUD 与日志不退化。
- **验证**: 全项目搜索 `BindingFlags`/`GetField` 确认破坏同步不再依赖；双开触发 5 类破坏，Client HUD 都收到状态。
- **依赖**: M0。
- **涉及**: `Online/SabotageSync.cs`、`Online/SabotagePanel.cs`、`Online/OnlineMatchController.cs`。
- **规模**: S-M。

#### Task 1.3 语音方案定调（README 与代码对齐）
- **描述**: Vivox 已移除但 README 仍承诺“近距离语音规则”。必须二选一并落地，消除对外承诺与能力脱节。
- **验收**: 方案 A 恢复语音（重装 Vivox 或接 WebRTC，行动/会议/鬼魂三频道规则明确）；或方案 B 移除语音承诺（README + HUD 去掉近距离语音，改文本聊天或第三方语音）。无论哪种，README 与 `UnityServiceBootstrap` 状态一致。
- **依赖**: M0。
- **涉及**: `README.md`、`UnityServiceBootstrap.cs`、`SocialDeduction/VoiceChatSystem.cs`、`OnlineMatchHud.cs`、`Packages/manifest.json`。
- **规模**: 方案 B 为 S；方案 A 为 L（建议先 B 解锁进度，A 留到 M6）。

#### Task 1.4 最小单元测试地基
- **描述**: 当前仅 1 个 smoke 文件。为后续重构建立安全网，先测规则与胜负。
- **验收**: 新增 EditMode 测试覆盖：`OnlineRuleSet` 默认值、胜负判定（证据胜/人数压制胜/超时失败）、投票统计（平票/跳过/最高票淘汰）。
- **依赖**: M0。
- **涉及**: `Assets/_Project/Editor/*Tests.cs`、`OnlineRuleSet.cs`、`OnlineVictoryBridge.cs`、`VictoryEvaluator.cs`。
- **规模**: M。

#### Task 1.5（可选）控制器职责瘦身起步
- **描述**: 12,690 行控制器仍偏重，但 0.2 已抽出规则/地图/暗线。本任务只抽“任务/破坏状态”到 `OnlineTaskService`，为 M4 小游戏做准备。不追求行数指标，追求职责边界与可测试。
- **验收**: 任务完成/破坏/修复/证据增减移入 `OnlineTaskService`，状态变更发事件供 HUD/音频/日志/胜负订阅；行为不变；双开任务同步不退化。
- **依赖**: 1.4。
- **涉及**: `Online/OnlineTaskService.cs`(新)、`Online/TaskSync.cs`、`Online/OnlineMatchController.cs`、`Online/OnlineVictoryBridge.cs`。
- **规模**: M。

#### Checkpoint M1
- [ ] 现有 worktree 已安全提交/保护。
- [ ] SabotageSync 不再反射。
- [ ] 语音 README 与代码一致。
- [ ] 规则/胜负/投票有 EditMode 测试。

---

### M2：2D 渲染地基

> 目标：把渲染与相机切到正交 2D，逻辑层不动。这是地图重做为 2D 的前置工程。

#### Task 2.1 行动相机正交化
- **描述**: 行动态相机当前是 perspective(`fieldOfView`)。改为 orthographic top-down，预览/会议/结算相机统一正交策略。
- **验收**: 行动相机 `orthographic=true`，`orthographicSize` 适配玩家可视范围；预览/会议/结算相机过渡平滑；玩家、尸体、任务点在正交视角不被遮挡。
- **验证**: 本地局观察各阶段相机；双开位置/朝向同步不退化。
- **依赖**: M1。
- **涉及**: `OnlineMatchController.cs:7029-7060` 相机配置区、`OnlineMatchController.cs:374-467` 相机校验属性。
- **规模**: M。

#### Task 2.2 2D 渲染抽象层
- **描述**: 把 `CreatePropChild` / `CreateTaskVisual` 等基于 `PrimitiveType` 的 3D 图元生成，抽象为可切换的渲染后端。第一步只引入接口与 2D Sprite 实现，默认仍可回退 3D 以防回归。
- **验收**: 新增 `IWorldRenderer` 或等价抽象，含 `Sprite2DRenderer` 实现（SpriteRenderer + 分层 sorting）；玩家/尸体/任务/墙体可用 2D 表现渲染；3D 路径保留为开发开关。
- **验证**: 切到 2D 后端跑本地局；双开烟测不退化。
- **依赖**: 2.1。
- **涉及**: `OnlineMatchController.cs` 世界生成区(`:6940`,`:7301+`,`:7406+`)、新 `Online/Rendering/Sprite2DRenderer.cs`、`Online/Rendering/IWorldRenderer.cs`。
- **规模**: L（建议拆：抽象接口 / 玩家+尸体 / 任务+墙体三步）。

#### Task 2.3 Sprite 分层与排序规则
- **描述**: 定义 2D sorting layer 规则，保证读图优先级。
- **验收**: sorting 层 Floor < Walls < Props < Interactables < Bodies < Players < Effects < Overlay；高频 sprite 进 atlas；玩家/尸体永远在装饰之上。
- **验证**: 缩略到 50% 仍能看出玩家/尸体/任务/破坏。
- **依赖**: 2.2。
- **涉及**: `Sprite2DRenderer.cs`、`ProjectSettings/TagManager.asset`(Sorting Layers)。
- **规模**: S-M。

#### Checkpoint M2
- [ ] 行动相机正交。
- [ ] 世界可用 2D Sprite 渲染，3D 可回退。
- [ ] sorting 层保证读图优先级。
- [ ] 双开烟测不退化。

---

### M3：港区 2D 玩法地图

> 顺序铁律：玩法定义 → 灰盒 → 互动点布局 → 多人测试 → tile/sprite 资产 → 灰盒替换。不要先画建筑装饰。

#### Task 3.1 港区地图玩法文档
- **描述**: 定义第一张正式港区图骨架（不谈美术素材）。
- **验收**: 8-10 个核心区域(≤12)；3 条主路+2-3 条高风险支路；1 公共会议点；6-8 任务簇（每簇 2-4 点）；4 暗线节点成 2 组长距机动；3-4 监控点覆盖主路留盲区；2 强破坏区(电力/通讯)；1-2 高风险击杀区有绕路与目击可能；每区一句话玩法目的。输出 `output/map_design_harbor_2d_v1.md` + 顶视草图。
- **验证**: 点位与 `OnlineMapService` 一一对应。
- **依赖**: M2。
- **涉及**: `output/map_design_harbor_2d_v1.md`、`OnlineMapService.cs`。
- **规模**: S。

#### Task 3.2 港区 2D 灰盒
- **描述**: 用 Tilemap / 简单 Sprite / 纯色矩形做正式港区 2D 灰盒，只验证走路/视野/碰撞/路线，允许丑但路线不能含糊。
- **验收**: 可走/不可走/门洞/窄路明确；任意出生点到任意任务点≥2 条路线；常见击杀点到报案点有 5-12 秒发现窗口；任务/尸体/玩家正交视角不被遮挡。
- **验证**: 2 真人+6 Bot 跑 5 局。
- **依赖**: 3.1、2.3。
- **涉及**: `Assets/_Project/Scenes/HarborGreybox2D.unity` 或 2D runtime map config、`OnlineMapService.cs`。
- **规模**: M。

#### Task 3.3 互动点布局
- **描述**: 在灰盒上放任务/暗线/监控/破坏/会议/出生点。
- **验收**: 任务分散不全在主路；暗线出口不贴所有高价值任务；监控覆盖关键路口但留盲区；破坏点迫使跨区移动；尸体常见点附近有报案路线。输出 `output/map_interaction_layout_harbor_2d_v1.md` + 点位表。
- **验证**: 8 人 AI 局任务分布不致长期聚集；黑帮出暗线后≥2 路线；监控产生会议线索但不锁死黑帮。
- **依赖**: 3.2。
- **涉及**: `OnlineMapService.cs`、布局文档。
- **规模**: M。

#### Task 3.4 灰盒多人玩法测试
- **描述**: 灰盒先玩得通再进美术。
- **验收**: 2 真人+6 Bot×5 局 + 6-8 真人×3 局；记录首杀地点/报案地点/会议次数/迷路点/任务完成率/胜负/危险区认知；平均局长接近 10-15 分钟；玩家能记住区域名；≥2 区成争论焦点；无长期无人区。
- **依赖**: 3.3。
- **涉及**: 测试记录 `output/map_playtest_harbor_2d_v1.md`。
- **规模**: M（含多轮测试）。

#### Task 3.5 港区 2D Tile/Sprite 资产包
- **描述**: 先做美术风格圣经，再做可复用功能资产，不先堆装饰。
- **验收**: 风格圣经 `output/art_direction_harbor_2d_v1.md`（题材关键词/色彩层级/尺寸 token/资产使用规则）；地面/墙体/门洞/任务台/监控台/暗线入口/破坏点都有 2D 资产；tile 尺寸、sprite pivot、collider、sorting 统一；交互物有 normal/hover/disabled/sabotaged 四态。
- **验证**: 用 tile/sprite 替换灰盒 80% 后路线不变。
- **依赖**: 3.4。
- **涉及**: `Assets/_Project/Art/2D/Harbor/{Tiles,Sprites,Interactables,Props,Characters,Effects,UIIcons}/`、`Assets/_Project/Prefabs/Map2D/Harbor/`、`output/tile_sprite_inventory_harbor_2d_v1.md`。
- **规模**: L。

#### Task 3.6 灰盒替换为第一版 2D 美术
- **描述**: 用 tile/sprite 替换灰盒，不改玩法布局。先地面/墙/阻挡，再任务/暗线/监控/破坏，最后装饰，每次替换跑路线碰撞 smoke。
- **验收**: 玩家不会因新 sprite 误判可走/不可走；任务点不被装饰遮挡；尸体在常见地面仍醒目；小地图与世界位置一致。
- **依赖**: 3.5。
- **涉及**: 地图 prefab/scene、`OnlineMapService.cs`、`Sprite2DRenderer.cs`。
- **规模**: M-L。

#### Checkpoint M3
- [ ] 港区玩法骨架文档与点位对应。
- [ ] 灰盒可完整跑局且多人测试通过。
- [ ] 2D 资产包功能齐全、规范统一。
- [ ] 灰盒 80% 被替换且玩法布局不变。

---

### M4：联机小游戏与破坏修复

> 复用 `SocialDeduction/MiniGames/`（13 个，基类 `MiniGameBase` 用 `OnComplete`/`OnCancel` 回调）。在联机侧包“会话 + Host 校验”。

#### Task 4.1 联机小游戏运行协议
- **描述**: 定义小游戏如何启动/提交/校验/取消/同步。只同步结果与必要状态，不同步每帧拖拽。
- **验收**: 有 `OnlineMiniGameDefinition` + `OnlineMiniGameSession` + `OnlineMiniGameResult`；Host 权威接收结果，Client 不能直接改证据；失败/取消/断线有处理。输出 `output/minigame_runtime_contract_20260604.md`；EditMode 测合法/非法提交。
- **依赖**: M3、Task 1.5。
- **涉及**: `Online/MiniGames/OnlineMiniGame{Definition,Session,Result}.cs`、`SocialDeduction/MiniGames/MiniGameBase.cs`。
- **规模**: M。

#### Task 4.2 接入第一个真实任务 WireTask（vertical slice）
- **验收**: 靠近电力/货柜任务打开真实小游戏；完成向 Host 提交；Host 校验后加进度/证据；取消不卡 activeTask。
- **验证**: 单机/双开完成 WireTask；Client 断面板后可继续或重开。
- **依赖**: 4.1。
- **涉及**: `WireTask.cs`、`OnlineMiniGameSession.cs`、`OnlineTaskService.cs`、`OnlineMatchHud.cs`。
- **规模**: M。

#### Task 4.3 接入 Keypad / SwipeCard
- **验收**: Keypad 用于门禁/保险箱/锁定修复；SwipeCard 用于证物/档案扫描；同一提交协议；错误输入不直接完成。
- **依赖**: 4.2。
- **涉及**: `KeypadTask.cs`、`SwipeCardTask.cs`、`OnlineMiniGameSession.cs`、`OnlineTaskService.cs`。
- **规模**: M。

#### Task 4.4 接入 Memory / Download / EvidenceArchive（凑满≥6 种）
- **验收**: Memory 用于监控回放、Download 用于数据上传、EvidenceArchive 用于证据归档；面板视觉不同；全局进度正确增长。建议拆 3 个提交。
- **依赖**: 4.3。
- **涉及**: `MemoryTask.cs`、`DownloadTask.cs`、`EvidenceArchiveTask.cs`、`MiniGameType.cs`、`OnlineTaskService.cs`、`OnlineMatchHud.cs`。
- **规模**: M-L。

#### Task 4.5 破坏修复小游戏
- **验收**: Blackout→Wire/Breaker；Communications→Calibrate；Lockdown→Keypad；EvidenceLeak→EvidenceArchive；修复失败/取消不立即清除破坏；破坏倒计时与修复状态 Client HUD 一致。
- **依赖**: 4.2~4.4、Task 1.2（去反射后的破坏状态模型）。
- **涉及**: `SabotageType.cs`、`OnlineTaskService.cs`、`SabotageSync.cs`、`OnlineMatchHud.cs`。
- **规模**: M。

#### Task 4.6 全局任务进度与个人任务清单
- **验收**: HUD 显示证据链+全局进度；玩家看到自己被分配任务；黑帮看伪装任务/破坏目标不暴露真实警方策略。
- **依赖**: 4.1、Task 1.5。
- **涉及**: `TaskSync.cs`、`OnlineTaskService.cs`、`OnlineMatchHud.cs`、`GameStateSnapshot.cs`。
- **规模**: M。

#### Checkpoint M4
- [ ] ≥6 种联机真实小游戏。
- [ ] 破坏须小游戏修复。
- [ ] Host 权威控制结果。
- [ ] 取消/断线/重进不死锁。

---

### M5：信息与欺骗系统

#### Task 5.1 联机监控基础版
- **描述**: 可用优先，不追求 RenderTexture。监控站显示区域/玩家轨迹用于会议推理。可参考离线 `SecurityCamera.cs`。
- **验收**: 靠近监控站可开界面；显示若干区域最近活动/可疑移动；黑帮可破坏监控，修复前不可用或延迟。
- **依赖**: M3、Task 4.5。
- **涉及**: `SocialDeduction/SecurityCamera.cs`(参考)、`OnlineMapService.cs`、`OnlineMatchHud.cs`、`OnlineTaskService.cs`。
- **规模**: M。

#### Task 5.2 会议证据板
- **验收**: 显示尸体地点/报案者/最后发现时间、最近破坏/任务完成/监控线索、玩家嫌疑摘要（不显真实阵营）；投票 UI 与证据板同屏或可切；Host/Client 一致。
- **依赖**: Task 5.1。
- **涉及**: `Online/MeetingSync.cs`、`OnlineMatchHud.cs`、`ChatSystem.cs`、`GameStateSnapshot.cs`。
- **规模**: M。

#### Task 5.3 嫌疑值规则
- **验收**: 嫌疑来自行为（靠近尸体/破坏地点/监控视野/任务失败/暗线痕迹），非随机；警方职业技能可揭示或降噪；会议只显示“线索强度”不确认身份。EditMode 测嫌疑增减。
- **依赖**: 5.2。
- **涉及**: `OnlinePlayerState`、`MeetingSync.cs`、`OnlineMatchHud.cs`。
- **规模**: M。

#### Task 5.4 暗线通道可读性
- **描述**: 暗线逻辑已统一（`TryUseUnderworldPassage`），本任务做体验与线索表达。
- **验收**: 黑帮 HUD 显示附近暗线与冷却；使用有短动画/音效；警方不直接看节点但可在监控/线索板看到“后巷活动”。
- **依赖**: 5.2。
- **涉及**: `OnlineMapService.cs`、`OnlineMatchHud.cs`、`OnlineAudioCueService`、`MeetingSync.cs`。
- **规模**: M。

#### Checkpoint M5
- [ ] 监控产生可讨论信息。
- [ ] 会议证据板支持推理。
- [ ] 嫌疑系统可解释。
- [ ] 暗线有体验与间接线索。

---

### M6：服务与房间

> Relay 调用代码已存在(`:1371-1444`)，本里程碑是成品化与稳定化，不是从零写。

#### Task 6.1 Unity Cloud 绑定与 Relay 验证
- **验收**: `UnityServiceBootstrap` 显示 Cloud/Services/Auth/Lobby/Relay OK；失败给明确原因；真机 Relay 建房得房间码。
- **依赖**: M0。
- **涉及**: `UnityServiceBootstrap.cs`、`OnlineMatchController.cs:1371-1444`、`ProjectSettings/UnityConnectSettings.asset`。
- **规模**: S（环境问题除外）。

#### Task 6.2 房间码路径成品化
- **验收**: 主菜单/大厅创建或加入 Relay 房间，不依赖 OnGUI 调试路径；展示房间码；Ready/AI 补位/规则/开始全在 Canvas。
- **验证**: 两台机器通过房间码开局。
- **依赖**: 6.1。
- **涉及**: `LobbyController.cs`、`OnlineMatchHud.cs`、`OnlineMatchController.cs`、`MainMenuController.cs`。
- **规模**: M。

#### Task 6.3 断线与重连策略
- **验收**: Client 断线 60s 内保留状态后可 AI 接管/移除；Host Migration 要么真实可用要么 UI 明确“本局终止”；断线不卡投票/任务/胜负。
- **依赖**: Task 6.2。
- **涉及**: `HostMigrationManager.cs`、`GameStateSnapshot.cs`、`OnlineMatchController.cs`。
- **规模**: M-L。

#### Task 6.4 Host 权威与基础反作弊
- **验收**: 移动超速被拒/纠正；击杀距离/冷却/目标存活由 Host 校验；任务结果 Host 校验；投票仅会议阶段一次；日志记非法尝试。
- **依赖**: Task 1.5、4.1。
- **涉及**: `OnlineMatchController.cs`、`OnlineTaskService.cs`、`MeetingSync.cs`、`PlayerStateSync.cs`。
- **规模**: M。

#### Task 6.5（条件）语音方案 A 落地
- **描述**: 若 M1 选了方案 B（移除承诺），此处可选恢复真实语音。
- **验收**: Vivox 或 WebRTC 全链路可用；行动/会议/鬼魂三频道规则；双机近距离语音、会议同频、死亡进鬼魂频道。
- **依赖**: 6.1。
- **涉及**: `UnityServiceBootstrap.cs`、`VoiceChatSystem.cs`、`OnlineMatchController.cs`、`Packages/manifest.json`。
- **规模**: L。

#### Checkpoint M6
- [ ] 双机 Relay 完整局。
- [ ] 房间码 Canvas 路径可用。
- [ ] 断线不卡局。
- [ ] Host 权威覆盖关键操作。

---

### M7：成品 UI 与反馈（2D 地图 UI 联动）

#### Task 7.1 默认关闭 OnGUI 玩家路径
- **验收**: 主菜单/房间/行动/会议/结算不需 OnGUI；开发模式可开调试面板；玩家路径无重复 UI。
- **依赖**: Task 6.2。
- **涉及**: `OnlineMatchController.cs`、`OnlineMatchHud.cs`、`UIManager.cs`、`MainMenuController.cs`、`LobbyController.cs`。
- **规模**: M。

#### Task 7.2 小地图/大地图与世界点位同源
- **描述**: 利用已统一的 `OnlineMapService`，让小地图/大地图/证据板都从同一份地图数据读取，不维护第二份坐标。
- **验收**: 世界任务点移动后小地图自动同步；按角色过滤信息（黑帮看暗线提示，警方看间接线索）；证据板能高亮区域。
- **依赖**: M3、Task 5.2。
- **涉及**: `OnlineMapService.cs`、`OnlineMatchHud.cs`、新 `Online/UI/MapHudController.cs`、`GameStateSnapshot.cs`。
- **规模**: M。

#### Task 7.3 行动 HUD 与交互提示收口
- **验收**: 玩家 3 秒内知道去哪；黑帮/警方目标文案不同；同位置多交互按优先级（尸体>危机修复>任务>暗线>监控>普通）；提示不互相遮挡；冷却/不可用原因显示。
- **验证**: 1366×768 / 1920×1080 / 2560×1440 截图无溢出遮挡。
- **依赖**: 7.1。
- **涉及**: `OnlineMatchHud.cs`、新 `Online/UI/InteractionPromptView.cs`、`ThemeManager.cs`。
- **规模**: M。

#### Task 7.4 会议/投票 UI 与任务小游戏外壳
- **验收**: 会议显示座位/存活死亡/投票状态/证据板/倒计时，结果动画解释平票/跳过/淘汰/是否公开身份；统一 `TaskMiniGameShell`（标题/目标/退出/进度/错误/提交），≥6 小游戏共用外壳。
- **依赖**: 5.2、7.1、M4。
- **涉及**: `OnlineMatchHud.cs`、`MeetingSync.cs`、`GameOverController.cs`、新 `Online/UI/{MeetingEvidenceBoard,TaskMiniGameShell}.cs`。
- **规模**: M-L。

#### Task 7.5 关键事件反馈与设置菜单
- **验收**: 击杀/报案/淘汰/胜负有动画音效相机短提示，结算解释胜负原因；音量/分辨率/画质/按键/语音可保存；色盲辅助对阵营/任务/破坏用非颜色标识。
- **依赖**: 7.3。
- **涉及**: `KillSystem.cs`、`OnlineAudioCueService`、`GameOverController.cs`、`SettingsManager.cs`、`SettingsData.cs`、`SettingsUIHelper.cs`。
- **规模**: M。

#### Checkpoint M7
- [ ] 默认玩家路径全 Canvas。
- [ ] 小地图/大地图/证据板与世界点位同源。
- [ ] 新玩家能理解目标/任务/报案/会议/投票。
- [ ] 核心事件有反馈，设置可保存。

---

### M8：内容与平衡

#### Task 8.1 角色分配与节奏调参
- **验收**: 5/8/10 人三档角色配置收口；默认讨论 25-35s、投票 30-40s；击杀冷却与证据目标按人数缩放；本地 5/8/10 人 AI 局各 3 局记录平均局长。
- **依赖**: M2 完成、Task 1.4。
- **涉及**: `OnlineRuleSet.cs`、`OnlineRole.cs`、`OnlineTaskService.cs`、`OnlineVictoryBridge.cs`。
- **规模**: S-M。

#### Task 8.2 警署第二张 2D 图联机化
- **描述**: 把离线警署图作为第二张 **2D** 联机图（走 M3 同一灰盒→布局→测试→美术流程）。
- **验收**: 房间可选港区/警署；警署有独立任务/暗线/监控/会议点；地图选择同步 Client；双开完成一局。
- **依赖**: M3、Task 4.6。
- **涉及**: `SocialDeduction/PoliceStation*`(参考)、`OnlineMapService.cs`、`OnlineMatchHud.cs`、`output/map_design_police_2d_v1.md`。
- **规模**: L（拆地图选择/点位配置/2D 美术/联机验证）。

#### Task 8.3 职业收敛与 Bot 升级
- **验收**: 保留 5-7 个高价值职业，每个一句话+单主技能+冷却反馈反制；黑帮 Bot 找落单/避监控/高价值点破坏，警方 Bot 做任务/修复/报案/按嫌疑投票，Bot 不明显作弊；2 真人+6 Bot×5 局不阻断。
- **依赖**: 8.1、Task 5.3。
- **涉及**: `OnlineRole.cs`、新 `Online/OnlineBotService.cs`、`OpponentAi.cs`(参考)、`MeetingSync.cs`。
- **规模**: M-L。

#### Task 8.4 平衡数据采集与迭代
- **验收**: 每局记录人数/地图/角色配置/局长/胜方/首杀时间/会议次数/任务完成率/破坏次数/断线，输出本地 CSV/JSON 不含隐私；据数据调到 8 人局 10-15 分钟、胜率 45-55(早期 40-60)、平均会议 2-4、首杀 1.5-4 分钟；≥20 局样本含真人局。
- **依赖**: Task 8.1。
- **涉及**: 新 `MatchAnalytics.cs`、`OnlineVictoryBridge.cs`、`GameOverController.cs`、`OnlineRuleSet.cs`。
- **规模**: M + 持续。

#### Checkpoint M8
- [ ] 两张 2D 图可联机。
- [ ] 职业可理解、Bot 能补位。
- [ ] 有平衡数据，局长/胜率达标。

---

### M9：封测与发行准备

#### Task 9.1 自动化编译与烟测
- **验收**: 一条命令做 Unity batchmode 编译/脚本编译检测；核心 EditMode/PlayMode 测试可运行；失败输出日志路径。
- **依赖**: M1 测试地基。
- **涉及**: `Editor/PrototypeSmokeTests.cs`、`Editor/*Tests.cs`、`scripts/`。
- **规模**: M。

#### Task 9.2 构建产物
- **验收**: macOS 或 Windows 至少一平台构建成功并含必要资源；启动进主菜单、创建/加入房间。
- **依赖**: 9.1、Task 6.2。
- **涉及**: `ProjectSettings/EditorBuildSettings.asset`、`ProjectSettings/ProjectSettings.asset`、构建脚本。
- **规模**: M。

#### Task 9.3 崩溃与日志
- **验收**: 本地日志含房间/阶段/角色/错误/断线原因，一键导出，不含敏感 token。
- **依赖**: Task 6.3。
- **涉及**: 新 `RuntimeLogCollector.cs`、`OnlineMatchController.cs`、`UnityServiceBootstrap.cs`。
- **规模**: S-M。

#### Task 9.4 封测流程与发布门槛
- **验收**: 1 页说明可开局；反馈模板收集开房/目标理解/困惑点/趣点/bug/卡顿/局长；Alpha(熟人 6-10 人稳定 5 局 P0=0) / Beta(外部不看说明玩完 3 局 P1 可控) / RC(72h 无 P0/P1，开房加入成功率>95%)。输出 `output/playtest_guide`、`playtest_feedback_template`、`release_gate`。
- **依赖**: 9.2。
- **涉及**: `output/*.md`。
- **规模**: S。

#### Checkpoint M9
- [ ] 可构建可分发可收集日志。
- [ ] 可组织封测，发布门槛清楚。

---

## 4. 2D 技术路线总览

**保留（不动）**: Netcode Host/Client、快照同步、任务/会议/胜负/Bot/证据链；玩家位置 `Vector3(x,y,0)`；`Collider2D` 碰撞；`OnlineMapService` 作为点位唯一来源；已统一的 `TryUseUnderworldPassage`；已存在的 Relay 调用。

**替换（M2-M3）**: 运行时 3D 图元生成(`PrimitiveType` + `CreatePropChild`) → 2D Tilemap/Sprite；行动相机 perspective → orthographic；3D 任务台/暗线/监控模型 → 2D sprite + 交互高亮。

**不重写**: 网络协议不为 2D 重写；任务/会议/投票不因 2D 推倒重来；角色短期可用 billboard 过渡，正式版改 2D sprite。

**渲染分层**:
- Tilemap: Floor / Walls / Props / Interactables / Overlay
- SpriteRenderer sorting: Floor < Walls < Props < Interactables < Bodies < Players < Effects < Overlay
- UI Canvas: HUD / MapOverlay / Meeting / MiniGame / Result

---

## 5. 测试矩阵

| 测试 | 频率 | 目标 |
|---|---|---|
| Unity 编译 | 每次任务完成 | 0 C# 编译错误 |
| EditMode 规则/胜负/投票 | 每次改规则服务 | 无回归 |
| 本地试玩局 | 每日 | 单机完整局不阻断 |
| Host/Client 双开 | 每个联机任务完成 | 快照/任务/会议/投票同步 |
| 2D 渲染回归 | M2 后每次世界生成改动 | 玩家/尸体/任务可读，无遮挡 |
| 灰盒多人测试 | 每张地图进美术前 | 路线/视野/击杀报案窗口成立 |
| 2D atlas/Tilemap 检查 | 2D 资产接入后 | atlas/collider/sorting 正常 |
| Relay 双机 | 每周或服务改动后 | 外网房间可用 |
| 6-10 人真人局 | M6 后每周 | 真实体验与稳定性 |
| 分辨率 UI 检查 | UI 改动后 | 无遮挡溢出 |
| 地图可读性检查 | 地图/美术/UI 大改后 | 玩家/尸体/任务/破坏/暗线可读，含色盲与黑灯 |
| 构建运行 | M9 后每次发布 | 非 Editor 可运行 |

---

## 6. Bug 优先级

- **P0**: 无法编译/开局/加入房间；对局无法结算；投票/任务/击杀致全员卡死。
- **P1**: Client 与 Host 明显不一致；任务/破坏可作弊直接完成；会议信息错误影响胜负；断线致他人无法继续；UI 阻挡核心操作；2D 替换后路线误判（可走判成不可走）。
- **P2**: 平衡偏一方；Bot 愚蠢但不阻断；动画/音频/提示缺失；局部 UI 难读但可操作。
- **P3**: 文案/装饰/轻微视觉；非核心设置缺失；低频边缘体验。

---

## 7. 目标模块结构

```text
Assets/_Project/Scripts/Online/
  OnlineMatchController.cs          # 逐步瘦身为协调层（当前 12,690 行）
  OnlineRuleSet.cs                  # 已存在：房间规则
  OnlineMapService.cs               # 已存在：地图与坐标（点位唯一来源）
  OnlineTaskService.cs              # M1.5 抽出：任务/破坏/修复
  OnlineBotService.cs               # M8 抽出：Bot 行为
  OnlineAudioCueService.cs          # 事件音效
  Rendering/
    IWorldRenderer.cs               # M2：渲染后端抽象
    Sprite2DRenderer.cs             # M2：2D 渲染实现
  MiniGames/
    OnlineMiniGameDefinition.cs     # M4
    OnlineMiniGameSession.cs        # M4
    OnlineMiniGameResult.cs         # M4
  UI/
    MapHudController.cs             # M7
    InteractionPromptView.cs       # M7
    MeetingEvidenceBoard.cs        # M7
    TaskMiniGameShell.cs           # M7

Assets/_Project/Art/2D/Harbor/{Tiles,Sprites,Interactables,Props,Characters,Effects,UIIcons}/
Assets/_Project/Art/2D/PoliceStation/...
Assets/_Project/Prefabs/Map2D/{Harbor,PoliceStation}/
Assets/_Project/Scenes/HarborGreybox2D.unity
```

---

## 8. 第一周执行表

- **Day 1**: Task 0.1 编译 + 0.2 本地局，输出基线报告（以源码为准）。
- **Day 2**: Task 0.3 双开烟测 + Task 1.1 保护 worktree（提交现有改动、清理临时脚本）。
- **Day 3**: Task 1.2 SabotageSync 去反射 + Task 1.3 语音方案 B（先移除承诺解锁进度）。
- **Day 4**: Task 1.4 最小 EditMode 测试（规则/胜负/投票）。
- **Day 5**: Task 2.1 行动相机正交化 + 双开回归。
- **Day 6-7**: Task 2.2 起步（渲染抽象接口 + 玩家/尸体 2D），修 M0/M1 遗留 P0/P1，决定是否进 M3。

---

## 9. 明确不做（M0-M6 期间）

- 商城、皮肤、账号系统。
- 第三、第四张地图。
- 专用服务器、移动端适配。
- 写实级 3D 建筑资产、大规模装饰精修。
- 灰盒未通过前替换整张地图 2D 美术。
- 让 UI 与世界地图维护两份坐标。
- 非必要的控制器全量重写（按 M1.5 增量抽服务即可）。

---

## 10. 最终成功标准

- 6-10 人通过房间码稳定进同一局；8 人局平均 10-15 分钟。
- 每局至少发生任务推进、破坏/修复、击杀/报案、会议投票。
- 任务不是单一按键校验，≥6 种真实联机小游戏。
- 死亡玩家不立刻退出体验（鬼魂/观战）。
- 会议有证据可讨论，不只瞎猜。
- 黑帮有暗线和破坏但可被推理反制。
- UI 默认路径不依赖 OnGUI。
- **港区与警署两张地图均为 2D，通过灰盒多人测试并完成第一版 tile/sprite 美术替换。**
- 小地图、大地图、会议证据板与世界点位使用同一份地图数据（`OnlineMapService`）。
- README 对外承诺与代码能力一致（尤其语音）。
- 72 小时封测无 P0/P1。
