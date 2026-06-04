---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9470d846c7eff9b24afb94a99a2cb3f0_1550f3f55e5e11f18d42525400d9a7a1
    ReservedCode1: niBf4l+wI2qmnrChzAK3I4604AJ6DV1MWVNOn4BnyaWBPA6EHXMj9zQUkS6OqS3quejCLdXCX3yTni1TPY4V3eZfz4FBwsa3BlxIsZEU9GF3CiHT0VfJclAjFLNJ/hPAQhST3fJpjFHy7Z/f9NqH4x6Os9JWd21YPbVAZfRPLUYqtjT8E6kBMFUHjyk=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9470d846c7eff9b24afb94a99a2cb3f0_1550f3f55e5e11f18d42525400d9a7a1
    ReservedCode2: niBf4l+wI2qmnrChzAK3I4604AJ6DV1MWVNOn4BnyaWBPA6EHXMj9zQUkS6OqS3quejCLdXCX3yTni1TPY4V3eZfz4FBwsa3BlxIsZEU9GF3CiHT0VfJclAjFLNJ/hPAQhST3fJpjFHy7Z/f9NqH4x6Os9JWd21YPbVAZfRPLUYqtjT8E6kBMFUHjyk=
---

# Stage 13: 场景深度美化 + 紧急任务系统

**日期**: 2026-06-02
**状态**: 完成

---

## 1. 环境管理器增强 (EnvironmentManager.cs)

### 分区颜色分级（LUT 模拟）

通过 `RegisterZoneGrading` 和 `TickAmbientProbe` 实现每帧动态环境色混合，模拟 LUT 效果：

| 区域 | 灯光色 | 环境色 Tint | Blend |
|------|--------|------------|-------|
| Dock (货柜码头) | 暖橙 `(1, 0.62, 0.28)` | `(0.14, 0.09, 0.04)` | 0.20 |
| Warehouse (证物库) | 暗蓝灰 `(0.36, 0.42, 0.62)` | `(0.03, 0.04, 0.07)` | 0.22 |
| NightMarket (夜市巷) | 霓虹粉紫 `(0.92, 0.18, 0.48)` | `(0.07, 0.02, 0.06)` | 0.18 |
| Office (专案办公室) | 冷白蓝 `(0.42, 0.52, 0.82)` | `(0.04, 0.05, 0.08)` | 0.15 |
| Clinic (地下诊所) | 柔和绿 `(0.2, 0.7, 0.42)` | `(0.02, 0.06, 0.04)` | 0.18 |
| Tenement (主街) | 暗暖黄 `(0.78, 0.62, 0.28)` | `(0.06, 0.05, 0.02)` | 0.14 |

### 动态环境光探针

- `TickAmbientProbe(Vector3 observerPosition)` — 根据主相机位置动态混合区域色（LightProbes API 不可用时的替代方案）
- 每帧在 Update 中调用，适时注入 `RenderSettings.ambientLight`

### 地面贴花

- `PlaceFloorDecal(DecalType, position)` — 在指定位置创建 Quad + 透明材质
- 三种类型：`Blood`（血迹，暗红透明 0.42）、`Oil`（油渍，黑灰透明 0.55）、`Paper`（纸屑，米黄透明 0.48）
- 随机尺寸（0.12~0.65）、随机旋转 0~360°
- 贴花 Z 坐标 -0.06，置于地面上方

---

## 2. 程序化房间装饰 (RoomDecoration.cs)

### 墙壁海报/通缉令

- `PlaceWallPosters` — 每房间 1~2 张，随机选择北墙/南墙
- 四种颜色模拟：红色通缉令、蓝色公告、黄色警示、绿色通知
- 海报边框独立 Quad（深色 1.12x 缩放）

### 货柜堆叠

- `PlaceContainerStack` — 货柜/仓库区域专属，2~4 个随机堆叠
- 随机旋转偏移（±4°）和位置偏移（±0.08），避免整齐排列
- 蓝/红/绿随机配色 + 横向条纹装饰

### 办公桌+椅子

- `PlaceDeskChairCombo` — 办公室专属，1~2 组
- 办公桌 `(0.62, 0.38, 0.42)` + 桌面显示器小方块 + 椅子 `(0.22, 0.22, 0.38)`
- 椅子自动放置在桌子前方 0.42m

### 诊所货架

- `PlaceClinicShelves` — 诊所专属，1~2 个货架 + 每个货架 3 个药瓶（绿色小方块）

---

## 3. 紧急任务系统 (CriticalTaskSystem.cs)

### 架构

```
CriticalTaskSystem (MonoBehaviour, 挂载在 SocialPrototypeController 上)
├── CriticalTaskType: { None, O2, Reactor }
├── CriticalTaskState: { Inactive, Active, Completed, Failed }
├── 事件回调
│   ├── OnCriticalTaskStarted → 暂停 AI
│   ├── OnCriticalTaskCompleted → 恢复 AI
│   └── OnCriticalTaskFailed → 阵营失败
└── 警报 UI（全屏 Quad 叠加层，红色闪烁 0.5s 周期）
```

### O2 修复

- 30 秒限时
- 空格键连点修复（每次 +0.08 进度）
- 进度条从 0 到 1，达到 1 即完成

### Reactor 熔毁

- 30 秒限时
- 两个按钮（Q 键 = Button A, E 键 = Button B）
- 需要**同时按住** 0.6 秒算一次成功
- 需完成 3 次同步按压
- 松开任意键重置窗口

### 超时处理

- 倒计时归零 → `State = Failed` → `OnCriticalTaskFailed` 触发
- 对应阵营自动失败（黑帮获胜）

---

## 4. 破坏面板更新 (SabotagePanel.cs)

### 新增按钮

| 按钮 | 破坏类型 | 冷却 | 效果 |
|------|---------|------|------|
| O2中毒 | `CriticalO2` | 60s | 触发 O2 紧急任务 |
| 反应堆 | `CriticalReactor` | 60s | 触发 Reactor 紧急任务 |

### ApplySabotageLocally 路由

- `CriticalO2` → `SocialPrototypeController.TriggerCriticalTask(O2)`
- `CriticalReactor` → `SocialPrototypeController.TriggerCriticalTask(Reactor)`
- 普通破坏保持原有反射逻辑

---

## 5. 回合制整合 (GameController.cs + GamePhase.cs)

### GamePhase 新增

- `Paused` — AI 暂停状态（紧急任务期间）

### GameController

- `PauseAI()` — 将 `AiTurn` → `Paused`
- `ResumeAI()` — 将 `Paused` → `AiTurn`

---

## 文件清单

| 文件 | 操作 | 行数 |
|------|------|------|
| `EnvironmentManager.cs` | 增强 | +120 |
| `RoomDecoration.cs` | 新建 | 268 |
| `CriticalTaskSystem.cs` | 新建 | 237 |
| `SabotagePanel.cs` | 修改 | +40 |
| `SocialPrototypeController.cs` | 修改 | +85 |
| `GameController.cs` | 修改 | +18 |
| `GamePhase.cs` | 修改 | +2 |
| `SabotageType.cs` | 修改 | +2 |
*（内容由AI生成，仅供参考）*
