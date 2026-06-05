# B4 平衡调参 Round 1 — 职业强度分级与调参建议

日期：2026-06-05
前置：M8.4 MatchStatsCollector 已上线，M8.2 OnlineProfession 职业系统就绪
目标：基于代码审计建立职业强度基准，为第一轮平衡测试提供调参依据

---

## 1. 胜率目标

### 1.1 阵营胜率

| 阵营 | 目标胜率 | 允许偏差 | 判定标准 |
|------|---------|---------|---------|
| 警察（Police + Undercover） | 45-55% | +/-5% | 统计 MatchLogEntry.WinningFaction == "Police" |
| 黑帮（Gang + Mole） | 45-55% | +/-5% | 统计 MatchLogEntry.WinningFaction == "Gang" |
| 卧底独立获胜 | 10-20% | 特殊 | Undercover 存活 + EvidenceScore 达标 |
| 黑帮线人独立获胜 | 5-10% | 特殊 | MoleIntel 达标触发 VictoryEvaluator.EvaluateGangVictory |

### 1.2 维持 45-55% 的原则

- 双向渗透模型中，Undercover 与 Mole 形成镜像对抗（代码参考 `VictoryEvaluator.cs:20-153`）
- 警察方优势路径：任务完成 → 证据达标（`UndercoverEvidenceTarget`）
- 黑帮方优势路径：击杀 → 投票淘汰 + 线人情报（`MoleIntelTarget`）
- 任何一方胜率 >60% 需立即调整 OnlineRuleSet 参数

---

## 2. 对局节奏指标

### 2.1 按人数分组的匹配指标目标

| 指标 | 6人局 | 8人局 | 10人局 |
|------|-------|-------|--------|
| 目标时长（中位数） | 6-8 分钟 | 8-12 分钟 | 10-15 分钟 |
| 最短时长下限 | 3 分钟 | 5 分钟 | 7 分钟 |
| 最长时长上限 | 12 分钟 | 18 分钟 | 22 分钟 |
| 会议次数范围 | 1-3 次 | 2-4 次 | 3-5 次 |
| 击杀次数范围 | 1-3 次 | 2-5 次 | 3-6 次 |
| 任务完成率目标 | 55-70% | 60-75% | 60-80% |
| Bot 任务贡献占比 | 30-45% | 25-40% | 20-35% |

### 2.2 指标数据来源

所有指标均可从 `MatchStatsCollector` 采集（`Assets/_Project/Scripts/Online/MatchStatsCollector.cs`）：

- **时长**：`MatchLogEntry.DurationSeconds` / `DurationFormatted`
- **会议次数**：`MatchLogEntry.MeetingCount`（当前为 PLACEHOLDER，需 OnlineMatchController 补全）
- **击杀次数**：`MatchLogEntry.KillCount`（当前为 PLACEHOLDER，需 KillSystem 上报）
- **任务完成率**：`MatchLogEntry.TaskCompletionRate`（计算属性 `CompletedTasks / TotalTasks`）
- **Bot 贡献**：`MatchLogEntry.BotCompletedTasks`

### 2.3 数据采集最低要求

- 最少 **10 场**有效对局日志（6/8/10 人各不少于 3 场）
- 覆盖 **两张地图**（HarbourDistrict + PoliceStation）
- 编辑器查看路径：**Tools → Match Stats Viewer**

---

## 3. 职业强度分级（Tier List）

> 基于代码审计：`OnlineProfession.cs:12-22` 枚举定义，`OnlineProfession.cs:47-79` AbilityType 能力表，
> `ProfessionExtensions.cs:121-148` 阵营归属，`KillSystem.cs` 击杀机制，`VentSystem.cs` 通风管机制。

### 3.1 分级标准

| 等级 | 标签 | 定义 |
|------|------|------|
| S | 顶级 | 能力显著影响对局走向，任何玩家拿到都有明显优势 |
| A | 强力 | 能力在大多数场景中有价值，核心角色首选 |
| B | 均衡 | 能力有明确场景价值但非必需，正常发挥不影响胜率 |
| C | 偏弱 | 能力收益有限或触发条件苛刻，需要队友配合 |

### 3.2 Tier List（Round 1 基准）

#### S Tier — Inspector（警探）

| 属性 | 值 |
|------|-----|
| 阵营 | Police（`FactionRole() => OnlineRole.Police`） |
| 核心能力 | **ReportCooldownReduce** — 报告冷却缩减（倍率） |
| 辅助能力 | **FootprintTrack** — 查看附近玩家足迹（Boolean） |
| 强度理由 | 报告冷却缩减直接影响会议触发频率，更快触发会议 = 更多推理机会。足迹追踪让 Inspector 能在走廊上留下信息线索，这是独一无二的信息获取能力，对团队价值极高 |
| 风险点 | 没有自保能力，在黑帮击杀冷却转好后容易被针对 |

#### A Tier — Tech（技术员）

| 属性 | 值 |
|------|-----|
| 阵营 | Police（`FactionRole() => OnlineRole.Police`） |
| 核心能力 | **TaskSpeedBonus** — 任务完成速度加成（倍率） |
| 辅助能力 | **EvidenceChainBonus** — 证据链加速（倍率） |
| 辅助能力 | **RemoteSurveillance** — 远程查看监控（Boolean） |
| 强度理由 | 快速完成任务 + 证据链加速双管齐下，是警方任务胜利路线的核心引擎。远程监控提供不移动即可获取信息的能力，降低了被击杀的风险 |

#### A Tier — Forensics（法医）

| 属性 | 值 |
|------|-----|
| 阵营 | Police（`FactionRole() => OnlineRole.Police`） |
| 核心能力 | **CorpseExamine** — 检验尸体获得额外线索（Boolean，BonusValue=线索数） |
| 辅助能力 | **ReportRangeBonus** — 报告范围加成 |
| 强度理由 | 尸体检验是会后讨论的关键信息来源。每次尸体出现都能提取比其他角色更多的情报，是会议推理阶段的强力信息角色。报告范围加成提升了安全性 |

#### B Tier — Enforcer（打手）

| 属性 | 值 |
|------|-----|
| 阵营 | Gang（`FactionRole() => OnlineRole.Gang`） |
| 核心能力 | **KillCooldownReduce** — 击杀冷却缩减（倍率） |
| 辅助能力 | **KillRangeBonus** — 击杀范围加成（世界单位） |
| 强度理由 | 击杀冷却缩短和范围加成是纯粹的黑帮战斗增益。当前 `KillSystem.cs:27` 默认冷却 18 秒，Enforcer 能进一步压缩，在 6 人小局中优势更大（需要更快消灭警力）。但在 10 人局中击杀不是唯一胜利路径 |

#### B Tier — Fixer（清道夫）

| 属性 | 值 |
|------|-----|
| 阵营 | Gang（`FactionRole() => OnlineRole.Gang`） |
| 核心能力 | **BodyDrag** — 可拖动尸体（Boolean） |
| 辅助能力 | **SabotageCooldownReduce** — 破坏冷却缩减（倍率） |
| 强度理由 | 拖动尸体是独特的隐蔽能力，能有效延迟警方发现尸体并触发会议。破坏冷却缩减提供额外的战术选项。这两个能力配合能让黑帮获得更长的不受干扰的行动窗口 |

#### B Tier — Driver（车手）

| 属性 | 值 |
|------|-----|
| 阵营 | Undercover（`FactionRole() => OnlineRole.Undercover`） — 但公开身份为 Gang |
| 核心能力 | **VentSpeedBonus** — 通风管速度加成（倍率） |
| 辅助能力 | **MoveSpeedBonus** — 移动速度加成（倍率） |
| 辅助能力 | **VentCooldownReduce** — 通风管冷却缩减（倍率） |
| 强度理由 | 移速加成提供全场景泛用价值。通风管加速让 Driver 作为 Undercover 阵营成员能更快使用通风管系统（`VentSystem.cs:36` 默认冷却 10 秒），提高机动性。但 Driver 的公开身份是 Gang，容易被警方怀疑 |

#### B Tier — UndercoverAgent（卧底）

| 属性 | 值 |
|------|-----|
| 阵营 | Undercover（`FactionRole() => OnlineRole.Undercover`） — 但公开身份为 Gang |
| 核心能力 | **SecretVote** — 秘密投票（会议投票不被公开） |
| 强度理由 | 秘密投票让 UndercoverAgent 能在会议中不受观察者压力地投票，降低被黑帮通过投票行为推断身份的风险。这是会议阶段的纯信息保护能力。但 Outside 会议阶段没有额外战斗/任务能力，需要依赖队友掩护 |

#### C Tier — Mole（内鬼）

| 属性 | 值 |
|------|-----|
| 阵营 | Mole（`FactionRole() => OnlineRole.Mole`） — 公开身份为 Police |
| 核心能力 | **SabotageCooldownReduce** — 破坏冷却缩减（倍率） |
| 辅助能力 | 作为黑帮阵营但伪装成警察，可内部破坏 |
| 强度理由 | Mole 的定位特殊——公开为警察但实际为黑帮线人（`SocialKnowledge.cs:44-54`）。破坏冷却缩减提供战术价值，但 Mole 不能击杀（只能通过 MoleIntel 路线获胜），且如果被识破则损失巨大。M8.2 新增职业，实战数据缺乏，暂定 C 级待验证 |

### 3.3 Tier List 总览

| 等级 | 职业 | 阵营 |
|------|------|------|
| S | Inspector（警探） | Police |
| A | Tech（技术员） | Police |
| A | Forensics（法医） | Police |
| B | Enforcer（打手） | Gang |
| B | Fixer（清道夫） | Gang |
| B | Driver（车手） | Undercover |
| B | UndercoverAgent（卧底） | Undercover |
| C | Mole（内鬼） | Mole |

> **注意**：当前 S/A 级全在 Police 阵营，这是有意为之的设计——Police 方需要通过任务和信息优势对抗 Gang 方的击杀能力。如果实测中 Police 胜率 >55%，优先调整 B 级 Gang 职业。

---

## 4. 每职业推荐调参杠杆

> 所有倍率在 `OnlineRuleSet.ProfessionAbilities` 数组中配置（`ProfessionAbility.Multiplier` / `BonusValue`）。

### 4.1 Inspector（警探）— S Tier

| 杠杆 | AbilityType | 当前建议值 | 调参方向 |
|------|-------------|-----------|---------|
| 报告冷却倍率 | ReportCooldownReduce | Multiplier: 0.6 | 上调至 0.7-0.8 减弱（Police 胜率高时） |
| 足迹可见范围 | FootprintTrack | BonusValue: 3.0m | 调整可见半径 |
| 报告范围加成 | ReportRangeBonus | BonusValue: 0.5 | 上调增加安全性 |

### 4.2 Tech（技术员）— A Tier

| 杠杆 | AbilityType | 当前建议值 | 调参方向 |
|------|-------------|-----------|---------|
| 任务速度倍率 | TaskSpeedBonus | Multiplier: 1.3 | 1.1-1.5 范围微调 |
| 证据链倍率 | EvidenceChainBonus | Multiplier: 1.25 | 1.0-1.5 范围 |
| 远程监控持续时间 | RemoteSurveillance | BonusValue: 5.0s | 调整查看时长 |

### 4.3 Forensics（法医）— A Tier

| 杠杆 | AbilityType | 当前建议值 | 调参方向 |
|------|-------------|-----------|---------|
| 尸体检验线索数 | CorpseExamine | BonusValue: 2 | 1-3 范围调整 |
| 报告范围加成 | ReportRangeBonus | BonusValue: 0.3 | 配合 Inspector 差异化 |

### 4.4 Enforcer（打手）— B Tier

| 杠杆 | AbilityType | 当前建议值 | 调参方向 |
|------|-------------|-----------|---------|
| 击杀冷却倍率 | KillCooldownReduce | Multiplier: 0.75 | 0.6-0.85 范围（影响显著） |
| 击杀范围加成 | KillRangeBonus | BonusValue: 0.3 | 基于 KillSystem.killRange(1.5) 叠加 |

### 4.5 Fixer（清道夫）— B Tier

| 杠杆 | AbilityType | 当前建议值 | 调参方向 |
|------|-------------|-----------|---------|
| 尸体拖拽速度 | BodyDrag | BonusValue: 0.7 | 拖拽速度倍率 |
| 破坏冷却倍率 | SabotageCooldownReduce | Multiplier: 0.8 | 0.65-0.9 范围 |

### 4.6 Driver（车手）— B Tier

| 杠杆 | AbilityType | 当前建议值 | 调参方向 |
|------|-------------|-----------|---------|
| 移动速度倍率 | MoveSpeedBonus | Multiplier: 1.12 | 1.05-1.2 范围 |
| 通风管速度倍率 | VentSpeedBonus | Multiplier: 1.3 | 配合 VentSystem.transitionDuration(0.5s) |
| 通风管冷却倍率 | VentCooldownReduce | Multiplier: 0.8 | 配合 VentSystem.ventCooldown(10s) |

### 4.7 UndercoverAgent（卧底）— B Tier

| 杠杆 | AbilityType | 当前建议值 | 调参方向 |
|------|-------------|-----------|---------|
| 秘密投票 | SecretVote | Enabled: true | 开关控制 |
| 会议嫌疑权重 | (通过 OnlineRuleSet) | 0.7 | 0.5-1.0（降低=更隐蔽） |

### 4.8 Mole（内鬼）— C Tier（待验证）

| 杠杆 | AbilityType | 当前建议值 | 调参方向 |
|------|-------------|-----------|---------|
| 破坏冷却倍率 | SabotageCooldownReduce | Multiplier: 0.85 | 0.65-0.95 范围 |
| 暗视能力 | DarkVision | Enabled: false | 启用提升生存力 |

---

## 5. 阵营平衡调参场景

### 5.1 场景 A：Police 胜率 >55%

**优先级从高到低**：
1. `KillCooldownSeconds` -5（让 Gang 更快获得击杀机会）
2. Inspector `ReportCooldownReduce` Multiplier 调高至 0.75（削弱报告频率）
3. Tech `TaskSpeedBonus` Multiplier 调低至 1.1（减慢任务完成速度）
4. `EvidenceTarget` +2（提高警方任务胜利门槛）
5. 启用 Mole 的 `DarkVision` 能力（增强线人隐蔽性）

### 5.2 场景 B：Gang 胜率 >55%

**优先级从高到低**：
1. `KillCooldownSeconds` +5（给 Police 更多反应时间）
2. Enforcer `KillCooldownReduce` Multiplier 调高至 0.85（削弱打手击杀频率）
3. Inspector `ReportCooldownReduce` Multiplier 调低至 0.5（增强报告频率）
4. Forensics `CorpseExamine` BonusValue +1（增强尸体信息量）
5. `BodyReportRadius` +0.3（让尸体更容易被发现）

### 5.3 场景 C：对局总是 <5 分钟结束（速杀问题）

1. `KillCooldownSeconds` +10
2. 开局前 30 秒击杀冷却强制生效
3. 检查 Enforcer 配置是否过于激进
4. `MatchTargetMinSeconds` +120

### 5.4 场景 D：对局总是 >20 分钟（拖延问题）

1. `KillCooldownSeconds` -8
2. `EvidenceTarget` -3
3. Fixer `SabotageCooldownReduce` Multiplier 调低至 0.65（增强破坏节奏）
4. `BotTaskSpeedMultiplier` +0.2

---

## 6. 已有系统引用

| 系统 | 文件路径 | 用途 |
|------|---------|------|
| MatchStatsCollector | `Assets/_Project/Scripts/Online/MatchStatsCollector.cs` | 对局数据采集，输出 MatchLogEntry（含胜率、时长、任务、击杀、会议等） |
| OnlineProfession | `Assets/_Project/Scripts/Online/OnlineProfession.cs` | 职业枚举 + ProfessionAbility 能力定义 + AbilityType 能力类型 |
| OnlineRuleSet | `Assets/_Project/Scripts/Online/OnlineRuleSet.cs`（ScriptableObject） | 所有可调参数：KillCooldown、EvidenceTarget、ProfessionAbilities 等 |
| KillSystem | `Assets/_Project/Scripts/Online/KillSystem.cs` | 击杀范围(1.5m)、冷却(18s)、屏幕闪光、报告按钮 |
| VentSystem | `Assets/_Project/Scripts/SocialDeduction/VentSystem.cs` | 通风管范围(0.9m)、冷却(10s)、过渡(0.5s) |
| VictoryEvaluator | `Assets/_Project/Scripts/Gameplay/VictoryEvaluator.cs` | 双向渗透胜利判定（证据达标/全淘汰/线人情报/掩护崩溃） |
| SabotageType | `Assets/_Project/Scripts/Online/SabotageType.cs` | 破坏类型枚举：Blackout/Lockdown/Communications/EvidenceLeak/PatrolAlert/CriticalO2/CriticalReactor |
| CriticalTaskSystem | `Assets/_Project/Scripts/SocialDeduction/CriticalTaskSystem.cs` | 紧急任务（O2/Reactor），30 秒限时 |
| OpponentAi | `Assets/_Project/Scripts/Gameplay/OpponentAi.cs` | AI 决策引擎（Gang/Undercover/Police/Mole 四角色策略） |
| MapLayoutData | `Assets/_Project/Scripts/Online/Map/MapLayoutData.cs` | 地图布局 ScriptableObject（房间/走廊/任务/通风管/监控/遮挡体） |

---

## 7. 调参流程

1. **采集数据**：打 10 场对局，确保 MatchStatsCollector 正常输出
2. **查看汇总**：Tools → Match Stats Viewer，确认各指标是否在目标范围
3. **识别偏差**：找出偏离目标 >10% 的指标
4. **单一调整**：每次只调整 1-2 个参数（参见调参场景）
5. **验证**：再打 5 场，确认指标是否趋近目标
6. **迭代**：重复步骤 2-5，直到所有指标在目标范围内
7. **记录**：每次调整记录到本文档版本记录

---

## 8. 版本记录

| 日期 | 修改内容 |
|------|---------|
| 2026-06-05 | 初版，基于代码审计建立 Round 1 基准 |

---

> **核心原则**：所有数值调整必须基于 MatchStatsCollector 采集的实际对局数据。每次只改 1-2 个参数，先观察再继续。
