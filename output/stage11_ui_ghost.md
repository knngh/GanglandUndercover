# Stage 11 — UI 视觉美化 + 鬼魂模式

## 一、UI 视觉美化

### 1.1 ThemeManager.cs（新增）
**路径**: `Assets/_Project/Scripts/UI/ThemeManager.cs`

统一管理所有 UI 颜色、字体大小、间距、动画时长等常量，避免硬编码。

| 类别 | 常量 | 值 | 用途 |
|------|------|-----|------|
| 背景色 | `BackgroundDark` | `#0A0E27` | 主菜单/结算背景 |
| 面板色 | `PanelBackground` | `#1A2340` | 面板背景 |
| 标题金 | `TitleGold` | `#FFD700` | 主标题 |
| 霓虹青 | `NeonCyan` | `#00E5FF` | 按钮边框/高亮 |
| 霓虹品红 | `NeonMagenta` | `#FF00FF` | 强调色 |
| 卧底色 | `UndercoverColor` | `#00BCD4` | 阵营色 |
| 警察色 | `PoliceColor` | `#2196F3` | 阵营色 |
| 黑帮色 | `GangColor` | `#F44336` | 阵营色 |
| 字体大小 | `FontSizeTitle` | 52 | 主标题 |
| 字体大小 | `FontSizeSubtitle` | 26 | 副标题 |
| 字体大小 | `FontSizeButton` | 20 | 按钮 |
| 按钮高度 | `ButtonHeight` | 54f | 按钮 |
| 面板内边距 | `PanelPadding` | 24f | 面板 |
| 滑入动画时长 | `SlideInDuration` | 0.45s | 结算面板 |

**辅助方法**:
- `GetFactionColor(role)` — 根据角色返回阵营色
- `GetRoleColor(role)` — 根据角色返回角色色

---

### 1.2 MainMenuController.cs（美化）

**路径**: `Assets/_Project/Scripts/UI/MainMenuController.cs`

#### 改动要点
1. **颜色统一迁移至 ThemeManager** — 所有硬编码颜色替换为 `ThemeManager.TitleGold` 等常量
2. **标题效果** — 添加 `Shadow`（黑色，偏移(2,-2)）和 `Outline`（黑色，宽度2）
3. **按钮美化** — 新增 `CreateStyledButton()` 工厂方法：
   - 霓虹青色边框（子 `Image` 层，-2/+2 内缩）
   - 悬停高亮（`ColorBlock.highlightedColor = ScaleColor(bg, 1.4f)`）
   - 按下变暗（`pressedColor = ScaleColor(bg, 0.6f)`）
4. **渐变遮罩** — 新增 `CreateGradientOverlay()` 方法，在 Canvas 顶层添加两层半透明 `Image` 模拟渐变（顶部亮 → 底部暗）

#### 新增私有方法
```csharp
private static void AddTextShadow(Text text, Color shadowColor, Vector2 distance)
private static void AddTextOutline(Text text, Color outlineColor)
private static GameObject CreateGradientOverlay(Transform parent)
private static GameObject CreateStyledButton(string name, Transform parent, string label, float w, float h, Color bg, Color txtColor, int fontSize)
```

---

### 1.3 GameOverController.cs（美化 + 动画）

**路径**: `Assets/_Project/Scripts/UI/GameOverController.cs`

#### 改动要点
1. **颜色统一迁移至 ThemeManager**
2. **结果面板滑入动画** — `Show()` 方法调用 `StartCoroutine(AnimateSlideIn(...))`：
   - 初始位置: `(0, -800)`（屏幕外下方）
   - 目标位置: `(0, 60)`（屏幕中上方）
   - 缓动函数: `EaseOutQuart`（t = 1 - (1-t)^4）
   - 时长: `ThemeManager.SlideInDuration`（0.45s）
3. **身份揭示卡片** — `RefreshRoleReveal()` 改为卡片式布局：
   - 每张卡片：暗底背景（`PanelBackground`）+ 左侧阵营色条（6px 宽）+ 名字左对齐 + 阵营标签右对齐
   - 使用 `ThemeManager.GetFactionColor(role)` 获取阵营色
4. **标题效果** — 添加 `Shadow` + `Outline`

#### 新增私有方法
```csharp
private System.Collections.IEnumerator AnimateSlideIn(RectTransform target, Vector2 from, Vector2 to)
private static void AddTextShadow(Text text, Color shadowColor, Vector2 distance)
private static void AddTextOutline(Text text, Color outlineColor)
private static GameObject MakeStyledButton(string name, Transform parent, string label, float w, float h, Color bg, Color txtColor, int fontSize)
```

---

## 二、鬼魂模式（Ghost Mode）

### 2.1 GhostMode.cs（新增组件）

**路径**: `Assets/_Project/Scripts/Gameplay/GhostMode.cs`

#### 核心功能
| 功能 | 实现方式 |
|------|----------|
| 半透明渲染 | `EnterGhostMode()` 中遍历所有 `Renderer`，设置 `color.a = 0.35f` |
| 穿越墙壁 | 将所有 `Collider2D` / `Collider` 设为 `isTrigger = true` |
| 自由飞行 | 启用 `GhostMovement`（新增简易飞行控制），Z 轴偏移 -0.5f |
| 继续做任务 | `CanDoTasks` 属性（默认 true），任务系统检查此标志 |
| 无法被活人看到 | `CanSeeGhost(GameObject viewer)` 静态方法，检查 viewer 是否存活 |
| 无法报告尸体 | `CanReportBody` 属性（默认 false） |
| 无法发起会议 | `CanCallMeeting` 属性（默认 false） |

#### 公共属性
```csharp
public bool IsGhost { get; private set; }
public bool CanDoTasks { get; set; }
public bool CanReportBody { get; set; }
public bool CanCallMeeting { get; set; }
```

#### 公共方法
```csharp
public void EnterGhostMode()
public void ExitGhostMode()
public static bool CanSeeGhost(GameObject viewer)
```

---

### 2.2 SocialPrototypeController.cs（集成 GhostMode）

**路径**: `Assets/_Project/Scripts/SocialDeduction/SocialPrototypeController.cs`

#### 改动要点
1. **KillCharacter() 方法**（约 L1376）:
   - 原逻辑：玩家被击杀直接 `FinishGame()`
   - 新逻辑：检查是否仍有友方存活（`IsSameFaction` 判断）
     - 有队友存活 → 添加 `GhostMode` 组件，调用 `EnterGhostMode()`，设置 `CanDoTasks=true`
     - 无队友存活 → 仍调用 `FinishGame()`

2. **新增辅助方法**:
   ```csharp
   private static bool IsSameFaction(SocialRole a, SocialRole b)
   private static Faction GetFaction(SocialRole role)
   ```

3. **Faction 枚举**（隐式新增）:
   ```csharp
   private enum Faction { None, Gang, Undercover, Police }
   ```
   - `Undercover` + `Police` 为同一阵营（友方）
   - `Gang` 为敌方

---

### 2.3 OnlineMatchController.cs（集成 GhostMode）

**路径**: `Assets/_Project/Scripts/Online/OnlineMatchController.cs`

#### 改动要点
1. **新增 using**: `using GanglandUndercover.Gameplay;`

2. **TryKill() 方法**（约 L3118）:
   - 在 `victim.Alive = false` 之后，新增检查：
     ```csharp
     if (victimClientId == LocalClientId())
     {
         ActivateGhostModeForLocalPlayer(victimClientId);
     }
     ```

3. **ResolveVotes() 方法**（约 L3545）:
   - 在 `ejected.Alive = false` 之后，新增检查：
     ```csharp
     if (ejectedClientId == LocalClientId())
     {
         ActivateGhostModeForLocalPlayer(ejectedClientId);
     }
     ```

4. **新增方法** `ActivateGhostModeForLocalPlayer(ulong clientId)`:
   - 通过 `FindObjectsOfType<SocialCharacter>()` 查找本地玩家的 `SocialCharacter`
   - 添加/获取 `GhostMode` 组件
   - 调用 `EnterGhostMode()`，设置 `CanDoTasks=true`, `CanReportBody=false`, `CanCallMeeting=false`
   - 输出 `CaseLog` 提示玩家已进入鬼魂模式

---

## 三、文件变更清单

| 文件 | 操作 | 说明 |
|------|------|------|
| `Assets/_Project/Scripts/UI/ThemeManager.cs` | 新增 | UI 主题常量统一管理 |
| `Assets/_Project/Scripts/UI/MainMenuController.cs` | 修改 | 美化：Shadow/Outline/渐变遮罩/按钮样式 |
| `Assets/_Project/Scripts/UI/GameOverController.cs` | 修改 | 美化：滑入动画/身份卡片/Shadow/Outline |
| `Assets/_Project/Scripts/Gameplay/GhostMode.cs` | 新增 | 鬼魂模式组件 |
| `Assets/_Project/Scripts/SocialDeduction/SocialPrototypeController.cs` | 修改 | KillCharacter 集成 GhostMode |
| `Assets/_Project/Scripts/Online/OnlineMatchController.cs` | 修改 | TryKill/ResolveVotes 集成 GhostMode |

---

## 四、测试建议

### UI 美化
1. 启动游戏，检查主菜单标题是否有阴影和描边效果
2. 鼠标悬停按钮，检查是否有高亮效果
3. 点击"开始游戏"，检查结算界面是否有滑入动画
4. 查看结算界面的身份揭示卡片布局是否正确

### 鬼魂模式
1. **离线模式**：开始游戏，让黑帮击杀玩家，检查是否进入鬼魂模式（半透明、可穿越墙壁、可继续做任务）
2. **离线模式**：让所有队友都被淘汰，检查是否触发 `FinishGame()`
3. **在线模式**：创建/加入房间，让其他玩家击杀本地玩家，检查是否进入鬼魂模式
4. **在线模式**：发起投票将本地玩家投出局，检查是否进入鬼魂模式
5. 验证鬼魂无法报告尸体、无法发起会议
6. 验证活着的玩家看不到鬼魂（需要 `CanSeeGhost` 逻辑集成到渲染系统）

---

## 五、已知限制 / 待完成

1. **GhostMode 渲染集成** — `CanSeeGhost()` 已实现，但需要集成到玩家渲染逻辑中（活人看不到鬼魂）
2. **GhostMovement** — 简易飞行控制脚本待实现（WASD + 鼠标控制飞行）
3. **任务系统集成** — 需要确认任务系统是否检查 `GhostMode.CanDoTasks` 标志
4. **联机同步** — GhostMode 状态需要在联机模式下同步给其他玩家（当前仅本地生效）

---

*生成时间: 2026-06-02*
*作者: Marvis AI Assistant*
