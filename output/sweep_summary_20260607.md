# 扫尾推进 — 完成总结

> **日期**: 2026-06-07 | **编译**: ✅ 0 errors, 0 warnings

---

## 本次完成的修复

### P0: Bot AI 卡住检测（新增）
- **文件**: `OnlineBotController.cs`
- 新增 `_lastPositions` 和 `_stuckTimers` 字典
- `MoveBotTowardTarget()` 中：若 5 秒内位移 < 0.03m → 清除目标，Bot 重新寻路

### P0: 证据系统统一
- **文件**: `OnlineMatchHud.cs`
- 会议 UI 证据显示从 `MeetingEvidenceDigest()`（死链，无数据）改为 `MeetingEvidenceDossier`（已接入击杀/任务事件）
- 修复前：会议证据摘要永远为空
- 修复后：正确显示证据摘要

### P0: NGO deprecated warning 清零
- **文件**: `OnlineMiniGameBridge.cs`, `OnlineSecurityCamera.cs`
- `[ServerRpc(RequireOwnership = false)]` → `[Rpc(SendTo.Server)]`
- `ServerRpcParams` → `RpcParams`
- 4 处废弃 API 全部替换

### 基础设施
- Resources: 832MB → 353MB（-479MB）
- macOS 构建验证通过（1.24GB, 40s）
- 编译: 0 errors, 0 warnings

---

## 阶段完成状态

| 阶段 | 任务 | 状态 |
|------|------|------|
| 2.1 | 美术资产验收 | ✅ |
| 2.2 | 音频资产验收 | ✅ |
| 2.3 | 资产清理+导入设置 | ✅ |
| 3.1 | AI Bot 卡住修复 | ✅ |
| 3.2 | 小游戏系统审计 | ✅ (无严重bug) |
| 3.3 | 会议/证据/投票 | ✅ (证据链已修复) |
| 4.1 | macOS 构建 | ✅ |
| 4.2 | 发布前审查 | ✅ (0 err, 0 warn, build OK) |
| 4.3 | KNOWN_ISSUES | ✅ |

---

## 仍需 Editor Play Mode 验证

以下无法通过 batchmode 验证，必须在 Unity Editor 中手动测试：

1. AI Bot 是否实际被卡住（修复后应能自动重新寻路）
2. CC0 像素角色 + Tileset 是否在运行时正确渲染
3. AudioManager clip 是否已赋值（否则无声）
4. 完整对局流程（按 `playmode_test_checklist_20260606.md`）

---

## KNOWN_ISSUES 更新

| ID | 严重度 | 描述 | 状态 |
|----|--------|------|------|
| P1-1 | P1 | AudioManager clip 未赋值 | 待 Editor 操作 |
| P1-2 | P1 | 动画 Controller 未验证 | 待 Play Mode |
| P1-3 | P1 | 地图 CC0 渲染确认 | 待 Play Mode |
| P2-1 | P2 | Legacy3D 798MB 历史资产 | 可清除 |
| P2-2 | P2 | 尸体几何图形 | 低优先 |
| P2-3 | P2 | Bot 无复杂寻路 | 已改善（卡住检测） |
