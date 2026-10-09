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
| 3 | 角色替换：CC0 换装 144 PNG | ✅ 资产落地 2026-10-08（详见批次 3 完成记录，门禁待编辑器/CI 收口） | 2~3 天 | 批次 1 |
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

### 批次 3 完成记录（2026-10-08，资产落地；门禁部分待收口）

**源方案修正**：D2 所述 Kenney Roguelike/Desert Shooter CC0 源**实际不可用**——16×16 素色人形、
Dealer/Triad 两类源文件损坏（整帧洋红）、撑不起 style_lock §3 的职业剪影可读性要求。
改用**已购买的 LimeZu Modern Interiors Character_Generator**（Bodies 9 + Outfits 33 款×多色 +
Hairstyles 29 款 + Eyes），按官方 CHARACTER_GENERATOR.txt 顺序四层同网格叠加。
授权干净（付费包，非 CC0）；`cc0_manifest.json` 的 `source` 字段已如实更新。

**管线破译**（Eyes 表像素级验证，此前无文档）：
- 部件表比 Premade 成品表**整体下移一行**：部件 r1=idle（c0=Right, c1=Up, c2=Left, c3=Down）、
  r3=walk（每方向 6 帧：c0-5 Right / c6-11 Up / c12-17 Left / c18-23 Down）
- 方向判定证据：r1 眼睛分布 c1=0px（背面无眼）、c3=36px 双眼（正面）、c0 单眼 x=33-35（朝右）、
  c2 单眼 x=12-14（朝左）

**新脚本**：`scripts/bake_limezu_characters_48.py`（幂等，仅覆盖既有同名 PNG，GUID 不变）。
职业组合：Inspector=Outfit19/H04 警蓝、Forensics=Outfit08 白大褂、Tech=Outfit16 黄工装、
UndercoverAgent=Outfit28 深色便装（不带警方色）、Enforcer=Outfit26 光头黑衣红领、
Fixer=Outfit03、Driver=Outfit23、**Mole=与 Inspector 完全同装**（隐藏阵营原则，仅 avatar
边框 MOLE_GREY 区分）。death=正面 idle 旋转 90°+去饱和；avatar=头部裁剪+2px 职业色边框。

**代码同步**：`ArtAssetReadinessReport.cs` 尺寸常量 64→48（帧）、32→48（avatar），对齐
style_lock v1.1。manifest 结构不变（键集合不动，仅更新 source/sprite_size/profession_map 值）。

**门禁状态（诚实条款）**：
- [x] 144 PNG 全量写入（8 职业 × 4 方向 × 4 帧 + death + avatar），文件级验证通过
- [x] 新旧对比图 + 全职业四方向网格图存 `output/art_batch3_characters/`
- [ ] EditMode 测试**未跑**——Unity 编辑器正打开项目（UnityLockfile 11:46），batchmode 报
  "project read only" 无法并行。请关闭编辑器后跑 `./ci_run.sh --skip-build` 或在编辑器内
  重跑 `ArtAssetReadinessReport` 确认 misconfig=0 / mismatch=0
- [ ] 实机 4 方向走动录屏（待编辑器验证后）
- [ ] UndercoverAgent 当前为 Outfit28 第一色变体（红白调），如需更低调深色可换变体号重跑

## 批次 4：UI 统一

- 全界面收敛 noir-clean 一套，清理 CC0 kit 混用
- HUD Report/Kill/Ability 三按钮加冷却态视觉（状态色遵 Art Bible §5.1）
- 确认中文字体资产授权（现 LegacyRuntime.ttf 是权宜）

### 批次 4 资产记录（2026-10-08，资产落地；代码接线待编辑器可用后）

**新脚本**：`scripts/bake_noir_ui.py`（幂等；策略=保留文件名与尺寸只换像素，引用零破坏）。

- 覆盖重绘（色板全部出自 gangland_palette，无新造基色）：
  - `buttonSquare_beige`（45×49，UIStyle 通用钮）→ 深底 + panel_border 边 + 顶部高光
  - `buttonSquare_blue`（45×49，HUD 主动作）→ POLICE_BLUE 实底 + 深蓝边
  - `buttonSquare_grey`（45×49，次要/禁用）→ button_disabled 底
  - `button_round_gloss`（64×64，ButtonRound）→ 深底圆 + 警蓝环 + 高光弧
  - `button_noir_clean`（120×32）**保持不动**（已是 noir 基准，测试断言其名）
- 新增三态资产（已写 meta，Point filter + 固定 GUID）：
  - `buttonSquare_hover.png`（青绿描边 = STATE_COLORS.active）
  - `buttonSquare_pressed.png`（内陷暗框）
  - `buttonSquare_cooldown.png`（暗底 + highlight 金描边；运行时建议 Image fillAmount 扇形遮罩叠加）
- 备份：`output/art_batch4_ui/backup_20261008/`（28 PNG）；前后对比图 `ui_current.png` / `ui_after.png`

**代码接线点（待办，需编译验证环境）**：
1. `OnlineMatchHud.cs` Report/Kill/Ability 三按钮：cooldown 残余时切 `buttonSquare_cooldown`
   + `Image.type=Filled, fillMethod=Radial360, fillAmount=剩余比例`
2. `UIStyle.cs` / HUD 动态按钮：hover/pressed 态挂 `buttonSquare_hover` / `buttonSquare_pressed`
3. 中文字体授权：LegacyRuntime.ttf 无中文字形覆盖，建议换思源黑体 SC（SIL OFL 免费商用），
   需用户拍板后落 `Resources/Fonts`

**门禁状态**：资产文件级验证通过；EditMode（含 SpriteResourceImportSettingsTests 的
AssertLoadableSprite 与 UIArtCache 断言）待编辑器/CI 收口——文件名未变，预期全绿。

### 音频深化记录（2026-10-08 下午，BGM 占位替换）

**现状**：BGM_InGame 4.2s / MainMenu 8.4s / Meeting 4.8s 单层芯片循环 → 深化为多层合成曲目。

**新脚本**：`scripts/bake_bgm_deep.py`（numpy 向量化合成 + ffmpeg 原生 vorbis 160k；用
`/opt/homebrew/bin/python3`，其自带 numpy，无安装行为）：

| 曲目 | BPM/结构 | 时长 | 编排 |
|---|---|---|---|
| BGM_MainMenu | 72，Dm-Gm-Bb-A ×8 | **106.7s** | pad 和声 + 哑键琶音，第二遍加五声 lead |
| BGM_InGame | 92，i-VI-III-VII ×4 | **83.5s** | bass 八分 + kick/snare/hat，第一遍减配第二遍全奏 |
| BGM_Meeting | 60，低音 D→C#→C→B 半音下行 | **64.0s** | 心跳双 kick + 时钟 tick + 小二度紧张 pad |

- 无缝循环：整数小节 + 音色尾音小节内衰减 + pad 首尾交叉淡化；tanh 软限幅；stereo pan 分轨
- 文件名不变（.meta 原样，引用零破坏）；备份 `output/art_batch4_audio/`
- Homebrew ffmpeg 无 libvorbis，用原生 `vorbis -strict experimental -b:a 160k` 编码

## 批次 5：小游戏 + VFX（阻塞中）

- 小游戏三件（wire/keypad/scan）互动美术，恢复 MinigameArtCache
- VFX pass 2：blackout/comms jam/door lock/patrol alert 对照真实局调
- **前置：P0-3 能力 VFX 联网**——远程玩家看不见的东西不打磨

### 批次 5 前置记录（2026-10-08 下午，资产先行；联网相关仍阻塞）

**用户拍板**（2026-10-08："美术资产缺的，你自己来生成"）：资产先行，代码接线后置。

- **小游戏美术 45/45 全量落地**（此前 `Resources/Sprites/MiniGames/` 整体缺失，
  原 59 张水印稿在隔离区不可恢复）：`scripts/bake_minigame_art.py` 按
  `MinigameArtCache.cs` 文件名约定生成 shared(4)/wire(6)/keypad(14)/scan(5)/
  download(4)/memory(9)/swipe(3)，noir-clean 风格 + gangland_palette 色板 +
  3×5 像素数字字体，全部含 meta（Point filter）。
  → 代码端 `MinigameArtCache.Ensure()` 的 Load() 即可全部命中（资源零改动），
    待编辑器刷新后运行验证 46 处非空。
- **环境音补齐**：`scripts/bake_ambience_policestation.py` 生成
  `amb_policestation_indoor.ogg`（60s 无缝循环：50Hz 电网哼声+空调低噪+风扇白噪
  +稀疏脚步/闷响空间事件），三地图主题环境音全部齐备（harbour_rain ✓ /
  kowloon_neon ✓ / policestation_indoor ✓）。
- **仍阻塞**：VFX pass 2 与 P0-3 能力 VFX 联网（远程玩家可见性），不动。
- 证据：预览图 `output/art_batch5_minigames/minigames_preview.png`

## 与 P0 的关系（诚实条款）

9/20 复审指出"P2 美术切片推进、P0 不动"的挑软柿子模式。本路线图启动期间，
每完成一个美术批次须自查三个 P0 是否有进展；若连续两个美术批次而 P0 零进展，
停止美术批次、先回 P0。
