---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9470d846c7eff9b24afb94a99a2cb3f0_d8fc695a5f0811f18d42525400d9a7a1
    ReservedCode1: WJl+mA34kgGa0jXdIhhUlBeZuxzyyajxA4Hh76hkw6Z8UQmM05BIReeYpq6JK05DacXPgebmAM3kYuwuDUak1NTW3qWV2zz8LqRfFiGwXMSBmQGfvPPh9HeW6Vm+K4SOAuswVHo8NFqvgEkWuI8+UaWKbOcCfUWMzp9yW4AocK9GE3WzGxupnZbQyS4=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9470d846c7eff9b24afb94a99a2cb3f0_d8fc695a5f0811f18d42525400d9a7a1
    ReservedCode2: WJl+mA34kgGa0jXdIhhUlBeZuxzyyajxA4Hh76hkw6Z8UQmM05BIReeYpq6JK05DacXPgebmAM3kYuwuDUak1NTW3qWV2zz8LqRfFiGwXMSBmQGfvPPh9HeW6Vm+K4SOAuswVHo8NFqvgEkWuI8+UaWKbOcCfUWMzp9yW4AocK9GE3WzGxupnZbQyS4=
---

# Stage 14 — 第二张地图：警察局 PoliceStation

**日期**：2026-06-03
**目标**：对标 Among Us 缩小差距 —— 新增 PoliceStation 地图及配套系统。

---

## 完成内容

### 1. PoliceStationMap.cs — 新地图空间定义

**路径**：`/Assets/_Project/Scripts/World/PoliceStationMap.cs`

定义了 6 个区域的完整静态数据：

| 区域 | 枚举值 | 坐标 | 主色 | 邻接区域 |
|------|-------|------|------|---------|
| Lobby（大厅） | `Lobby` | (-3, 0, 0) | 白/灰 | Interrogation, Evidence, Briefing |
| Interrogation（审讯室） | `Interrogation` | (-8, -4, 0) | 深蓝/冷色 | Lobby, Cells |
| Evidence（证物室） | `Evidence` | (2, -4, 0) | 黄/棕 | Lobby, Armory |
| Armory（武器库） | `Armory` | (7, -4, 0) | 金属灰 | Evidence, Briefing |
| Cells（拘留室） | `Cells` | (-8, 2, 0) | 铁灰 | Interrogation |
| Briefing（简报室） | `Briefing` | (7, 2, 0) | 绿/蓝 | Lobby, Armory |

**附加数据**：
- 任务站位置（5 个任务站，每个区域一个）
- 玩家出生点（Lobby 中央）
- Bot 出生点（4 个分散位置）
- 通风管配置（3 条通风路径，用于快速移动/潜入）
- 监控节点位置（3 个摄像头覆盖关键区域）
- 紧急按钮位置（Lobby 中心）

---

### 2. PoliceStationTasks.cs — 警察局专属任务

**路径**：`/Assets/_Project/Scripts/SocialDeduction/PoliceStationTasks.cs`

5 个警察局主题任务 + 小游戏映射表：

| 任务名 | 类型 | 复用/新建 | 说明 |
|-------|------|----------|------|
| 整理档案 | Sort | 复用 SortTask | 拖拽文件归类到对应卷宗 |
| 调取监控 | Scan | 复用 ScanTask | 圆形扫描锁定监控画面 |
| 武器清点 | Tap | 复用 TapTask | 快速点击核对武器编号 |
| 审讯记录 | Keypad | 复用 KeypadTask | 输入嫌疑人审讯编号 |
| 证据归档 | **新建** | EvidenceArchiveTask | 拖拽物证到对应案件槽 |

**`GetMiniGameType(taskName)`**：将中文字符串任务名映射到 `MiniGameType?` 枚举，供 SocialPrototypeController 路由。

**`GenerateEvidencePuzzle()`**：证据归档谜题生成器，随机生成 4-6 个物证 + 3-4 个案件槽的匹配关系。

---

### 3. EvidenceArchiveTask.cs — 新小游戏

**路径**：`/Assets/_Project/Scripts/SocialDeduction/MiniGames/EvidenceArchiveTask.cs`

约 380 行的拖拽证据归档小游戏：
- 继承 `MiniGameBase`
- 运行时构建 Canvas UI（物证卡片 + 案件槽位）
- 拖拽交互系统（鼠标/触控）
- 吸附检测（距离阈值判断是否归入正确槽位）
- 完成判定（所有物证正确归档后触发 `OnTaskCompleted`）

---

### 4. MiniGameType.cs — 小游戏类型枚举

**路径**：`/Assets/_Project/Scripts/SocialDeduction/MiniGames/MiniGameType.cs`

```csharp
public enum MiniGameType
{
    WireTask,      // 连线类
    MemoryTask,    // 记忆类
    SwipeCardTask, // 刷卡/扫描类
    KeypadTask,    // 数字密码键盘
    SortTask,      // 拖拽分类/排序
    ScanTask,      // 圆形扫描
    TapTask,       // 快速点击
}
```

---

### 5. MapType.cs — 地图类型枚举

**路径**：`/Assets/_Project/Scripts/World/MapType.cs`

```csharp
public enum MapType
{
    GanglandDistrict,  // 九龙港区（默认）
    PoliceStation,      // 警察局
}
```

---

### 6. SocialPrototypeController.cs 修改

**路径**：`/Assets/_Project/Scripts/SocialDeduction/SocialPrototypeController.cs`

| 修改点 | 说明 |
|-------|------|
| 新增 `CurrentMapType` 属性和 `SetMapType()` 方法 | 运行时切换地图类型 |
| `BuildWorld()` 分支 | 根据 `CurrentMapType` 调用 `BuildGanglandWorld()` 或 `BuildPoliceStationWorld()` |
| 新增 `BuildPoliceStationWorld()` | 构建警察局6个区域、房间渲染、5个任务站、监控节点、通风管、紧急按钮 |
| `CreateCharacters()` 重构 | 拆分为 `BuildPoliceStationCharacters()`（2 Police + 1 Undercover + 1 Mole）和 `BuildGanglandCharacters()`（1:1:1:1） |
| 新增 `BuildPoliceStationVents()` | 警察局通风管系统 |
| `PickMiniGameType()` 扩展 | 优先匹配 `PoliceStationTasks.GetMiniGameType()`，支持警察局任务名路由 |

---

### 7. PrototypeBootstrap.cs 修改

**路径**：`/Assets/_Project/Scripts/Gameplay/PrototypeBootstrap.cs`

| 修改点 | 说明 |
|-------|------|
| 新增 `_offlineMapType` 字段 | 默认为 `GanglandDistrict` |
| `StartOfflineGame()` 签名扩展 | 新增 `MapType mapType` 参数 |
| `BuildOfflinePrototype()` | 在 `StartOfflineMode` 前调用 `controller.SetMapType(_offlineMapType)` |

---

### 8. MainMenuController.cs — 地图选择UI

**路径**：`/Assets/_Project/Scripts/UI/MainMenuController.cs`

离线面板新增地图选择区域（位于身份卡片下方、开始按钮上方）：

| 元素 | 说明 |
|------|------|
| 两个地图按钮 | "九龙港区" / "警察局"，点击切换 + 高亮条动画 |
| 地图描述文本 | 根据选中地图动态切换描述文案 |
| `OnMapSelected(index)` | 更新选中状态、高亮条位置、描述文本 |
| `OnStartOffline()` | 将地图类型透传给 `_bootstrap.StartOfflineGame(role, mapType)` |

---

## 文件清单

| 文件 | 类型 | 说明 |
|------|------|------|
| `World/MapType.cs` | 新建 | 地图类型枚举 |
| `World/PoliceStationMap.cs` | 新建 | 警察局6区域完整地图数据 |
| `SocialDeduction/PoliceStationTasks.cs` | 新建 | 警察局5个任务定义 + 映射表 |
| `SocialDeduction/MiniGames/EvidenceArchiveTask.cs` | 新建 | 证据归档拖拽小游戏（约380行） |
| `SocialDeduction/MiniGames/MiniGameType.cs` | 新建 | 小游戏类型枚举 |
| `SocialDeduction/SocialPrototypeController.cs` | 修改 | BuildWorld分支 + CreateCharacters重构 + PickMiniGameType扩展 |
| `Gameplay/PrototypeBootstrap.cs` | 修改 | 地图参数透传 |
| `UI/MainMenuController.cs` | 修改 | 地图选择UI |

**新建文件**：5 个
**修改文件**：3 个

---

## 未完成 / 注意事项

1. **证据归档小游戏**：`EvidenceArchiveTask.cs` 的 MiniGameType 枚举中尚未新增 `EvidenceArchiveTask` 类型，当前在 `PoliceStationTasks.cs` 中暂时映射到 `SortTask` 占位。待小游戏验证通过后，需：
   - 在 `MiniGameType.cs` 中新增 `EvidenceArchiveTask` 枚举值
   - 在 `PoliceStationTasks.cs` 中修改映射
   - 在 `PickMiniGameType()` 中添加 case 分支

2. **地图缩略图**：主菜单当前使用纯文字按钮 + 色块区分地图，未实现缩略图图片预览。后续需准备两张地图截图作为缩略图资源，在 `OnMapSelected` 中切换 Image sprite。

3. **联机模式地图选择**：当前仅离线面板有地图选择 UI。联机模式（Lobby）的地图选择需在后续 LobbyController 改造中实现。

4. **编译验证**：所有代码编写完成，但未在 Unity Editor 中进行编译测试。建议在 Editor 中打开项目验证零编译错误后再进行运行时测试。
*（内容由AI生成，仅供参考）*
