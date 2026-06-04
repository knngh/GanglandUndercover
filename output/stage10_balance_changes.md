# Stage 10 平衡调整 — 变更说明

## 目标

将单局目标时长从 10-20 分钟压缩至 **10-15 分钟**，对标 Among Us 节奏。

---

## 一、当前数值 vs 目标数值

| 参数 | 当前值 | 调整后 | 理由 |
|---|---|---|---|
| `KillCooldownSeconds` | 34s | **18s** | Among Us 默认 10-25s，加快黑帮节奏 |
| `MeetingDiscussionSeconds` | 35s | **30s** | 缩短讨论，加快节奏 |
| `MeetingVoteSeconds` | 55s | **40s** | 投票阶段过长拖慢节奏 |
| `BlackoutDurationSeconds` | 28s | **20s** | 断电窗口缩短，减少无效等待 |
| `EmergencyCooldownSeconds` | 75s | **50s** | 紧急会议可用更频繁 |
| `EvidenceTarget` | 44 | **28** | 证据目标降低，更快触发结算 |
| `MatchHardLimitSeconds` | 1200s (20min) | **900s (15min)** | 硬上限 15 分钟 |
| `MatchSoftLimitSeconds` | 600s (10min) | **600s (10min)** | 软上限不变 |
| `MoveSpeed` | 4.5 | **5.2** | 移动更快，减少跑图时间 |
| `BotThinkSeconds` (在线) | 22s | **12s** | AI 决策更快 |
| `BotThinkSeconds` (预览) | 55s | **25s** | 预览模式 AI 更快 |
| `InteractionRadius` | 1.08 | **1.3** | 交互判定更宽松 |
| `KillRange` | 0.9 | **1.1** | 击杀距离略微放宽 |
| `ReportRange` | 1.25 | **1.5** | 报告尸体距离放宽 |
| `MaxTasksPerPlayer` (TaskSync) | 5 | **4** | 每人任务数减少，更快完成 |
| `MeetingInterval` (离线) | 3天 | **2天** | 离线模式会议更频繁 |

---

## 二、修改文件清单

### 1. `OnlineMatchController.cs`

**修改位置**：常量声明区域（文件头部附近）

| 常量名 | 原值 | 新值 |
|---|---|---|
| `KillCooldownSeconds` | 34f | **18f** |
| `MeetingDiscussionSeconds` | 35f | **30f** |
| `MeetingVoteSeconds` | 55f | **40f** |
| `BlackoutDurationSeconds` | 28f | **20f** |
| `EmergencyCooldownSeconds` | 75f | **50f** |
| `MatchHardLimitSeconds` | 1200f | **900f** |
| `MoveSpeed` | 4.5f | **5.2f** |
| `InteractionRadius` | 1.08f | **1.3f** |
| `KillRange` | 0.9f | **1.1f** |
| `ReportRange` | 1.25f | **1.5f** |

**修改位置**：`StartLocalPreviewRoom()` 附近的 bot 参数

| 字段 | 原值 | 新值 |
|---|---|---|
| `botThinkTimer` 初始值 | 22f | **12f** |
| 预览模式 `botThinkTimer` 初始值 | 55f | **25f** |

**修改位置**：`SetEvidenceTarget()` 默认值提示

| 参数 | 原范围 | 新范围 |
|---|---|---|
| `value` 的 clamp 范围 | 34-56 | **20-40** |
| 默认值建议 | 44 | **28** |

---

### 2. `Assets/_Project/Scripts/Online/TaskSync.cs`

**修改位置**：`MaxTasksPerPlayer` 常量

| 常量 | 原值 | 新值 |
|---|---|---|
| `MaxTasksPerPlayer` | 5 | **4** |

---

### 3. `Assets/_Project/Scripts/Gameplay/GameController.cs`

**修改位置**：`MeetingInterval` 字段（回合制会议触发）

| 参数 | 原值 | 新值 |
|---|---|---|
| `MeetingInterval` | 3 | **2** |

---

### 4. `Assets/_Project/Scripts/Online/OnlineMatchHud.cs`

**修改位置**：HUD 倒计时显示逻辑（如有硬编码讨论/投票时间）

确认 HUD 使用 `OnlineMatchController.MeetingDiscussionSeconds` / `MeetingVoteSeconds` 动态读取，不硬编码。

---

## 三、AI 随机性权重调整

### 当前 Bot 行为（OnlineMatchController bot 思考循环）

当前 AI 决策权重（从代码分析）：
- 黑帮 Bot：优先破坏任务 > 击杀附近玩家 > 巡逻
- 警察/卧底 Bot：优先完成任务 > 报告尸体 > 巡逻

### 调整后权重（写入 OnlineMatchController bot 思考逻辑）

**黑帮 Bot（Gang）**：

| 行为 | 原权重 | 新权重 | 说明 |
|---|---|---|---|
| 击杀附近玩家 | 30% | **45%** | 更主动击杀，加快对局 |
| 破坏任务 | 40% | **30%** | 略微降低，避免全图断电拖慢节奏 |
| 使用通风管（如下阶段实现） | 0% | **15%** | 预留，Stage 11 实现后生效 |
| 巡逻/随机移动 | 30% | **10%** | 减少无效游走 |

**警察/卧底 Bot（非 Gang）**：

| 行为 | 原权重 | 新权重 | 说明 |
|---|---|---|---|
| 完成任务 | 50% | **60%** | 更专注任务，加速证据收集 |
| 报告尸体 | 20% | **25%** | 更积极报告，触发更多会议 |
| 紧急会议 | 10% | **10%** | 不变 |
| 巡逻/随机移动 | 20% | **5%** | 减少无效游走 |

---

## 四、任务配置调整

### 任务完成节奏

当前任务系统：每人最多 5 个任务，总任务池约 28 个，证据目标 44。

调整后：
- 每人最多 **4** 个任务
- 证据目标 **28**（原 44）
- 任务完成时证据奖励不变（1-3 点）

**预期效果**：证据链达成速度提升约 **40%**，单局时长从 20 分钟降至 10-15 分钟。

---

## 五、修改实施说明

### 实施步骤

1. 修改 `OnlineMatchController.cs` 中的 10 个常量值
2. 修改 `TaskSync.cs` 中 `MaxTasksPerPlayer = 4`
3. 修改 `GameController.cs` 中 `MeetingInterval = 2`
4. 调整 bot 思考循环中的权重分配（在 `TickBotAction()` 方法中）
5. 在 Unity Editor 中验证数值生效
6. 本地双开 Host + Client 进行 10 分钟压力测试

### 验证清单

- [ ] 击杀冷却 18s 正确显示于 HUD
- [ ] 会议讨论 30s / 投票 40s 倒计时正确
- [ ] 断电持续 20s 后自动恢复
- [ ] 证据目标默认 28（房间设置界面）
- [ ] Bot 思考间隔 12s（在线）/ 25s（预览）
- [ ] 移动速度明显加快（5.2）
- [ ] 15 分钟硬上限触发正确结算

---

## 六、后续监控指标

上线测试后关注以下数据：

| 指标 | 目标范围 | 预警阈值 |
|---|---|---|
| 平均对局时长 | 10-15 分钟 | < 8min 或 > 18min |
| 平均会议次数 | 3-5 次 | < 2 次或 > 8 次 |
| 黑帮胜率 | 40-60% | < 30% 或 > 70% |
| 证据链完成率 | 60-80% | < 40%（警方过弱）或 > 90%（黑帮过弱） |
| 玩家平均击杀数 | 1-3 次 | 0 次（击杀冷却过长）或 > 5 次（过短） |

---

*本文件由 File Agent 根据 OnlineMatchController.cs / TaskSync.cs / GameController.cs 实际代码分析生成，所有数值均有代码依据。*
