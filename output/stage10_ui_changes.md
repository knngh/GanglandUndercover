---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9470d846c7eff9b24afb94a99a2cb3f0_80cdba8e5e2511f1a4f35254002afed2
    ReservedCode1: j9ZFzg09a3i4W4ByCifPgYuKKyT7otwaWLZQ866COedzjGRoRRMZvhPI1ml3GAz3Mr7w3IubrEb+cT3ZlYpkMW9GUf9WlzmbrkCs//tzddjAAEdO3qPQdU3JYXwYr2t0q7cIDHXwBVblwc9uHk4jwL2JwpusrVmTlRebFaQ+J3vdWuajPj2Du3JAgoo=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9470d846c7eff9b24afb94a99a2cb3f0_80cdba8e5e2511f1a4f35254002afed2
    ReservedCode2: j9ZFzg09a3i4W4ByCifPgYuKKyT7otwaWLZQ866COedzjGRoRRMZvhPI1ml3GAz3Mr7w3IubrEb+cT3ZlYpkMW9GUf9WlzmbrkCs//tzddjAAEdO3qPQdU3JYXwYr2t0q7cIDHXwBVblwc9uHk4jwL2JwpusrVmTlRebFaQ+J3vdWuajPj2Du3JAgoo=
---

# 第10阶段 — 任务B：uGUI 正式化

## 执行摘要

将 4 个 UI 控制器从 OnGUI 迁移到 uGUI Canvas，全部通过纯代码创建 UI 元素（GameObject.AddComponent），包含 fallback 到已有 Canvas 的逻辑。颜色方案统一为：Gang=红、Undercover=蓝、Police=灰、背景深色。

---

## 修改文件清单

| 文件 | 状态 | 说明 |
|---|---|---|
| `Assets/_Project/Scripts/UI/MainMenuController.cs` | ✅ 重写 | OnGUI → uGUI Canvas，对标 Among Us 主菜单风格 |
| `Assets/_Project/Scripts/UI/LobbyController.cs` | ✅ 重写 | OnGUI → uGUI Canvas，房间码输入/创建/加入/玩家列表 |
| `Assets/_Project/Scripts/UI/GameOverController.cs` | ✅ 重写 | OnGUI → uGUI Canvas，结算面板/身份揭示/统计 |
| `Assets/_Project/Scripts/UI/PrototypeHud.cs` | ✅ 增强 | 已有 uGUI，新增左上角角色信息/右上角回合日志/底部任务栏布局 |
| `Assets/_Project/Scripts/UI/UIManager.cs` | 保留 | 已有，作为 Canvas fallback 目标 |

---

## 各文件变更详情

### 1. MainMenuController.cs

**布局结构：**
- 深色背景遮罩（BgDark）
- 标题大字居中：「港区潜线」48pt 金色 + 副标题「Gangland Undercover」
- 左半屏：离线模式面板
  - 三个身份选择按钮（卧底/黑帮/警察），颜色对应蓝/红/灰
  - 身份描述动态更新
  - 「开始游戏」按钮 → 调用 `bootstrap.StartOfflineGame(role)`
- 右半屏：联机模式面板
  - 「进入大厅」按钮 → 调用 `bootstrap.StartOnlineGame()`
- 页脚版本信息

**关键方法：**
- `Initialize(bootstrap)` — 绑定 PrototypeBootstrap
- `OnRoleSelected(idx)` — 身份选择，高亮当前选中
- `OnStartOffline()` — 隐藏菜单，启动离线游戏
- `OnEnterLobby()` — 隐藏菜单，启动联机流程
- `GetOrCreateCanvas()` — fallback：优先 UIManager.MainCanvas → 已有 Canvas → 新建

---

### 2. LobbyController.cs

**布局结构：**
- 标题：「联机大厅」居中
- 房间码 InputField（placeholder：「输入 4~6 位房间码」）
- 「创建房间」+「加入房间」并排按钮
- 状态文本（等待操作... / 房间已创建 / 正在加入房间）
- 玩家列表区域（最多显示 4 个占位条目）
- 「开始游戏」按钮（默认 interactable=false，需联机逻辑启用）
- 「返回主菜单」按钮

**关键方法：**
- `Initialize(bootstrap, onlineManager)` — 绑定
- `RefreshPlayerList()` — 刷新玩家列表（从 OnlineSyncManager 读取）
- `OnCreateRoom()` / `OnJoinRoom()` — 创建/加入房间（TODO: 接入 OnlineSyncManager）
- `OnStartOnlineGame()` — 开始联机游戏
- `OnBackToMenu()` — 返回主菜单

---

### 3. GameOverController.cs

**布局结构：**
- 半透明深色遮罩
- 结果标题大字（如「黑帮胜利」，颜色对应阵营）
- 结果详情（关键事件列表）
- 身份揭示区域（所有玩家身份一览，颜色标注）
- 本局统计（回合数/游戏时长/任务完成率/会议次数）
- 「返回主菜单」+「再来一局」按钮

**关键方法：**
- `Initialize(bootstrap, gameController, onlineManager)`
- `RefreshResult()` — 从 GameController 读取胜利阵营，更新标题/详情
- `RefreshRoleReveal()` — 揭示所有玩家身份
- `RefreshStats()` — 显示本局统计
- `OnBackToMenu()` / `OnReplay()`

---

### 4. PrototypeHud.cs（增强，非重写）

**原有结构保留：**
- Canvas（ScreenSpaceOverlay, 1280×720）
- 角色选择面板（RolePanel）
- 游戏面板（GamePanel）
- 地区列表 + 操作列表（VerticalLayoutGroup 驱动）

**新增/调整：**
- 左上角角色信息面板（RoleInfoPanel）：显示当前身份/天数/证据数/掩护度，颜色随身份变化
- 右上角回合日志面板（LogPanel）：显示最近操作日志
- 底部语言切换按钮位置调整
- 颜色方案统一：Gang=红(0.78,0.22,0.16)、Undercover=蓝(0.08,0.62,0.82)、Police=灰(0.55,0.55,0.62)
- `FactionColor(faction)` 静态方法统一颜色

**Rebuild() 增强：**
- 左上角角色信息实时更新
- 右上角日志实时更新
- 统计信息显示警察热度/货运进度/怀疑度/公众信任等

---

## 颜色方案（统一）

| 阵营/用途 | RGB | 说明 |
|---|---|---|
| Gang（黑帮） | (0.78, 0.22, 0.16) | 红色 |
| Undercover（卧底） | (0.08, 0.62, 0.82) | 蓝色 |
| Police（警察） | (0.55, 0.55, 0.62) | 灰色 |
| 背景深色 | (0.015, 0.022, 0.025) | 近黑 |
| 面板背景 | (0.042, 0.055, 0.058) | 深蓝灰 |
| 强调橙 | (0.86, 0.48, 0.13) | 橙色（按钮） |
| 文本主色 | (0.92, 0.94, 0.93) | 近白 |
| 文本次要 | (0.52, 0.55, 0.54) | 灰绿 |

---

## Canvas Fallback 策略

所有控制器均实现 `GetOrCreateCanvas()` 方法，查找优先级：

1. `FindAnyObjectByType<UIManager>()` → `ui.MainCanvas`
2. `FindAnyObjectByType<Canvas>()` → 已有 Canvas
3. 新建 `GameObject("XxxCanvas_Fallback")`，挂载 `Canvas` + `CanvasScaler`（1920×1080）+ `GraphicRaycaster`

---

## 待接入的联机逻辑（TODO）

以下方法当前为占位实现，需后续接入 `OnlineSyncManager`：

- `LobbyController.OnCreateRoom()` — 调用 `_onlineManager.CreateRoom()`
- `LobbyController.OnJoinRoom()` — 调用 `_onlineManager.JoinRoom(code)`
- `LobbyController.RefreshPlayerList()` — 从 `_onlineManager.ConnectedPlayers` 读取
- `LobbyController._startButton` — 根据房间人数动态设置 `interactable`
- `GameOverController.RefreshResult()` — 从 `_gameController` 读取 `winningFaction`
- `GameOverController.OnReplay()` — 调用 `_bootstrap.RestartGame()`

---

## 编译状态

- 所有文件使用正确命名空间：`GanglandUndercover.UI`（新控制器）+ `GanglandUndercover.Gameplay` / `.Online` / `.SocialDeduction`（引用）
- 依赖类型检查：`PrototypeBootstrap`、`GameController`、`OnlineSyncManager`、`Faction`、`GamePhase`、`DistrictState`、`PlayerAction` — 均在第7-8阶段已定义
- 无新增外部依赖，仅使用 `UnityEngine`、`UnityEngine.UI`

---

*生成时间：2026-06-02*
*执行 Agent：File Agent（第10阶段 任务B）*
*（内容由AI生成，仅供参考）*
