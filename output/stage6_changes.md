---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9470d846c7eff9b24afb94a99a2cb3f0_289345245dc111f1a4f35254002afed2
    ReservedCode1: Jf98okOmTbwyxRZKX7E4QTcGtYbJeXQ/fVLttOTaavcGe80UB19Pi/ZyKgeh22m6daYgBABDCmF6jqOdAQND3JDVnBphowmxsAf5M61NwUQVaPpyxJXSnNss/fvFVmmK5ibrF2BlUhrC4RDR+/pf3S8DDWxy9nz2PNsXvDqRVp7Adv5AF+SeD5lT7hk=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9470d846c7eff9b24afb94a99a2cb3f0_289345245dc111f1a4f35254002afed2
    ReservedCode2: Jf98okOmTbwyxRZKX7E4QTcGtYbJeXQ/fVLttOTaavcGe80UB19Pi/ZyKgeh22m6daYgBABDCmF6jqOdAQND3JDVnBphowmxsAf5M61NwUQVaPpyxJXSnNss/fvFVmmK5ibrF2BlUhrC4RDR+/pf3S8DDWxy9nz2PNsXvDqRVp7Adv5AF+SeD5lT7hk=
---

# Stage 6: 社交推理 — 联机模式启动与场景统一

**日期**: 2026-06-01  
**目标**: 模式选择 + 离线/联机互斥启动 + 网络层审查

---

## 1. 修改清单

| 文件 | 改动量 | 说明 |
|------|--------|------|
| `Assets/_Project/Scripts/Gameplay/PrototypeBootstrap.cs` | +60 行 | 新增 GameMode 枚举、Offline 分支、互斥清理 |
| `Assets/_Project/Scripts/SocialDeduction/SocialPrototypeController.cs` | +22 行 | AutoStartOnAwake 标志、StartOfflineMode 入口 |

---

## 2. PrototypeBootstrap.cs — 模式选择架构

### 2.1 GameMode 枚举

```csharp
public enum GameMode
{
    Offline,
    Online
}
```

### 2.2 Inspector 配置

```csharp
[SerializeField] private GameMode _mode = GameMode.Online;
[SerializeField] private SocialRole _offlinePlayerRole = SocialRole.Undercover;
```

默认 `GameMode.Online` 保持向后兼容。在 Unity Editor 中可将 `_mode` 切换为 `Offline`，并通过 `_offlinePlayerRole` 选择玩家身份（Undercover/Gang/Police）。

### 2.3 Awake 分支

```
_mode == Offline ?
  → BuildOfflinePrototype()
  → 创建 SocialPrototypeController（GameObject "Port Undercover Offline"）
  → AutoStartOnAwake = false  → 手动调用 StartOfflineMode(role)

_mode == Online ?
  → BuildOnlinePrototype()   [原有逻辑]
  → 创建 UnityServiceBootstrap + OnlineMatchController
```

### 2.4 互斥保证

两种模式在启动时相互销毁对方的组件，避免场景中同时存在两个控制器：

- **Offline 启动时**：销毁已有的 `OnlineMatchController` + `UnityServiceBootstrap`
- **Online 启动时**：销毁已有的 `SocialPrototypeController`

`DontDestroyOnLoad` 在运行时保护 Offline 模式对象不被场景重载清除，Offline 模式从 Awake 立即启动游戏，无需额外触发。

---

## 3. SocialPrototypeController.cs — 移除硬编码自启动

### 3.1 AutoStartOnAwake 属性

```csharp
public bool AutoStartOnAwake { get; set; } = true;
```

- 默认 `true`，保持 Awake 自动启动的原有行为（向后兼容）
- Bootstrap 在 Offline 模式下设为 `false`，接管启动控制

### 3.2 StartOfflineMode 入口

```csharp
public void StartOfflineMode(SocialRole role)
{
    if (HasStarted) return;
    StartGame(role);
}
```

Guard 子句防止重复初始化。`HasStarted` 由 `StartGame()` 在第 236 行设为 true。

---

## 4. Prototype.unity 场景改造（Editor 操作）

以下操作需在 Unity Editor 中手动完成：

1. 选中场景中的 `PrototypeBootstrap` GameObject
2. 在 Inspector 中将 `_mode` 下拉框切换为 `Offline` 即启动离线模式
3. `_offlinePlayerRole` 下拉框选择初始身份（Undercover/Gang/Police）

**无需额外场景配置**——两个控制器均由 Bootstrap 的 Awake 动态创建和销毁，场景中只保留 PrototypeBootstrap 一个根对象。

---

## 5. OnlineMatchController 网络层审查

**审查范围**: `Assets/_Project/Scripts/Online/OnlineMatchController.cs`（12335 行）

### 5.1 网络栈初始化

```
EnsureNetworkStack() [lines ~1114-1143]
  → FindAnyObjectByType<NetworkManager>()
  → 不存在则 new GameObject("NetworkManager") + AddComponent<NetworkManager> + AddComponent<UnityTransport>
  → OnClientConnectedCallback / OnClientDisconnectCallback 绑定
```

**问题**:
- [ ] **避免动态创建 NetworkManager**。Unity Netcode 推荐使用场景中预配置的 NetworkManager prefab。动态创建缺乏 Prefab 覆盖、网络预制体列表等配置能力。
- [ ] **NetworkManager 的 NetworkConfig 完全依赖 UnityTransport 默认值**。未显式设置 TickRate、MaxPayloadSize、ConnectionApproval 等关键参数。

### 5.2 Transport 配置

```
ConfigureTransport(string address) [line ~1828]
  transport.UseWebSockets = false;
  transport.UseEncryption = false;
  transport.SetConnectionData(address, DefaultPort=7777);
```

**问题**:
- [ ] **无传输加密**。`UseEncryption = false` 意味着所有网络流量明文传输。原型阶段可接受，但需记录为上线前必须修复的安全项。
- [ ] **端口硬编码 7777**。多实例开发或端口冲突场景下无自动端口协商。

### 5.3 消息系统

```
RegisterMessages() [line ~1833]
  → 5 条命名消息:
    ClientStateMessage    → ReceiveClientState     [客户端状态快照]
    ClientActionMessage   → ReceiveClientAction    [玩家行动]
    ClientProfileMessage  → ReceiveClientProfile   [玩家档案]
    ServerSnapshotMessage → ReceiveServerSnapshot  [服务端广播]
    RoleAssignMessage     → ReceiveRoleAssign      [角色分配]
```

**评价**: 使用 `RegisterNamedMessageHandler` + `FastBufferWriter` 是标准 NGO 模式，结构清晰。消息命名使用 `const string` 避免拼写错误。

**问题**:
- [ ] **无消息优先级/可靠 vs 非可靠区分**。所有消息走同一通道，Snapshot 和 Action 同等对待。
- [ ] **Snapshot 广播频率极高**（0.08s = 12.5Hz）。对于移动/弱网客户端可能造成带宽压力。建议降至 5Hz 并加增量压缩。

### 5.4 Relay（中继服务）

```
StartRelayHost() / StartRelayClient() [lines ~1320-1530]
  → Unity.Services.Relay.RelayService.Instance.CreateAllocationAsync()
  → NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData()
  → StartHost() / StartClient()
  → 失败时回退到本地试玩模式 (StartLocalPreviewRoom)
```

**问题**:
- [ ] **无重试/退避策略**。Relay 分配失败后直接回退，不尝试重连。
- [ ] **无 Relay 区域选择**。`CreateAllocationAsync` 未传 region 参数，可能导致跨区域高延迟。
- [ ] **房间码明文传输**。Join Code 无校验/加密，有被暴力枚举风险。

### 5.5 客户端连接/断连处理

```
HandleClientConnected(ulong) [line ~1857]:
  → 分配出生点 → 初始化 OnlinePlayerState → BroadcastSnapshot → UpsertLocalPlayer/SendClientProfile

HandleClientDisconnected(ulong) [line ~1890]:
  → 清理 players/votes/killCooldowns/abilityCooldowns/bot data/bodies
  → EvaluateWinConditions → BroadcastSnapshot
```

**问题**:
- [ ] **无重连机制**。客户端断开后必须重新创建房间，无法恢复游戏状态。
- [ ] **Host 迁移缺失**。Host 断线后所有客户端断开，无自动 Host 迁移逻辑。
- [ ] **UpsertLocalPlayer 在 client 连接时也调用**。可能造成双端玩家对象创建冲突。

### 5.6 数据同步架构

**整体架构**: 手动序列化 + 命名消息 + Host 权威。不使用 `NetworkVariable` / `NetworkBehaviour`。

**评价**: 这是有意的设计选择（2.5D 游戏，自定义物理和 AI）。手动序列化可以精确控制带宽，但长期维护成本高。

**问题**:
- [ ] **无客户端预测/插值**。客户端移动完全依赖服务端 Snapshot 回传，本地无预测。会导致明显的输入延迟。
- [ ] **无网络时间同步**。客户端和服务端无统一时钟，timing 相关逻辑（cooldown、任务计时）可能出现不同步。
- [ ] **无服务端反作弊校验**。所有 ClientAction 在 Host 端直接执行，没有合法性检查（如超速移动、非法的 kill 距离）。

### 5.7 世界构建与网络层分离

Good: 世界构建代码（8000 行起：`CreateProp`、`CreateModelProp`、`CreateShipLikeSightlineWalls` 等 2.5D 场景元素）与网络层完全解耦，不依赖网络状态。这意味着离线模式可以复用大量场景构建代码。

---

## 6. 风险评估

| 风险 | 级别 | 说明 |
|------|------|------|
| 编辑器热重载时两个模式组件共存 | 低 | 互斥清理仅在 Awake 执行，热重载可能绕过。已通过 Application.isPlaying 判断处理 |
| SocialPrototypeController 直接挂场景中 | 低 | AutoStartOnAwake 默认 true 作为安全兜底，不会导致场景无响应 |
| Online-only 项目引入离线依赖 | 无 | `using GanglandUndercover.SocialDeduction` 仅在 Offline 分支被引用 |

---

## 7. 后续建议

1. **草稿6.1** — 在 OnlineMatchController 中补 NetworkConfig（TickRate=64, ConnectionApproval）
2. **草稿6.2** — 为 Relay 添加重试和区域选择
3. **草稿6.3** — 离线模式的场景世界构建从 OnlineMatchController 复用（避免两套世界构建代码）
4. **草稿6.4** — 添加客户端预测（Rigidbody2D + 服务端校正）降低输入延迟
*（内容由AI生成，仅供参考）*
