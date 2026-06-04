# Stage 11 — 场景美术 + 监控摄像头

> 完成日期：2026-06-02

---

## 产出物清单

| 文件 | 类型 | 说明 |
|---|---|---|
| [EnvironmentManager.cs](</Users/zhugehao/projects/GanglandUndercover/Assets/_Project/Scripts/SocialDeduction/EnvironmentManager.cs>) | 新增 | 环境管理器：雾效、区域灯光、断电切换 |
| [SecurityCamera.cs](</Users/zhugehao/projects/GanglandUndercover/Assets/_Project/Scripts/SocialDeduction/SecurityCamera.cs>) | 新增 | 监控摄像头系统：4 路摄像头 + 监控站 |
| [SocialPrototypeController.cs](</Users/zhugehao/projects/GanglandUndercover/Assets/_Project/Scripts/SocialDeduction/SocialPrototypeController.cs>) | 修改 | BuildWorld/Update/ClearWorld 集成 |

---

## 1. 场景美术优化

### 1.1 区域氛围灯光（EnvironmentManager）

| 区域 | 类型 | 颜色 | 强度 | 范围 |
|---|---|---|---|---|
| 货柜码头 | Point Light 暖橙 | (1.0, 0.62, 0.28) | 2.2 | 2.8 |
| 夜市巷 | Point Light 暗红 | (0.82, 0.2, 0.08) | 1.5 | 2.6 |
| 专案办公室 | Point Light 冷蓝 | (0.42, 0.52, 0.82) | 1.8 | 2.6 |
| 证物库 | Point Light 苍白紫 | (0.5, 0.48, 0.65) | 1.5 | 2.5 |
| 地下诊所 | Point Light 荧光绿 | (0.2, 0.7, 0.42) | 1.8 | 2.5 |
| 主街 | Point Light 暖黄 | (0.78, 0.62, 0.28) | 1.2 | 5.5 |

### 1.2 全局雾效

- `RenderSettings.fog = true`，线性模式 (Linear)
- `fogStartDistance = 6`, `fogEndDistance = 16`
- 雾色 `(0.015, 0.035, 0.05)` — 深蓝黑雾，港区工业感
- 环境光 `(0.08, 0.09, 0.12)` — 低压暗调

### 1.3 天花板 / 屋顶

5 个房间全部添加天花板：货柜码头、夜市巷、专案办公室、证物库、地下诊所。

- 优先从 Synty `SM_Bld_Base_Ceiling_01.prefab` 加载，按房间尺寸自适应缩放
- 回退方案：手动 Cube，暗灰色 `(0.18, 0.16, 0.14)`

### 1.4 断电（Blackout）灯光切换

**Enter Blackout（Sabotage 触发）**：
- 所有注册灯光强度降至原始 15%
- 颜色 Lerp 至深蓝黑色
- 环境光降至 `(0.02, 0.03, 0.08)`

**Exit Blackout（断电计时结束）**：
- 所有灯光恢复原始强度和颜色
- 环境光恢复 `(0.08, 0.09, 0.12)`

整合点：`TriggerBlackout()` → `environmentManager.SetBlackout(true)`，计时器归零 → `SetBlackout(false)`。

---

## 2. 监控摄像头系统

### 2.1 摄像头节点（4 路）

| # | 名称 | 位置 | 朝向 | 覆盖区域 |
|---|---|---|---|---|
| 0 | 码头监控 | (-4.05, 1.55) | 右下 (0.9, -0.45) | 货柜区 |
| 1 | 夜市监控 | (0.65, 3.05) | 左下 (-0.55, -0.85) | 夜市巷 |
| 2 | 办公室监控 | (4.55, 1.5) | 左 (-0.95, -0.3) | 办公区 |
| 3 | 走廊监控 | (-0.55, 0.85) | 下 (0, -1) | 主走廊 |

### 2.2 锥形检测

- **检测范围**：3.5 单位
- **锥形半角**：55°（总 FOV 110°）
- **算法**：距离筛选 + `Vector3.Angle()` 角度判断
- **目标**：所有非 Police 角色（Gang + Undercover）
- **视觉指示**：每路摄像头绘制三条半透明青色锥形线（左边界 / 中心 / 右边界）

### 2.3 指示灯

- 每路摄像头模型上挂载绿色球体指示灯
- `TickDetection()` 每帧检测：视野内有 Impostor → 变红（可被所有玩家看到）

### 2.4 监控站

- **位置**：`(0.55, -1.55)` — 地图中部偏下，竖巷与主街交汇处
- **模型**：3 个 `SM_Gen_Prop_Screen_01` 显示器 + 底座 Cube
- **标签**："监控站\nE 查看"

### 2.5 交互流程

```
玩家靠近监控站 → 提示 "V 查看监控摄像头"
    ↓ 按 V
进入查看模式（镜头锁定监控站位置）
    ↓ 提示 "V 切换摄像头 | E 退出"
    ↓ 按 V
循环切换 4 路摄像头 + 退出选项
每条提示包含：
  - 摄像头名称
  - ● 正常 / 🔴 可疑活动
  - 按键指引
    ↓ 按 E 或走出范围
自动退出，恢复跟随镜头
```

### 2.6 控制器集成

| 位置 | 集成内容 |
|---|---|
| `BuildWorld()` | `SetupEnvironment()` → `CreateCeilings()` → `CreateSecuritySystems()` |
| `Update()` | `TickSecurityCamera()` — 检测 + 自动退出 |
| `HandleInput()` | E/V 键优先处理监控站交互 |
| `BuildInteractionPrompt()` | 监控站提示优先于通风管 |
| `FollowCamera()` | 查看中锁定镜头于监控站 |
| `TriggerBlackout()` | 断电/恢复通知 EnvironmentManager |
| `ClearWorld()` | 清理 securityCamera + environmentManager |

---

## 3. 运行效果

- 5 个房间各具氛围色温：码头暖橙、办公室冷蓝、夜市暗红、证物苍白、诊所绿光
- 全局线性雾效营造港区低压感
- 断电时所有灯光均暗至 15%，恢复后复原
- 4 路监控摄像头带锥形视野指示 + 红绿指示灯
- 玩家可在监控站按 V/E 切换查看各摄像头画面，镜头锁定监控站

---

## 4. 未完成 / 后续优化

| 项目 | 状态 | 备注 |
|---|---|---|
| 监控画面渲染（PIP） | 跳过 | 正交俯视视角下 PIP 效果有限，当前用文字描述替代 |
| Synty 地板贴图替换 | 跳过 | 当前纯色 Cube 地板已足够，后续可替换为 Generic_Concrete.mat |
| 监控站录像回放 | 后续 | 可作为额外 Sabotage 玩法（清除录像） |