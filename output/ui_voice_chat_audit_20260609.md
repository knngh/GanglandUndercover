# Gangland Undercover — UI 文案审计：语音/聊天相关

> 日期: 2026-06-09 | 类型: 只读审计，不改代码
> 范围: 所有玩家可见的 "Vivox / 语音 / voice / 聊天 / chat" 文案

---

## 分类说明

| 类别 | 说明 |
|------|------|
| 🟥 A | 玩家可见，需要修改（文本出现在 HUD/面板/设置中） |
| 🟨 B | 玩家不可见，内部变量名/类名，可保留 |
| 🟩 C | 世界构建锚点名称（Editor 内部），美术管线保留 |
| 🟩 D | Debug.Log 字符串，不面向玩家，可保留 |
| 🟧 E | 文档/注释，可更新以消除歧义 |

---

## 🟥 A 类：玩家可见，需修改

### A1. 主菜单描述文字

| 文件 | 行号 | 当前文案 | 替换建议 | 原因 |
|------|------|---------|---------|------|
| `MainMenuController.cs` | 200 | "语音/文字交流（需自行组队）" | "文字交流（联机文本聊天，需自行组队）" | Vivox 已移除，"语音/文字"暗示两者并存 |

### A2. 会议面板标题

| 文件 | 行号 | 当前文案 | 替换建议 | 原因 |
|------|------|---------|---------|------|
| `OnlineMatchHud.cs` | 700 | "会议原因 / 证据墙 / 票型 / 语音" | "会议原因 / 证据墙 / 票型 / 聊天" | 状态 chip 中的"语音"应改为"聊天" |

### A3. 会议席位面板标题

| 文件 | 行号 | 当前文案 | 替换建议 | 原因 |
|------|------|---------|---------|------|
| `OnlineMatchHud.cs` | 1435 | "语音全员" | "聊天全员" | Localization 键 `meeting.seat.voice` 的中文值 |
| `Localization.cs` | 94 | `"meeting.voice"` = "语音：" | → "聊天：" | 中文 Localization 表 |
| `Localization.cs` | 100 | `"meeting.seat.voice"` = "语音全员" | → "聊天全员" | 中文 Localization 表 |

### A4. HUD 行内文本

| 文件 | 行号 | 当前文案 | 替换建议 | 原因 |
|------|------|---------|---------|------|
| `OnlineMatchHud.cs` | 505 | `CreateText("Voice", ...)` | → `CreateText("Chat", ...)` | 内部对象名，但 UI 标签显示"Voice" |
| `OnlineMatchHud.cs` | 1325 | `"聊天: " + controller.VoiceHudLine` | ✅ 已正确（文本聊天） | 不需要改 |
| `OnlineMatchHud.cs` | 1359 | `"聊天: " + controller.VoiceStatus` | ✅ 已正确（VoiceStatus 返回"Vivox 已移除，使用文本聊天"） | 不需要改 |
| `OnlineMatchHud.cs` | 1308 | `"近距离聊天"` / `"会议聊天"` | ✅ 已正确（文本聊天频道名称） | 不需要改 |
| `OnlineMatchController.OnGUI.cs` | 30 | `"聊天: " + chatSystem.CurrentChannel` | ✅ 已正确 | 不需要改 |
| `ChatSystem.cs` | 102 | `"聊天"` (default channel) | ✅ 已正确 | 不需要改 |

### A5. 设置面板

| 文件 | 行号 | 当前文案 | 替换建议 | 原因 |
|------|------|---------|---------|------|
| `SettingsManager.cs` | 423 | `VoiceModeNames = { "按键说话", "自由发言", "禁用" }` | `{ "按键发送", "自由发送", "禁用" }` | Vivox 已移除，改为文本聊天发送模式 |
| `SettingsData.cs` | 144 | `/// <summary>语音模式：0=按键说话，1=自由发言，2=禁用</summary>` | `/// <summary>发送模式：0=按键发送，1=自由发送，2=禁用</summary>` | 注释同步 |
| `SettingsData.cs` | 317 | `/// <summary>语音聊天按键</summary>` | `/// <summary>文本聊天发送按键</summary>` | 注释同步 |
| `SettingsData.cs` | 43 | `/// <summary>音量变更事件（主音量, 音效, BGM, 语音聊天, 麦克风灵敏度）</summary>` | `/// <summary>音量变更事件（主音量, 音效, BGM, 文本聊天, 其他）</summary>` | 麦克风灵敏度无对应后端 |
| `OnlineMatchHud.cs` | 600 | `"行动阶段近距离聊天"` | ✅ 已正确（文本聊天toggle） | 不需要改 |

### A6. 新手引导

| 文件 | 行号 | 当前文案 | 替换建议 | 原因 |
|------|------|---------|---------|------|
| `TutorialGateway.cs` | 77 | "语音或文字，合理表达你的推理" | "文字聊天，合理表达你的推理" | Vivox 已移除 |

---

## 🟨 B 类：内部变量名，可保留

| 文件 | 行号 | 当前名称 | 说明 |
|------|------|---------|------|
| `SettingsManager.cs` | 23 | `KeyVoiceChatVolume` | PlayerPrefs 键名，内部使用 |
| `SettingsManager.cs` | 33 | `KeyVoiceMode` | PlayerPrefs 键名，内部使用 |
| `SettingsData.cs` | 56-60 | `_voiceChatVolume` / `VoiceChatVolume` | 内部字段，序列化到 PlayerPrefs |
| `SettingsData.cs` | 146-150 | `_voiceMode` / `VoiceMode` | 内部字段，序列化到 PlayerPrefs |
| `OnlineMatchController.Gameplay.cs` | 64 | `VoiceStatus` → 返回"文本聊天: ..." | 已更新语义 |
| `OnlineMatchController.Gameplay.cs` | 65 | `VoiceParticipantCount` | 返回聊天消息计数 |
| `OnlineMatchController.Gameplay.cs` | 66 | `VoiceRoutingEnabled` → 返回 true | 文本聊天始终可用 |
| `OnlineMatchController.Visuals.cs` | 18 | `StageTwoActiveVoiceRadiusCount` | 美术锚点计数 |
| `OnlineMatchController.Visuals.cs` | 66 | `ProximityVoiceEnabled` → 返回 false | 已更新 |
| `OnlineRuleSet.cs` | 120 | `ProximityVoiceEnabled = true` | 布尔开关，仍存在但无后端 |

> **建议**: 这些内部名称虽保留但容易误导新人。可在后续重构中批量重命名（如 `VoiceStatus` → `ChatStatus`），但不阻塞当前版本。

---

## 🟩 C 类：世界构建锚点名称（Editor 内部）

| 文件 | 行号 | 当前名称 | 性质 |
|------|------|---------|------|
| `OnlineWorldBuilder.cs` | 426 | `"VerticalSlice Stage1 Meeting voice channel blue strip"` | Editor 内部 prop 名 |
| `OnlineWorldBuilder.cs` | 437 | `"VerticalSlice Stage1 Meeting player voice seat N"` | Editor 内部 prop 名 |
| `OnlineWorldBuilder.cs` | 486 | `"VerticalSlice Stage1 GameplayAnchor action voice radius N"` | Editor 内部 prop 名 |
| `OnlineWorldBuilder.cs` | 1840 | `"Stage2 VoiceRadius action proximity"` | Editor 内部 prefab 子对象名 |
| `OnlineWorldBuilder.cs` | 1885 | `"Stage2 Meeting voice mic"` | Editor 内部 prefab 子对象名 |
| `OnlineMatchController.VerticalSlice.cs` | 504 | `"VerticalSlice Stage1 Meeting voice channel blue strip"` | Editor 内部 |
| `OnlineMatchController.VerticalSlice.cs` | 515 | `"VerticalSlice Stage1 Meeting player voice seat N"` | Editor 内部 |
| `OnlineMatchController.VerticalSlice.cs` | 573 | `"VerticalSlice Stage1 GameplayAnchor action voice radius N"` | Editor 内部 |
| `VerticalSliceStageOneAnchor.cs` | 61 | "会议阶段镜头，圆桌、语音席位..." | Designer tooltip |
| `VerticalSliceStageOneAnchor.cs` | 79 | "行动语音范围：近距离语音..." | Designer tooltip |

> **性质**: 这些是世界构建器（WorldBuilder）创建的 3D prop 名称和锚点描述，用于美术管线定位。即使语音功能移除，这些锚点仍有其他用途（作为关卡设计参考点、相机焦点等）。建议保留名称但更新 tooltip 描述。

---

## 🟩 D 类：Debug.Log 字符串（不面向玩家）

| 文件 | 行号 | 当前文案 | 说明 |
|------|------|---------|------|
| `VoiceChatSystem.cs` | 329-906 | 约 20 处 `[VoiceChatSystem] ...` | Debug 日志，仅开发环境可见 |

> 建议: 保留原样，不影响玩家体验。可加 TODO 标记后续移除 VoiceChatSystem 时清理。

---

## 🟧 E 类：文档/注释

| 文件 | 行号 | 当前文案 | 建议 |
|------|------|---------|------|
| `OnlineRuleSet.cs` | 119 | "行动阶段是否启用近距离语音。M1 收尾：Vivox 已移除，方案 B（文本聊天）替代" | 已含正确信息，但"近距离语音"一词易误解 → 建议改为"行动阶段是否启用近距离聊天范围标记" |
| `OnlineMatchController.Gameplay.cs` | 584 | `proximityVoiceEnabled = ruleSet.ProximityVoiceEnabled;` | 注释中可加：`// 美术锚点开关，无语音后端` |
| `README.md` | 33 | "Proximity voice (Vivox) has been removed — text chat replaces it" | ✅ 已正确 |

---

## 汇总：优先级排序

| 优先级 | 类别 | 数量 | 影响 |
|--------|------|------|------|
| 🟥 P0 | A1-A6 玩家可见文案 | 9 处 | 直接可见，必须改 |
| 🟨 P1 | B 内部变量名 | 9 处 | 代码可维护性，建议后续批量重命名 |
| 🟩 P2 | C 世界锚点名 | 9 处 | 美术管线保留，tooltip 可更新 |
| 🟩 P2 | D Debug 日志 | 20 处 | 无玩家影响 |
| 🟧 P3 | E 注释 | 3 处 | 降低新人理解成本 |

---

## 执行建议

1. **P0 立即执行**: 修改 `MainMenuController.cs:200`、`OnlineMatchHud.cs:700/1435`、`Localization.cs:94/100`、`TutorialGateway.cs:77`、`SettingsManager.cs:423`、`SettingsData.cs:144/317/43`
2. **P1 后续迭代**: 批量重命名 `VoiceStatus` → `ChatStatus`、`VoiceRoutingEnabled` → `ChatAvailable` 等
3. **P2 按需**: 更新 WorldBuilder anchor tooltip
4. **P3 顺手**: 更新 `OnlineRuleSet.cs:119` 注释

> 注：`OnlineMatchHud.cs:600` 的 "行动阶段近距离聊天" **已正确**（表示文本聊天频道切换），不需要改。
> `OnlineMatchHud.cs:1308` 的 "近距离聊天" / "会议聊天" **已正确**（文本聊天频道名称），不需要改。
