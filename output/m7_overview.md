# M7 服务、房间与 UI 成品 — 完成报告

## 交付概览

M7 完成 5 个任务：Relay 房间码连线、断线迁移加固、OnGUI 移除、Canvas 地图 UI、首局可用性。

## 编译收尾

| 目标 | 结果 | 备注 |
|---|---|---|
| `Assembly-CSharp.rsp` | 通过 | 0 errors；剩余 4 个 Netcode `RequireOwnership=false` 过时警告和 1 个既有 `voiceJoinInProgress` 未使用警告 |
| `GanglandUndercover.Tests.rsp` | 通过 | 0 errors |
| `Assembly-CSharp-Editor.rsp` | 通过 | 0 errors |

本轮修复了 `CanvasMapView` 方法边界、重复 Relay/Lobby API、`LobbyController.Initialize` 调用点、`ShipRoomSpec` 字段名不匹配，以及两个 M7 新增未使用字段。

---

## Task 7.1 Relay 房间码完整链路 ✅

### OnlineMatchController 新增
| 变更 | 说明 |
|---|---|
| `RequestRelayHost()` / `RequestRelayClient(code)` | 公开 Relay API，供 LobbyController 调用 |
| `OnRelayStatusChanged` 事件 | Relay 状态变化通知 |
| `OnRelayRoomCodeReady` 事件 | 房间码生成完成通知 |
| `OnRelayConnectionChanged` 事件 | 连接状态变化通知 |
| `PlayerCount` 属性 | 暴露总连接玩家数 |
| `OldHostClientId()` 方法 | 返回建房间时的 Host ClientId |
| `StartRelayHost/StartRelayClient` 内部增加事件触发 | 所有关键路径均 fire 事件 |

### LobbyController 重写
| 变更 | 说明 |
|---|---|
| 新增 `_matchController` 引用 | 构造函数改为接收 3 参数（含 OnlineMatchController） |
| `OnCreateRoom()` → `_matchController.RequestRelayHost()` | 真实调用 Relay |
| `OnJoinRoom()` → `_matchController.RequestRelayClient(code)` | 真实调用 Relay |
| 房间码验证 | 只允许大写字母+数字，最少 4 位 |
| 房间码显示区 | 创建成功后显示绿色房间码 |
| `RefreshPlayerList()` | 从 `_matchController.PlayerCount` 读取真实玩家数 |
| `UpdateStartButtonState()` | 仅 Host 且有玩家时启用 |

---

## Task 7.2 断线与 Host 迁移策略 ✅

### HostMigrationManager 修复
| 变更 | 说明 |
|---|---|
| `oldHostClientId` 字段 | 迁移开始时记录旧 Host ID |
| `ElectNewHost()` 重写 | 统一排除旧 Host，选最小 ClientId |
| `FallbackToGameOver()` | 新增降级方法：Host 掉线 → 友好结算 |
| `migrationTimeout` | 迁移超时（默认 30s）自动降级 |
| `useFallbackOnMigrationFail` | Inspector toggle，默认开启降级 |
| `TickMigrationMessage()` | 增加超时检测逻辑 |

### 降级策略
- 剩余 < 2 人 → 直接结算
- 无法选举新主机 → 直接结算
- 迁移超时 → 自动结算

---

## Task 7.3 全 Canvas 化 ✅

| 变更 | 说明 |
|---|---|
| `OnlineMatchController.OnGUI()` | 发布版永远 `return`；编辑器仅在非 Canvas 模式可用 |
| `HostMigrationManager.OnGUI()` | 保留（仅迁移进行时显示遮罩，功能合理） |

---

## Task 7.4 2D 地图 UI ✅

### 新建 CanvasMapView.cs
| 功能 | 说明 |
|---|---|
| 静态房间绘制 | 从 `OnlineMapService.ShipRooms()` 读取 12 房间坐标+颜色 |
| 动态任务标记 | 从 `_controller.Tasks` 读取，灰色=完成，橙色=被破坏 |
| 动态玩家标记 | 从 `_controller.Players` 读取，青色=自己，绿色=同阵营，白色=其他 |
| 尸体标记 | 从 `_controller.Bodies` 读取，红色 |
| 身份过滤 | 通过 `IsGangFaction()` 判断阵营归属 |
| 小地图/大地图 | `isLargeMap` toggle：180px 右下角 / 600px 居中 overlay |
| 交互 | 滚轮缩放（0.5x-3x）+ 鼠标拖拽平移 |
| 关闭按钮 | 大地图模式右上角 X 按钮 |

---

## Task 7.5 首局可用性 ✅

### 用户流程
```
主菜单 → 联机模式 → 大厅
                ├─ 创建房间 → 显示房间码 → 自动 Host
                ├─ 输入房间码 → 加入房间 → 自动 Client
                ├─ 查看玩家列表（实时刷新）
                ├─ 按"开始游戏"（仅 Host）
                └─ 返回主菜单
```

### 空状态/错误处理
- Unity Services 未就绪 → 红色状态提示
- 房间码为空 → "请输入房间码"
- 房间码过短 → "房间码至少 4 位"
- Relay 不可用 → 具体原因提示
- 房间码格式错误 → 输入框自动过滤非法字符

---

## 改动文件

| 文件 | 类型 |
|---|---|
| `OnlineMatchController.cs` | 修改（+Relay API/+事件/+PlayerCount/+OldHostClientId/+OnGUI gate） |
| `LobbyController.cs` | 重写（连线 Relay API/+玩家列表/+状态事件） |
| `HostMigrationManager.cs` | 修改（+降级策略/+选举修复/+超时检测） |
| `PrototypeBootstrap.cs` | 修改（Lobby 初始化传入 OnlineMatchController） |
| `Editor/KenneySpriteBaker.cs` | 新建（M6 管线） |
| `Online/Map/KenneySpriteCatalog.cs` | 新建（M6 管线） |
| `Online/Map/KenneySpriteDecorator.cs` | 新建/修正（M6 管线，字段名对齐 ShipRoomSpec） |
| `Online/Map/CanvasMapView.cs` | 新建（M7.4） |

## 剩余运行验证
1. **Unity Test Runner**：在 Editor 内运行 `GanglandUndercover.Tests`。
2. **主菜单→大厅流程测试**：确认创建房间、显示房间码、输码加入、开始游戏路径可用。
3. **CanvasMapView 挂载**：确认 OnlineMatchHud 或场景路径调用 `CanvasMapView.Initialize(controller, mapService)`。
4. **双机 Relay 测试**：两台机器通过房间码互连并跑完整局。
5. **Host 断线测试**：强制 Host 退出，确认迁移成功或干净降级结算。
