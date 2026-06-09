# Gangland Undercover — UI 文案一致性审计

> 日期: 2026-06-09 | 只改中文提示，不动联网逻辑

---

## 1. Localization.cs 重复键

| 大写键 | 小写键 | 中文值一致? | 状态 |
|--------|--------|-----------|------|
| `role.Gang`="黑帮" | `role.gang`="黑帮" | ✅ 一致 | 🟡 大写键0处引用，**可删除** |
| `role.Police`="警察" | `role.police`="警察" | ✅ 一致 | 🟡 大写键0处引用，**可删除** |
| `role.Undercover`="卧底" | `role.undercover`="卧底" | ✅ 一致 | 🟡 大写键0处引用，**可删除** |
| `role.choose` (L10) | `role.choose` (L180) | 中文=英文 | 正常（中英双语表） |

---

## 2. 中文 vs 英文不一致

| 键 | 中文 | 英文 | 不一致? |
|----|------|------|---------|
| `action.interact.ready` | [E] 查证 | [E] Inspect | ⚠️ "查证" vs "Inspect" |
| `action.kill.ready` | [Q] 击倒 | [Q] Kill | ✅ |
| `action.report.ready` | [R] 报案 | [R] Report | ✅ |
| `profession.undercover_agent` | 卧底 | Undercover Agent | ⚠️ 中文省略"Agent" |
| `meeting.title` | — | Kowloon Port Meeting | ❌ 中文键缺失 |

### 3. OnlineMatchHud.cs 硬编码中文（应迁移到 Localization.T()）

| 行号 | 硬编码文本 | 建议 Localization 键 |
|------|-----------|---------------------|
| 572 | "连接与开局" | `ui.section.connection` |
| 574 | "玩家" | `ui.label.player_name` |
| 575 | "房间" | `ui.label.room_name` |
| 580 | "创建 Host" | — (中英混合，建议统一) |
| 581 | "加入 Client" | — |
| 588 | "本地完整局" | `ui.button.local_preview` |
| 589 | "离开房间" | `ui.button.leave_room` |
| 593 | "房间规则" | `ui.section.room_rules` |
| 595 | "最少人数" | `ui.label.min_players` |
| 598 | "人数不足时 AI 补位" | `ui.toggle.auto_fill_ai` |
| 599 | "出局时公开身份" | `ui.toggle.reveal_role` |
| 600 | "行动阶段近距离语音" | `ui.toggle.proximity_voice` — ⚠️ Vivox 已移除，应改为"行动阶段文本聊天" |
| 602 | "房间流程" | `ui.section.room_flow` |

**共约 30 处硬编码中文**，建议统一抽取到 Localization 表。但属 UI 层改动，不改联网逻辑。

---

## 4. 建议修改（纯文档，不改代码）

| 优先级 | 改动 | 影响 |
|--------|------|------|
| 🟢 P3 | 删除 3 个大写 role 键 | 清理死数据 |
| 🟢 P3 | "查证"→"互动"或英文"Inspect"对应 | 语义一致 |
| 🟢 P3 | 补充 `meeting.title` 中文 | 会议标题本地化 |
| 🟡 P2 | "行动阶段近距离语音" → "行动阶段文本聊天" | Vivox 已移除，UI 文案应同步 |
