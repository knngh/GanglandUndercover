# Gangland Undercover — 手工 QA 截图清单

> 日期: 2026-06-09 v2 | 版本: v0.2.0-dev
> 工具: `Gangland > Screenshots > Capture Demo Shots` (Editor PlayMode 内)

---

## 目标截图清单

| # | 截图内容 | 操作路径 | 预期尺寸 | 文件名建议 |
|---|---------|---------|---------|-----------|
| 1 | **主菜单设置入口** | PlayMode → 主菜单 → 观察右下"打开设置"按钮和设置状态行 | 1920×1080 | `20260609_mainmenu_setting_entry.png` |
| 2 | **设置覆盖层** | 主菜单 → 点击"打开设置" → 半透明覆盖层弹出 | 1920×1080 | `20260609_setting_overlay.png` |
| 3 | **登录区（匿名登录）** | 主菜单 → 联机面板右上 → "匿 名 登 录"按钮 + 登录状态行 | 1920×1080 | `20260609_login_anonymous.png` |
| 4 | **登录区（已登录状态）** | 点击"匿 名 登 录"→ 状态变为"匿名账号已就绪 | PlayerId ..." | 1920×1080 | `20260609_login_ready.png` |
| 5 | **HUD 行动快捷区 - 无消息** | 开局 → 行动阶段 → 无聊天消息时，"举报最近"/"屏蔽最近"灰色禁用 | 1920×1080 | `20260609_hud_report_disabled.png` |
| 6 | **HUD 行动快捷区 - 有消息** | 发送一条消息 → "举报最近"/"屏蔽最近"变亮可点击 | 1920×1080 | `20260609_hud_report_enabled.png` |
| 7 | **举报后状态** | 点击"举报最近" → 反馈显示 | 1920×1080 | `20260609_hud_report_done.png` |
| 8 | **屏蔽后状态** | 点击"屏蔽最近" → 反馈显示 + 状态行"已屏蔽 N" | 1920×1080 | `20260609_hud_block_done.png` |

---

## 操作指南

### 工具
已创建 `Assets/_Project/Editor/ScreenshotTool.cs`，提供 3 个菜单项：
| 菜单 | 功能 |
|------|------|
| `Gangland > Screenshots > Capture Demo Shots` | 截取当前屏幕（1080p） |
| `Gangland > Screenshots > Capture Current Screen (4K)` | 截取当前屏幕（4K 超采样） |
| `Gangland > Screenshots > Open Screenshots Folder` | 打开 Screenshots/ 目录 |

### 步骤

1. **Unity Editor 中打开 Stage1VerticalSlice 场景**
2. **点击 Play 按钮进入 PlayMode**
3. **截图 #1-#2**：停留在主菜单，先截取 #1，再点"打开设置"截取 #2
4. **截图 #3-#4**：观察联机面板右侧登录区，截取 #3，点"匿 名 登 录"后截取 #4
5. **截图 #5-#8**：进入大厅 → 补 AI 开局 → 行动阶段 → 无消息时截 #5 → 按 Enter 发一条消息 → 截 #6 → 点"举报最近"截 #7 → 点"屏蔽最近"截 #8
6. 每张截图后执行 `Gangland > Screenshots > Capture Demo Shots`
7. 完成后 `Gangland > Screenshots > Open Screenshots Folder` 查看，按表格重命名

### 质量标准
- 分辨率: ≥ 1920×1080
- 窗口模式: 全屏
- 语言: zh-CN
- 色盲: 0（关闭）
- 无 OnGUI 调试叠加面板（如有 `[Debug]` 面板则隐藏）

---

## 更新 Screenshots/output

截图完成后：
1. 将 PNG 文件复制/重命名为表格中的目标文件名
2. 更新 `ui_screenshot_checklist_20260609.md` 中的截图清单
3. 如果有新的代表性截图，可替换旧的 `stage1-vertical-slice.png` 和 `gangland-online-demo.png`
