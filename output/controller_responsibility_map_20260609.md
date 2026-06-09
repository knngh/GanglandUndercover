# Gangland Undercover - OnlineMatchController 职责地图

> 审计日期: 2026-06-09 | 当前事实快照，不改代码

## 总览

`OnlineMatchController` 当前由 1 个主文件和 12 个 partial 文件组成：

| 范围 | 行数 |
|------|------|
| 主文件 `OnlineMatchController.cs` | 3519 |
| 12 个 partial 文件合计 | 8533 |
| 控制器合计 | 12052 |

结论：控制器仍承担核心编排、运行时 UI、地图/视觉/任务辅助、联机协议等多类职责。下阶段应优先减少高变动 UI 和世界构建逻辑对控制器的直接依赖，避免先拆网络层。

## 文件职责矩阵

| 文件 | 行数 | 主要职责 | 内聚度 | 建议 |
|------|------|----------|--------|------|
| `OnlineMatchController.cs` | 3519 | 主循环、阶段推进、角色分发、投票、胜负、音频/提示桥接 | 中 | 保留核心编排，后续只抽离明确子系统 |
| `.Network.cs` | 1786 | NGO 自定义消息注册、收发、序列化、防御性校验 | 中高 | 暂不拆；先补真实 Netcode 路径恶意消息测试 |
| `.OnGUI.cs` | 1787 | 房间、行动 HUD、会议、结果等运行时 IMGUI | 低 | P0/P1 迁移候选，优先会议/投票/房间 UI |
| `.Gameplay.cs` | 2324 | 任务交互、小任务绘制、进度条、运行时辅助 | 中低 | 按 Task UI、任务服务桥接、纯 helper 再拆 |
| `.Visuals.cs` | 890 | 角色、尸体、道具、VFX 2D 显示 | 中 | 可抽 `OnlineVisualService` |
| `.VerticalSlice.cs` | 615 | 地图构建和竖切演示摆放 | 中 | 长期移入 `OnlineWorldBuilder`/布局服务 |
| `.Abilities.cs` | 249 | 职业能力运行时 | 高 | 保持 |
| `.CharacterAdapters.cs` | 225 | 角色组件适配 | 中 | 保持，后续只补测试 |
| `.Identity.cs` | 180 | 卧底/内鬼身份机制 | 高 | 保持 |
| `.CriticalTasks.cs` | 152 | 危机/破坏任务逻辑 | 中 | 可并入任务/破坏服务 |
| `.Evidence.cs` | 115 | 证据链和指证桥接 | 高 | 保持 |
| `.Underworld.cs` | 110 | 暗线/通道 | 中 | 保持 |
| `.World.cs` | 110 | 世界配置入口 | 中 | 后续和 WorldBuilder 边界统一 |

## 已解耦或半解耦模块

| 模块 | 行数 | 状态 | 备注 |
|------|------|------|------|
| `KillSystem.cs` | 800 | 已独立 | 击杀、尸体、报告冷却 |
| `OnlineTaskService.cs` | 935 | 已独立 | 任务进度、证据、破坏计时 |
| `HostMigrationManager.cs` | 487 | 已独立 | 主机迁移/心跳，仍有少量 OnGUI |
| `EvidenceChain.cs` | 214 | 已独立 | 证据节点和关系 |
| `EvidenceDossier.cs` | 98 | 已独立 | 会议证据摘要 |
| `OnlineMapService.cs` | 461 | 已独立 | 地图位置/缩放 |
| `OnlineBotController.cs` | 801 | 已独立 | Bot 决策、寻路、能力 |
| `ChatSystem.cs` | 408 | 半独立 | 聊天逻辑独立，但 UI 仍用 IMGUI |
| `OnlineWorldBuilder.cs` | 5839 | 独立但过大 | 资源化地图构建主承载，需另做职责地图 |

## 重构优先级

| 优先级 | 工作 | 理由 |
|--------|------|------|
| P0 | 补真实 Netcode 路径恶意消息 PlayMode 测试 | `.Network.cs` 已加边界校验，但还需要跑真实自定义消息路径 |
| P1 | 会议/投票 UI 从 `.OnGUI.cs` 抽成 uGUI 面板 | 用户最常见高压流程，当前集中在大文件 |
| P1 | ChatSystem 改 InputField + ScrollView | 可切掉独立模块内 35 个 IMGUI 调用点 |
| P2 | Gameplay 小任务绘制迁移到 MiniGameBridge/uGUI | 降低 `.Gameplay.cs` 体积和 UI 耦合 |
| P2 | Visuals 抽服务 | 降低控制器场景对象创建职责 |
| P3 | `OnlineWorldBuilder` 单独拆分布局、装饰、资源选择 | 体量已超过控制器 partial，应另起切片 |

## 禁止项

- 不在没有回归测试的情况下重写 `OnlineMatchController.cs` 核心编排。
- 不把 `.Network.cs` 和 UI/世界构建重构混在同一提交。
- 不删除资源或迁移大目录，除非先有清单、引用检查和可回滚提交。
