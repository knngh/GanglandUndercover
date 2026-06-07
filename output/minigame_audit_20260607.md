# 阶段 3.2: 任务小游戏体验统一 — 审计修复报告

> **日期**: 2026-06-07

---

## 审计结论: 系统完整，2 处修复

### 系统架构
```
OnlineMiniGameBridge (NetworkBehaviour)
  ├── OpenMinigameOnClient(clientId, taskId)    服务器→客户端: 打开任务
  ├── OpenRepairMinigameOnClient(clientId, taskId) 服务器→客户端: 打开修复
  ├── SubmitTaskResultServerRpc(taskId, success)  客户端→服务器: 提交任务结果
  ├── SubmitRepairResultServerRpc(taskId, success) 客户端→服务器: 提交修复结果
  ├── StartTaskClientRpc → OpenMinigame()         本地: 创建小游戏
  ├── TaskDoneClientRpc → CloseMinigame()         广播: 任务完成
  └── CancelTaskClientRpc → CloseMinigame()       广播: 任务取消
```

### 小游戏清单 (11 种全部存在)
| # | 小游戏 | 文件 |
|---|--------|------|
| 0 | WireTask (接线) | WireTask.cs ✅ |
| 1 | KeypadTask (数字键盘) | KeypadTask.cs ✅ |
| 2 | SwipeCardTask (刷卡) | SwipeCardTask.cs ✅ |
| 3 | ScanTask (扫描) | ScanTask.cs ✅ |
| 4 | DownloadTask (下载) | DownloadTask.cs ✅ |
| 5 | MemoryTask (记忆) | MemoryTask.cs ✅ |
| 6 | SortTask (分类) | SortTask.cs ✅ |
| 7 | TapTask (点击) | TapTask.cs ✅ |
| 8 | AsteroidTask (小行星) | AsteroidTask.cs ✅ |
| 9 | CalibrateTask (校准) | CalibrateTask.cs ✅ |
| 10 | EvidenceArchiveTask (证据档案) | EvidenceArchiveTask.cs ✅ |

---

## 已验证的正常行为

| 检查项 | 状态 | 代码位置 |
|--------|------|---------|
| 打开任务前先关闭旧任务（防重复） | ✅ | L178: `OpenMinigame` → `CloseMinigame()` |
| 任务完成/取消后 Destroy Canvas | ✅ | L201: `Destroy(_currentMinigame)` |
| 取消时取消事件订阅 | ✅ | L197-198: `-= OnMinigameComplete/OnMinigameCancel` |
| ServerRpc 有 IsServer 检查 | ✅ | L34, L117 |
| 所有 RPC 入口有空 controller 检查 | ✅ | L40, L60, L118, L138 |
| 控制器方法全部存在 | ✅ | 6 个方法全部在线 |

---

## 已修复

### FIX #1: `_pendingRepair` 字典泄漏
- 新增 `CleanupPendingRepair(clientId)` — 供外部在玩家断线时调用
- 新增 `OnDestroy()` — 组件销毁时清理字典
- 修复前: 断开连接的客户端条目永久残留
- 修复后: 有明确的清理路径

### FIX #2: `SingleTarget` clientId 校验
- clientId=0 时添加 warning 日志
- 防止误将 host 作为 RPC 目标发送

---

## 结论
小游戏系统代码层面完整可靠。Canvas 生命周期管理正确（打开→销毁），事件订阅正确取消，协议流程完整（单向服务器驱动）。实际体验效果需在 Play Mode 中打一局验证。
