# 语音方案二选一定调 —— 决策记录

- 日期: 2026-06-04
- 决策者: File Agent（按 game_development_plan_2d_full_20260604.md M1/Task 1.3 执行）
- 状态: 已定调

---

## 1. 背景

### 1.1 现状

| 证据 | 文件 | 内容 |
|---|---|---|
| Vivox 已移除 | `UnityServiceBootstrap.cs:31-35` | `VivoxReady => false`，`VoiceStatus => "Vivox 未安装（已移除）"`，所有 Vivox 属性均为 stub |
| 语音系统纯本地 | `VoiceChatSystem.cs`(1080) | namespace `SocialDeduction`，无 NetworkBehaviour/RPC，仅离线占位 |
| 聊天系统非网络类 | `ChatSystem.cs:19` | `class ChatSystem`，无 NetworkBehaviour/RPC，仅在离线会议中工作 |
| README 仍承诺语音 | `README.md:29,53` | 第 29 行 "proximity voice rules"，第 53 行 "Vivox integration" |

### 1.2 两方案

| | 方案 A：不做语音 | 方案 B：联机文本聊天替代 |
|---|---|---|
| 做法 | 删除所有语音/聊天 UI 承诺，README 不再提语音 | ChatSystem 改造为 NetworkBehaviour，实现会议/全局/近距离三通道 + 鬼魂频道 |
| 优势 | 零开发成本 | 成本可控、信息量大、文本可追溯、对社交推理有价值 |
| 劣势 | 会议只靠投票 UI，缺失沟通维度 | 需改造 ChatSystem + HUD（约 M 规模） |
| 玩家体验 | 纯投票推理，沟通全靠外部工具 | 游戏内文本聊天，沉浸感更强 |

---

## 2. 决策

**选择方案 B：联机文本聊天替代语音，保留近距离概念。**

### 2.1 理由

1. **成本可控**：ChatSystem.cs(282行) 已有消息管理/输入/渲染基础，改造为 NetworkBehaviour 约 M 规模（与计划 Task 1.4 一致），无需第三方 SDK 费用。
2. **信息量大**：文本聊天天然可追溯——会议发言可回看、可引用，这对警匪卧底题材的"证据链推理"是加分项。
3. **Vivox 已移除**：UnityServiceBootstrap.cs 中 Vivox 相关代码已是 stub 态，无回退成本。
4. **文本聊天对社交推理的价值**：可追溯的会议记录让"前后矛盾""改口"成为可玩要素，比纯语音更有利于推理深度。
5. **降低玩家门槛**：麦克风硬件/环境噪音/语言障碍等问题不复存在，纯键盘即可完整体验。

### 2.2 三通道定义

| 通道 | 触发条件 | 可见范围 | 用途 |
|---|---|---|---|
| **会议频道** | 会议/投票阶段 | 所有存活玩家 | 集体讨论、指认、辩护 |
| **全局频道** | 自由行动阶段 | 所有存活玩家（不分阵营） | 公开喊话、求助 |
| **近距离频道** | 自由行动阶段 + 玩家距离 ≤ 阈值 | 附近存活玩家（不分阵营） | 悄悄结盟、私下交易、制造目击 |
| **鬼魂频道** | 玩家死亡后 | 仅其他死亡玩家 | 观战交流，不影响活人局 |

### 2.3 技术路线（对应 Task 1.4）

- `ChatSystem` 改为 `NetworkBehaviour`，消息经 RPC 同步。
- 频道规则由 `OnlineMatchController` 的会议状态 + 玩家存活状态 + 玩家位置判定。
- 纯文本渲染，防 XSS/注入。
- 死亡玩家自动切换鬼魂频道，不得与存活玩家交互。

---

## 3. 影响范围

| 系统 | 操作 | 优先级 |
|---|---|---|
| `VoiceChatSystem.cs`(1080) | 标注为「本地占位，不联机」，加 `[Obsolete]` 或头部注释 | M1 |
| `UnityServiceBootstrap.cs` | 已是 stub 态，无需改动 | — |
| `ChatSystem.cs` / `ChatMessage.cs` | Task 1.4 改造为 NetworkBehaviour（不在本文范围） | M1 |
| `OnlineMatchHud.cs` | 语音 UI 替换为文本聊天入口（不在本文范围） | M1 |
| `README.md` | 删除"近距离语音"承诺 → 改为"联机文本聊天（三通道）" | M1（本文） |

---

## 4. 后续任务

- **Task 1.4**（同 M1）：ChatSystem 联机化 —— NetworkBehaviour + RPC + 三通道 + 鬼魂频道 + 防注入。
- **M7 Task 7.3**：聊天 UI 随全 Canvas 化一起收口。

---

## 5. 风险与缓解

| 风险 | 缓解 |
|---|---|
| 文本聊天降低社交推理的"氛围感" | 港区题材 + 会议 UI 设计可弥补（警匪对峙氛围靠视觉/音效而非语音本身） |
| 打字速度影响会议节奏 | 预设快捷语 + 投票阶段固定时长、发言冷却可调 |
| 防刷屏/滥用 | 限流 + 冷却 + 纯文本渲染防注入 |
