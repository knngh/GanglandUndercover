# Gangland Undercover — 陈旧注释/文档扫描

> 日期: 2026-06-09 | 只做 doc-only 修正

---

## 1. NetworkPrefab 旧描述

| 文件:行 | 旧注释 | 状态 | 建议 |
|---------|--------|------|------|
| Visuals.cs:810 | "导致 globalObjectIdHash=0、远端 'NetworkPrefab could not be found'" | ⚠️ 半陈旧 | 问题仍存在但原因已定位，注释无需改 |
| Network.cs:195 | "[MiniGameBridge] NetworkPrefab template not registered!" | ⚠️ 半陈旧 | Debug.LogError 仍触发，代码逻辑未变 |

---

## 2. Legacy 引用

| 文件:行 | 旧描述 | 状态 | 建议 |
|---------|--------|------|------|
| KillSystem.cs:564 | `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` | 🟡 旧API | 建议改为 Resources.Load("Fonts/KenneyFuture") |
| CanvasMapView.cs:505 | 同上 | 🟡 旧API | 同上 |
| OnlineMatchHud.cs:2440 | `TryLoadBuiltinFont("LegacyRuntime.ttf")` | 🟡 同上 | 同上 |
| WorldBuilder.cs:2359 | "Public entry for building the legacy ship map" | 🟢 准确 | "Legacy" 在此处是有意命名的旧地图系统 |

---

## 3. Relay 状态文本（硬编码）

| 文件:行 | 文本 | 状态 |
|---------|------|------|
| OnlineMatchController.cs:131 | `relayStatus = "Relay 房间码未创建。"` | ⚠️ 应使用 Localization |
| Gameplay.cs:2072 | `? "Relay 房间码未创建。"` | ⚠️ 应使用 Localization |
| Network.cs:480 | `reason = "Relay 未就绪："` | ⚠️ 应使用 Localization |

---

## 4. [PLACEHOLDER] 标记

| 文件 | 标记 | 说明 |
|------|------|------|
| MatchStatsCollector.cs:300-301 | `// [PLACEHOLDER]` | 会议/击杀统计未接入实际值 |

---

## 5. 输出文档需要更新的旧描述

| 文件 | 旧描述 | 修正 | 状态 |
|------|--------|------|------|
| output/KNOWN_ISSUES.md | "Resources 目录体积 832MB" | → 104MB | 待更新 |
| output/KNOWN_ISSUES.md | "AudioManager AudioClip 槽位未赋值" | → 已修复 (Resources fallback) | 待更新 |
| output/project_status.md:158 | "Vivox 语音未配置（文档标注为降级状态）" | → "Vivox 已移除，文本聊天替代" | 待更新 |
| output/project_status.md:183 | "Vivox 语音配置：完成 Unity Dashboard Vivox 生产配置" | → 删除（Vivox 已移除） | 待更新 |
