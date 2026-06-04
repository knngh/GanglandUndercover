# Stage 11: 击杀机制 + Sabotage 破坏 UI

## 概览

在现有 `OnlineMatchController` 的击杀与破坏管道基础上，新增三个独立模块：

| 模块 | 文件 | 职责 |
|------|------|------|
| **KillSystem** | `KillSystem.cs` | 客户端击杀 UI（靠近目标显示按钮）、击杀效果、冷却管理 |
| **SabotagePanel** | `SabotagePanel.cs` | Gang 专属破坏面板（熄灯/封锁/断讯）、独立冷却 |
| **SabotageSync** | `SabotageSync.cs` | 破坏同步监听、Crewmate 修复提示 UI、状态轮询 |

---

## 一、KillSystem — 击杀系统

### 文件路径
`Assets/_Project/Scripts/Online/KillSystem.cs`

### 功能清单

| 功能 | 状态 | 说明 |
|------|------|------|
| 靠近目标 1.5m 显示"击杀"按钮 | ✅ 已实现 | `killRange = 1.5f`，Inspector 可配 |
| 击杀动画/效果 | ✅ 已实现 | 血迹 Sprite + 击杀音效（`SoundEffect.Kill`） |
| 尸体生成 | ✅ 复用 | 复用现有 `OnlineBodyState` + `BroadcastSnapshot` |
| 击杀冷却 18 秒 | ✅ 已实现 | `killCooldownSeconds = 18f`，Inspector 可配 |
| 联机同步 | ✅ 复用 | 通过 `SendClientAction(OnlineActionType.Kill)` → `TryKill` |

### 与现有系统集成

```
KillButton 点击
  → OnlineMatchHud.RequestKill()
  → OnlineMatchController.SendClientAction(Kill)
  → 服务端 ApplyClientAction → TryKill(sender, victim)
  → 设置 victim.Alive=false，spawn OnlineBodyState
  → BroadcastSnapshot（12.5Hz 同步所有客户端）
```

### 服务端 TryKill 流程（已有逻辑，无需修改）

- 检查 `phase == Action`、玩家存活、Gang 角色
- 冷却检查：`killCooldowns[sender] > 0` 则拒绝（默认 34s 联机 / 8s 离线）
- `TryFindNearestVictim`：KillRange=0.9m，找最近非 Gang 存活玩家
- 设置死亡：`victim.Alive = false`、生成 Body、记录击杀
- `EvaluateWinConditions`

---

## 二、SabotagePanel — 破坏 UI

### 文件路径
`Assets/_Project/Scripts/Online/SabotagePanel.cs`

### 破坏类型与配置

| 破坏类型 | 显示名 | 持续时间 | 冷却时间 | 效果 |
|----------|--------|----------|----------|------|
| Blackout | 熄灯 | 10s | 30s | 全图视野降低 |
| Lockdown | 封锁 | 15s | 45s | 随机区域门封锁 |
| Communications | 断讯 | 10s | 40s | 禁用会议按钮 |

### 面板交互

- **显示条件**：本地玩家存活 + Gang 角色 + Action 阶段
- **打开方式**：Tab 键切换面板
- **按钮状态**：冷却中禁用，显示剩余秒数
- **独立冷却**：每个破坏类型独立冷却计时

### 触发管道

```
SabotagePanel.OnSabotageButtonClicked(type)
  → localPreview / host: ApplySabotageLocally → 反射设置 sabotageTimer
  → client: SendClientAction(OnlineActionType.Ability) → 服务端处理
  → 服务端 ApplySabotageEffect(type, duration)
  → BroadcastSnapshot → 所有客户端收到 sabotage 计时器
```

### 与现有 ApplySabotageEffect 的关系

`OnlineMatchController` 已有五种破坏类型实现：

- `blackoutTimer`：Tick 减少，使 `IsBlackout` 为 true
- `lockdownTimer`：Tick 减少，封锁 Bot 移动
- `communicationJamTimer`：Tick 减少，禁用 `TryReportOrEmergency`
- `evidenceLeakTimer` / `patrolAlertTimer`：保留现有逻辑

`SabotagePanel` 通过 Ability 管道触发服务端 ApplySabotageEffect，无需修改 `OnlineMatchController` 已有代码。

---

## 三、SabotageSync — 破坏同步 + Crewmate 提示

### 文件路径
`Assets/_Project/Scripts/Online/SabotageSync.cs`

### 功能

| 功能 | 实现方式 |
|------|----------|
| 破坏状态检测 | 轮询（0.5s 间隔）反射读取 `OnlineMatchController` 的 sabotage timer 字段 |
| 状态变更通知 | `previousTimers` 快照对比，检测 active→inactive 转换 |
| Crewmate 修复提示 | `repairHintPanel` 显示修复指引文字（4s 持续） |
| Gang 确认提示 | `AddCaseLog` 记录破坏触发事件 |
| Sabotage 指示器 | Crewmate 端 `SabotageIndicator` 激活闪烁 |

### Crewmate 修复提示内容

| 破坏类型 | 提示文字 |
|----------|----------|
| Blackout | ⚠ 停电！前往配电室修复电闸（交互按E） |
| Lockdown | ⚠ 封锁！前往被封锁区域修复门锁（交互按E） |
| Communications | ⚠ 断讯！前往通讯室修复天线（交互按E） |
| EvidenceLeak | ⚠ 证据泄露！前往档案室销毁敏感文件（交互按E） |
| PatrolAlert | ⚠ 巡逻警报！前往哨站关闭警报（交互按E） |

### 同步流程

```
任一客户端触发 Sabotage
  → Host: ApplySabotageEffect (OnlineMatchController)
  → BroadcastSnapshot (12.5Hz, 包含 sabotageTimer)
  → 所有客户端: SabotageSync.CheckSabotageChanges()
  → 检测到 timer > 0 && prevStatus == "inactive"
  → Gang: ShowSabotageConfirmation
  → Crewmate: ShowRepairHint + SabotageIndicator
```

---

## 四、与现有代码的侵入性

### KillSystem.cs
- **新增文件**，零侵入。绑定到 `OnlineMatchHud` 已有的 killButton。
- 依赖现有管道：`OnlineMatchHud.RequestKill()` → `SendClientAction(Kill)`

### SabotagePanel.cs
- **新增文件**，零侵入。创建独立 Canvas 子面板。
- 通过 Ability 管道触发，复用现有 `ApplySabotageEffect`

### SabotageSync.cs
- **新增文件**，零侵入。
- 通过反射读取 `OnlineMatchController` 的私有 timer 字段（`blackoutTimer` 等）
- 通过反射调用 `AddCaseLog` 和读取 `localRole`

---

## 五、待后续优化

1. **KillSystem 距离预判**：当前客户端预判 `CheckKillRange` 为占位，实际距离判定由服务端 `TryFindNearestVictim`（KillRange=0.9m）执行。后续可改为通过 `PlayerStateSync` 的 `PlayerPositionMoved` 事件做客户端距离预判。

2. **Sabotage 精准类型同步**：当前通过 Ability 管道触发，服务端统一分配破坏类型。后续可扩展消息协议，增加 `SabotageAction` 专用消息（含 type 枚举）。

3. **修复任务绑定**：当前提示文字为指引性文本，后续可将修复逻辑绑定到 Task 系统的 `Sabotaged` 状态下，Crewmate 走到对应 Task 位置按 E 修复。

4. **尸体预制体（Body Prefab）**：当前 `OnlineBodyState` 为纯数据，场景 Visual 的生成需由 HUD 层渲染。后续可添加 `BodyVisual.cs` 读取 `bodies` 列表并渲染 2D/3D 尸体。

---

## 产出文件清单

```
Assets/_Project/Scripts/Online/KillSystem.cs          — 击杀系统
Assets/_Project/Scripts/Online/SabotagePanel.cs       — 破坏 UI 面板
Assets/_Project/Scripts/Online/SabotageSync.cs        — 破坏同步 + Crewmate 提示
output/stage11_kill_sabotage.md                        — 本文档
```