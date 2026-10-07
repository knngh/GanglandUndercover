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
| 2 | 地图换皮：LimeZu 接线替换纯色矩形 | 待开工 | 2~3 天 | 批次 1 |
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
