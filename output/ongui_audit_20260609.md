# Gangland Undercover - OnGUI 调用点审计

> 审计日期: 2026-06-09 | `rg "GUI\.|GUILayout\.|OnGUI\("` 快照

## 总览

当前核心运行时代码中约有 423 个 IMGUI 相关调用点，集中在在线对局控制器：

| 文件 | 调用点 | 分类 | 迁移建议 |
|------|--------|------|----------|
| `OnlineMatchController.OnGUI.cs` | 300 | 运行时 UI | P0/P1，会议/投票/房间优先 |
| `OnlineMatchController.Gameplay.cs` | 78 | 小任务/进度绘制 | P2，迁移到 MiniGameBridge/uGUI |
| `ChatSystem.cs` | 35 | 聊天 UI | P1，改 InputField + ScrollView |
| `HostMigrationManager.cs` | 8 | 迁移状态提示 | P3，改 overlay 文本组件 |
| `SocialPrototypeController.cs` | 2 | 旧原型层 | 保留或随旧层清理 |

## 运行时 UI 分布

| 区域 | 现状 | 风险 |
|------|------|------|
| 房间/连接面板 | `OnlineMatchController.OnGUI.cs` 直接绘制 Host/Client/Relay/Ready/Start | 逻辑和 UI 状态混杂，后续联网状态变更容易回归 |
| 行动 HUD | 顶部状态、操作提示、小地图、技能/任务提示仍在 IMGUI | 字体、布局、按钮音效和截图验收难统一 |
| 会议/投票 | 会议证据墙、投票列表、跳过投票和结果 UI 在 IMGUI | 对核心玩法影响高，应优先迁移 |
| 任务小游戏 | `Gameplay.cs` 仍有绘制 helper 和进度条 | 和已存在 MiniGameBridge/uGUI 方向不一致 |
| 聊天 | 逻辑独立，但 `DrawChatPanel` 使用 GUILayout | 适合单独短切片迁移 |
| 主机迁移提示 | 少量 `OnGUI` overlay | 风险低，可晚处理 |

## 推荐迁移顺序

| 优先级 | 切片 | 验证方式 |
|--------|------|----------|
| P0 | 保持现状，先完成真实 Netcode 恶意消息测试 | EditMode + PlayMode |
| P1 | 会议/投票面板 uGUI 化 | PlayMode 全阶段循环 + 截图清单 |
| P1 | ChatSystem uGUI 化 | 聊天频道单测/PlayMode + 手工输入截图 |
| P2 | 行动 HUD 关键状态 uGUI 化 | FullMatchLoop + UI 截图 |
| P2 | 小任务绘制迁移到 MiniGameBridge | `MiniGameOnlineIntegrationPlayTests` |
| P3 | HostMigration overlay | 主机迁移专项测试 |

## uGUI 替代映射

| IMGUI 功能 | uGUI 替代 |
|------------|-----------|
| `GUILayout.Button` | `Button` + `UiButtonSfx` |
| `GUILayout.TextField` | `TMP_InputField` 或 `InputField` |
| `GUILayout.BeginScrollView` | `ScrollRect` + Content |
| `GUI.Label`/`GUILayout.Label` | Text/TextMeshPro |
| `GUI.DrawTexture` 进度条 | `Image.fillAmount` 或 Slider |
| `GUILayout.Toggle` | Toggle |

## 注意

本审计不代表已经完成迁移。后续每次迁移应按“一块 UI + 一个验证场景 + 一个提交”推进，避免把视觉迁移和联网协议变更混在一起。
