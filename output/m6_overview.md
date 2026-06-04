# M6 2D 港区玩法地图 — 代码收尾

## 编译错误修复
- `MapValidator.cs` — `BfsReachable` 的 `maxSteps` 需要 `int`，灰盒可达性检查已改为 `Mathf.CeilToInt(MaxDistBetweenSpawnAndTask / GridStep)`。
- `OnlineMatchController.cs` — 小游戏完成证据写回改为 `taskService.AddEvidence(gain, status)`，避免写入只读转发属性，并修复证据板日志里 `gain` 作用域错误。
- `OnlineMatchController.cs` — 监控摄像头生成改用 `networkManager != null && networkManager.IsServer`，避免在非 `NetworkBehaviour` 控制器中直接访问 `IsServer`。
- `OnlineMatchController.cs` — Host/Relay Host 启动后补生成监控 `NetworkObject`，并在 Host simulation 中 tick `OnlineSecurityCamera.ServerTick()`。
- `OnlineSecurityCamera.cs` — ClientRpc target helper 从 `RpcTarget` 改名为 `SingleTarget`，避免遮蔽 `NetworkBehaviour.RpcTarget`。

## 编译验证

| Target | Result | Notes |
|---|---|---|
| `Assembly-CSharp.rsp` | Pass | 0 error；剩余 Netcode `RequireOwnership=false` 过时 warning + 既有 `voiceJoinInProgress` 未使用 warning |
| `GanglandUndercover.Tests.rsp` | Pass | No compile errors |
| `Assembly-CSharp-Editor.rsp` | Pass | No compile errors |

## M6 交付物

### 新建文件

| 文件 | 行数 | 说明 |
|---|---|---|
| `Assets/_Project/Scripts/Online/Map/MapLayoutData.cs` | ~250 | ScriptableObject 灰盒地图布局数据源，含房间/走廊/任务/暗线/监控/遮视线定义 + AABB视线检测 |
| `Assets/_Project/Scripts/Online/Map/GreyboxMapBuilder.cs` | ~290 | 灰盒建造器，从 MapLayoutData 生成纯几何地图（不加载任何美术资源） |
| `Assets/_Project/Scripts/Online/Map/MapValidator.cs` | ~320 | 地图验证器 — BFS可达性、任务分布均匀度、暗线连通性、监控覆盖率、房间连通性 |
| `output/m6_playtest_and_art_strategy.md` | ~140 | M6.2 多人测试计划 + M6.3 美术替换策略 |

### 修改文件

| 文件 | 变更 |
|---|---|
| `OnlineMapService.cs` | 新增 `SurveillanceZoneSpec` 结构 + `SurveillanceZones()` 方法（6个摄像头布点，设计坐标） |
| `OnlineMatchController.cs` | 新增 `mapLayoutData`/`useGreyboxMode` 字段；新增 `SpawnSurveillanceCameras()` 和 `BuildGreyboxMap()` 方法；添加 `GanglandUndercover.Online.Map`/`Surveillance` using |
| `OnlineMiniGameBridge.cs` | M5 小游戏联机桥：任务/修复小游戏打开、客户端提交、服务器校验、ClientRpc 反馈 |
| `OnlineSecurityCamera.cs` | M5/M6 联机监控组件：服务器维护可视区玩家集合，只下发摄像头区域内的轻量位置数据 |

## 架构决策

- **设计/世界坐标双层分离**：灰盒建造器只操作设计坐标，WorldBuilder 内部负责 `ScaleMapPosition/ScaleMapSize` 转换
- **灰盒模式可选**：`useGreyboxMode` toggle，不影响现有视觉层
- **监控摄像头布点**：从 `OnlineMapService.SurveillanceZones()` 读取数据 → Host 运行时生成 `OnlineSecurityCamera` NetworkBehaviour 并由服务端 tick 推送裁切后的可见玩家数据
- **walkableRects 坐标**：世界坐标 Rect，灰盒加入前做坐标转换

## 后续
- 关闭当前 Unity Editor 后运行 Unity Test Runner 与本机 Host/Client 双开烟测。
- 验证监控站实时刷新：双开确认客户端只收到当前摄像头区域内玩家数据。
- 将 Netcode `ServerRpc(RequireOwnership=false)` 迁移到新版 `[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]`，消除过时 warning。
- 执行 M6.2 灰盒多人测试；M6.3 tile/sprite 替换仍是策略文档，尚未完成 80% 美术替换。
- M7: Relay 房间码 + Canvas UI + Host迁移
