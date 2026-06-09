# Gangland Undercover — 网络消息契约表（完整版）

> 审计日期: 2026-06-09 | 26 个网络接口 | 接口盘点: 100%

---

## 1. 自定义命名消息 (Custom Named Messages) — 11 条

| # | 消息名 | 方向 | Payload | Handler | 发送方 | 校验项 | MapSelect修复 |
|---|--------|------|---------|---------|--------|--------|---------------|
| 1 | GanglandClientState | C→S (80ms) | position(x,y,z), input(x,y), ready(bool) | ReceiveClientState | SendClientState | IsServer；position/input 必须 finite；Action 阶段只接受已知玩家；input 单位向量钳制；Ready 仅 Lobby/未开局可变 | — |
| 2 | GanglandClientAction | C→S | actionType(int), targetId(ulong) | ReceiveClientAction | SendClientAction | IsServer；action enum 白名单；TryGetValue；Phase(Lobby/Opening/Result→reject)；!player.Alive→reject | — |
| 3 | GanglandClientProfile | C→S | displayName(string) | ReceiveClientProfile:741 | SendClientProfile:723 | IsServer, TryGetValue | — |
| 4 | GanglandServerSnapshot | S→All | full State(phase/players/bodies/votes/cases) | ReceiveServerSnapshot | BroadcastSnapshot | Client-only；sender == Server；phase/criticalTask enum 白名单；snapshot count 上限；role/profession 规整到已定义值 | — |
| 5 | GanglandRoleAssign | S→SingleClient | roleByte(byte) | ReceiveRoleAssign | SendRole | sender == Server；role enum 白名单；定向单播 | — |
| 6 | GanglandChatSend | C→S | message(string), channel(int) | ReceiveChatSend:1289 | SendChatMessage:1280 | IsServer, TryGetValue | — |
| 7 | GanglandChatBroadcast | S→All/Single | message, channel, senderName | ReceiveChatBroadcast:1387 | Re-broadcast from ReceiveChatSend | 频道路由: Meeting→All, Ghost→deadOnly, Proximity→nearbyAlive, Global→allAlive | — |
| 8 | GanglandMapSelect | S→All | mapTypeIndex(int) | ReceiveMapSelect | SetActiveMapType → SendNamedMessageToAll | sender == Server；mapType enum 白名单；**✅ 修复**: SendNamedMessage(id=0) → SendNamedMessageToAll | **已修复** |
| 9 | GanglandHostHeartbeat | S→All (Unreliable) | timestamp(float) | HandleHeartbeat:431 | SendHeartbeat:137 | Non-host client only | — |
| 10 | GanglandHostMigration | S→All | fullState snapshot (ReliableFragmentedSequenced) | HandleMigrationSnapshot:442 | BroadcastMigrationSnapshot:285 | Version check, IsValid | — |
| 11 | GanglandCharacterCustom | C→S / S→Clients | objectId, jsonLength, jsonData | OnCustomMessageReceived | BroadcastCustomData / SendCustomDataToClients | Server 校验 sender == NetworkObject.OwnerClientId；bounded json；S→Clients 定向转发 | — |

---

## 2. ServerRpc — 4 条

| # | 方法 | 文件:行 | Sender→Server payload | 服务端校验 | 失败处理 |
|---|------|---------|----------------------|-----------|----------|
| 12 | SubmitTaskResultServerRpc | MiniGameBridge:55 | taskId(int), success(bool) | ValidateAndCompleteTask (alive/taskExists/notCompleted/notSabotaged/phase) | RejectResultClientRpc / CancelTaskClientRpc |
| 13 | SubmitRepairResultServerRpc | MiniGameBridge:134 | taskId, success | ValidateAndRepairTask (alive/taskExists/mustBeSabotaged/phase) | CancelTaskClientRpc |
| 14 | StartWatchingServerRpc | SecurityCamera:82 | — | IsServer | — |
| 15 | StopWatchingServerRpc | SecurityCamera:92 | — | IsServer | — |

---

## 3. ClientRpc — 8 条

| # | 方法 | 文件:行 | 触发方 | 定向/广播 |
|---|------|---------|--------|----------|
| 16 | StartTaskClientRpc | MiniGameBridge:80 | Server→特定Client | 定向（SingleTarget） |
| 17 | RejectStartClientRpc | MiniGameBridge:86 | Server→特定Client | 定向 |
| 18 | RejectResultClientRpc | MiniGameBridge:92 | Server→特定Client | 定向 |
| 19 | CancelTaskClientRpc | MiniGameBridge:98 | Server→特定Client | 定向 |
| 20 | TaskDoneClientRpc | MiniGameBridge:105 | Server→所有Client | 广播 |
| 21 | StartRepairClientRpc | MiniGameBridge:157 | Server→特定Client | 定向 |
| 22 | RepairDoneClientRpc | MiniGameBridge:164 | Server→所有Client | 广播 |
| 23 | UpdateWatcherClientRpc | SecurityCamera:99 | Server→特定Client | 定向（SingleTarget） |

---

## 4. NetworkVariable — 3 个

| # | 字段 | 文件:行 | 类型 | 默认值 | 读写权限 |
|---|------|---------|------|--------|----------|
| 24 | ZoneCenterNet | SecurityCamera:16 | NetworkVariable<Vector2> | zero | Server写/自动同步 |
| 25 | ZoneSizeNet | SecurityCamera:17 | NetworkVariable<Vector2> | (6,4) | Server写/自动同步 |
| 26 | CameraLabelNet | SecurityCamera:18 | NetworkVariable<FixedString32Bytes> | "监控摄像头" | Server写/自动同步 |

---

## 5. 测试覆盖

| 消息组 | 测试文件 | 状态 |
|--------|---------|------|
| ClientState/Action/Profile | CoreSystemTests.cs / MatchLoopPlayTests.cs | ✅ ClientState/Action 边界已覆盖；Profile 仍走基础路径 |
| ServerSnapshot/RoleAssign/MapSelect | CoreSystemTests.cs | ✅ sender、phase/count、role、mapType 边界已覆盖 |
| Task/Repair RPC | MiniGameOnlineIntegrationPlayTests.cs | ⏳ |
| 双端消息路由 | RelayTwoProcessPlayTests.cs | ⏳ |
| 越权操作 | AntiCheatPlayTests.cs（待新建） | ❌ |
| 断线重连 | HostMigrationTest（待新建） | ❌ |
| Chat频道 | ChatChannelTest（待新建） | ❌ |

---

## 6. 已知风险点

| 风险 | 消息 | 说明 |
|------|------|------|
| 🟡 MiniGameBridge IsSpawned | #12/#13 | 运行时 AddComponent 的 NetworkBehaviour，ServerRpc 需 NetworkObject 已 spawn |
| 🟡 Camera NetworkPrefab | #14/#15/#23 | 同 AddComponent 问题，推荐改为 Instantiate prefab |
