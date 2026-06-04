# Stage 12 — 双向渗透模型核心重构

## 变更摘要

将游戏核心逻辑从三阵营（Gang / Police / Undercover）重构为四角色双向渗透模型：黑帮安插线人（Mole）混入警察内部，警察派遣卧底（Undercover）潜伏黑帮。双方表面互相认识，各藏一张暗牌。

---

## 1. SocialRole 枚举

**文件**: `Assets/_Project/Scripts/Core/GameState.cs`

新增 `SocialRole.Mole` — 黑帮线人，伪装为警方技侦。

| 枚举值 | 显示阵营 | 实际阵营 | 伪装身份 |
|--------|---------|---------|---------|
| Gang | 黑帮成员 | 黑帮 | — |
| Undercover | 黑帮成员 | 警察 | 黑帮 |
| Police | 警察成员 | 警察 | — |
| Mole | 警察成员 | 黑帮 | 警察 |

---

## 2. SocialKnowledge.cs（新增）

**文件**: `Assets/_Project/Scripts/Core/SocialKnowledge.cs`

双向渗透信息可见性系统：

- **警察视角** (Undercover + Police)：可见所有 Gang 身份 + 所有 Police 身份（含 Mole 伪装者），但不知道哪个 Police 是 Mole
- **黑帮视角** (Gang + Mole)：可见所有 Police 身份 + 所有 Gang 身份（含 Undercover 伪装者），但不知道哪个 Gang 是卧底
- 提供 `GetVisibleRoles()`, `IsHiddenRoleKnown()` 等查询接口

---

## 3. GameState.cs 新增字段

| 字段 | 类型 | 说明 |
|------|------|------|
| `UndercoverEvidence` | int | 卧底收集的证据量（警察阵营共享） |
| `MoleIntel` | int | 线人情报量（黑帮阵营共享） |
| `KnownGangToPolice` | List<int> | 警察已知的黑帮 ID 列表 |
| `KnownPoliceToGang` | List<int> | 黑帮已知的警察 ID 列表 |
| `GangEliminated` | bool | Gang 被票选出局 |
| `UndercoverEliminated` | bool | Undercover 被票选出局 |
| `PoliceEliminated` | bool | Police 被票选出局 |
| `MoleEliminated` | bool | Mole 被票选出局 |

新增方法：`AddUndercoverEvidence(int)`, `AddMoleIntel(int)`, `EliminateRole(SocialRole)`

---

## 4. VictoryEvaluator.cs 重写胜利条件

| 条件 | 结果 |
|------|------|
| `UndercoverEvidence >= 目标值` AND Undercover 存活 | **警察胜利** |
| `MoleIntel >= 目标值`（识别出卧底）OR Undercover 被消灭 | **黑帮胜利** |
| 时间耗尽且双方未达标 | **双输/僵局** |

---

## 5. OpponentAi.cs — 四角色 AI 策略

| 角色 | 行动阶段策略 | 会议投票策略 |
|------|-------------|-------------|
| **Gang** | 优先去高风险区搜查，投票可疑目标 | 根据嫌疑度选票；优先投 Undercover |
| **Undercover** | 去信息区收集证据；嫌疑高时维持掩护 | 混入黑帮投票；保护自己不暴露 |
| **Police** | 巡逻保护卧底；辅助收集辅助证据 | 根据已知信息投票；优先投 Gang |
| **Mole** | 混入警察中；去卧底出没区域调查 | 伪装警察投票；暗中投 Undercover |

---

## 6. GameController.cs — 回合逻辑调整

- `RunPlayerAction`：调用 `opponentAi.Run()` 四角色 AI
- `RunMeeting`：调用 `opponentAi.CastMeetingVote()` 返回 SocialRole；通过 `EliminateRole()` 淘汰
- `PlayerCastVote(SocialRole targetRole)`：玩家直接投票淘汰指定角色
- Undercover 行动写入 `UndercoverEvidence`；Mole 行动写入 `MoleIntel`

---

## 7. ActionResolver.cs — Mole 新增行动

| 行动 | 描述 | 效果 |
|------|------|------|
| `mole_surveil` | 跟踪可疑警察 | +MoleIntel；可能暴露目标区域 |
| `mole_infiltrate` | 潜入档案室 | +MoleIntel；查看案件日志 |
| `mole_tipoff` | 秘密接头传情报 | +MoleIntel 大量；有暴露风险 |
| `mole_frame` | 伪造证据陷害卧底 | 增加 Undercover 暴露值 |

所有 Police 调查和 Undercover 情报行动已同步调用 `state.AddUndercoverEvidence()`。

---

## 8. SocialPrototypeController.cs 修改

| 位置 | 修改内容 |
|------|---------|
| `RoleName` | 新增 `SocialRole.Mole` → "线人" |
| `CreateCharacters` | botRoles 从 `[Police,Police,Undercover,Gang]` 改为 `[Police,Undercover,Gang,Mole]` |
| `GetPrefabPathForRole` | Mole 使用 `police_Male_A` 预设 |
| `StartGame` | Mole 玩家开场词："你是黑帮线人。伪装为警方技侦，暗中收集卧底情报。" |
| `BeginRound` | Mole 操作提示："E 跟踪调查，F 潜入档案，C 秘密接头；保持伪装。" |
| `GetFactionForRole` | Mole → `Faction.Gang` |
| `CastTurnVote` | 改为传递 `target.Role` (SocialRole) 而非 Faction |
| `SyncTurnElimination` | 改为按 SocialRole 标志位逐个判断淘汰 |
| 角色颜色 | Gang/Undercover=红色，Police/Mole=蓝色 |

---

## 9. SocialCharacter.cs 修改

- 新增 `MoleColor` = (0.18, 0.48, 0.42)
- `GetPlayerColor(SocialRole.Mole)` 返回 MoleColor
- 保留原有阵营颜色映射：Gang→红、Police→蓝

---

## 10. 联机模式修改

- **OnlineRole.cs**：枚举新增 `Mole` 值
- **OnlineMatchController.cs**：`RoleName` 方法新增 `"线人"` case

---

## 11. UI 交互修改

### MainMenuController.cs
- 身份选择从 3 个扩展为 4 个（卧底/黑帮/警察/线人）
- 按钮布局 `startX` 从 `1.5f` 调整为 `2.0f`，循环从 3 改为 4
- `GetRoleColor` 新增 index=3（Mole）返回 `ThemeManager.MoleTeal`
- 角色描述新增：`"混入警方技侦，暗中收集卧底情报"`

### ThemeManager.cs
- 新增 `MoleTeal` (0.12, 0.42, 0.38) — 线人深青
- `GetRoleColor(SocialRole)` 新增 Mole 分支

### SocialPrototypeHud.cs
- `RoleName` 新增 Mole 中英文翻译："线人 / Mole"

---

## 12. 文件变更清单

| 文件 | 操作 | 行数变化 |
|------|------|---------|
| `Core/SocialKnowledge.cs` | 新增 | +125 |
| `Core/GameState.cs` | 重写 | 新增 8 字段 / 3 方法 |
| `Core/VictoryEvaluator.cs` | 重写 | 四条件胜利模型 |
| `Core/OpponentAi.cs` | 重写 | 四角色 AI |
| `Core/GameController.cs` | 重写 | SocialRole 投票 |
| `Core/ActionResolver.cs` | 修改 | +4 Mole 行动 |
| `SocialDeduction/SocialPrototypeController.cs` | 修改 | 6 处 |
| `SocialDeduction/SocialCharacter.cs` | 修改 | MoleColor |
| `SocialDeduction/SocialPrototypeHud.cs` | 修改 | Mole 翻译 |
| `Online/OnlineRole.cs` | 修改 | Mole 枚举值 |
| `Online/OnlineMatchController.cs` | 修改 | RoleName |
| `UI/MainMenuController.cs` | 修改 | 4 按钮布局 |
| `UI/ThemeManager.cs` | 修改 | MoleTeal 颜色 |