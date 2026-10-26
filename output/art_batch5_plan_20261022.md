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
| 5d | **房间家具铺量**：LimeZu 家具按房名映射铺进 12 栋建筑（茶餐厅/仓库/诊所/夜市…各有主题表） | ⬜ 低收益——普查显示每房间已有 4-6 件 LimeZu 实物（`房间实物 LimeZu ≥75` 回归门即其产物），再加属于锦上添花 |
| 5e | **角色醒目度审查** | ◐ 审查完成（2026-10-26），修复项见下 |

## 5e 审查结论（2026-10-26，工具：`CaptureActionViewWithPlayers`）

截图 `Screenshots/action-view-players.png`（建图+4 职业假玩家+游戏内同参数相机 ActionSize 视角）：

1. **世界文字镜像 bug（已修）**：`BillboardLabel` 的 LookRotation 方向反了——TextMesh 从
   local -z 正读，旧实现把文字背面朝向玩家，**全部世界标签（任务名/玩家名/房间名）
   在真对局里左右镜像**（"车牌追踪"显示为"踪牌车"）。整图截图文字太小从未被发现，
   D-3 人工走查又被跳过。修复=forward 取反（标签 local +z 背向相机）。
2. **角色醒目度不足（待拍板）**：0.28 单位角色基座在 ActionSize=3.0 视距下过小，
   4 个测试玩家只有 1 个可一眼辨认。候选：base scale 1.12→1.3（旋钮
   CharacterAnimController.cs:124）/ 描边 / 本地圈加粗。
3. **会议中心遮挡（待拍板）**：会议舱前景遮挡件（z 0.6-0.98 的黑胶囊/顶梁）盖住
   z=0 的玩家——而会议舱中心恰恰是每次会议后全员回集的站位（grace 22s 全程被挡）。
   候选：遮挡件降密度/降 alpha；或玩家 sorting 抬高（代价=破坏 2.5D 立面纵深感，
   Among Us 纯俯视无此问题）。

## 硬不变量（沿用批次 2a 纪律）

- 服务端权威几何/碰撞零改动；LimeZu 原文件不改名（manifest 映射）；tile PPU=48
- 换皮不改形状：道具替换=同位置同 scale 换 sprite，碰撞体不动
- 每波次出图对比 + EditMode 门禁后提交

## 已完成的配套（本提交）

- **批次 2d**：`DistrictShowcaseDressing=false` 默认关闭五个展示层
  （Quaternius 模型/大尺度港景/官方免费资产/商业美术适配/垂直切片生产）——
  这些层以 Mesh 色块为主（**MeshRenderer 不被降噪/提亮 pass 触达**，历史豁免盲区），
  是玩家视角红块/黑胶囊噪点主力。要回看旧效果把 WorldBuilder 上的勾选打开即可。
