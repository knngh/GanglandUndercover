---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9470d846c7eff9b24afb94a99a2cb3f0_15a824ac5e5e11f1a4f35254002afed2
    ReservedCode1: eEgRvvppMN0XkhUfx8GyUoePtLEYUXru3SeQNfy/fQcZJzfm1pv1yMgaKh32au5Uawyy+Uplx1Bydr57M4Ue3qp0wske8ULiV7ZbxI49qbOU++tDimEcJdXehyB7lJ8AcZykpGYgSJtvd3JzEekRZgovz/1e+NtS8ho0kLKqEiXoKd+dab8ZIZ0CUjU=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9470d846c7eff9b24afb94a99a2cb3f0_15a824ac5e5e11f1a4f35254002afed2
    ReservedCode2: eEgRvvppMN0XkhUfx8GyUoePtLEYUXru3SeQNfy/fQcZJzfm1pv1yMgaKh32au5Uawyy+Uplx1Bydr57M4Ue3qp0wske8ULiV7ZbxI49qbOU++tDimEcJdXehyB7lJ8AcZykpGYgSJtvd3JzEekRZgovz/1e+NtS8ho0kLKqEiXoKd+dab8ZIZ0CUjU=
---

# Stage 13：小游戏扩展（再+4种）

**日期**：2026-06-02
**目标**：对标 Among Us，新增 4 种小游戏，丰富任务玩法多样性

---

## 新增小游戏列表

| # | 类名 | 玩法 | 对标 Among Us | 状态 |
|---|------|------|---------------|------|
| 1 | `KeypadTask` | 4位密码键盘，3次尝试 | 安全门密码 | ✅ 完成 |
| 2 | `SortTask` | 4物品拖拽分类到4个目标槽 | 垃圾分类/整理柜子 | ✅ 完成 |
| 3 | `ScanTask` | 圆形扫描环收缩，绿色区域点击停止 | MedBay 扫描 | ✅ 完成 |
| 4 | `TapTask` | 限时快速点击8个随机目标 | 校准/射击 | ✅ 完成 |

---

## 文件清单

### 新增文件（4个）

```
Assets/_Project/Scripts/SocialDeduction/MiniGames/
├── KeypadTask.cs    (11,954 bytes)
├── SortTask.cs      (14,365 bytes)
├── ScanTask.cs      (10,853 bytes)
└── TapTask.cs       (11,047 bytes)
```

### 修改文件（1个）

```
Assets/_Project/Scripts/SocialDeduction/SocialPrototypeController.cs
```

修改内容：`PickMiniGameType()` 方法扩展，新增4种小游戏类型映射。

---

## 各小游戏设计说明

### 1. KeypadTask（密码键盘）

**玩法**：
- 随机生成4位数字密码（0-9）
- 9宫格数字按钮（1-9），0单独放置
- 点击按钮输入，实时显示已输入位数
- 3次错误尝试机会，用完则任务失败
- 输入正确4位后自动完成

**UI 元素**：
- 标题："密码键盘：输入4位密码"
- 密码显示区（圆点指示）
- 9宫格数字按钮（深色主题）
- 清除按钮 + 确认按钮
- 剩余尝试次数指示

**对标**：Among Us 安全门、密码锁任务

---

### 2. SortTask（分类排序）

**玩法**：
- 4个可拖拽物品（不同颜色/图标）
- 4个固定目标槽（带标签）
- 拖拽物品到目标槽，正确匹配吸附，错误弹回
- 全部4个正确匹配后完成

**UI 元素**：
- 标题："分类任务：将物品拖到正确位置"
- 物品区（上方，4个可拖拽卡片）
- 目标槽区（下方，4个带标签的槽位）
- 拖拽时半透明跟随鼠标
- 正确/错误视觉反馈

**对标**：Among Us 垃圾分类、整理柜子

---

### 3. ScanTask（扫描任务）

**玩法**：
- 圆形扫描区域，扫描环从外圈向内圈收缩
- 绿色安全区域固定在环的中段
- 玩家需在扫描环进入绿色区域时点击停止
- 成功则完成，失败则环重置重新收缩
- 类似 Among Us MedBay 扫描仪

**UI 元素**：
- 标题："扫描任务：在绿色区域点击停止"
- 圆形扫描盘（深色背景）
- 绿色安全区域（半透明绿色环）
- 蓝色扫描环（从外向内收缩）
- 中心状态指示点
- 提示文字

**对标**：Among Us MedBay 扫描（完全一致）

---

### 4. TapTask（快速点击）

**玩法**：
- 屏幕随机位置持续出现目标圆圈
- 共需点击8个目标
- 每个目标有1.2秒生命周期，超时消失
- 目标出现间隔0.65秒
- 6.5秒总时限，时间到未完成则失败
- 目标消失前有脉冲动画和变红警告

**UI 元素**：
- 标题："快速点击：点掉所有目标！"
- 进度显示："3 / 8"
- 倒计时器（底部，<2s变红）
- 目标圆圈（蓝色，脉冲动画）
- 点击后绿色放大淡出效果

**对标**：Among Us 校准仪表、流星射击

---

## SocialPrototypeController 修改

### `PickMiniGameType()` 新增映射

```csharp
// ── 密码键盘类 ──
if (taskName.Contains("密码") || taskName.Contains("保险箱") || taskName.Contains("门禁"))
    return typeof(KeypadTask);

// ── 分类排序类 ──
if (taskName.Contains("分类") || taskName.Contains("垃圾") || 
    taskName.Contains("归档") || taskName.Contains("整理"))
    return typeof(SortTask);

// ── 扫描类 ──
if (taskName.Contains("扫描") || taskName.Contains("体检") || 
    taskName.Contains("化验") || taskName.Contains("MedBay"))
    return typeof(ScanTask);

// ── 快速点击类 ──
if (taskName.Contains("点击") || taskName.Contains("反应") || 
    taskName.Contains("射击") || taskName.Contains("校准"))
    return typeof(TapTask);
```

默认随机池也从3种扩展到7种（含新增4种）。

---

## 与 Among Us 对标情况

| Among Us 原版小游戏 | 本作对应 | 完成度 |
|---------------------|----------|--------|
| 连线（Wires） | `WireTask` | ✅ Stage 11 |
| 刷卡（Swipe Card） | `SwipeCardTask` | ✅ Stage 11 |
| 记忆（Memory） | `MemoryTask` | ✅ Stage 11 |
| 密码键盘（Keypad） | `KeypadTask` | ✅ 本Stage |
| 垃圾分类（Sort） | `SortTask` | ✅ 本Stage |
| MedBay扫描（Scan） | `ScanTask` | ✅ 本Stage |
| 校准/射击（Calibrate） | `TapTask` | ✅ 本Stage |
| 航向校准（Align Engine） | 待做 | ⬜ |
| 清理陨石（Clear Asteroids） | 待做 | ⬜ |
| 下载数据（Download Data） | 待做 | ⬜ |

---

## 测试建议

1. **KeypadTask**：验证密码生成随机性、3次失败逻辑、UI按钮响应
2. **SortTask**：验证拖拽流畅性、正确/错误匹配判定、完成条件
3. **ScanTask**：验证扫描环收缩速度、绿色区域判定精度、成功/失败反馈
4. **TapTask**：验证目标生成随机性、计时器准确性、点击响应速度

---

## 下一步（Stage 14 建议）

- 补充剩余 Among Us 小游戏（航向校准、清理陨石、下载数据）
- 为所有小游戏添加音效（按钮点击、成功、失败）
- 添加小游戏难度梯度（简单/中等/困难）
- UI 美化：添加图标、动画过渡、粒子效果
*（内容由AI生成，仅供参考）*
