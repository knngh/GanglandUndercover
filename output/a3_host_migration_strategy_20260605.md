# A3 断线、重连和 Host 迁移策略

日期：2026-06-05  
基线：`HostMigrationManager.cs`（489 行）  

## 当前实现状态

### HostMigrationManager 功能清单

| 功能 | 状态 | 实现位置 |
|------|------|---------|
| Host 心跳发送（2s 间隔） | ✅ | `SendHeartbeat()` |
| Client 心跳超时检测（5s） | ✅ | `TickClientHeartbeatWatchdog()` |
| 新 Host 选举（最小 clientId） | ✅ | `ElectNewHost()` |
| Snapshot 缓存与恢复 | ✅ | `CaptureSnapshot()` / `RestoreFromSnapshot()` |
| 迁移广播（快照+新Host ID） | ✅ | `BroadcastMigrationSnapshot()` |
| 迁移 UI 提示（OnGUI） | ✅ | `OnGUI()` 半透明遮罩+居中文字 |
| 降级策略（迁移失败→友好结算） | ✅ | `FallbackToGameOver()` |
| 迁移超时（30s） | ✅ | `migrationTimeout` |
| 单玩家降级 | ✅ | `GetRemainingPlayerCount() <= 1` |
| 注册/反注册消息处理器 | ✅ | `RegisterMessageHandlers()` / `Unregister` |
| 断线玩家处理 | ⚠️ 部分 | 仅处理 Host 断线，Client 断线需补充 |

---

## 边界规则矩阵

### 玩家断线时的状态处理

| 断线时机 | 断线类型 | 规则 |
|---------|---------|------|
| 进行中任务 | Client 断线 | 任务自动释放，其他玩家可接 |
| 会议中 | Client 断线 | 该玩家视为弃权，若剩余票数足够则继续 |
| 投票中 | Client 断线 | 断线玩家票数移除，重新计算多数 |
| 尸体存在 | Client 断线 | 尸体保留，可由其他玩家报案 |
| 作为尸体 | Client 断线 | 该尸体仍可被报案 |
| 破坏进行中 | Client 断线 | 破坏继续倒计时，或释放修复任务 |
| Host 断线 | 任意 | 触发 HostMigrationManager → 迁移或降级 |

### Host 迁移策略选择

```
Host 离线检测
  ├─ 剩余玩家 >= 2 → 选举新 Host，尝试迁移
  │   ├─ 迁移成功（30s内）→ 游戏恢复
  │   └─ 迁移超时 → FallbackToGameOver("主机迁移超时")
  └─ 剩余玩家 <= 1 → FallbackToGameOver("主机已离线")
```

---

## 断线玩家的数据一致性规则

### 状态清理优先级

1. **任务占有**：`OnlineMatchController.ReleaseTask(clientId, taskId)` — 最高优先级
2. **投票状态**：从投票列表中移除，重新计算多数
3. **破坏修复**：若断线者正在修复破坏，释放修复任务
4. **小游戏实例**：客户端自动销毁（本地GameObject），无需服务器处理
5. **聊天/Snapshot**：自动停止向断线端发送

### 断线后重返规则（如后续实现重连）

```
断线重连流程（Phase 2 规划）：
1. Client 重连 → 接收完整 Snapshot
2. 恢复到断线时的阵营/职业/状态
3. 若断线时 Alive=true 则复活到安全出生点
4. 若断线时 Alive=false 则维持鬼魂状态
5. 任务占有全部释放（保守策略，避免死锁）
```

---

## 实现建议（Phase 2）

### 优先级 P0：补充 Client 断线处理

当前 `HostMigrationManager` 仅处理 Host 断线。需要在 `OnlineMatchController.HandleClientDisconnected()` 中补充：

```csharp
private void HandleClientDisconnected(ulong clientId)
{
    // 1. 释放该玩家的所有任务
    ReleaseAllTasks(clientId);

    // 2. 若在会议中：从投票列表移除，检查是否需要重新计算多数
    if (IsMeetingActive)
        RemovePlayerFromMeeting(clientId);

    // 3. 清理破坏修复占用
    if (miniGameBridge != null)
        miniGameBridge.ReleaseRepairByPlayer(clientId);

    // 4. 标记玩家状态
    if (players.TryGetValue(clientId, out var state))
    {
        state.Alive = false; // 断线视为出局
        players[clientId] = state;
    }

    // 5. 广播玩家离开通知
    BroadcastPlayerDisconnected(clientId);
}
```

### 优先级 P1：Host Migration 后 NetworkObject 所有权转移

当前 `BecomeNewHost()` 恢复 Snapshot 但未处理 NetworkObject 所有权。需要：
- 新 Host 对所有 `NetworkObject` 调用 `ChangeOwnership`
- 监控摄像头 NetworkObject 需要从旧 Host 转移到新 Host

### 优先级 P2：增量 Snapshot

当前每次发送全量 Snapshot（16384 字节）。建议：
- 全量 Snapshot：每 5 秒或玩家加入/离开时
- 增量 Snapshot：每 0.08 秒只发送变化的玩家位置和计时器

---

## 验证清单

### 单进程测试（PlayMode）
- [ ] Host 停止 → Client 显示迁移遮罩 → 超时后友好结算 ✅（当前实现）
- [ ] 2 Client + 1 Host → Host kill → 剩余 Client 选举新 Host → 游戏恢复
- [ ] 1 Client + 1 Host → Host kill → 降级结算 ✅（当前实现）

### 多进程测试（实际联调）
- [ ] 4 端 → kill Host process → 3 端完成迁移
- [ ] 迁移过程中完成小游戏 → 状态在新 Host 下正确
- [ ] 迁移过程中触发会议 → 会议状态恢复一致
- [ ] 迁移后破坏计时器正确恢复
- [ ] 迁移后任务占有状态正确

### 断线场景
- [ ] 任务中断线 → 任务被释放 → 其他玩家可接
- [ ] 会议中断线 → 票数重新计算 → 流程继续
- [ ] 破坏修复中断线 → 修复释放 → 其他玩家可修

---

## 代码质量评估

| 维度 | 评分 | 说明 |
|------|------|------|
| 架构清晰度 | 8/10 | 心跳/选举/迁移/降级分层明确 |
| 错误处理 | 7/10 | 有空值检查，但缺少 NetworkObject 所有权转移 |
| 可测试性 | 6/10 | OnGUI 和消息回调耦合，难以纯 PlayMode 测试 |
| 边界覆盖 | 6/10 | Host 迁移完备，Client 断线处理缺失 |
| 日志质量 | 8/10 | 关键路径日志完整 |

**总结**：HostMigrationManager 架构坚实，心跳+选举+快照恢复+降级四阶段设计合理。主要缺口是 Client 断线处理（P0）和 NetworkObject 所有权转移（P1），不影响当前单人+双人测试场景。
