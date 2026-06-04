# 基线债务登记册

- 日期: 2026-06-04
- 来源: `game_development_plan_2d_full_20260604.md` §0.3
- 项目路径: `/Users/zhugehao/projects/GanglandUndercover`

---

## 债务清单

| 编号 | 描述 | 证据（源码行号/文件） | 归属里程碑 | 优先级 |
|---|---|---|---|---|
| **D-01** | **控制器超重** — `OnlineMatchController.cs` 达 12,673 行，仍含世界生成/相机/Bot/会议/快照/OnGUI 等多种不相关职责。单体类难以维护、测试、多人协作。 | `OnlineMatchController.cs` 全文 12,673 行；世界生成 `:7301+/7406+`；相机 `:374-467/7029-7060`；Bot 散布全文；OnGUI 段散布 | M2（增量瘦身） | P1 |
| **D-02** | **破坏 timer 状态重复** — 控制器自有 `blackoutTimer` 等字段并倒计时+序列化，`OnlineTaskService` 也持有一份。两处各自倒计时，存在不一致风险，快照/迁移时可能丢失状态。 | 控制器 `:407/:463/:2185-2197/:2361/:2480`；服务 `:80-84` | M1（Task 1.2） | P0 |
| **D-03** | **渲染层仍 3D 图元** — 世界生成仍使用 `PrimitiveType.Cube/Sphere/Cylinder` + 带 z 深度 `CreatePropChild`，与 2D 重做方向不一致。 | 控制器 `:7301+` / `:7406+` | M3（Task 3.2） | P1 |
| **D-04** | **行动相机透视** — 预览态 orthographic，行动态 `fieldOfView` 透视。2D 重做要求全程正交。 | 控制器 `:7029-7060`、`:374-467` | M3（Task 3.1） | P1 |
| **D-05** | **联机小游戏缺失** — 13 个小游戏仅在 `SocialDeduction/MiniGames/`，联机端 `Online/MiniGames/` 目录不存在。联机对局无法进行任务小游戏交互。 | 目录 `SocialDeduction/MiniGames/` 存在 13 个类(3,487行)；`Online/MiniGames/` 缺失 | M5（Task 5.1/5.2） | P0 |
| **D-06** | **监控仅离线** — `SecurityCamera.cs`(383行) 仅在离线循环使用，联机无监控系统。 | `SocialDeduction/SecurityCamera.cs` 全文 383 行；Online 目录无对应实现 | M5（Task 5.4） | P2 |
| **D-07** | **语音 stub 但 UI 仍承诺** — Vivox 已移除（`UnityServiceBootstrap.cs:31-35`），但 README 仍写近距离语音；`VoiceChatSystem.cs`(1080行) 纯本地无 NetworkBehaviour。UI 承诺与实现严重不一致。 | `UnityServiceBootstrap.cs:31-35` Vivox 注释移除；`VoiceChatSystem.cs` class 声明无 NetworkBehaviour；README "Current Playable Loop" 段 | M1（Task 1.3） | P1 |
| **D-08** | **ChatSystem 非网络类** — `ChatSystem.cs:19` class 声明无 NetworkBehaviour/RPC，文本聊天不能联机同步。 | `ChatSystem.cs:19` `class ChatSystem`（非网络类） | M1（Task 1.4，若选方案 B） | P1 |
| **D-09** | **Host Migration 未联调** — `HostMigrationManager.cs`(452行) 结构完整但无联机验证，心跳/选举/快照恢复均未在真实网络环境测试。 | `HostMigrationManager.cs` 全文 452 行 | M7（Task 7.2） | P2 |
| **D-10** | **单元测试近零** — 仅 1 个 smoke 测试文件 `PrototypeSmokeTests.cs`，无 EditMode/PlayMode 测试覆盖规则/胜负/任务/坐标等核心逻辑。 | `Assets/_Project/Editor/PrototypeSmokeTests.cs`；无 `Editor/Tests/` 目录 | M1（Task 1.5） | P1 |
| **D-11** | **仓库卫生** — 约 19,500+ 未提交改动（多为 Kenney CityKit 二进制）+ 根目录临时脚本 `temp_fix_calls.py` / `temp_fix2.py`。仓库不可 clean clone 使用。 | `git status` 显示 19,500+ 未提交；根目录 `temp_fix_calls.py` / `temp_fix2.py` | M1（Task 1.1） | P1 |
| **D-12** | **离线/联机双循环重叠** — `SocialPrototypeController`(3,848) 与 `OnlineMatchController`(12,673) 各有完整循环；2D 化需明确是否两套都改。 | `SocialPrototypeController.cs` 全文 3,848 行；`OnlineMatchController.cs` 全文 12,673 行；重叠分析见 `offline_cycle_decision_20260604.md` | M0（Task 0.2 已决策） | P1 |

---

## 优先级定义

| 级别 | 含义 | 处理策略 |
|---|---|---|
| **P0** | 阻塞下一里程碑的核心功能缺陷 | 当前里程碑内必修 |
| **P1** | 架构/质量重大债务，影响后续开发效率 | 归属里程碑内必修 |
| **P2** | 可推迟但不修复会累积风险 | 排期修复，不阻塞里程碑通过但需登记 |

---

## 按里程碑分布

| 里程碑 | 债务编号 | 数量 |
|---|---|---|
| M0（已在本轮决策） | D-12 | 1 |
| M1 | D-02, D-07, D-08, D-10, D-11 | 5 |
| M2 | D-01 | 1 |
| M3 | D-03, D-04 | 2 |
| M5 | D-05, D-06 | 2 |
| M7 | D-09 | 1 |

---

## 交叉核对记录

| 债务编号 | 核对日期 | 核对结果 |
|---|---|---|
| D-01 | 2026-06-04 | 确认 12,673 行，世界生成/相机/Bot 逻辑仍在控制器内 |
| D-02 | 2026-06-04 | 确认控制器 `:407/:463` 和 TaskService `:80-84` 均持有 timer 字段 |
| D-03 | 2026-06-04 | 确认 `:7301+/7406+` 存在 PrimitiveType 世界生成 |
| D-04 | 2026-06-04 | 确认 `:7029-7060` 行动态 fieldOfView 配置 |
| D-05 | 2026-06-04 | 确认 `SocialDeduction/MiniGames/` 存在 13 个小游戏，`Online/MiniGames/` 缺失 |
| D-06 | 2026-06-04 | 确认 `SecurityCamera.cs` 仅在 SocialDeduction，Online 无引用 |
| D-07 | 2026-06-04 | 确认 Vivox 已移除 (`:31-35`)，README 仍写语音 |
| D-08 | 2026-06-04 | 确认 `ChatSystem.cs:19` 为非网络类 |
| D-09 | 2026-06-04 | 确认 `HostMigrationManager.cs` 存在但未联调 |
| D-10 | 2026-06-04 | 确认仅 1 个 smoke 测试文件 |
| D-11 | 2026-06-04 | 确认 git status 有大量未提交二进制和临时脚本 |
| D-12 | 2026-06-04 | 已决策：离线保留为沙盒，不投入2D美术（见 `offline_cycle_decision_20260604.md`） |
