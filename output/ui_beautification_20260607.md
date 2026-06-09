# UI 美化 — 修正报告

> **日期**: 2026-06-07 | **修正**: 2026-06-08 | **最近推进**: 2026-06-08 16:31

---

## 结论修正

2026-06-07 的 neon / 像素字体 / 扫描线方向已经证明不够好，用户观感仍觉得 UI 拉胯。后续不继续在高饱和 neon 和装饰性噪点上叠加效果，改走低饱和、信息密度清晰的“警署行动 HUD”方向。

当前主线是 2D / 俯视 2D。3D 资产只保留在 `Legacy3D` 或历史资源路径中，不作为本阶段 UI/地图采购和接入主线。

---

## 当前已落地

### 1. 新建统一主题系统 `UIStyle.cs`
```
Assets/_Project/Scripts/UI/UIStyle.cs
```
- **低饱和行动调色板**: 深色背景 + 蓝/红/琥珀/绿状态色
- **CJK 字体优先**: 中文界面默认走系统 CJK 字体，Kenney Future 仅保留为资源/fallback
- **面板边框装饰器**: `CreateStyledPanel()` 自动添加四边彩色边框
- **统一文字**: `CreateStyledText()` 默认应用 CJK 字体

### 2. OnlineMatchHud 行动 HUD 基准切片
- 行动阶段紧凑 HUD 改为状态卡 / 命令条 / 身份卡三段式
- 去掉全屏 CRT scanline
- 按钮颜色从霓虹悬停改为低饱和高可读状态
- 开局/身份文案改为游戏内行动简报语气，去掉 emoji 和营销式提示
- 新增 `CompactActionHudVisualElementCount`，方便后续测试/验收检查

### 3. 运行时资产部署
| 资产 | 路径 | 用途 |
|------|------|------|
| KenneyFuture.ttf | Resources/Fonts/ | 保留为 fallback / 英文装饰字体 |
| buttonSquare_grey.png | Resources/Sprites/UI/Buttons/ | 会议主面板 2D skin |
| buttonSquare_blue.png | Resources/Sprites/UI/Buttons/ | 会议投票按钮 2D skin |
| buttonSquare_beige.png | Resources/Sprites/UI/Buttons/ | 会议席位 / 票卡 2D skin |
| Resources/Sprites/Characters | Resources/Sprites/Characters/ | 会议席位职业头像 |

### 4. OnlineMatchHud 会议/投票基准切片
- 会议 overlay 改为三栏结构: 证据板 / 会议席位 / 投票列表
- 投票按钮改为低饱和票卡，显示玩家名、嫌疑值和职业
- 会议席位改为职业头像 + 状态卡，不再只是纯色小块
- 新增 `MeetingOverlayVisualElementCount` 和 `MeetingOverlay2DAssetElementCount`
- 新增 EditMode 回归 `MeetingOverlay_Uses2DAssetSkinnedVisualSlice`

### 5. OnlineMatchHud 任务面板基准切片
- 任务 overlay 改为任务终端结构: 头部状态条 / 任务站点预览 / 简报卡 / 小游戏板
- CCTV、录音、接线、车牌、通用任务画布的关键块接入已有免费 2D UI/tileset sprite
- 新增 `TaskOverlayVisualElementCount` 和 `TaskOverlay2DAssetElementCount`
- 新增 EditMode 回归 `TaskOverlay_Uses2DAssetSkinnedVisualSlice`

---

## 视觉对比

| 元素 | Before | After |
|------|--------|-------|
| 背景色 | 高饱和深黑蓝 | 更暗、更低饱和的行动界面底色 |
| 面板色 | 装饰性 neon 面板 | 细边线、低噪点信息卡 |
| 重点色 | 霓虹蓝/红/紫 | 降饱和蓝/红/琥珀/绿状态色 |
| 字体 | Kenney Future 像素字 | CJK 字体优先 |
| 行动 HUD | 分散文字块 | 状态卡 / 命令条 / 身份卡 |
| 会议 UI | 纯色程序面板 | 证据板 / 头像席位 / 资产皮肤票卡 |
| 任务 UI | 纯色程序面板 | 任务终端 / 站点预览 / 资产皮肤小游戏板 |
| 扫描线 | 全屏 CRT 噪点 | 已移除 |

---

## 待继续
- 战术地图面板降低橙紫色块，改成更清晰的房间/任务点/路线图例
- 截图验收需要在不改动场景/Prefab 的方式下补一条专用捕获流程

## 验证

- Compile: passed (`Logs/codex-task-ui-assets-compile.log`)
- EditMode / PlayMode: 本轮未得到有效结果；`-runTests` 两次卡在 Unity Licensing Client 初始化/重连，未生成 XML，不计为通过
