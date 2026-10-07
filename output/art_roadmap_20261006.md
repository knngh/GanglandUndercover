# 美术推进路线图 2026-10-06

> 前置审查：本轮核实（2026-10-06）
> 决策：分辨率统一 48×48（D1）、角色 CC0 换装（D2），详见 `art_style_lock_20261006.md`
> 铁律：**美术排在三个 P0 之后**（节奏引擎 / Windows 构建 / 能力反馈联网）。
> 唯一例外：能力 VFX 联网（P0-3）是美术×netcode 交叉点，批次 5 依赖它先落地。

## 批次总览

| 批次 | 内容 | 状态 | 预估 | 依赖 |
|------|------|------|------|------|
| 0 | 清烂尾：提交 9/12 图标切片 | ✅ 完成 `8a21e54c`（242 EditMode 绿） | — | — |
| 1 | 风格锁定：Art Bible v1.1 修订 + 风格 gate | 🔄 本文档 + style_lock | — | — |
| 2 | 地图换皮：LimeZu 接线替换纯色矩形 | 🔄 2a+2b 完成 `c2886fb2`/`40a595dd` | 2~3 天 | 批次 1 |
| 3 | 角色替换：CC0 换装 144 PNG | 待开工 | 2~3 天 | 批次 1 |
| 4 | UI 统一：noir-clean 收敛 + HUD 冷却态 | 待开工 | 1~2 天 | 批次 1 |
| 5 | 小游戏美术 + VFX 打磨 pass 2 | 阻塞 | 2~3 天 | **P0-3 VFX 联网** |

## 批次 2：地图换皮（观感提升最大、风险最低）

**现状（2026-10-06 管线探查修正）**：运行时地图全部由 `OnlineWorldBuilder.BuildDistrictMap()`
硬编码搭建；`GreyboxMapBuilder`/`RoomDecorator`/`MapTilePalette` 是**休眠代码**（零运行时调用方，
不在本批次范围）。LimeZu 已部分接线（75+ 元素、室外地板 asphalt-48 已平铺），
真正缺口：① 16px atlas 只切左上角一格（`Sprite2DAssetCache.cs:536-589`）；
② 地面/道路/墙/房间底的纯色 `CreateProp`（圆角矩形+tint）占位。

- 核心 unlock：`Sprite2DAssetCache` atlas 切片从"仅左上 cell"扩到按 cell 索引取
- `CreateFloor`/`CreateRoadNetwork`/`CreateShipRoom` 底+墙等纯色点 → LimeZu tile
  （Art Bible §4.3 三图区分表定 theme）
- 硬不变量：solid prop 换 sprite 时 scale/位置不动（碰撞一致）；z 层级约定不动
- **不引入 Tilemap 重构**；一致性风险低（地图各端本地确定性生成，网上只同步地图类型 int）
- 验收：风格 gate 全过 + 同局多客户端截图一致

### 批次 2a 完成记录（2026-10-07，`c2886fb2`）

- `Sprite2DAssetCache.CreateAtlasCellSprite`/`LimeZuFloorCell`：解锁 16px atlas 任意格切片（此前只取左上角一格）
- 房名关键词 → 已目检地砖格映射（木纹 13,27 / 石砖 12,25 / 花毯 9,19 / 菱形 14,30 / 灰格 4,18），确定性、零 Random
- `CreateRoomFloorTiles` 棋盘格 + `CreateShipRoom` 中央地板：色块 → tile，位置/尺寸/z 层级原样
- gate：245/245 EditMode 绿；readiness 报告无回归；三图整图截图见 `art_batch2a_evidence/`
- 新工具：`Editor/DistrictMapCaptureTool.cs`（批处理整图截图，勿加 -nographics，必须带 -quit）
- **诚实评估**：2a 技术上全过，但整图观感提升被既有 dressing 噪声淹没——满屏彩色小条/白块
  （道路细节层/微装饰层，非本次引入）。建议批次 2b = **dressing 降噪**（把彩色装饰条数量/亮度
  压下来，让地砖和 LimeZu 实物读得出来），预期观感收益大于继续铺新 tile。

### 批次 2b 完成记录（2026-10-07，`40a595dd`，dressing 降噪）

- **普查先行**：新工具 `Editor/DistrictMapCensusTool.cs` 量化噪声预算——建图 sprite ~3855 个/图，
  其中程序化色块（"Runtime " 几何 sprite）~3358，亮白/高饱和 1393 个、噪声面积 ~4590 单位²；
  主力不是微装饰层，而是**垂直切片展示层残留**（CameraShot 取景框 11.6 单位饱和橙块、
  GameplayAnchor/语音半径色洗）+ 走廊导向线/道路弧线/巡逻导光条/警戒黄线/灯牌霓虹
  （普查表见 `art_batch2b_evidence/district-census-*.txt`）
- **降噪手法（`ApplyDecorationNoiseMutePass`）**：建图收尾统一 pass，只动颜色/透明度——
  高饱和（s≥0.5）程序化色块 s×0.3、v×0.62；SoftCircle 大色洗再 α×0.38。
  <0.35 单位小微件与玩法指代色（任务/交互/门禁/入口/暗线/门牌）豁免；
  **几何、数量、名字、碰撞零改动** → smoke 密度断言（CameraShot≥20 等）与服务端权威布局不受影响
- **效果**：噪声面积 4590→727（**−84%**），高饱和噪声件 1393→560（余为刻意保留的小微件+玩法色）；
  三图整图对比见 `art_batch2b_evidence/`——取景框橙块/黄导向线/霓虹彩条消失，房间结构与
  2a 地砖读得出来，门灯蓝线等寻路色保留
- gate：compile + **245/245 EditMode** + PlayMode 0 failed（12 skipped 为既有平台跳过）
- 遗留观察：整图观感下一瓶颈是**明度对比**（画面整体偏深灰）与实物道具密度，不是色相噪声；
  建议批次 3（角色换装）前如再做地图 pass，优先"提亮主走廊/房间底、收敛灰阶层次"而非继续铺 tile

## 批次 3：角色替换

- 保持 `cc0_manifest.json` 结构与路径约定，换皮零代码改动（`Sprite2DAssetCache` 靠路径约定）
- `scripts/import_cc0_characters.py` 管线复用，重定向到 48×48 源
- 职业可读性规范见 style_lock §3（阵营内区分、Mole=Inspector）
- 验收：144 PNG 全量替换 + readiness 0 mismatch + 实机 4 方向走动录屏

## 批次 4：UI 统一

- 全界面收敛 noir-clean 一套，清理 CC0 kit 混用
- HUD Report/Kill/Ability 三按钮加冷却态视觉（状态色遵 Art Bible §5.1）
- 确认中文字体资产授权（现 LegacyRuntime.ttf 是权宜）

## 批次 5：小游戏 + VFX（阻塞中）

- 小游戏三件（wire/keypad/scan）互动美术，恢复 MinigameArtCache
- VFX pass 2：blackout/comms jam/door lock/patrol alert 对照真实局调
- **前置：P0-3 能力 VFX 联网**——远程玩家看不见的东西不打磨

## 与 P0 的关系（诚实条款）

9/20 复审指出"P2 美术切片推进、P0 不动"的挑软柿子模式。本路线图启动期间，
每完成一个美术批次须自查三个 P0 是否有进展；若连续两个美术批次而 P0 零进展，
停止美术批次、先回 P0。
