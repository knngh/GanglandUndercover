---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9470d846c7eff9b24afb94a99a2cb3f0_8fd320945dac11f1bd025254006c9bbf
    ReservedCode1: eN9KMl97F2NXGyqSMbxFDPnn7F6ogaUpvA0Y9JW5xC9Dao8vLAAenD1quhKagmbWiAx/ckJSyXyJPzCg2DXgn3QkKuOuFhJumCa4+LBb2EIreWy32OIHSdl1OolzToAdd9E0Vaz0Zqiy93i5BWD8CFe/z+diw6/61q3HYgodQD232Ij7f+PyGB9aArw=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9470d846c7eff9b24afb94a99a2cb3f0_8fd320945dac11f1bd025254006c9bbf
    ReservedCode2: eN9KMl97F2NXGyqSMbxFDPnn7F6ogaUpvA0Y9JW5xC9Dao8vLAAenD1quhKagmbWiAx/ckJSyXyJPzCg2DXgn3QkKuOuFhJumCa4+LBb2EIreWy32OIHSdl1OolzToAdd9E0Vaz0Zqiy93i5BWD8CFe/z+diw6/61q3HYgodQD232Ij7f+PyGB9aArw=
---

# Stage 4 改动说明：离线模式 UI 与地图系统接入

## 概述

在 SocialPrototypeController（离线单机模式入口）中实例化 GameController 策略回合制系统，并接入 PrototypeHud 和 DistrictMapView 两个 UI 组件。三者原先孤立存在，现通过桥接代码形成完整游戏循环。

## 修改文件

| 文件 | 路径 | 改动类型 |
|------|------|----------|
| SocialPrototypeController.cs | Assets/_Project/Scripts/SocialDeduction/ | 修改（+197行） |

## 详细改动

### 1. 新增 using 声明

```csharp
using GanglandUndercover.Gameplay;   // GameController, PlayerAction
using GanglandUndercover.UI;         // PrototypeHud
using GanglandUndercover.World;      // DistrictMapView
```

### 2. 新增私有字段

| 字段 | 类型 | 用途 |
|------|------|------|
| turnController | GameController | 策略回合制系统实例 |
| turnHudObject | GameObject | PrototypeHud 挂载根节点 |
| turnMapObject | GameObject | DistrictMapView 挂载根节点 |

### 3. 生命周期修改

**Awake()** — 新增 `InitTurnController()` 和 `InitTurnHud()` 调用，在 `BuildHud()` 之后、`StartGame()` 之前执行。

**OnDestroy()** — 新增 `turnMapObject` 和 `turnHudObject` 的清理逻辑。

**BuildWorld()** — 在 `CreateCharacters()` 之后新增 `InitTurnMap()` 调用。

### 4. 新增桥接方法（12个）

#### 初始化

| 方法 | 说明 |
|------|------|
| `InitTurnController()` | 创建 GameController 实例，订阅 `Changed` 事件到 `OnTurnStateChanged` |
| `InitTurnHud()` | 创建 "Turn Prototype HUD" GameObject，挂载 PrototypeHud 组件并 `Bind(turnController)` |
| `InitTurnMap()` | 创建 "District Map View" GameObject，挂载 DistrictMapView 组件并 `Bind(turnController)` |

#### 状态桥接

| 方法 | 说明 |
|------|------|
| `OnTurnStateChanged()` | 监听 GameController 状态变化，同步 `LastEvent`、`IsMeeting`、`IsGameOver` 到 SocialPrototypeController，触发 `Changed` 事件通知 SocialPrototypeHud |

#### 游戏循环 API

| 方法 | 参数 | 说明 |
|------|------|------|
| `ExecuteTurnAction()` | SocialCharacter, DistrictType, PlayerAction | 选择角色 → 移动到3D区域坐标 → 执行策略动作 |
| `StartTurnMeeting()` | 无 | 进入会议阶段，设置 IsMeeting=true |
| `CastTurnVote()` | SocialCharacter | 投票指定角色，Kill + RemoveBodies + 胜利检测 |
| `SkipTurnVote()` | 无 | 跳过投票，清空会议状态 |

#### 映射工具

| 方法 | 说明 |
|------|------|
| `GetDistrictForZone(string)` | 区域名 → DistrictType（货柜码头→Dockyard, 夜市巷→NightMarket 等） |
| `GetDistrictWorldPosition(DistrictType)` | DistrictType → 3D 世界坐标，与 BuildWorld 中区域位置对应 |
| `GetFactionForRole(SocialRole)` | SocialRole → Faction 双向转换 |
| `GetRoleForFaction(Faction)` | Faction → SocialRole 双向转换 |

## 架构设计要点

### 双 UI 共存

- **SocialPrototypeHud**（实时模式 UI）：通过 `BuildHud()` 在 Awake 中创建，绑定 SocialPrototypeController，处理 W/A/S/D 移动、任务挑战、紧急报告
- **PrototypeHud**（回合策略 UI）：通过 `InitTurnHud()` 在 Awake 中创建，绑定 GameController，处理角色选择、区域点击、行动执行

两者均为 ScreenSpaceOverlay Canvas，共享屏幕空间。SocialPrototypeHud 的 `Changed` 事件和 PrototypeHud 的 `GameController.Changed` 事件通过 `OnTurnStateChanged` 桥接同步状态。

### 3D 区域节点与策略层映射

DistrictMapView 在 Build() 中创建的 6 个 3D 节点（Dockyard / WarehouseRow / NightMarket / PolicePrecinct / Clinic / TenementBlock）自动对应 BuildWorld() 中的同名区域。每个节点上的 DistrictNode 组件通过 `OnMouseDown` 触发 `controller.SelectDistrict()`，无需额外点击处理代码。

### 回合推进流程

```
选择身份(PrototypeHud) → GameController.SelectFaction()
  → Changed 事件 → OnTurnStateChanged() 更新 LastEvent
  → 玩家点击区域(DistrictMapView/PrototypeHud) → GameController.SelectDistrict()
  → 玩家点击行动(PrototypeHud) → GameController.RunPlayerAction()
  → 内部执行 AI 回合 + 事件 + 日期推进
  → Changed 事件 → OnTurnStateChanged() 同步状态
```

## 未修改文件

以下文件保持原样，无需修改即可通过桥接工作：

- **PrototypeHud.cs** — Bind(GameController) 签名匹配
- **DistrictMapView.cs** — Bind(GameController) 签名匹配
- **DistrictNode.cs** — OnMouseDown 自动工作
- **GameController.cs** — 无 MonoBehaviour 依赖，纯 C# 可任意处实例化
- **GameState.cs / ActionResolver.cs / OpponentAi.cs / EventResolver.cs / VictoryEvaluator.cs** — GameController 内部依赖，无改动
- **SocialPrototypeHud.cs** — 已有 Bind(SocialPrototypeController) 不变

## 产出文件

本次任务仅修改 1 个文件，无新增文件。改动摘要即本文档。
*（内容由AI生成，仅供参考）*
