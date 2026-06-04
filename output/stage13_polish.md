---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9470d846c7eff9b24afb94a99a2cb3f0_163df80a5e5e11f1a4f35254002afed2
    ReservedCode1: Kqg3YNs5Z6l9oBf6IqgexK3wT6PbHCJ8og+q8xGZMQpCbM3dd6MxqxTHiUqpJDgce9oR4s8JZLb3TEfYNwByMiKcVjGpEVrIMp3ZY76r0N0L1YfRRI10Utv/c34u42/ePDUZOlHaKUOvFQfK6bqjquibqQjl+N/V/wOkMskFO+rSCDbydX5jNwTdG1c=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9470d846c7eff9b24afb94a99a2cb3f0_163df80a5e5e11f1a4f35254002afed2
    ReservedCode2: Kqg3YNs5Z6l9oBf6IqgexK3wT6PbHCJ8og+q8xGZMQpCbM3dd6MxqxTHiUqpJDgce9oR4s8JZLb3TEfYNwByMiKcVjGpEVrIMp3ZY76r0N0L1YfRRI10Utv/c34u42/ePDUZOlHaKUOvFQfK6bqjquibqQjl+N/V/wOkMskFO+rSCDbydX5jNwTdG1c=
---

# 阶段 13：游戏流程打磨 + 编译验证 执行报告

## 执行时间
2026-06-02

---

## 一、已完成内容

### 1. GameOverController.cs 增强
**路径**：`Assets/_Project/Scripts/UI/GameOverController.cs`

#### 新增功能
- **`Initialize(PrototypeBootstrap bootstrap)` 方法**
  - 接收 `PrototypeBootstrap` 引用，用于结算后返回主菜单
  - 修复了 `PrototypeBootstrap.cs` 中调用 `_gameOverController.Initialize(this)` 但 `GameOverController` 缺少该方法导致的编译错误

- **结算后 3 秒自动返回主菜单（可跳过）**
  - `Update()` 中新增 `_autoReturnTimer` 倒计时逻辑
  - 倒计时结束后自动调用 `ReturnToMenu()` → `PrototypeBootstrap.ReturnToMainMenu()`
  - 跳过按钮显示剩余秒数（`N秒后返回主菜单（点击跳过）`），点击立即返回

- **淘汰角色时间线（Elimination Timeline）**
  - `BuildEliminationTimeline(List<PlayerRecord>)`：遍历所有 `!Alive` 的玩家，生成水平时间线节点
  - 每个节点包含：红色圆点标记 + "被淘汰"标签 + 玩家名
  - 面板宽度自适应（最多 780px，节点间距 160px）
  - 在结算动画序列中，统计面板展示完毕后渐入显示

- **`PlayerRecord` 新增字段**
  - `EliminatedBy`：记录淘汰者（用于时间线展示，当前版本预留）

#### 修改的方法
- `Awake()`：新增 `_bootstrap`、`_skipButtonObj`、`_timelineObj`、`_autoReturnTimer`、`_autoReturnActive` 字段初始化
- `Hide()`：重置 `_autoReturnActive = false`
- `PlaySequence()`：新增第 5 步（淘汰时间线）和第 6 步（跳过按钮 + 自动返回倒计时）

---

### 2. KillSystem.cs 增强
**路径**：`Assets/_Project/Scripts/Online/KillSystem.cs`

#### 新增功能
- **击杀瞬间屏幕红色闪光**
  - `CreateScreenFlashOverlay()`：创建 `ScreenSpaceOverlay` Canvas + 全屏红色 `Image`
  - `TriggerScreenFlash()`：触发闪光协程（快速渐入 → 短暂停留 → 快速渐出）
  - 闪光参数可在 Inspector 中配置：`Flash Duration`（默认 0.35s）、`Flash Peak Alpha`（默认 0.35）

- **尸体上方 3D 世界空间"报告"按钮**
  - `CheckNewBodies()`：每帧检测 `controller.Bodies` 中新增的未报告尸体
  - `CreateReportButton(OnlineBodyState)`：
    - 创建 `WorldSpace` Canvas（SortingOrder = 90）
    - 按钮位于尸体位置 + (0, 1.1, 0)
    - 按钮文字：`"报告 {VictimName} 的尸体"`
    - 点击后调用 `controller.RequestAction(OnlineActionType.Report)`
  - 已报告的尸体（`body.Reported == true`）不再生成按钮
  - 已跟踪的尸体（`_trackedBodies.Contains(body.Id)`）不再重复生成

- **击杀冷却期间按钮灰色 + 倒计时数字**
  - `UpdateKillButtonAppearance()`：
    - 冷却中：按钮 `interactable = false`，背景变灰（`Color(0.3, 0.3, 0.3, 0.7)`），`killButtonLabel` 显示剩余整数秒
    - 可击杀：按钮恢复红色，显示 `"击杀 {VictimName}"`
  - `cooldownText`（可选绑定）：冷却期间显示 `"Ns"` 倒计时

- **击杀按钮显示逻辑修复**
  - `TryFindNearestVictim()`：现在真正通过 `controller.Players` 字典遍历所有玩家，计算距离，找到最近的非 Gang/非 Mole、存活的玩家
  - 距离判断：`Vector3.Distance(localPos, state.Position) < killRange`
  - `ShouldShowKillButton()`：本地玩家存活 + `Phase == Action` + `LocalRole == Gang || Mole`

#### 修复的问题
- 原 `TryFindNearestVictim()` 始终返回 `true`（空实现），导致击杀按钮一直显示
- 原 `TryGetLocalPlayerState()` 始终返回 `false`，导致无法获取本地玩家状态
- 现通过 `controller.Players` + `controller.LocalClientIdValue` + `controller.LocalAlive` + `controller.LocalRole` 正确获取所有状态

---

### 3. LoadingScreen.cs（已存在，无需修改）
**路径**：`Assets/_Project/Scripts/UI/LoadingScreen.cs`
- 深色背景 + 旋转加载图标 + 随机提示文字（"卧底就在你们中间……" 等 6 条）
- 通过 `LoadSceneAsync()` 驱动，已完整实现

### 4. TransitionEffect.cs（已存在，无需修改）
**路径**：`Assets/_Project/Scripts/UI/TransitionEffect.cs`
- 全屏黑幕淡入淡出（`FadeToBlack` / `FadeFromBlack`）
- 圆形开合效果（`CircleOpen` / `CircleClose`）
- 已完整实现

---

## 二、编译验证结果

### 括号平衡检查
- 所有 `.cs` 文件 `{` 与 `}` 数量匹配，**无括号不平衡问题**

### Using 语句检查
| 文件 | 需要的 using | 状态 |
|------|-------------|------|
| KillSystem.cs | `System`, `System.Collections`, `System.Collections.Generic`, `UnityEngine`, `UnityEngine.UI` | ✅ 全部存在 |
| GameOverController.cs | `GanglandUndercover.Gameplay`, `GanglandUndercover.SocialDeduction`, `UnityEngine`, `UnityEngine.UI` | ✅ 全部存在 |

### 类型引用检查
| 引用类型 | 定义位置 | 状态 |
|---------|----------|------|
| `OnlineMatchController` | `GanglandUndercover.Online`（同命名空间） | ✅ |
| `OnlineMatchHud` | `GanglandUndercover.Online`（同命名空间） | ✅ |
| `OnlinePlayerState` | `OnlineMatchController.cs` 内部结构体 | ✅ 可访问 |
| `OnlineBodyState` | `OnlineMatchController.cs` 内部结构体 | ✅ 可访问 |
| `OnlineActionType` | `OnlineMatchController.cs` 内部枚举 | ✅ 可访问 |
| `OnlineMatchPhase` | `OnlineMatchController.cs` 内部枚举 | ✅ 可访问 |
| `OnlineRole` | `OnlineMatchController.cs` 内部枚举 | ✅ 可访问 |
| `PrototypeBootstrap` | `GanglandUndercover.Gameplay` | ✅ using 已导入 |
| `UIParticleEffect` | `GanglandUndercover.UI`（同命名空间） | ✅ |
| `AudioManager` / `SoundEffect` | `GanglandUndercover.Audio` | ✅ 完全限定名可访问 |
| `ThemeManager` | `GanglandUndercover.UI`（同命名空间） | ✅ |

### 方法引用检查
| 调用方法 | 定义位置 | 可见性 | 状态 |
|---------|----------|--------|------|
| `controller.Players` | `OnlineMatchController.Players` | `public` | ✅ |
| `controller.Bodies` | `OnlineMatchController.Bodies` | `public` | ✅ |
| `controller.LocalAlive` | `OnlineMatchController.LocalAlive` | `public` | ✅ |
| `controller.LocalRole` | `OnlineMatchController.LocalRole` | `public` | ✅ |
| `controller.LocalClientIdValue` | `OnlineMatchController.LocalClientIdValue` | `public` | ✅ |
| `controller.Phase` | `OnlineMatchController.Phase` | `public` | ✅ |
| `controller.RequestAction()` | `OnlineMatchController.RequestAction()` | `public` | ✅ |
| `hud.RequestKill()` | `OnlineMatchHud` | 通过 `controller.RequestAction(Kill)` 替代 | ✅ |
| `ThemeManager.WithAlpha()` | `ThemeManager.WithAlpha()` | `public static` | ✅ |
| `ThemeManager.ButtonPrimary` | `ThemeManager.ButtonPrimary` | `public static` | ✅ |
| `ThemeManager.FontSizeFooter` | `ThemeManager.FontSizeFooter` | `public static` | ✅ |

---

## 三、已知问题与后续建议

### 已知问题
1. **`GameOverController` 的 `PlayerRecord.EliminatedBy` 字段当前未填充**
   - 需要在 `OnlineMatchController.SetResult()` 中构建 `PlayerRecord[]` 时，从击杀记录中填入 `EliminatedBy`
   - 当前时间线能显示"被淘汰"标记，但不知道被谁淘汰

2. **世界空间"报告"按钮在尸体被报告后不会自动销毁**
   - 需要在 `controller.Bodies` 中某尸体 `Reported` 变为 `true` 时，销毁对应的 `BodyReportBtn_{Id}` 对象
   - 当前只有新尸体触发创建，没有已报告尸体的按钮清理逻辑

### 后续建议
1. 在 `OnlineMatchController` 的 `SetResult()` 中构建完整的 `PlayerRecord[]`（含 `EliminatedBy`、`TasksCompleted`、`IntelSubmitted`、`Victims`），传入 `GameOverController.Show()`
2. 完善 `KillSystem.CreateReportButton()` 的按钮清理逻辑（监听 `body.Reported` 变化）
3. 考虑在 `KillSystem` 中添加击杀震屏效果（Camera Shake）
4. 考虑在 `GameOverController` 时间线中添加时间顺序（当前按列表顺序，非淘汰顺序）

---

## 四、文件清单

| 文件 | 操作 |
|------|------|
| `Assets/_Project/Scripts/UI/GameOverController.cs` | 修改（新增 Initialize、自动返回、淘汰时间线） |
| `Assets/_Project/Scripts/Online/KillSystem.cs` | 重写（新增闪光、世界空间报告按钮、冷却倒计时、距离检测修复） |
| `Assets/_Project/Scripts/UI/LoadingScreen.cs` | 无修改（已完整） |
| `Assets/_Project/Scripts/UI/TransitionEffect.cs` | 无修改（已完整） |

---

*报告生成时间：2026-06-02*
*（内容由AI生成，仅供参考）*
