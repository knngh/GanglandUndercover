# Stage 11：通风管系统（Vent System）

## 目标
实现 Among Us 核心机制：Impostor（本项目中为 Gang 阵营）可通过通风管在地图两点间瞬移，包含单机原型模式和联机模式完整支持。

---

## 实现内容

### 1. VentSystem.cs（核心脚本）
**路径**：`Assets/_Project/Scripts/SocialDeduction/VentSystem.cs`

**核心类**：
- `VentNode`：通风管节点（Name / Position / ConnectedNodeIndices / VisualTransform）
- `VentSystem`：通风管系统管理器

**功能**：
- 管理所有通风管节点及连通关系
- 玩家进入通风管 → 显示可选目标列表 → 选择后瞬移
- 权限控制：仅 `SocialRole.Gang` 可用，Crewmate（Police）不可用
- 进出通风管 0.5s 黑屏过渡动画（`VentTransitionState` 状态机）
- 冷却时间：10 秒（`VentCooldownSeconds`）
- 3D 可视化：每个节点生成绿色格栅立方体 + 连线（`BuildVisuals()`）
- 每帧 `Tick()` 更新冷却和过渡状态

**状态机**：
```
Idle → Entering（黑屏0.5s）→ Choosing（选择目标）→ Traveling（黑屏0.5s）→ Idle
```

---

### 2. SocialPrototypeController.cs 修改
**路径**：`Assets/_Project/Scripts/SocialDeduction/SocialPrototypeController.cs`

**修改点**：

| 位置 | 修改内容 |
|------|---------|
| 字段声明（行60附近） | 新增 `private VentSystem ventSystem;` |
| `Update()` | 新增 `TickVentSystem();` 调用 |
| `HandleInput()`（行824） | 新增 `V` 键 → `TryVentAction();` |
| `BuildWorld()` | 新增 `CreateVents();` 调用 |
| `BuildInteractionPrompt()` | 新增通风管状态提示（`BuildVentPrompt()`） |
| 新增方法区块 | `CreateVents()` / `TickVentSystem()` / `TryVentAction()` / `ShowVentDestinationMenu()` / `TravelVent()` / `BuildVentPrompt()` |
| `ClearWorld()` | 新增 `ventSystem` 清理逻辑 |

**6个通风管节点**（原型地图）：
| 节点 | 位置 | 连通节点 |
|------|------|---------|
| 货柜码头通风管 | (-7.2, 3.8) | 1, 4 |
| 夜市巷通风管 | (-2.1, 5.2) | 0, 2, 5 |
| 专案办公室通风管 | (3.5, 4.1) | 1, 3 |
| 证物库通风管 | (-4.8, -2.5) | 2, 5 |
| 地下诊所通风管 | (6.2, -3.1) | 0, 3 |
| 主街通风管 | (0.5, 0.2) | 1, 4 |

---

### 3. SocialCharacter.cs 修改
**路径**：`Assets/_Project/Scripts/SocialDeduction/SocialCharacter.cs`

- 新增 `public bool isInsideVent;` 字段，标记角色是否正在通风管内

---

### 4. OnlineMatchController.cs 修改（联机模式）
**路径**：`Assets/_Project/Scripts/Online/OnlineMatchController.cs`

**修改点**：

| 位置 | 修改内容 |
|------|---------|
| `OnlineActionType` 枚举 | 新增 `Vent` 值 |
| 字段声明 | 新增 `ventCooldowns` 字典（冷却管理） |
| `ReadLocalActions()` | 新增 `V` 键 → `SendClientAction(OnlineActionType.Vent)` |
| `ApplyClientAction()` | 新增 `Vent` 分支 → `TryVent()` |
| 新增方法 | `TryVent()` / `TickVentCooldowns()` / `OnlineVentNode` 结构体 / `OnlineVents[]` 数据 |
| `TickCooldowns()` | 新增 `TickVentCooldowns()` 调用 |
| 紧凑HUD提示 | 按键提示新增 `V` |

**6个联机通风管节点**（设计坐标系）：
| 节点 | 位置(design) | 连通节点 |
|------|-------------|---------|
| 监控室通风管 | (-9.25, 2.12) | 1, 3 |
| 茶餐厅通风管 | (-5.0, 1.9) | 0, 2, 5 |
| 夜市通风管 | (-1.1, 3.2) | 1, 4 |
| 后巷通风管 | (5.3, -1.1) | 0, 4, 5 |
| 电房通风管 | (8.25, 5.2) | 2, 3 |
| 集合点通风管 | (-0.8, -0.35) | 1, 3 |

**联机同步机制**：
- 使用现有 `CustomMessagingManager.SendNamedMessage` 管道
- `OnlineActionType.Vent` 通过 `SendClientAction` → `ApplyClientAction` 执行
- 服务器验证权限（仅 Gang）→ 执行瞬移 → 更新 `player.Position` → `BroadcastSnapshot()`
- 冷却通过 `ventCooldowns` 字典管理，在 `TickCooldowns()` 中统一递减

---

### 5. OnlineMatchHud.cs 修改（联机HUD）
**路径**：`Assets/_Project/Scripts/Online/OnlineMatchHud.cs`

**修改点**：
- 字段声明：新增 `private Button ventButton;`
- 动作按钮区：新增 `V 通风管` 按钮，绑定 `RequestAction(OnlineActionType.Vent)`

---

## 操作指南

### 单机原型模式
1. 打开 `SocialPrototype` 场景
2. 确保玩家角色为 **Gang** 阵营（在 `SocialPrototypeController` 中设置）
3. 靠近通风管节点（绿色格栅立方体）按 **V** 键
4. 自动瞬移到第一个连通节点（当前简化为自动选择，可扩展为选择菜单）
5. 冷却10秒后可再次使用

### 联机模式
1. 启动联机对局，选择 **Gang** 阵营角色
2. 靠近通风管节点按 **V** 键
3. 服务器验证权限和冷却 → 执行瞬移
4. 所有客户端通过 `BroadcastSnapshot` 同步位置
5. HUD 动作栏显示 **V 通风管** 按钮（非 Gang 阵营点击无效）

---

## 文件清单

| 文件 | 状态 | 说明 |
|------|------|------|
| `Assets/_Project/Scripts/SocialDeduction/VentSystem.cs` | 新增 | 通风管系统核心逻辑 |
| `Assets/_Project/Scripts/SocialDeduction/SocialPrototypeController.cs` | 修改 | 集成通风管到原型模式 |
| `Assets/_Project/Scripts/SocialDeduction/SocialCharacter.cs` | 修改 | 新增 isInsideVent 字段 |
| `Assets/_Project/Scripts/Online/OnlineMatchController.cs` | 修改 | 联机通风管同步逻辑 |
| `Assets/_Project/Scripts/Online/OnlineMatchHud.cs` | 修改 | 联机HUD通风管按钮 |

---

## 待扩展功能
- [ ] 通风管选择菜单 UI（当前自动选第一个连通节点）
- [ ] 通风管进入/退出动画（当前用黑屏过渡）
- [ ] 联机模式下通风管视觉表现同步（其他玩家看到格栅特效）
- [ ] 通风管被破坏/封锁机制（Sabotage）
- [ ] 更多地图的通风管拓扑配置
