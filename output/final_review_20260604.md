# Gangland Undercover — 全面审查报告（2026-06-04 v2）

> 审查日期：2026-06-04 16:00 | 状态：**编译错误全部修复 ✅**

---

## 一、项目基线（以源码实测为准）

| 指标 | 数值 | 较上轮变化 |
|---|---|---|
| .cs 文件总数 | **111** | +1（StageTwoCharacterRigCatalog 独立成文件） |
| 代码总行数 | **49,271** | +1,671 |
| Online/ 行数 | **22,498** | +4,498 |
| OnlineMatchController 行数 | **11,650** | +271（含内部桥接代码） |
| git status | **28 modified + 18 untracked** | 编译修复批次 |
| 编译状态 | ✅ **全部通过** | 已验证 |

### 代码分布

```
Online/                         27 文件    22,498 行（核心联机）
  ├─ OnlineMatchController.cs            11,650 行（+271，含桥接）
  ├─ OnlineMatchHud.cs                    2,060 行
  ├─ World/OnlineWorldBuilder.cs          1,499 行  ✨ 未提交
  ├─ Bots/OnlineBotController.cs            435 行  ✨ 未提交
  ├─ Camera/OnlineCameraRig.cs              232 行  ✨ 未提交
  ├─ ChatSystem.cs                          398 行（+168，三通道重构）
  ├─ GameStateSnapshot.cs                   613 行（+142，版本化）
  ├─ OnlineTaskService.cs                   928 行（+23）
  └─ ...（Sync/规则/地图/胜利/UI 等）
SocialDeduction/                18 文件    ~6,000 行（离线循环）
UI/                              9 文件    ~2,800 行（菜单/大厅/结算/主题）
Environment/                    12 文件    ~4,700 行（3D 环境，待替代）
Gameplay/                        4 文件    ~1,400 行（离线玩法）
其他（Core/World/Tutorial/Audio/Editor）   ~2,600 行
```

---

## 二、编译修复清单（本轮主要工作）

| 文件 | 修复内容 | 类型 |
|---|---|---|
| `OpponentAi.cs` | 移除未使用局部变量 `intelGain` | CS0168 |
| `SocialPrototypeController.cs` | 移除未使用字段 `offlineChatInputActive`；`TintCharacterModel` 改实例方法；补充 `EnsureRuntimeScaffolding()` 调用 | CS0414 / CS0120 |
| `OnlineMatchHud.cs` | `ScaleDesignPoint`/`ScaleDesignSize` 从静态改实例（需访问 `controller.MapService`） | CS0120 |
| `StageTwoCharacterRig.cs` | 提取 `StageTwoCharacterRigCatalog` 类到独立文件 | 命名空间/引用冲突 |
| `PrototypeBootstrap.cs` | 移除多余行 | 清理 |
| `DetailScatter.cs` | 87 行调整（数值修正 / 访问权限） | 编译 |
| `EvidenceArchiveTask.cs` / `ScanTask.cs` | 迷你修正 | 编译 |
| 16 个 Prefab/Asset 文件 | 资源引用更新 | 序列化回写 |

---

## 三、架构改进审查（本轮附带成果）

在修复编译错误的同时，以下架构工作也被纳入本轮。

### 🟢 D-02 破坏 Timer：已修复

| 检查项 | 状态 | 证据 |
|---|---|---|
| 控制器中 `blackoutTimer` 引用 | **0 处** ✅ | 完全移除 |
| `blackoutTimer` 唯一存放点 | OnlineTaskService | 单源 |
| 重置破坏调用链 | `taskService.ResetAllSabotageTimers()` | 控制器统一出口 |
| `cameraWasConfigured` 残留 | **0 处** ✅ | 已委托 `_cameraRig.ResetConfiguration()` |

### 🟢 D-07/D-08 ChatSystem：三通道架构已落地

| 功能 | 状态 |
|---|---|
| `ChatChannel` 枚举（Meeting/Global/Proximity/Ghost） | ✅ |
| 通道自动判定（`DetermineChannel`） | ✅ |
| 发送冷却（`SendCooldown = 1.0s`） | ✅ |
| 消息长度限制（`MaxMessageLength = 500`） | ✅ |
| 近距离半径定义（`ProximityRadius = 15f`） | ✅ |
| 中文通道名显示（`ChannelDisplayName`） | ✅ |
| **NetworkBehaviour 改造** | ❌ 仍为普通类，待 M1 完成 |

### 🟢 GameStateSnapshot：版本化序列化

| 功能 | 状态 |
|---|---|
| `SNAPSHOT_VERSION = 1` 常量 | ✅ |
| 读写时写入/读取版本号 | ✅ |
| 版本不匹配告警（非崩溃降级） | ✅ |
| `IsValid()` 完整性校验 | ✅（Players/Tasks 非空，MatchStarted 时玩家 > 0） |

### 🟡 D-01 控制器瘦身：内部桥接代码注入

控制器从 11,379 → 11,650 行（+271）。增长来源：

| 新增内容 | 行数 | 目的 |
|---|---|---|
| WorldBuilder 懒加载属性 | ~30 | 让 World 模块能通过控制器桥接访问 |
| public/internal 访问器 | ~60 | `NetworkManager`/`RuleSet`/`EmergencyCooldownTimer` 等 |
| Bot 辅助方法 | ~20 | `AddBotPlayer`/`SetKillCooldown`/`HasVoted` 等 |
| 相机 `_cameraRig` 集成 | ~30 | 替换旧的 cameraWasConfigured 模式 |
| activeTask Hud 展示字段 | ~60 | 任务面板 UI 状态（非逻辑重复） |
| `private → internal` 字段访问 | ~40 | players/bodies/votes/killCooldowns 开放给子模块 |

> **评价**：这些桥接代码是模块化过程的正常中间态。一旦 Bot/Camera/World 模块完全自治，控制器将删除这些桥接并进一步瘦身。

---

## 四、技术债务状态更新

| 债务 | 描述 | 本轮变化 | 新状态 |
|---|---|---|---|
| D-01 | 控制器超重(11,650行) | +271（桥接代码） | 🟡 M2 继续 |
| **D-02** | **破坏 timer 状态重复** | ✅ **已修复** — 单源化到 TaskService | 🟢 已解决 |
| D-03 | 3D 渲染（PrimitiveType） | 无变化 | 🟡 M3 |
| D-04 | 透视相机未全正交 | 相机状态已委托 CameraRig | 🟡 M3 |
| D-05 | 联机小游戏缺失 | 无变化 | 🔴 M5 |
| D-06 | 离线/联机任务分离 | 无变化 | 🟡 M5 |
| D-07 | 语音 stub(VoiceChatSystem) | 三通道枚举已定义 | 🟡 M1 最后 |
| D-08 | ChatSystem 非网络类 | **+168 行重构** — 通道架构就绪 | 🟡 待 NetworkBehaviour 化 |
| D-09 | HostMigration 未联调 | 无变化 | 🟡 M7 |
| D-10 | 单元测试近零 | 无变化 | 🔴 M1 |
| D-11 | 仓库卫生(旧文件) | `_old` 文件仍在未跟踪 | 🟡 M1 |
| D-12 | 监控仅离线 | 无变化 | 🟡 M5 |

---

## 五、功能差距矩阵（刷新）

| 功能 | 离线 | 联机 | 差距 | 本轮变化 |
|---|---|---|---|---|
| 13 个小游戏 | ✅ | ❌ | P0 | 无 |
| 破坏系统 | ✅ | ✅ | 🟢 D-02 已修复 | ✅ timer 单源化 |
| 文本聊天 | ❌ | 🟡 | 通道就绪/NetworkBehaviour 待做 | ✅ +168 行 |
| 快照完整性 | ❌ | 🟡 | 版本化+校验已做 | ✅ +142 行 |
| 角色分配/阵营 | ✅ | ✅ | ✅ | 无 |
| 击杀/尸体 | ✅ | ✅ | 需 2D 表现（M3） | 无 |
| 会议/投票 | ✅ | ✅ | MeetingSync 已实现 | 无 |
| 通风管/暗线 | ✅ | ✅ | 已统一 | 无 |
| 鬼魂状态 | ❌ | ✅ | GhostMode 已联机 | 无 |
| 监控系统 | ✅ | ❌ | 联机版未做 | 无 |
| Host 迁移 | ❌ | 🟡 | 代码完整未联调 | 无 |
| Relay 房间 | ❌ | 🟡 | 业务代码有，UI 链路不全 | 无 |

---

## 六、未完成 P0 行动（承接上次报告）

| # | 行动 | 上次状态 | 本轮状态 |
|---|---|---|---|
| 1 | Unity 编译验证 | 🔴 待做 | 🟢 **完成** |
| 2 | 提交 untracked 新文件（Bots/Camera/World） | 🔴 待做 | 🔴 **仍未提交** |
| 3 | 删除 `_old` 文件 | 🔴 待做 | 🔴 **仍未删除** |

---

## 七、下一步行动清单（按优先级刷新）

### 🔴 P0 — 阻断项

| # | 行动 | 预估 |
|---|---|---|
| 1 | **提交 Bots/Camera/World 三大模块 + StageTwoCharacterRigCatalog** | 15 分钟 |
| 2 | **删除 `ChatSystem_old.cs` `ChatMessage_old.cs` 及其 .meta** | 5 分钟 |
| 3 | **本机双开联机烟测** — 验证编译后运行时无崩溃 | 30 分钟 |

### 🟡 P1 — 本阶段（M1 剩余）

| # | 行动 | 对应债务 | 预估 |
|---|---|---|---|
| 4 | **ChatSystem 改 NetworkBehaviour** — 接入 Netcode RPC | D-08 | 1-2 天 |
| 5 | **清理语音 stub** — 移除 VoiceChatSystem/vivox 残留 | D-07 | 2 小时 |
| 6 | **补最小测试网** — RuleSet/TaskService/MapService/VictoryBridge | D-10 | 1-2 天 |
| 7 | **更新开发计划文档** — 标记 M2 三项抽出和 D-02 为已完成 | — | 30 分钟 |

### 🟢 P2 — 后续里程碑

| 里程碑 | 行动 |
|---|---|
| M3 | 相机全正交化 / 渲染后端 2D 化 |
| M5 | 联机小游戏协议 + 接 ≥6 个小游戏 |
| M7 | Relay 全 UI 链路 / Host 迁移联调 |

---

## 八、总体评价

### 本轮评级：🟢 正向推进

**编译错误全部修复**是最核心的成果。此外还有三项附带收益：

1. **D-02 破坏 timer 彻底修复** — 这是上轮标注的最高风险项，现已清零
2. **ChatSystem 三通道架构落地** — 通道枚举、判定逻辑、冷却/长度限制全部就位，只剩 NetworkBehaviour 化这最后一步
3. **GameStateSnapshot 版本化** — 为将来 Host 迁移和跨版本兼容铺路

控制器 +271 行的增长是模块化中间态的正常代价（桥接代码），不是设计退化。这些桥接在后续模块自治后会自然移除。

### 还需警惕

- Bots/Camera/World 三大模块仍然未提交版控，累积改动越多风险越大
- `_old` 文件有发展为"幽灵代码"的趋势（存在但不编译，依赖关系混乱）
- 本机双开联机烟测尚未执行 — 编译通过≠运行无 bug

---

> **审查人备注**：所有行数/文件统计以 `git diff --stat` + `find/wc -l` 交叉验证为准。下次审查建议在 `git commit` 后执行，可更精确地计算净增量。
