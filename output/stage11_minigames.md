# Stage 11 — 小游戏系统（Among Us 风格）

## 概述

为 `SocialPrototypeController` 集成了 3 种 Among Us 风格的小游戏，通过 uGUI Canvas 实现，统一深色主题。

---

## 新增文件

### 1. `Assets/_Project/Scripts/SocialDeduction/MiniGames/MiniGameBase.cs`

**抽象基类**，定义所有小游戏的通用接口。

| 成员 | 说明 |
|------|------|
| `OnComplete` | 小游戏成功完成时触发 |
| `OnCancel` | 小游戏取消或失败时触发 |
| `Show()` | 显示小游戏 UI |
| `Hide()` | 隐藏并清理小游戏 UI |
| `Complete()` | 通知外部：成功完成（protected） |
| `Cancel()` | 通知外部：取消或失败（protected） |

---

### 2. `Assets/_Project/Scripts/SocialDeduction/MiniGames/WireTask.cs`

**连线小游戏**（Among Us Wire Task 复刻）

- 4 条交叉线，左右各 4 个端点
- 点击端点旋转匹配颜色
- 全部匹配正确即完成
- 深色主题：`#1E2632` 背景，`#E84D3F` / `#3498DA` / `#5AC45A` / `#F2D833` 四色线

**核心逻辑**：
- `GenerateWires()`：生成 4 条线，随机打乱右侧顺序
- `OnEndpointClicked()`：点击端点旋转颜色
- `CheckCompletion()`：检查是否全部匹配

---

### 3. `Assets/_Project/Scripts/SocialDeduction/MiniGames/SwipeCardTask.cs`

**刷卡小游戏**（Among Us Swipe Card Task 复刻）

- 滑块在绿色区域内点击确认
- 速度适中即可通过
- 深色主题：`#1E2632` 背景，绿色目标区域 `#2DB851`

**核心逻辑**：
- `CreateUI()`：创建滑条、绿色目标区域、滑块
- `OnThumbClicked()`：点击判定是否在绿色区域
- `SuccessRoutine()` / `FailRoutine()`：成功/失败反馈

---

### 4. `Assets/_Project/Scripts/SocialDeduction/MiniGames/MemoryTask.cs`

**记忆小游戏**（Among Us Memory Task 复刻）

- 3 对符号（◆ ▲ ●）闪烁显示 → 隐藏 → 点击匹配
- 6 个格子，3×2 网格布局
- 深色主题：`#1E2632` 背景，匹配成功显示 `#2DB851`

**核心逻辑**：
- `ShowThenHideRoutine()`：协程控制显示/隐藏
- `OnCellClicked()`：处理格子点击
- `CheckMatchRoutine()`：协程判断匹配结果

---

## 修改文件

### `Assets/_Project/Scripts/SocialDeduction/SocialPrototypeController.cs`

| 修改点 | 说明 |
|--------|------|
| 新增 `using GanglandUndercover.SocialDeduction.MiniGames;` | 引入小游戏命名空间 |
| 新增字段 `MiniGameBase activeMiniGame` | 当前活跃的小游戏引用 |
| 修改 `Update()` | 增加 `activeMiniGame` 状态检查，Esc 取消小游戏 |
| 修改 `StartTaskChallenge()` | 优先实例化 MiniGame，回退到文本多选 |
| 新增 `PickMiniGameType()` | 根据任务名称分配小游戏类型 |
| 新增 `OnMiniGameComplete()` | MiniGame 成功完成回调 |
| 新增 `OnMiniGameCancel()` | MiniGame 取消回调 |
| 新增 `CancelMiniGame()` | 手动取消小游戏 |
| 新增 `CleanupMiniGame()` | 清理小游戏资源 |
| 修改 `ClearWorld()` | 清理 `activeMiniGame` |
| 修改 `TickVentSystem()` | 小游戏活跃时禁止通风管操作 |
| 修改 `IsTaskChallengeVisible` 属性 | 同时检查 `activeMiniGame` |

---

## 任务 → 小游戏映射

| 任务名称 | 小游戏类型 | 理由 |
|----------|------------|------|
| 查封货柜 | `WireTask` | 连线匹配货柜封条 |
| 调取监控 | `MemoryTask` | 记忆摄像头画面特征 |
| 修复电闸 | `WireTask` | 连线修复电路 |
| 扫描证物 | `SwipeCardTask` | 刷卡扫描证物 |
| 上传档案 | `SwipeCardTask` | 刷卡上传档案 |

默认随机分配（哈希取模）。

---

## 使用方式

1. 玩家靠近 `TaskStation` 按 **E**
2. 根据任务类型自动弹出对应小游戏（全屏 uGUI Canvas）
3. 完成小游戏 → 调用 `OnComplete` → 推进任务进度
4. 按 **Esc** 取消 → 调用 `OnCancel` → 任务进度不推进
5. 无 MiniGame 时回退到原有文本多选模式

---

## 深色主题配色

| 元素 | 颜色 |
|------|------|
| 背景 | `#1E2632`（深蓝灰） |
| 面板 | `#2E3442`（中蓝灰） |
| 文字 | `#E0E6EB`（浅灰白） |
| 成功 | `#2DB851`（绿） |
| 失败 | `#D12E2E`（红） |
| 中性 | `#404660`（灰蓝） |

---

## 后续优化方向

- [ ] 完善 `WireTask` 端点旋转动画
- [ ] 完善 `SwipeCardTask` 拖拽物理
- [ ] 增加小游戏音效（UI 点击、成功、失败）
- [ ] 增加小游戏完成动画（粒子/闪烁）
- [ ] 支持更多小游戏类型（校准仪、燃料注入等）
