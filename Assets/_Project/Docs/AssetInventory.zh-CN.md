# 港区潜线：素材资源清单

更新时间：2026-05-04 18:59

当前结论：正式垂直切片只使用稳定导入的免费资源和项目内程序化兜底层。`LowpolyStreetPack` 已从 Unity 主资源路径隔离，不能再作为正式地图、任务或装饰依赖，除非后续重新从 Asset Store 干净导入并通过烟测。

## 可用于正式垂直切片

| 类别 | 资源包 | 当前路径 | 体量 | 用法 |
| --- | --- | --- | ---: | --- |
| Environment | SimplePoly City - Low Poly Assets | `Assets/_Project/Resources/AssetStore/SimplePoly City - Low Poly Assets` | 约 138 个 prefab | 港区建筑、店铺、车辆、街景主表现 |
| Environment | ModularLowpolyStreetsFree | `Assets/_Project/Resources/AssetStore/ModularLowpolyStreetsFree` | 约 33 个 prefab | 道路、路口、行人道、路灯、垃圾桶、消防栓、路锥 |
| Environment | Quaternius ModularSciFiMegaKit | `Assets/_Project/Art/ThirdParty/Quaternius/ModularSciFiMegaKit` | 约 322 个模型/贴图资源 | 工业港口、仓库、设备、未来感任务件；运行时镜像到 `Resources/Quaternius` |
| Props | Synty PolygonGeneric | `Assets/_Project/Resources/AssetStore/Synty/PolygonGeneric` | 约 438 个 prefab | 任务道具、货箱、开关、电箱、纸张、瓶罐、FX |
| Props | Synty PolygonStarter | `Assets/_Project/Resources/AssetStore/Synty/PolygonStarter` | 约 58 个 prefab | 原型建筑件、门框、地面、基础角色和道具补位 |
| Characters | DenysAlmaral CityPeople | `Assets/_Project/Resources/AssetStore/DenysAlmaral/CityPeople` | 约 29 个 prefab | 市民、警员、工人、会议席位角色适配 |
| Characters | Synty PolygonStarter characters | `Assets/_Project/Resources/AssetStore/Synty/PolygonStarter/Prefabs/Characters` | 已接入若干角色 | 警方、黑帮、卧底的统一风格角色兜底 |
| Audio | Free Pack | `Assets/_Project/Resources/AssetStore/Free Pack` | 约 51 个 wav | 临时事件音效、会议提示、破坏、击倒、结果反馈 |
| UI | 项目内 Canvas / 程序化 UI | `Assets/_Project/Scripts/Online/OnlineMatchHud.cs` | 已运行 | 当前主菜单、房间、HUD、大地图、任务、会议、投票、结算 |

## 已进入运行路径的资源

1. 角色：`OnlineMatchController.CharacterAdapters.cs` 优先加载 Synty / DenysAlmaral 角色 prefab，失败时才用程序化外观。
2. 场景：`OnlineMatchController.cs` 使用 SimplePoly、ModularLowpolyStreetsFree、Synty、Quaternius 填充港区建筑、道路、车辆、道具和工业设备。
3. 任务：`OnlineMatchController.VerticalSlice.cs` 使用 Synty / SimplePoly 的监控、电箱、录音、车牌追踪相关 set piece。
4. 音频：`OnlineMatchController.cs` 从 `Free Pack` 读取局内事件音效，找不到时用程序化音频兜底。

## 已隔离资源

| 资源包 | 原路径 | 隔离路径 | 原因 | 后续处理 |
| --- | --- | --- | --- | --- |
| LowpolyStreetPack | `Assets/_Project/Resources/AssetStore/LowpolyStreetPack` | `TempAssets/Quarantined/LowpolyStreetPack` | 大量 `.meta` 缺少合法 32 位 GUID，Unity 会忽略或警告，不能作为正式表现依赖 | 只允许重新从 Unity Package Manager / Asset Store 干净导入；导入后必须通过烟测资源基线门禁 |

## 资源使用原则

1. 正式切片优先使用 SimplePoly + ModularLowpolyStreetsFree 搭街区骨架。
2. 港口、仓库、设备密度优先用 Quaternius + Synty PolygonGeneric。
3. 角色优先用 DenysAlmaral / Synty，并逐步替换成带动画控制器的正式 prefab。
4. 程序化方块只保留为缺资源兜底，不能作为第一屏主表现。
5. 新资源进入项目后必须先写入本清单，再接入运行路径和烟测。

## 阶段 0 验收项

1. `Assets/_Project/Resources/AssetStore/LowpolyStreetPack` 不存在。
2. `Assets/_Project/Scripts`、`Assets/_Project/Editor`、`Assets/_Project/Scenes` 不再引用 `LowpolyStreetPack`。
3. `Gangland/Run Smoke Tests` 必须通过资源基线门禁。
4. `Gangland/Play Online Demo` 能打开并刷新演示截图。
