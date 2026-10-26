# 批次 5 提案：世界美术整合（LimeZu 实物上岗计划）

> 2026-10-22 | 触发：用户反馈"画面质量差距很大"
> 诊断结论：UI 层（菜单/HUD/按钮）已达标；**差距集中在对局世界层**——
> 世界视觉语言是"程序化彩色几何体"（椭圆/胶囊/色条/大色块），不是美术资产。
> 2b 降噪 / 2c 提亮 / 2d 展示层减法都是"减噪"，无法把几何体变成美术。

## 现状证据

- 玩家视角六阶段：`Screenshots/DemoBaseline/03_action_hud_092515508.png`（2d 后）
- 全图：`Screenshots/district-map-harbour.png`（2d 后）
- 对比 2b 前：`output/art_batch2b_evidence/`

## 已确认的资产库存（`Sprite2DAssetCache`，166 个成员）

- **墙类（零上岗）**：`WallBrick` / `WallConcrete` / `WallStripe` / `WallBlock`
- **道具（少量上岗）**：Barrel/OilBarrel、BenchedTable、ButcherCarcass、Cabinet、
  CanteenCakeFridge、Crate（新旧/铁锈）、Vent（Rust）、Office/Interior 家具系列
  （OldTv、ChartBoard、JailLocker、Trapdoor、SecurityCamera 等——仅会议舱用了 4 件）
- **地板（已上岗，批次 2a）**：FloorTile/Wood/Metal/Concrete/Carpet 按房名映射 ✓

## 分层整合顺序（按玩家视角可见面积排序）

> **2026-10-26 执行修订**：5b 原诊断错诊——`CreateTaskConsole` 是零调用死代码；
> 真实任务站链路 `CreateTaskVisual`/`CreateTaskEquipment` 早在 `1ac46dcc` 就已
> LimeZu 实物化（7 种站型各有实物底座）。5c 原目标"换皮纯色椭圆/胶囊"在 2d
> 关闭展示层后也大幅缩水（普查 noiseSprites=376 大多为低饱和设计件/玩法色），
> 真实缺口变为**走廊空旷**——5c 重定向为"沿边铺量"并已落地。

| 波次 | 内容 | 状态 |
|------|------|------|
| 5a | **建筑墙线贴图化**：WallBrick/WallConcrete/WallStripe 条带沿 12 栋建筑四边 | ✅ `0f874a60` |
| 5b | ~~任务站实物化~~（错诊：真实任务站早已 LimeZu 化） | ➖ 无需做 |
| 5c | **走廊沿边道具铺量**：`CreateCorridorEdgeProps` 19 槽位 ×5 主题（货箱堆/油桶对/排档桌椅/杂物角/绿植托盘），全非碰撞零几何改动 | ✅ 本提交 |
| 5d | **房间家具铺量**：LimeZu 家具按房名映射铺进 12 栋建筑（茶餐厅/仓库/诊所/夜市…各有主题表） | ⬜ 下一步 |
| 5e | **角色服装/配色审查**：48px 角色在 2c 提亮后的地面上是否醒目（描边/影子） | ⬜ |

## 硬不变量（沿用批次 2a 纪律）

- 服务端权威几何/碰撞零改动；LimeZu 原文件不改名（manifest 映射）；tile PPU=48
- 换皮不改形状：道具替换=同位置同 scale 换 sprite，碰撞体不动
- 每波次出图对比 + EditMode 门禁后提交

## 已完成的配套（本提交）

- **批次 2d**：`DistrictShowcaseDressing=false` 默认关闭五个展示层
  （Quaternius 模型/大尺度港景/官方免费资产/商业美术适配/垂直切片生产）——
  这些层以 Mesh 色块为主（**MeshRenderer 不被降噪/提亮 pass 触达**，历史豁免盲区），
  是玩家视角红块/黑胶囊噪点主力。要回看旧效果把 WorldBuilder 上的勾选打开即可。
