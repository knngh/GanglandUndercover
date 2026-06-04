---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9470d846c7eff9b24afb94a99a2cb3f0_d01837745f1d11f18d42525400d9a7a1
    ReservedCode1: eXuEOCi1sUY4mMqPADMgWcf2GUmSn3rtwtBJYb1+DqfgEnvMkUOwkDZUFrw+RD0Cc/Ab+TW5i7Upndpo3Up4E4S/KUzEN2qxzjSKvQ5CnFw5GPOGyIf4FB1ov/9z+DjaX3JXQuhu4o1/DFzmc87d41GdEFmIAIevPx+zb/rhUqWZt/CL8nP9wlqFM5M=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9470d846c7eff9b24afb94a99a2cb3f0_d01837745f1d11f18d42525400d9a7a1
    ReservedCode2: eXuEOCi1sUY4mMqPADMgWcf2GUmSn3rtwtBJYb1+DqfgEnvMkUOwkDZUFrw+RD0Cc/Ab+TW5i7Upndpo3Up4E4S/KUzEN2qxzjSKvQ5CnFw5GPOGyIf4FB1ov/9z+DjaX3JXQuhu4o1/DFzmc87d41GdEFmIAIevPx+zb/rhUqWZt/CL8nP9wlqFM5M=
---

# Stage 15 — 编译错误全面修复

> 日期：2026-06-03 | 项目：GanglandUndercover

## 概述

对 `Assets/_Project/Scripts` 下所有 .cs 文件进行系统扫描和修复，解决编译错误。修复策略：先处理已知问题（备份文件、枚举缺失），再按模块逐类修复命名空间引用问题。

---

## 1. 已删除文件

| 文件 | 原因 |
|------|------|
| `Online/KillSystem_20260602_150052_224.cs` | write_file 自动重命名残留备份，与 KillSystem.cs 重复定义 |

---

## 2. 枚举成员补充

### 2.1 MiniGameType.cs

**文件**：`SocialDeduction/MiniGames/MiniGameType.cs`

新增枚举成员：

```csharp
EvidenceArchiveTask
```

### 2.2 PoliceStationTasks.cs

**文件**：`SocialDeduction/PoliceStationTasks.cs`

将"证据归档"任务映射从 `WireTask` 改为 `EvidenceArchiveTask`：

```csharp
{ "证据归档", MiniGameType.EvidenceArchiveTask }
```

### 2.3 SocialPrototypeController.cs

**文件**：`SocialDeduction/SocialPrototypeController.cs`

`PickMiniGameType()` switch 新增 case：

```csharp
case MiniGameType.EvidenceArchiveTask:
    return typeof(EvidenceArchiveTask);
```

---

## 3. 缺失 using 语句修复

通过 Python 脚本构建项目类型名→命名空间映射数据库，检测跨命名空间类型引用而缺少对应 `using` 的情况。

### 修复清单

| # | 文件 | 缺失的 using | 引用的类型 |
|---|------|------------|----------|
| 1 | `UI/GameOverController.cs` | `GanglandUndercover.Core` | `Faction` |
| 2 | `UI/ThemeManager.cs` | `GanglandUndercover.Core` | `Faction`, `MiniGameType` |
| 3 | `UI/ThemeManager.cs` | `GanglandUndercover.SocialDeduction` | `SocialRole` |
| 4 | `Online/KillSystem.cs` | `GanglandUndercover.Audio` | `SoundEffect` |
| 5 | `Online/OnlineMatchController.cs` | `GanglandUndercover.Core` | `Faction` 等 Core 类型 |
| 6 | `Online/OnlineMatchController.cs` | `GanglandUndercover.SocialDeduction` | `SocialCharacter` 等 |
| 7 | `Online/OnlineMatchController.cs` | `GanglandUndercover` | 根命名空间 `SabotageType` |
| 8 | `Online/OnlineMatchController.CharacterAdapters.cs` | `GanglandUndercover.SocialDeduction` | `OnlinePlayerState` 等 |
| 9 | `Online/OnlineSyncManager.cs` | `GanglandUndercover` | `SabotageType` |
| 10 | `Online/SabotagePanel.cs` | `GanglandUndercover` | `SabotageType` |
| 11 | `Online/SabotageSync.cs` | `GanglandUndercover` | `SabotageType` |
| 12 | `Online/TaskSync.cs` | `GanglandUndercover` | 根命名空间类型 |
| 13 | `SocialDeduction/MiniGames/DownloadTask.cs` | `GanglandUndercover.Audio` | `SoundEffect` |
| 14 | `SocialDeduction/MiniGames/EvidenceArchiveTask.cs` | `GanglandUndercover.SocialDeduction` | `PoliceStationTasks` |

---

## 4. 全面验证

修复后执行以下验证步骤，所有通过：

| 检查项 | 结果 |
|--------|------|
| 大括号平衡（所有 .cs 文件） | 全部平衡 |
| 块注释闭合 | 全部闭合 |
| `#if`/`#endif` 配对 | 全部配对 |
| 重复方法定义（partial 类跨文件） | 无重复 |
| `new MonoBehaviour` 实例化（Unity 禁止） | 未发现 |
| `await` 缺少 `async` | 未发现 |
| UnityEditor 类型在非 Editor 目录 | 已用 `#if UNITY_EDITOR` 保护 |
| 根命名空间类型引用缺少 `using GanglandUndercover` | 全部已修复 |
| 子命名空间跨引用（Core/SocialDeduction/Audio） | 全部已修复 |

---

## 5. 修改文件汇总

| 文件 | 操作 | 修改内容 |
|------|------|----------|
| `Online/KillSystem_20260602_150052_224.cs` | **删除** | 备份文件 |
| `SocialDeduction/MiniGames/MiniGameType.cs` | 编辑 | 添加 `EvidenceArchiveTask` |
| `SocialDeduction/PoliceStationTasks.cs` | 编辑 | 更新任务映射 |
| `SocialDeduction/SocialPrototypeController.cs` | 编辑 | switch 新增 case |
| `UI/GameOverController.cs` | 编辑 | 添加 `using GanglandUndercover.Core` |
| `UI/ThemeManager.cs` | 编辑 ×2 | 添加 2 个 using |
| `Online/KillSystem.cs` | 编辑 | 添加 `using GanglandUndercover.Audio` |
| `Online/OnlineMatchController.cs` | 编辑 ×2 | 添加 2 个 using（Core + 根） |
| `Online/OnlineMatchController.CharacterAdapters.cs` | 编辑 | 添加 `using GanglandUndercover.SocialDeduction` |
| `Online/OnlineSyncManager.cs` | 编辑 | 添加 `using GanglandUndercover` |
| `Online/SabotagePanel.cs` | 编辑 | 添加 `using GanglandUndercover` |
| `Online/SabotageSync.cs` | 编辑 | 添加 `using GanglandUndercover` |
| `Online/TaskSync.cs` | 编辑 | 添加 `using GanglandUndercover` |
| `SocialDeduction/MiniGames/DownloadTask.cs` | 编辑 | 添加 `using GanglandUndercover.Audio` |
| `SocialDeduction/MiniGames/EvidenceArchiveTask.cs` | 编辑 | 添加 `using GanglandUndercover.SocialDeduction` |

**总计**：删除 1 个文件，编辑 14 个文件（17 处修改）。

---

## 6. 架构改进

所有修复遵循项目现有架构约定：

- **命名空间层次**：`GanglandUndercover` → `.Core` / `.SocialDeduction` / `.Online` / `.Audio` / `.Gameplay` / `.UI` / `.World`
- **文件名规范**：与命名空间对应，Editor 脚本使用 `#if UNITY_EDITOR` 保护
- **Enum 定义位置**：`Faction` 在 `GanglandUndercover.Core`，`SabotageType` 在 `GanglandUndercover`（根），`MiniGameType` 在 `GanglandUndercover.SocialDeduction.MiniGames`
- **警察局专属类型**：`EvidenceItem`、`CaseSlot`、`Area` 等为 sealed class，定义在 `PoliceStationTasks.cs` 和 `PoliceStationMap.cs` 中
*（内容由AI生成，仅供参考）*
