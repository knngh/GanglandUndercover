# A1 NetworkPrefab 和远端对象复制审计

日期：2026-06-05  
基线提交：67216bd7（阶段0冻结）  

## 审计范围

审计所有运行时 NetworkObject 的生成、注册、远端复制路径。重点关注：
1. `globalObjectIdHash=0` 风险
2. 远端 `NetworkPrefab could not be found` 错误
3. 运行时 `AddComponent<NetworkObject>.Spawn()` 反模式

## 审计结果总结

| 项目 | 状态 | 风险 |
|------|------|------|
| 监控摄像头 | ✅ 已修复 | 低 |
| 小游戏桥 | ✅ 无问题 | 低 |
| 玩家角色 | ✅ 标准 Prefab | 低 |
| 任务站可视化 | ✅ 纯本地对象 | 无 |
| 破坏对象 | ⚠️ 需复核 | 中 |
| 会议/投票系统 | ✅ 基于 Snapshot | 低 |

---

## 1. 监控摄像头（OnlineSecurityCamera）

**文件**：`OnlineMatchController.cs:1366-1400`, `:9349-9400`

**当前状态**：✅ 已正确实现

```
流程：
1. RegisterSurveillanceCameraPrefab() → new GameObject + AddComponent<NetworkObject>
2. NetworkConfig.Prefabs.Add(template)
3. CreateSurveillanceCameraNetworkObject() → Instantiate(template) → netObj.Spawn()
```

**验证要点（需 Unity 运行时）**：
- [ ] 双进程 Relay 日志搜索 `NetworkPrefab could not be found` → 期望 0
- [ ] Host 触发摄像头 → Client 可看到摄像头状态变化（`OnlineSecurityCamera` 使用 `NetworkVariable`）
- [ ] 摄像头视野覆盖在双端一致

**无风险点**：
- 模板在 `DontDestroyOnLoad` 中，不会因场景切换丢失
- `Spawn()` 前先设置所有 `NetworkVariable` 字段，避免初始值不同步

---

## 2. 小游戏桥（OnlineMiniGameBridge）

**文件**：`OnlineMiniGameBridge.cs`

**当前状态**：✅ 无问题

`OnlineMiniGameBridge` 是 `OnlineMatchController` GameObject 上的 `NetworkBehaviour` 组件。因为它在场景中的 NetworkObject 上，不需要额外注册。

**协议审计**：
- `OpenMinigameOnClient` → `StartTaskClientRpc`（单向目标）→ 客户端打开小游戏 ✅
- `SubmitTaskResultServerRpc`（RequireOwnership=false）→ 服务器校验 → `TaskDoneClientRpc` 广播 ✅
- `OpenRepairMinigameOnClient` → 修复小游戏协议同上 ✅
- 小游戏 GameObject 在客户端本地创建（`Instantiate`），不是 NetworkObject ✅

**验证要点（需 Unity 运行时）**：
- [ ] Client 完成小游戏 → Host 收到 `SubmitTaskResultServerRpc` → 任务状态同步
- [ ] Client 修复破坏 → Host 收到 `SubmitRepairResultServerRpc` → 破坏状态变化

**注意**：小游戏桥依赖 `OnlineMatchController` 的 `ValidateTaskStart`/`ValidateAndCompleteTask` 等服务器权威方法。只要这些方法在 Host 上正确运行，协议就不会有问题。

---

## 3. 玩家角色（CharacterCustomizer）

**文件**：`CharacterCustomizer.cs`

**当前状态**：✅ 标准流程

`CharacterCustomizer` 是 `NetworkBehaviour`，作为玩家 Prefab 的一部分由 `NetworkManager` 的玩家生成系统管理。不需要额外注册。

**同步路径**：
- `NetworkVariable` 同步角色外观（通过 `NetworkObjectId` + JSON 自定义数据）
- 序列化/反序列化使用 `FastBufferWriter/Reader`

**验证要点（需 Unity 运行时）**：
- [ ] 双端角色外观一致
- [ ] 角色死亡/复活后外观正确同步

---

## 4. 任务站可视化

**文件**：`OnlineMatchController.cs:9998-10060`

**当前状态**：✅ 纯本地对象

任务控制台（`CreateTaskConsole`）和交互光环（`CreateTaskInteractionHalos`）是客户端本地创建的 GameObject，不包含 NetworkObject 组件。任务状态通过 Snapshot 同步（`BroadcastSnapshot` → `ReceiveServerSnapshot`）。

**无风险**：纯本地渲染对象不需要 NetworkObject。

---

## 5. 破坏对象（Sabotage system）

**文件**：`OnlineMatchController.cs`（破坏状态机），`SabotageVFX.cs`（视觉反馈）

**当前状态**：⚠️ 需复核

破坏状态（停电、锁门、通讯干扰等）通过 `OnlineMatchController` 上的公开 timer 变量同步（通过 Snapshot）。视觉反馈（`SabotageVFX`）是纯客户端组件，不需要 NetworkObject。

**潜在风险**：
- `SabotageVFX.Bind()` 必须在 `OnlineMatchController` 启动后调用，否则 timer 引用为 null
- 破坏修复需要通过 `OnlineMiniGameBridge` 协议，依赖已审计 ✅
- 停电遮罩的 `SortingOrder = 500` 可能与其他 UI 层冲突，需要运行时验证

**验证要点（需 Unity 运行时）**：
- [ ] Host 触发停电 → Client 看见蓝黑遮罩
- [ ] Client 修复停电 → Host 和所有 Client 遮罩消失
- [ ] 多种破坏叠加时遮罩层叠正确（不闪烁、不穿透）

---

## 6. Snapshot 系统（整体同步）

**文件**：`OnlineMatchController.cs:2589-2874`

**当前状态**：✅ 完整实现

Snapshot 是服务器→客户端的全量状态同步，包含玩家位置、任务状态、破坏计时器、会议状态。通过 `CustomMessagingManager` 的命名消息传输。

**审计发现**：
- 发送频率：0.08 秒（`SnapshotIntervalSeconds`）→ 约 12.5 Hz，对 10 人社交推理游戏足够
- 传输方式：`ReliableFragmentedSequenced` → 有序可靠，大消息自动分片 ✅
- 完整性验证：`ValidateClientSnapshotIntegrity()` 检查玩家列表、任务列表完整性 ✅
- 客户端接收：`ReceiveServerSnapshot()` 解析并应用到本地状态 ✅

**优化建议**（非阻塞）：
- 后续可改为增量快照减少带宽
- `FastBufferWriter` 大小 16384 字节，10 人局可能接近上限

---

## 7. 审计结论

### 当前无阻塞问题

所有 6 个网络对象路径已审计完毕：
- 运行时 NetworkObject 数量：1（监控摄像头 x N 个实例）
- NetworkBehaviour 类：3（OnlineMiniGameBridge、OnlineSecurityCamera、CharacterCustomizer）
- 无 `globalObjectIdHash=0` 模式
- 无运行时 `AddComponent<NetworkObject>.Spawn()` 反模式

### 运行时验证清单（需双进程或多机运行）

| # | 验证项 | 预期结果 |
|---|--------|---------|
| 1 | Host/Client 日志搜 `NetworkPrefab could not be found` | 0 匹配 |
| 2 | Host 触发摄像头 → Client 看到状态 | 一致 |
| 3 | Client 做任务 → Host 收到完成 | 成功 |
| 4 | Client 修破坏 → Host 破坏计时器清零 | 成功 |
| 5 | 双端角色外观 | 一致 |
| 6 | 10 分钟局 Snapshot 无丢失/错序 | 0 error |
| 7 | 停电遮罩双端同步 | 同时出现/消失 |
| 8 | 3-4 端完整局全程无网络异常日志 | 0 warning |

### 建议

等到 Unity 重新打开后，优先执行验证项 1-4（核心路径），然后扩展到 3-4 端完整局（A2）。
