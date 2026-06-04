---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9470d846c7eff9b24afb94a99a2cb3f0_d89d1f6a5f0811f18d42525400d9a7a1
    ReservedCode1: VI3T9KWuX+Q9msmIgQKg+SpxPRq/pIpKddWqLGzfXlI85pHjiWTbDt8j7aBbL/mTAoAvLXaCoV0S/Hq7/BUx+gkLcEqsBqbeUofwRav632M6Vbv8ruLs+WhB9+TvYGTWkc/HK+VJRkzLSZCiinCDB13HTcAQbqZXW1DsqeHMGrgY0DcLTlu5BPo8H+E=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9470d846c7eff9b24afb94a99a2cb3f0_d89d1f6a5f0811f18d42525400d9a7a1
    ReservedCode2: VI3T9KWuX+Q9msmIgQKg+SpxPRq/pIpKddWqLGzfXlI85pHjiWTbDt8j7aBbL/mTAoAvLXaCoV0S/Hq7/BUx+gkLcEqsBqbeUofwRav632M6Vbv8ruLs+WhB9+TvYGTWkc/HK+VJRkzLSZCiinCDB13HTcAQbqZXW1DsqeHMGrgY0DcLTlu5BPo8H+E=
---

# Stage 14: Host Migration 主机迁移系统

## 概述

实现了完整的主机迁移（Host Migration）系统，对标 Among Us 的主机断连无缝切换能力。当主机玩家离线或网络中断时，系统自动从剩余客户端中选举新主机、重建游戏状态并恢复对局，避免整局作废。

## 产出物

| 文件 | 路径 | 说明 |
|------|------|------|
| GameStateSnapshot.cs | `Assets/_Project/Scripts/Online/GameStateSnapshot.cs` (472行) | 游戏状态快照数据结构，完整序列化/反序列化所有对局状态 |
| HostMigrationManager.cs | `Assets/_Project/Scripts/Online/HostMigrationManager.cs` (452行) | 主机迁移管理器，负责心跳检测、主机选举、状态恢复 |
| OnlineMatchController.cs | `Assets/_Project/Scripts/Online/OnlineMatchController.cs` (修改) | 集成迁移系统（+280行），新增 CaptureSnapshot/RestoreFromSnapshot/ForceGameOver |

## 系统设计

### 1. 心跳协议 (Heartbeat Protocol)

```
┌──────────┐         每 2 秒          ┌──────────┐
│   Host   │ ─── GanglandHostHeartbeat ───> │  Client  │
│          │                           │          │
│          │   超时 5 秒 → 启动迁移      │          │
└──────────┘                           └──────────┘
```

- **心跳间隔**：2 秒（与现有 Snapshot 12.5Hz 分离，避免额外带宽压力）
- **超时阈值**：5 秒未收到心跳即判定主机离线
- **传输方式**：Unreliable NamedMessage，轻量级仅 16 字节

### 2. 迁移流程 (Migration Flow)

```
阶段一: 检测 (Detection)
  ├─ Client 心跳超时 5s
  └─ 或 OnClientDisconnectCallback 触发（clientId==ServerClientId）

阶段二: 缓存 (Snapshot)
  └─ 调用 CaptureSnapshot() 序列化全局状态到 GameStateSnapshot

阶段三: 选举 (Election)
  ├─ 算法: 最小 clientId 优先
  └─ 只剩 1 人 → ForceGameOver，对局结束

阶段四: 恢复 (Restoration)
  ├─ 新主机调用 RestoreFromSnapshot() 重建游戏
  ├─ 通过 GanglandHostMigration 命名消息广播快照
  └─ 所有客户端收到后恢复本地状态

阶段五: 恢复 (Resume)
  ├─ 清除迁移UI遮罩
  └─ 游戏正常继续
```

### 3. 状态快照 (GameStateSnapshot)

**覆盖的数据维度：**

| 类别 | 字段 | 说明 |
|------|------|------|
| 全局配置 | MatchStarted, Phase, RoomName, RoomMinPlayers 等 | 房间和对局参数 |
| 全局状态 | EvidenceScore, EvidenceTarget, EmergencyMeetingsLeft, PhaseTimer | 实时游戏进度 |
| 计时器 | BlackoutTimer, LockdownTimer, EvidenceLeakTimer 等 8 项 | 所有活跃倒计时 |
| 事件摘要 | LastMeetingReason, LastVoteOutcome, LastEvidenceEvent, LastSabotageEvent | UI 提示文字 |
| 玩家 | 15 个字段（ClientId/Position/Input/Alive/Role/Profession/Suspicion/Cooldowns） | 每个玩家的完整状态 |
| 私密角色 | ClientId → OnlineRole 映射 | Gang/Mole/Undercover 等隐藏身份 |
| 任务 | Id/Name/Position/Progress/Completed/Sabotaged | 全部任务进度 |
| 尸体 | Id/VictimClientId/Position/Reported | 未报告的尸体 |
| 投票 | VoterClientId → TargetClientId | 当前轮投票记录 |
| 案卷 | List\<string\> | 完整的事件日志 |
| 冷却 | KillCooldowns, AbilityCooldowns, VentCooldowns | 技能冷却状态 |
| Bot | BotThinkTimers, BotVoteTimers, BotTargets | AI 内部状态 |

**序列化格式：**
- `ToBytes(FastBufferWriter)` — 固定字段顺序，布尔/整型/浮点/字符串/列表混合编码
- `FromBytes(FastBufferReader)` — 严格对称的反序列化，字段顺序必须与 ToBytes 一致
- 使用 `Unity.Collections.FastBufferWriter/Reader` 与现有 Netcode 序列化体系统一

### 4. 集成点 (Integration Touchpoints)

| 位置 | 修改内容 |
|------|----------|
| `Awake/Start` 初始化 | 添加 `EnsureMigrationManager()` 确保组件存在 |
| `RegisterMessages()` | 追加注册 GanglandHostHeartbeat / GanglandHostMigration 处理器 |
| `UnregisterMessages()` | 追加反注册迁移消息处理器 |
| `HandleClientDisconnected()` | 首行调用 `migrationManager.OnClientDisconnected(clientId)` 转发事件 |
| `ReturnToLobby()` | 添加 `migrationManager.ResetState()` 重置迁移状态 |
| 新增 `CaptureSnapshot()` | 遍历所有 players/tasks/bodies/votes/caseLog/cooldowns 字典打包成快照 |
| 新增 `RestoreFromSnapshot()` | 清空现有状态并逐字段从快照恢复 |
| 新增 `ForceGameOver(resultText)` | 委托给 `SetResult()` 终止游戏 |
| 新增 `CooldownsToList/ListToCooldowns` | Dictionary↔List 转换工具方法 |

### 5. UI 反馈

迁移进行中显示：
- 半透明黑色全屏遮罩
- 居中大字 "主机迁移中..."
- 副文本 "正在选举新主机并同步对局状态，请稍候..."

迁移完成后 UI 自动清除，`status` 更新为"主机迁移完成，对局已恢复。"

## 边界情况处理

| 场景 | 处理方式 |
|------|----------|
| 只剩 1 名玩家 | ForceGameOver("主机离线，存活玩家不足，对局结束。") |
| 心跳恢复（假超时） | 重置 hostDisconnectedDetected 标志，取消迁移 |
| 迁移过程中再次断连 | migrationInProgress 守卫阻止重入 |
| 新主机自己也是 Bot | 正常接管；所有玩家状态从快照恢复 |
| 对局未开始时主机离线 | Update 中 matchStarted 守卫跳过迁移检测 |
| 非联机模式 | IsOnline 守卫跳过所有迁移逻辑 |

## 安全约束

- 所有消息注册/反注册成对出现，避免 HandleClientDisconnected 回调中空引用
- 心跳和迁移消息使用独立命名通道，不与现有 Snapshot/Action/Chat 消息混用
- 快照序列化不包含 Animator/SocialChar 等不可序列化引用
- MigrationManager 重置确保旧状态不会泄露到下一局
*（内容由AI生成，仅供参考）*
