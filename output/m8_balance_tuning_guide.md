# M8.4.4 平衡调参指南

> 本文档为 Gangland Undercover 的 OnlineRuleSet 参数提供数据驱动的调参依据。
> 请在至少采集 5 场对局数据后，结合 Match Stats Viewer（Tools → Gangland → Match Stats Viewer）的汇总建议使用本表。

---

## 目标指标

| 指标 | 目标范围 | 说明 |
|------|----------|------|
| 8 人局时长 | 8–15 分钟 | 从第一次杀青到结算 |
| 警方胜率 | 45–55% | 与黑帮接近 50:50 |
| 卧底胜率 | 10–20% | 低概率，作为变数 |
| 任务完成率 | 60–80% | 过低说明 Bot 破坏太强，过高说明警方太容易 |
| 平均会议次数 | 2–4 次/局 | 过多说明节奏断裂，过少说明信息不充分 |

---

## 核心参数速查表

| 参数 | 当前值 | 推荐范围 | 调参触发条件 |
|------|--------|----------|--------------|
| `KillCooldownSeconds` | 30 | 20–45 | 警方胜率 > 60% → 调低；黑帮胜率 > 60% → 调高 |
| `MeetingCooldownSeconds` | 0 | 0–15 | 会议过于频繁 → 调高 |
| `EvidenceTarget` | 10 | 6–15 | 警方胜率过高 → 调高；对局经常超时 → 调低 |
| `MatchTargetMinSeconds` | 600 | 480–900 | 平均时长 < 8 min → 调高目标；> 15 min → 调低 |
| `MatchHardLimitSeconds` | 1200 | 900–1500 | 硬限时到达频率过高 → 调高 |
| `BotThinkMinSeconds` | 1.2 | 0.8–2.5 | Bot 太强 → 调高（变慢）；Bot 太弱 → 调低 |
| `BotThinkMaxSeconds` | 3.4 | 2.0–5.0 | 同上 |
| `BotTaskSpeedMultiplier` | 1.0 | 0.6–1.5 | 任务完成率 > 85% → 调低；< 50% → 调高 |
| `EmergencyMeetingsTotal` | 1 | 1–3 | 会议次数持续 < 2 → 调高 |
| `KillRange` | 1.2 | 0.8–1.8 | 击杀过于容易 → 调低；尸体经常不被发现 → 调高 |

---

## 分场景调参流程

### 场景 A：对局总是 < 8 分钟结束

**现象**：黑帮快速连杀，警方来不及完成任务。

**调参步骤**：
1. `KillCooldownSeconds` +10（给警方喘息空间）
2. `EvidenceTarget` -2（降低警方获胜门槛）
3. 检查 Mole 是否过于强势 → 降低 Mole 的 `KillCooldownMultiplier`（在 `OnlineRuleSet.ProfessionAbilities` 中）

**验证**：再进行 3 场，平均时长应趋近 10 分钟。

---

### 场景 B：对局总是 > 18 分钟或超时

**现象**：黑帮无法有效击杀，警方稳步完成任务。

**调参步骤**：
1. `KillCooldownSeconds` -10
2. `EvidenceTarget` +3
3. 检查 Enforcer 的 `SabotageSpeedMultiplier` 是否过低 → 调高至 1.3–1.5
4. `BotTaskSpeedMultiplier` +0.2（让 Bot 更积极破坏/完成任务）

**验证**：再进行 3 场，超时率应 < 20%。

---

### 场景 C：警方胜率持续 > 60%

**调参步骤**：
1. 确认 `EvidenceTarget` 是否过低 → 逐步 +2
2. 检查 Undercover 的 `MeetingSuspicionWeight` 是否过高 → 降低至 0.7
3. 给 Gang 增加 `FootprintTrack` 能力（部分职业）→ 提高黑帮反制能力
4. `KillCooldownSeconds` -5

---

### 场景 D：黑帮胜率持续 > 60%

**调参步骤**：
1. `KillCooldownSeconds` +5
2. 检查 Inspector 的 `EvidenceGainMultiplier` 是否过低 → 调高至 1.3
3. 给 Police 增加 `RemoteSurveillance` 能力覆盖率
4. `BodyReportRadius` +0.5（让尸体更容易被发现）

---

### 场景 E：卧底从未获胜

**调参步骤**：
1. 确认 Mole 的职业掩护分配是否合理（应在 `ProfessionFor()` 中分配 Inspector/Tech/Forensics）
2. 提高 Mole 的 `DarkVision` 能力触发频率
3. `MoleSuspicionPenalty` 调低（让 Mole 更难被怀疑）

---

## 地图差异调整

### HarbourDistrict（港区）
- 地图较大，`KillRange` 可 +0.2
- 任务点分散，`BotTaskSpeedMultiplier` 建议 1.1（补偿移动时间）

### PoliceStation（警署）
- 地图紧凑，击杀频率天然更高 → `KillCooldownSeconds` +5
- 监控摄像头提供额外信息 → `EmergencyMeetingsTotal` 可 -1
- 暗线（通风管）密集 → 确保 `VentCooldownSeconds` ≥ 15

---

## 职业能力微调参考

在 `OnlineRuleSet.ProfessionAbilities` 数组中，每个职业有下列关键倍率：

| 职业 | 关键倍率 | 建议范围 |
|------|----------|----------|
| Enforcer | `SabotageSpeedMultiplier` | 1.2–1.5 |
| Fixer | `RepairSpeedMultiplier` | 1.2–1.5 |
| Driver | `MoveSpeedMultiplier` | 1.1–1.3 |
| Inspector | `EvidenceGainMultiplier` | 1.2–1.5 |
| Tech | `SurveillanceDurationMultiplier` | 1.3–2.0 |
| Forensics | `BodyReportRadiusMultiplier` | 1.5–2.5 |
| UndercoverAgent | `MeetingSuspicionWeight` | 0.6–1.0 |
| Mole | `KillCooldownMultiplier` | 0.7–1.0 |

---

## 采集数据的最低要求

在开始调参前，请确保：

1. ✅ 至少 **5 场** 有效对局日志（Match Stats Viewer 中可见）
2. ✅ 覆盖 **两种地图**（HarbourDistrict + PoliceStation）
3. ✅ 包含 **人类玩家**（不止是 Bot vs Bot）
4. ✅ 每场 **完整打完**（不中途退出，否则 `DurationSeconds` 不准）

---

## 使用 Match Stats Viewer

1. Unity Editor 菜单栏 → **Tools → Gangland → Match Stats Viewer**
2. 切换到「汇总」Tab：
   - 查看各阵营胜率柱状图
   - 确认平均时长是否在 8–15 分钟
3. 切换到「平衡建议」Tab：
   - 根据提示调整对应参数
4. 每次调参后 → 打 3 场 → 再次查看建议

---

## 参数修改位置

所有参数在 `Assets/_Project/Scripts/Online/OnlineRuleSet.cs` 的 `ScriptableObject` 中配置：

1. Project 视图 → 搜索 `OnlineRuleSet`
2. 选中对应的 `.asset` 文件
3. 在 Inspector 中修改数值
4. 保存（Ctrl/Cmd + S）

---

## 版本记录

| 日期 | 修改内容 | 修改人 |
|------|----------|--------|
| 2026-06-04 | 初版，配合 M8.4 发布 | — |
| | | |

---

> **注意**：所有数值调整都应基于实际对局数据，而非理论推测。每次只调整 1–2 个参数，观察效果后再继续。
