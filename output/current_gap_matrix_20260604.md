# 当前差距复核清单 (Current Gap Matrix)

> **项目路径**: `/Users/zhugehao/projects/GanglandUndercover`
> **复核日期**: 2026-06-04
> **来源**: Task 0.4 — M0 基线验证阶段的差距复核

---

## 复核结果总览

| # | 功能模块 | 状态 | 判定依据 |
|---|---------|------|---------|
| 1 | 通风管/暗线系统 | **仅离线可用** | SocialDeduction/VentSystem.cs(365行) 通过回调绑定；无 OnlineVents.cs / UnderworldPassage.cs；Online/ 目录无任何 VentSystem 引用 |
| 2 | 监控系统 | **仅离线可用** | SocialDeduction/SecurityCamera.cs(384行)；无在线版本；Online/ 目录无 SecurityCamera 引用 |
| 3 | 紧急任务 | **缺失** | EmergencyButton.cs 仅 20 行，只有 TextMesh 标签渲染，无任何任务系统逻辑；未找到 UrgentTask 相关文件 |
| 4 | 小游戏 | **仅离线可用** | 13 个离线小游戏文件；Online/MiniGames/ 目录不存在，无任何小游戏文件 |
| 5 | 鬼魂状态 | **已联机可用** | Gameplay/GhostMode.cs(228行)；OnlineMatchController.cs 中 3 处调用 EnterGhostMode()（淘汰 L3608、投票驱逐 L4044、辅助方法 L4130-L4154） |
| 6 | 语音系统 | **服务阻塞** | UnityServiceBootstrap.cs 中 Vivox 已移除(VivoxReady→false, VoiceStatus="Vivox 未安装（已移除）")；VoiceChatSystem.cs(1081行) 无 NetworkBehaviour/RPC/网络传输，为纯本地独立实现 |
| 7 | Relay房间 | **服务阻塞** | UnityServiceBootstrap.cs 仅检查 RelayService.Instance 存在性；全项目 Scripts/ 中无 CreateRelay/JoinRelay 实际调用代码 |
| 8 | Host Migration | **代码存在未验证** | HostMigrationManager.cs(453行) 实现完整(心跳/选举/快照/恢复)，依赖 OnlineMatchController.CaptureSnapshot/RestoreFromSnapshot/ForceGameOver，待联调验证 |
| 9 | OnlineMatchController 行数 | **13,101 行** | 含 2 个 partial 文件：OnlineMatchController.CharacterAdapters.cs(6.3KB) + OnlineMatchController.VerticalSlice.cs(45.4KB) |
| 10 | SabotageSync 反射检查 | **无反射使用** | 全文搜索未发现 System.Reflection / GetField / GetProperty / BindingFlags；SabotageSync.cs(352行) 使用 FindAnyObjectByType + 公共 API 接入 |

---

## 逐项详细复核

### 1. 通风管/暗线系统 — 仅离线可用

| 维度 | 详情 |
|------|------|
| **离线实现** | `SocialDeduction/VentSystem.cs` — 365行，通过 `Action<Vector3>` / `Func<bool>` 回调绑定，支持节点图导航、冷却、过渡动画 |
| **在线实现** | **不存在**。未找到 `OnlineVents.cs`、`UnderworldPassage.cs` |
| **是否重复** | 无重复。仅此一套离线实现 |
| **Online/ 引用** | 无。`fs_search_content` 搜索 Online/ 目录下 `VentSystem` 返回 0 结果 |
| **接入路径** | 需要新增 Netcode `NetworkBehaviour` 封装，将 VentSystem 的 `Bind()` 回调替换为 RPC 同步 |

### 2. 监控系统 — 仅离线可用

| 维度 | 详情 |
|------|------|
| **离线实现** | `SocialDeduction/SecurityCamera.cs` — 384行，4 路摄像头 + 监控站，锥形检测、Impostor 红灯、视角切换 |
| **在线实现** | **不存在** |
| **Online/ 引用** | 无。`fs_search_content` 搜索 Online/ 目录下 `SecurityCamera` 返回 0 结果 |

### 3. 紧急任务 — 缺失

| 维度 | 详情 |
|------|------|
| **现有代码** | `SocialDeduction/EmergencyButton.cs` — 仅 20 行，只有 `Awake()` 中设置 TextMesh 标签 "紧急会议\nE"，无任何任务系统逻辑 |
| **任务系统** | 未找到 `UrgentTask`、`EmergencyTask` 相关文件 |
| **结论** | 紧急任务系统完全缺失，EmergencyButton 仅为 UI 占位符 |

### 4. 小游戏 — 仅离线可用

**离线小游戏清单 (SocialDeduction/MiniGames/)**:

| 文件 | 大小 | 修改日期 |
|------|------|---------|
| MiniGameBase.cs | 1.3 KB | 2026-06-02 |
| MiniGameType.cs | 690 B | 2026-06-03 |
| WireTask.cs | 9.2 KB | 2026-06-02 |
| MemoryTask.cs | 10.5 KB | 2026-06-02 |
| SwipeCardTask.cs | 8.5 KB | 2026-06-02 |
| KeypadTask.cs | 11.7 KB | 2026-06-02 |
| SortTask.cs | 14.0 KB | 2026-06-02 |
| ScanTask.cs | 10.6 KB | 2026-06-02 |
| TapTask.cs | 10.8 KB | 2026-06-02 |
| AsteroidTask.cs | 11.9 KB | 2026-06-03 |
| CalibrateTask.cs | 9.8 KB | 2026-06-03 |
| DownloadTask.cs | 12.4 KB | 2026-06-03 |
| EvidenceArchiveTask.cs | 16.2 KB | 2026-06-04 |

**在线小游戏**: `Online/MiniGames/` 目录不存在，无任何在线小游戏文件。13 个小游戏全部仅离线可用。

### 5. 鬼魂状态 — 已联机可用

| 维度 | 详情 |
|------|------|
| **离线实现** | `Gameplay/GhostMode.cs` — 228行，半透明渲染、碰撞器 trigger、飞行移动、光环效果 |
| **在线集成点** | `OnlineMatchController.cs` 中 3 处调用： |
|   | L3608: 淘汰时 `ActivateGhostModeForLocalPlayer(victimClientId)` |
|   | L4044: 投票驱逐时 `ActivateGhostModeForLocalPlayer(ejectedClientId)` |
|   | L4130-L4154: 辅助方法，通过 `SocialCharacter.GetComponent<GhostMode>()` 或 `AddComponent<GhostMode>()` 激活 |
| **判定** | GhostMode 组件在联机流程中被正确引用和激活，联机可用 |

### 6. 语音系统 — 服务阻塞

| 维度 | 详情 |
|------|------|
| **Vivox 状态** | `UnityServiceBootstrap.cs` 中 Vivox 已完全移除：`VivoxReady => false`，所有方法返回 stub（`EnsureVivoxLoggedInAsync`→false, `JoinVoiceChannelAsync`→false），`VoiceStatus = "Vivox 未安装（已移除）"` |
| **VoiceChatSystem** | `SocialDeduction/VoiceChatSystem.cs` — 1081行，实现了 VAD、音频滤波器配置、PTT 输入、Proximity/Global/Whisper 通道概念，但： |
|   | - 无 `NetworkBehaviour` 继承 |
|   | - 无 `[ServerRpc]` / `[ClientRpc]` |
|   | - 无 `NetworkVariable` |
|   | - 无实际音频网络传输层 |
| **Online/ 引用** | 无。Online/ 目录中未引用 VoiceChatSystem |
| **阻塞原因** | Vivox SDK 已移除，VoiceChatSystem 是纯本地实现，缺少音频网络传输后端 |

### 7. Relay 房间 — 服务阻塞

| 维度 | 详情 |
|------|------|
| **Bootstrap 层** | `UnityServiceBootstrap.cs` 检查 `RelayService.Instance != null` 并设置 `relayReady` 标志 |
| **实际使用** | 全项目 `Assets/_Project/Scripts/` 中搜索 `RelayService`、`Relay.Instance`、`CreateRelay`、`JoinRelay` 均返回 0 结果 |
| **阻塞原因** | Relay 服务 SDK 已安装但无任何 Relay 房间创建/加入的业务代码 |

### 8. Host Migration — 代码存在未验证

| 维度 | 详情 |
|------|------|
| **实现文件** | `Online/HostMigrationManager.cs` — 453行，`[RequireComponent(typeof(OnlineMatchController))]` |
| **实现内容** | 心跳协议（2s 间隔 / 5s 超时）、新主机选举（最小 clientId）、快照捕获/恢复、迁移广播、OnGUI 迁移 UI 提示、断连检测 |
| **依赖项** | `OnlineMatchController.CaptureSnapshot()`、`RestoreFromSnapshot()`、`ForceGameOver()`、`IsHost`、`LocalClientIdValue` |
| **未验证原因** | 代码结构完整但依赖 OnlineMatchController 的 snapshot 方法（可能尚未实现），且缺少实际联机测试验证 |

### 9. OnlineMatchController.cs 行数统计

| 文件 | 行数 |
|------|------|
| OnlineMatchController.cs（主文件） | **13,101** |
| OnlineMatchController.CharacterAdapters.cs | ~6.3 KB |
| OnlineMatchController.VerticalSlice.cs | ~45.4 KB |
| **总计（主文件）** | **13,101 行** |

### 10. SabotageSync.cs 反射检查

| 维度 | 详情 |
|------|------|
| **反射使用** | **未发现**。全文搜索 `System.Reflection` / `GetField` / `GetProperty` / `BindingFlags` 返回 0 结果 |
| **接入方式** | 通过 `FindAnyObjectByType<OnlineMatchController>()` 获取控制器引用，使用公共 API 访问 sabotage 计时器和状态 |
| **结论** | 无反射读取私有字段，代码合规 |

---

## 差距统计

| 状态 | 数量 | 功能 |
|------|------|------|
| 已联机可用 | 1 | 鬼魂状态 |
| 仅离线可用 | 3 | 通风管系统、监控系统、小游戏(13个) |
| 缺失 | 1 | 紧急任务系统 |
| 服务阻塞 | 2 | 语音系统(Vivox移除)、Relay房间(无业务代码) |
| 代码存在未验证 | 1 | Host Migration |
| 信息统计 | 2 | OnlineMatchController行数(13,101)、SabotageSync无反射 |

---

## 关键风险提示

1. **语音系统是 M1 联机核心的硬阻塞**：Vivox 已移除且无替代方案，VoiceChatSystem 为纯本地实现，联机语音需要完整的 SDK 集成或 WebRTC 方案。
2. **Relay 房间与 Lobby 系统零业务代码**：Bootstrap 仅校验服务可用性，M1 联机需要从零实现房间创建/加入逻辑。
3. **OnlineMatchController 超重**：13,101 行主文件远超合理范围，在 M1 瘦身阶段需要拆分。
4. **13 个小游戏全离线**：M1 联机核心需要至少接入关键小游戏（WireTask/KeypadTask 等）的联机同步。
