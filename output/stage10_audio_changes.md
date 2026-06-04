# Stage 10 音效系统 — 变更说明

## 概述

创建完整音效系统（AudioManager 单例 + 全部接入点），对标 Among Us 音频体系。所有 AudioClip 字段均为 Inspector 占位符，无实际音频资源可留空，运行时静默跳过。

---

## 新增文件

### `/Assets/_Project/Scripts/Audio/AudioManager.cs`

**核心架构：**

| 特性 | 实现 |
|---|---|
| 生命周期 | MonoBehaviour 单例，`DontDestroyOnLoad` |
| SoundEffect 枚举 | 13 项：UIClick / Footstep / Kill / BodyReport / MeetingStart / VoteCast / PlayerEliminated / TaskComplete / Sabotage / Victory / Defeat / Emergency / Ambient |
| 音频源 | 3 个 AudioSource（sfx / music / ambient）自动初始化 |
| 音量控制 | MasterVolume / SFXVolume / MusicVolume（[0,1] float 属性，Clamp 保护） |
| 2D 播放 | `PlaySFX(SoundEffect)` → sfxSource.PlayOneShot |
| 3D 空间化 | `PlaySFXAtPoint(SoundEffect, Vector3)` → AudioSource.PlayClipAtPoint |
| 环境音 | Awake 时自动循环 ambient clip（有赋值时） |
| 空安全 | 所有 null clip 静默跳过，使用 `?.` 调用 Instance |

**Inspector 面板：**
- 13 个 [SerializeField] AudioClip 占位字段（Header: "Sound Effects"）
- 3 个 [Range(0,1)] 音量滑块（Header: "Volume"）

---

## 修改文件

### 1. `SocialPrototypeController.cs`

| 接入点 | 位置 | 音频 | 触发条件 |
|---|---|---|---|
| `MovePlayer()` | Update 循环 | `Footstep`（空间化） | 移动时每 0.4s 播放一次 |
| `ResolveEvidenceChallenge()` | 任务完成分支 | `TaskComplete` | `task.IsCompleted == true` |
| `ResolveSabotageChallenge()` | 破坏应用 | `Sabotage` | 触发 Blackout + sabotage |
| `TryReportBody()` | 尸体报告 | `BodyReport` | 成功找到尸体并报告 |
| `KillCharacter()` | 角色击杀 | `Kill` | 任何人被击杀时 |

新增字段：`private float audioFootstepTimer`（0.4s 间隔控制）

新增常量：`private const float AudioFootstepIntervalSeconds = 0.4f`

新增引用：`using GanglandUndercover.Audio;`

### 2. `GameController.cs`（Gameplay）

| 接入点 | 方法 | 音频 | 说明 |
|---|---|---|---|
| 阵营会议触发 | `RunPlayerAction()` | `MeetingStart` | `ShouldHoldMeeting = true` 时 |
| AI 投票淘汰 | `RunMeeting()` | `VoteCast` + `PlayerEliminated` | `EliminateFaction()` 调用后 |
| 玩家投票淘汰 | `PlayerCastVote()` | `VoteCast` + `PlayerEliminated` | 玩家票直接淘汰 |
| 紧急会议 | `ForceMeeting()` | `Emergency` | 强制召集会议 |

新增引用：`using GanglandUndercover.Audio;`

### 3. `GameOverController.cs`

| 接入点 | 方法 | 音频 | 说明 |
|---|---|---|---|
| 结算面板 | `Show()` | `Victory` / `Defeat` | 根据 `State.Result` 字符串 + 玩家角色判定 |
| 返回主菜单 | `OnBackToMenu()` | `UIClick` | 按钮回调 |
| 再来一局 | `OnReplay()` | `UIClick` | 按钮回调 |

新增引用：`using GanglandUndercover.Audio;` `using GanglandUndercover.Core;`

### 4. `OnlineMatchController.cs`

| 接入点 | 方法 | 音频 | 与现有 PlayCue 关系 |
|---|---|---|---|
| 击杀 | `TryKill()` | `Kill` | 在 `PlayCue("kill")` 之后并行调用 |
| 玩家报告尸体 | `TryReportOrEmergency()` | `BodyReport` | 首次发现尸体时 |
| Bot 报告尸体 | bot 思考循环 | `BodyReport` | AI 发现尸体时 |
| 破坏任务 | 任务互动逻辑 | `Sabotage` | `ApplySabotageEffect()` 之后 |

新增引用：`using GanglandUndercover.Audio;`

### 5. `MainMenuController.cs`

| 接入点 | 方法 | 音频 |
|---|---|---|
| 角色选择 | `OnRoleSelected()` | `UIClick` |
| 开始离线 | `OnStartOffline()` | `UIClick` |
| 进入联机 | `OnEnterLobby()` | `UIClick` |

新增引用：`using GanglandUndercover.Audio;`

### 6. `LobbyController.cs`

| 接入点 | 方法 | 音频 |
|---|---|---|
| 创建房间 | `OnCreateRoom()` | `UIClick` |
| 加入房间 | `OnJoinRoom()` | `UIClick` |
| 开始游戏 | `OnStartOnlineGame()` | `UIClick` |
| 返回菜单 | `OnBackToMenu()` | `UIClick` |

新增引用：`using GanglandUndercover.Audio;`

---

## Among Us 对标分析

| Among Us 音效 | 本项目映射 | 状态 |
|---|---|---|
| UI Click | `UIClick` | ✅ 覆盖所有菜单按钮 |
| Footstep | `Footstep`（0.4s 空间化） | ✅ 对标 0.4s 间隔 |
| Kill | `Kill` | ✅ 离线 + 联机双路径 |
| Body Reported | `BodyReport` | ✅ 玩家 + Bot 报告 |
| Meeting Start | `MeetingStart` | ✅ 离线 + 联机 |
| Vote Cast | `VoteCast` | ✅ 投票后 |
| Player Ejected | `PlayerEliminated` | ✅ 淘汰后 |
| Task Complete | `TaskComplete` | ✅ 任务进度满 |
| Sabotage | `Sabotage` | ✅ 离线 + 联机双路径 |
| Victory | `Victory` | ✅ 结算面板判定 |
| Defeat | `Defeat` | ✅ 结算面板判定 |
| Emergency Button | `Emergency` | ✅ 紧急会议 |
| Ambient Loop | `Ambient` | ✅ Awake 自动循环 |

差异项会合：全部 13 项 SoundEffect 已完成接入。

---

## 待后续阶段操作

- **Stage 11**：实际音频资源导入 → Inspector 拖入 AudioManager 的 13 个 Clip 槽位
- **Stage 11**：AudioMixer 组创建（可选的更精细音量控制，当前已覆盖 Master/SFX/Music 三层）
- 现阶段无需音频资源即可运行，所有 PlaySFX 调用在 clip 为空时静默跳过，不影响游戏逻辑