# 美术整改计划 — 现状诊断 & 优先级清单

> **日期**: 2026-06-07 | **最近推进**: 2026-06-08 16:31 | **不动大文件，只做精准手术**

当前生产主线已修正为 **2D / 俯视 2D**。历史 3D 资源只作为 `Legacy3D` / 原型兜底 / 可选参考，不再作为本阶段采购和接入方向。

---

## 现状诊断

### 地图渲染 — 🟡 已有基准，但仍显程序化
```
地图 = tile 铺贴 + 墙体边框 + 大量程序化几何道具
       CreateTiledFloor(...)              ← 已避免 32px tile 被大面积拉伸
       CreateRoomFrame / wall borders     ← 已有房间边界和墙体厚度感
       CreateOperationalLightingLayer()   ← 已替代旧随机霓虹光斑
```

- 2026-06-08 已确认 `CreateTiledFloor()`、走廊墙边、房间 frame 已在 `OnlineWorldBuilder.cs` 中落地
- 2026-06-08 15:47 新增低饱和“行动照明层”，去掉主地图旧 `霓虹0/1/...` 随机高饱和光斑
- 当前主要问题变为: 大量地标/道具仍是程序化矩形拼装，缺少真实资产质感和截图验收
- 仍需继续处理会议/任务/战术地图 UI，以及关键 VFX 的廉价几何效果

### 角色 — 🟡 勉强及格
- CC0 像素精灵加载正常（8 职业 × 4 向 × 3 帧）
- 64×64 像素，Point filter，像素感保留
- 但与纯色几何地图风格脱节
- 在线角色视觉已有脚下阴影和职业小配件；下一步应补出局/幽灵态可读性

### UI — 🟡 已建立第一块基准，旧 neon 方向不继续加码
- 2026-06-08 已把行动阶段紧凑 HUD 改为状态卡 / 命令条 / 身份卡三段式
- 全屏 CRT scanline 已移除，避免廉价噪点
- UIStyle 从高饱和 Neon 改为低饱和警署行动风
- 文本默认 CJK 字体优先，不再让中文 UI 依赖像素字体
- 2026-06-08 16:19 已完成会议/投票 overlay 2D 资产皮肤切片
- 2026-06-08 16:31 已完成任务面板/任务小游戏板 2D 资产皮肤切片
- 剩余问题: 战术地图大面板还未按新基准重做，真实地图资产替换仍需继续

### VFX — 🔴 全程序化
- 血迹 = DrawBloodPool() → 红色椭圆
- 任务光晕 = DrawGlowRing() → 蓝色圆环
- 尸体标记 = DrawCorpse() → 白色X
- 无粒子系统、无精灵动画

---

## 整改计划（按优先级）

### P0 — 地图纹理化（已完成第一轮，继续验收）

| # | 整改项 | 方案 | 涉及文件 |
|---|--------|------|---------|
| 1 | **走廊地板纹理** | ✅ 已用 `CreateTiledFloor()` 重复铺贴，避免单张 tile 拉伸 | `OnlineWorldBuilder.cs` |
| 2 | **房间地板** | ✅ 已有 `CreateRoomFloorTiles()` / 建筑地台层，仍需截图验收 | `OnlineWorldBuilder.cs` |
| 3 | **墙壁厚度感** | ✅ 已有走廊 wallT/wallB/wallL/wallR 与房间 frame | `OnlineWorldBuilder.cs` |

### P0.5 — UI 基准扩展

| # | 整改项 | 方案 | 涉及文件 |
|---|--------|------|---------|
| 4 | **会议面板重做** | ✅ 三栏结构: 证据板 / 会议席位 / 投票列表，接入 2D UI skin 与职业头像 | `OnlineMatchHud.cs` Meeting Overlay |
| 5 | **任务面板重做** | ✅ 任务终端 / 站点预览 / 简报卡 / 小游戏板，接入 2D UI skin 与 tileset sprite | `OnlineMatchHud.cs` Task Overlay |
| 6 | **战术地图重做** | 大地图降低橙紫色块，改为清晰楼层/房间/任务点图例 | `OnlineMatchHud.cs` Map Overlay |

### P1 — 颜色/氛围调优

| # | 整改项 | 方案 |
|---|--------|------|
| 7 | **地面色从纯黑灰 → 有色调** | 地板 +0.02 blue tint（港区雨夜氛围） |
| 8 | **走廊颜色分层** | 主干道/支路/服务通道用 3 种不同明度 |
| 9 | **行动照明/警示色斑** | ✅ 已新增低饱和蓝/红/琥珀行动照明层，替代旧随机霓虹 |

### P2 — 角色视觉增强

| # | 整改项 | 方案 |
|---|--------|------|
| 10 | **角色脚下阴影** | ✅ 已在 `CreatePlayerVisual()` 中生成半透明软阴影 |
| 11 | **被淘汰角色灰色滤镜** | GhostMode 激活时 sprite color 改为 dark gray |

### P3 — VFX 升级

| # | 整改项 | 方案 |
|---|--------|------|
| 12 | **击杀血迹** | Replace DrawBloodPool with 3-frame animated splatter sprite |
| 13 | **破坏特效** | Blackout 时叠加红色 pulsating overlay |

---

## 实施估计

| 优先级 | 项数 | 预计改动行数 | 生效方式 |
|--------|------|------------|---------|
| P0 | 3 | ~80 行 | 地图即刻可见纹理 |
| P1 | 3 | ~40 行 | 氛围提升 |
| P2 | 2 | ~20 行 | 角色更有存在感 |
| P3 | 2 | ~30 行 | 关键时刻有反馈 |

总改动: ~170 行，零新文件，不改架构。

---

## 2026-06-08 15:47 推进记录

- `OnlineWorldBuilder.BuildDistrictMap()` 不再调用旧 `CreateNeonDecor()`。
- 新增 `CreateOperationalLightingLayer()`，生成指挥车冷光、监控室反光、封控灯带、低位地灯等 19 个低饱和行动照明元素。
- 新增 `OperationalLightingElementCount` 和 EditMode 回归 `DistrictMap_UsesOperationalLightingInsteadOfRandomNeonSpots`。
- 修复 `GanglandUndercover.Tests.asmdef` 的 Unity Test Framework 引用，EditMode 测试重新可直接运行。

## 2026-06-08 16:19 推进记录

- `OnlineMatchHud.BuildMeetingOverlay()` 改为 2D 资产皮肤会议面板: 证据板 / 会议席位 / 投票列表三栏结构。
- 会议席位使用 `Sprite2DAssetCache.CharacterSets` 的职业头像，投票按钮显示玩家名、嫌疑值和职业。
- 新增 `MeetingOverlayVisualElementCount` / `MeetingOverlay2DAssetElementCount` 与 EditMode 回归 `MeetingOverlay_Uses2DAssetSkinnedVisualSlice`。

## 2026-06-08 16:31 推进记录

- `OnlineMatchHud.BuildTaskOverlay()` 改为任务终端式 2D UI: 头部状态条、任务站点预览、简报卡、小游戏板。
- CCTV / 录音 / 接线 / 车牌 / 通用任务画布中的关键块接入已有免费 2D sprite skin，不再只是纯色矩形。
- 新增 `TaskOverlayVisualElementCount` / `TaskOverlay2DAssetElementCount` 与 EditMode 回归 `TaskOverlay_Uses2DAssetSkinnedVisualSlice`。
- 编译验证通过: `Logs/codex-task-ui-assets-compile.log`。
- EditMode / PlayMode TestRunner 本轮被 Unity Licensing Client 初始化/重连阻塞，未生成 XML，不计为通过。

---

## 采购口径

- 现在不买 3D: Synty、Quaternius、low-poly 城市包都不是当前 2D 主线的最优投入。
- 现在优先买 2D modern interior/exterior tileset: 用于替换会议室、任务房、走廊、港区/街区地标。
- 音频优先买 SFX/UI/ambience 小包，不优先买大 BGM 包；现有 Kenney/项目内音频先继续兜底。
