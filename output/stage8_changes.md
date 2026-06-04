# Stage 8 变更报告：主菜单与完整游戏流程

**日期**: 2026-06-01
**产出目录**: /Users/zhugehao/projects/GanglandUndercover/output/

---

## 文件变更清单

| 文件 | 类型 | 行数 | 说明 |
|------|------|------|------|
| `Assets/_Project/Scripts/UI/MainMenuController.cs` | 新建 | ~230 行 | OnGUI 主菜单，离线/联机模式入口 |
| `Assets/_Project/Scripts/UI/GameOverController.cs` | 新建 | ~260 行 | 游戏结算界面，身份揭示与统计 |
| `Assets/_Project/Scripts/UI/LobbyController.cs` | 新建 | ~370 行 | 联机大厅，房间创建/加入/玩家列表 |
| `Assets/_Project/Scripts/Gameplay/PrototypeBootstrap.cs` | 重写 | ~270 行 | 菜单驱动架构重构，原 174 行 → 270 行 |

## 架构变更

### 之前（Stage 6）
```
Awake → _mode 枚举判断 → BuildOfflinePrototype() 或 BuildOnlinePrototype()
                                   ↓                        ↓
                        SocialPrototypeController    OnlineMatchController
                        (AutoStartOnAwake=true)      (立即进入连接面板)
```
场景启动后直接进入游戏，没有主菜单，无法自然退出回到起点。

### 之后（Stage 8）
```
Awake → CreateMainMenu() → MainMenuController (显示主菜单)
                                |
                    +-----------+-----------+
                    |                       |
            StartOfflineGame()      StartOnlineGame()
                    |                       |
        BuildOfflinePrototype()     BuildOnlinePrototype()
                    |                       |
        GameOverController           LobbyController
                    |                       |
                游戏运行              连接 → 大厅 → 游戏
                    |                       |
              IsGameOver              Phase == Result
                    |                       |
              GameOver 结算           GameOver 叠加按钮
                    |                       |
              ReturnToMainMenu() ← 用户点击返回主菜单
```

## 各控制器详细设计

### 1. MainMenuController.cs

**职责**：场景入口 UI，100% OnGUI 绘制，依赖关系单一（仅引用 PrototypeBootstrap）。

| 区域 | 内容 |
|------|------|
| 标题区 | "港区潜线" + "Gangland Undercover" + 副标题 |
| 离线区 | 身份选择（卧底/黑帮/警察）+ 身份说明 + "开始游戏"按钮 |
| 联机区 | "进入大厅"按钮 + Unity Netcode 提示 |
| 页脚 | 版本号 + 模式说明 |

**公开方法**：
- `Initialize(PrototypeBootstrap)` — 绑定 Bootstrap 引用
- `Show()` / `Hide()` — 控制菜单可见性

**模式切换**：点击"开始游戏"→ Bootstrap.StartOfflineGame(role) → 菜单隐藏。返回主菜单时 Show() 重新激活。

### 2. LobbyController.cs

**职责**：联机模式大厅 UI，包裹 OnlineMatchController 的网络操作。

**两阶段 UI**：

| 阶段 | 触发条件 | UI 内容 |
|------|---------|---------|
| 连接面板 | `!IsOnline` | 玩家名/房间名/直连地址/Relay码输入框；Host/Client/Relay开房/Relay加入/本地试玩 按钮；Relay 状态行 |
| 大厅面板 | `IsOnline && Phase == Lobby` | 房间名标题、就绪摘要、玩家列表（名称+Ready标记+AI标记）、Ready/开始/离开按钮、进度信息 |

**与 OnlineMatchController 的交互**：
- 通过 `FindAnyObjectByType<OnlineMatchController>()` 延迟绑定
- 输入框变更实时写入 OnlineMatchController（SetLocalPlayerName 等）
- 按钮事件代理到 OnlineMatchController（RequestHost / RequestClient / RequestRelayHost / RequestRelayClient / RequestLocalPreview / ToggleLocalReady / RequestStartMatch）
- `Update()` 中检测 `Phase != Lobby` 自动隐藏，OnlineMatchController 接管游戏内 UI

### 3. GameOverController.cs

**职责**：监控游戏结束状态，展示结算界面。

**双模式结算**：

| 模式 | 检测方式 | UI |
|------|---------|-----|
| 离线 | `SocialPrototypeController.IsGameOver` | 全屏半透明遮罩 + 居中面板：游戏结束标题 → 结果文本 → 统计信息（证据链/任务/卧底暴露/黑帮热度） → 身份揭示（角色名+存活状态，按阵营着色） → 返回主菜单按钮 |
| 联机 | `OnlineMatchController.Phase == Result` | 仅叠加"返回主菜单"按钮于 OnlineMatchController 已有的结果界面上 |

**身份揭示着色**：黑帮=红色系 / 警察=蓝色系 / 卧底=灰色系 / 胜方标题色调自适应。

**引用方式**：`FindAnyObjectByType` 延迟绑定，支持 Bootstrap 销毁后安全退回。

### 4. PrototypeBootstrap.cs（重写）

**核心变更**：

| 项 | 原逻辑 | 新逻辑 |
|----|--------|--------|
| Awake | 按 `_mode` 枚举直接启动游戏 | 仅创建 MainMenuController |
| 离线启动 | `BuildOfflinePrototype()` 由 Awake 调用 | 改为 public `StartOfflineGame(role)` 由 MainMenuController 调用 |
| 联机启动 | `BuildOnlinePrototype()` 由 Awake 调用 | 改为 public `StartOnlineGame()` 由 MainMenuController 调用，同时创建 LobbyController |
| 游戏返回 | 无 | public `ReturnToMainMenu()` — 销毁所有控制器对象并重新显示主菜单 |

**新增成员**：
- `_mainMenuController` / `_gameOverController` / `_lobbyController` 字段缓存
- `StartOfflineGame(SocialRole)` / `StartOnlineGame()` / `ReturnToMainMenu()` 公共方法
- `CreateMainMenu()` / `CreateGameOverController()` / `CreateLobbyController()` 工厂方法
- `DestroyActiveGame()` — 批量销毁所有运行时游戏组件，确保模式切换无残留
- `DestroyController()` / `DestroyControllerObject()` — 统一销毁辅助方法

**向后兼容**：
- `_mode` / `_offlinePlayerRole` SerializeField 保留，Inspector 中仍可编辑（但 Awake 中不再读取 _mode）
- 保留 `GameMode` 枚举不变
- 保留 ResourceMirror Editor 代码不变
- 保留 EnsureEventSystem / EnsureCamera / EnsureLight 基础设施

## 完整游戏流程

```
启动场景
   ↓
MainMenu (主菜单)
   ├─ [离线模式] → 选择身份 → StartOfflineGame
   │    ├─ 销毁旧对象
   │    ├─ BuildOfflinePrototype: 创建 SocialPrototypeController (AutoStartOnAwake=false)
   │    ├─ StartOfflineMode(role): 初始化回合制控制器 + 3D 场景 + 角色
   │    ├─ CreateGameOverController
   │    └─ 游戏运行中... IsGameOver → GameOverController 显示结算
   │
   └─ [联机模式] → 进入大厅 → StartOnlineGame
        ├─ 销毁旧对象
        ├─ BuildOnlinePrototype: 创建 OnlineMatchController + UnityServiceBootstrap + OnlineSyncManager
        ├─ CreateGameOverController
        ├─ CreateLobbyController
        ├─ LobbyController 显示连接面板
        │    └─ 选择 Host/Client/Relay → 连接到房间 → Phase=Lobby → 显示大厅面板
        │         └─ Ready / 等待开始 → Phase != Lobby → LobbyController 隐藏
        └─ 游戏运行中... Phase=Result → GameOverController 叠加返回按钮

任意结算界面 → [返回主菜单] → ReturnToMainMenu
   ├─ DestroyActiveGame (销毁所有运行时对象)
   ├─ 销毁 GameOverController / LobbyController
   └─ MainMenuController.Show() → 回到步骤 2
```

## 依赖关系

```
MainMenuController ──→ PrototypeBootstrap
GameOverController ──→ PrototypeBootstrap
                     └→ SocialPrototypeController (FindAnyObjectByType)
                     └→ OnlineMatchController (FindAnyObjectByType)
LobbyController ────→ OnlineMatchController (FindAnyObjectByType)
PrototypeBootstrap ─→ MainMenuController
                   └→ GameOverController
                   └→ LobbyController
```

- 所有 UI 控制器通过 GameObject.Find 或 Inspector 引用方式对接，无硬编码路径
- MainMenuController 通过 Bootstrap.Initialize() 注入引用，其余通过 FindAnyObjectByType 延迟绑定

## 未完成 / 待验证

1. **Unity Editor 编译测试**：新文件需在 Unity Editor 中打开项目确认编译通过
2. **联机网络对接**：LobbyController 依赖 OnlineMatchController 的 Relay 初始化状态，需在真实网络环境下验证连接流程
3. **场景切换**：当前设计在同一场景内通过 GameObject 创建/销毁实现模式切换，未使用 Unity SceneManager。若后续需要多场景架构，需调整 DontDestroyOnLoad 策略
