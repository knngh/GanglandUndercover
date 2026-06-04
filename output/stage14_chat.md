---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9470d846c7eff9b24afb94a99a2cb3f0_d7b8b2735f0811f18d42525400d9a7a1
    ReservedCode1: jFh/Nd7x1NNLKr/730g7KolTwUzt1frzNz5sR6ogZI+FinXD7UkPdK6Y/F24MPbfHbMSZ0AAM7eiFLf7XZdxoJpvVNIaTORMM83MJbuQIdDwQ0qgDnpKEpiUhnG8EYvPcfpn+Gg+WUbAVk08ontRrR0YfOagj2lfRA8oCbiCYiCslVQARpqz9vG2DaQ=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9470d846c7eff9b24afb94a99a2cb3f0_d7b8b2735f0811f18d42525400d9a7a1
    ReservedCode2: jFh/Nd7x1NNLKr/730g7KolTwUzt1frzNz5sR6ogZI+FinXD7UkPdK6Y/F24MPbfHbMSZ0AAM7eiFLf7XZdxoJpvVNIaTORMM83MJbuQIdDwQ0qgDnpKEpiUhnG8EYvPcfpn+Gg+WUbAVk08ontRrR0YfOagj2lfRA8oCbiCYiCslVQARpqz9vG2DaQ=
---

# Stage 14: 联机文本聊天系统 — 完成报告

**日期**：2026-06-03
**状态**：完成

---

## 产出物清单

| 文件 | 类型 | 状态 |
|------|------|------|
| `Assets/_Project/Scripts/Online/ChatMessage.cs` | 新建 | 已存在 |
| `Assets/_Project/Scripts/Online/ChatSystem.cs` | 新建 | 已存在 |
| `Assets/_Project/Scripts/Online/OnlineMatchController.cs` | 修改 | 已集成 |
| `Assets/_Project/Scripts/SocialDeduction/SocialPrototypeController.cs` | 修改 | 本次完成 |

---

## 1. ChatMessage.cs — 消息数据结构

**路径**：`Assets/_Project/Scripts/Online/ChatMessage.cs`

```csharp
public struct ChatMessage
{
    public string SenderId;
    public string SenderName;
    public string Content;
    public float Timestamp;
    public bool IsDead;
    public Faction Faction;
}
```

所有字段均已实现：senderId、senderName、content、timestamp、isDead、faction。跨联机/离线模式共用。

---

## 2. ChatSystem.cs — 跨模式聊天引擎

**路径**：`Assets/_Project/Scripts/Online/ChatSystem.cs`

### 功能清单

| 功能 | 实现 |
|------|------|
| 会议阶段：所有存活玩家发言，广播给所有客户端 | `CurrentPhase = Meeting` → CanSend=true for alive |
| 自由阶段：仅同阵营私聊（Police+Undercover / Gang+Mole） | `CurrentPhase = Action` → OnlineMatchController 使用 `IsSameFaction` 过滤 |
| 死亡玩家只读 | `IsAlive = false` → 显示"[你已死亡，无法发言]" |
| 消息格式：[玩家名]：内容，阵营色 | `GetFactionColor()` 返回蓝/绿/红/橙色 |
| 聊天框：底部输入栏 + 消息列表滚动 | `DrawChatPanel()` 完整实现 |
| 最多 50 条 | `MaxMessages = 50`，超出自动移除 |
| Enter 打开/发送，Esc 关闭 | `ProcessInputKeys()` 完整实现 |

### 阵营色映射

| Faction | 颜色 |
|---------|------|
| Police | 蓝 (0.35, 0.68, 1.0) |
| Undercover | 绿 (0.28, 0.88, 0.52) |
| Gang | 红 (0.92, 0.28, 0.22) |
| Mole | 橙 (0.95, 0.55, 0.12) |
| None | 灰 (0.6, 0.6, 0.6) |

---

## 3. OnlineMatchController.cs — 联机集成

**路径**：`Assets/_Project/Scripts/Online/OnlineMatchController.cs`

### 集成点

- `chatSystem` 字段：声明 `private ChatSystem chatSystem`
- `EnsureChatSystem()`：初始化，传入 `SendChatMessage` 回调
- `SendChatMessage(string content)`：本地添加消息 + 网络发送
- `ReceiveChatSend(...)`：服务端接收，基于 phase 决定广播范围（会议全员 / 自由同阵营）
- `ReceiveChatBroadcast(...)`：客户端接收广播
- 会议 UI：`DrawMeetingScreen` 渲染聊天面板，`CurrentPhase = OnlineMatchPhase.Meeting`
- 行动 UI：`DrawActionChatPanel` 渲染聊天，`CurrentPhase = OnlineMatchPhase.Action`
- 死亡禁用：`IsAlive = IsLocalAlive()`
- `Shutdown()`：调用 `chatSystem?.Clear()`

---

## 4. SocialPrototypeController.cs — 离线模式聊天（本次完成）

**路径**：`Assets/_Project/Scripts/SocialDeduction/SocialPrototypeController.cs`

### 新增内容

| 方法 | 说明 |
|------|------|
| `offlineChatSystem` 字段 | 跨模式 ChatSystem 实例 |
| `UpdateOfflineChat()` | 每帧调用，AI 消息定时器驱动 |
| `SendNextAiChatMessage()` | 从 10 条预设消息池选取，由随机 NPC 发送 |
| `OnOfflineChatSend(string)` | 玩家输入回调，写入 ChatSystem |
| `OnGUI()` | IMGUI 渲染聊天面板（右下角 27%×34%） |

### 会议阶段聊天行为

- AI 每 3.5 秒自动发送一条预设讨论消息（中英文两套模板）
- 玩家可通过 Enter 打开输入框发送消息
- Esc 关闭输入框
- 聊天面板右下角定位，不遮挡投票 UI

### 预设消息（中文）

1. "我觉得我们应该查一下监控录像，看看谁的路线有问题。"
2. "有人看到可疑人物在货柜码头附近吗？"
3. "我注意到昨晚夜市巷有异常活动。"
4. "证物库那边好像被翻动过，有人承认吗？"
5. "专案办公室的情报显示黑帮有内应。"
6. "我昨天在主街看到有人鬼鬼祟祟的。"
7. "地下诊所的登记记录对不上，谁去过那里？"
8. "我建议先查一下每个人的任务完成情况。"
9. "侦探日志里提到货柜码头有异常交易记录。"
10. "证据链断了几条，说明有人在干扰调查方向。"

---

## 技术要点

- ChatSystem 设计为**跨模式引擎**（联机+离线），通过 `CurrentPhase` 字段控制行为
- 离线模式将 `CurrentPhase` 设为 `OnlineMatchPhase.Meeting`，复用联机聊天面板渲染
- 非会议阶段无聊天功能（离线模式下不会有自由阶段的阵营私聊）
- 消息列表最多 50 条，超出自动丢弃最早消息
*（内容由AI生成，仅供参考）*
