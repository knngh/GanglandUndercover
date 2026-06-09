# Gangland Undercover — 网络消息契约表

> 审计日期: 2026-06-07 | 最近推进: 2026-06-08 | 总计: 11 自定义消息 + 4 ServerRpc + 8 ClientRpc + 3 NetworkVar = 26 网络接口

---

## 自定义命名消息 (Custom Named Messages) — 11 条

| # | 消息名 | 方向 | Payload | 服务端校验 | 状态 |
|---|--------|------|---------|-----------|------|
| 1 | GanglandClientState | C→S (0.08s) | position, input, ready | ✅ IsServer + TryGetValue | OK |
| 2 | GanglandClientAction | C→S | actionType, targetId/payload | ✅ IsServer + TryGetValue | OK |
| 3 | GanglandClientProfile | C→S | displayName | ✅ IsServer + sender check | OK |
| 4 | GanglandServerSnapshot | S→All | phase, players[], bodies[], votes[], cases[] | N/A (S→C, 只读) | OK |
| 5 | GanglandRoleAssign | S→SingleClient | roleByte | N/A (S→C) | OK |
| 6 | GanglandChatSend | C→S | structured message | ✅ IsServer + sender exists；role/channel/position 服务端重算 | OK |
| 7 | GanglandChatBroadcast | S→All/Single | structured senderId/name/content/isDead/faction/channel | N/A (S→C) | OK |
| 8 | GanglandMapSelect | S→All | mapTypeIndex | N/A (S→C，已改 `SendNamedMessageToAll`) | OK |
| 9 | GanglandHostHeartbeat | S→All(Unreliable) | timestamp | N/A (S→C) | OK |
| 10 | GanglandHostMigration | S→All | fullState snapshot | N/A (S→C) | OK |
| 11 | GanglandCharacterCustom | C→S / S→Clients | structured objectId,jsonLength,jsonData | ✅ Server 校验 sender == NetworkObject.OwnerClientId；S→Clients 定向转发 | OK |

---

## ServerRpc — 4 条

| # | 方法 | 文件 | Payload | 校验 |
|---|------|------|---------|------|
| 12 | SubmitTaskResultServerRpc | MiniGameBridge.cs:55 | taskId, success | IsServer + controller 校验 phase/alive/range/active lock |
| 13 | SubmitRepairResultServerRpc | MiniGameBridge.cs:134 | taskId, success | IsServer + controller 校验 phase/alive/range/active lock |
| 14 | StartWatchingServerRpc | SecurityCamera.cs:82 | — | IsServer + Action/alive + RemoteSurveillance 或摄像头附近 |
| 15 | StopWatchingServerRpc | SecurityCamera.cs:92 | — | IsServer |

---

## ClientRpc — 8 条

| # | 方法 | 文件 | 触发方 |
|---|------|------|--------|
| 16 | StartTaskClientRpc | MiniGameBridge.cs:80 | 服务器→定向客户端 |
| 17 | RejectStartClientRpc | MiniGameBridge.cs:86 | 服务器→定向客户端 |
| 18 | RejectResultClientRpc | MiniGameBridge.cs:92 | 服务器→定向客户端 |
| 19 | CancelTaskClientRpc | MiniGameBridge.cs:98 | 服务器→定向客户端 |
| 20 | TaskDoneClientRpc | MiniGameBridge.cs:105 | 服务器→所有客户端 |
| 21 | StartRepairClientRpc | MiniGameBridge.cs:157 | 服务器→定向客户端 |
| 22 | RepairDoneClientRpc | MiniGameBridge.cs:164 | 服务器→所有客户端 |
| 23 | UpdateWatcherClientRpc | SecurityCamera.cs:99 | 服务器→定向客户端 |

---

## NetworkVariable — 3 个

| # | 字段 | 文件 |
|---|------|------|
| 24 | ZoneCenterNet | SecurityCamera.cs:16 |
| 25 | ZoneSizeNet | SecurityCamera.cs:17 |
| 26 | CameraLabelNet | SecurityCamera.cs:18 |

---

## 🔴 发现问题 / 修复状态

### 已修复: MapSelect 不是广播
`OnlineMatchController.cs:1960`: `SendNamedMessage(MapSelectMessage, ServerClientId(0), writer)`
- 目标 = ServerClientId(0) = 只发给 Host 自己
- 注释写明"广播给所有客户端"，但实际只发给了一个客户端
- 2026-06-08 修复: 改为 `SendNamedMessageToAll()`

### 已修复: Chat payload 使用 `|` 拼接
- 旧格式 `senderId|senderName|content|...` 会在 `content` 包含 `|` 时破坏接收解析
- 2026-06-08 修复: 改为结构化 `FastBufferWriter` 字段
- 回归: `ChatSendPayload_RoundTripsContentWithPipes` / `ChatBroadcastPayload_RoundTripsContentWithPipes`

### 已修复: CharacterCustom 转发行为
- 旧路径: 客户端发到 `ServerClientId` 后缺明确服务端转发；每个 `CharacterCustomizer` 实例还会重复注册同一个 named message，后注册会覆盖前注册
- 2026-06-08 修复: 单一静态 handler 按 `NetworkObjectId` 路由；Server 校验 sender 是对象 owner 后再转发给非 Server 客户端
- 回归: `CharacterCustomPayload_RoundTripsObjectIdAndJson` / `CharacterCustomPayload_RejectsOversizedJson`

### 已缓解: MiniGameBridge 越权提交
`OnlineMiniGameBridge` 的 ServerRpc 路径仍需双端确认，但 task/repair submit 已由 `ValidateAndCompleteTask` / `ValidateAndRepairTask` 校验 phase、alive、range 与 active lock；直接伪造完成会被拒。
