# 港区潜线：素材资源清单

更新时间：2026-08-03

当前结论：正式垂直切片按 **2D / 俯视 2D** 推进。生产路径优先使用已经稳定导入的免费 2D 资源、项目内 2D 资源和程序化兜底层。历史 3D 资源只作为 `Legacy3D` / 原型兜底 / 可选参考，不再作为本阶段地图、任务、UI 或采购主线。

## 当前 2D 主线资源

| 类别 | 资源 | 当前路径 | 用法 |
| --- | --- | --- | --- |
| Characters | CC0 / 项目 2D 职业角色方向帧 | `Assets/_Project/Resources/Sprites/Characters` | 运行时角色、会议头像、职业识别 |
| Characters | 项目 2D 角色源文件 | `Assets/_Project/Art/2D/Characters` | 后续替换和扩展角色动画 |
| Tilesets | 免费 2D tileset | `Assets/_Project/Resources/Sprites/Tilesets` | 当前运行时地板、墙体、任务面板视觉块 |
| Tilesets | LimeZu Modern Interiors / Exteriors / Office 精选运行时切片 | `Assets/_Project/Resources/Sprites/Tilesets/LimeZu` | 当前世界生成地板 tile、任务站点、关键地标、房间实物道具和事件反馈优先使用，室内/街区主要房间道具已完成高覆盖替换 |
| Tilesets | 项目 2D 地图 tile | `Assets/_Project/Art/2D/Tiles` | 港区、九龙、警署主题地图资产池 |
| UI | 已审阅无水印 2D skin | `Assets/_Project/Resources/Sprites/UI` | 会议/投票/任务面板与通用按钮 sprite-backed UI |
| UI | 项目 2D UI 源文件 | `Assets/_Project/Art/2D/UI` | 面板、按钮、徽章、图标、进度条 |
| Props | 任务站点 tile | `Assets/_Project/Art/2D/Props/TaskStations` | 后续任务点真实资产替换 |
| VFX | 项目 2D VFX 帧 | `Assets/_Project/Art/2D/VFX` | 后续破坏、封锁、黑灯、证据泄露、击倒反馈 |
| Audio | 项目内 SFX/BGM/Ambience | `Assets/_Project/Resources/Audio` | UI、会议、任务、击倒、报告、环境声兜底 |
| Audio | Kenney Interface Sounds / Impact Sounds 精选运行时切片 | `Assets/_Project/Resources/Audio/SFX/Kenney` | `AudioManager` 优先加载的 UI、任务、会议、击倒、报告等短音效 |
| Audio | Free Pack | `Assets/_Project/Resources/AssetStore/Free Pack` | 临时事件音效兜底，需逐步筛选/替换 |
| Audio | Kenney Interface Sounds / Impact Sounds 原始 zip | `.asset_cache/free/kenney` | 完整原始 zip 缓存，不全量导入 Unity |

## 已进入运行路径的 2D 资源

1. 角色：`Sprite2DAssetCache` 优先加载 `Resources/Sprites/Characters/{Profession}`，失败时才用程序化角色。
2. 地图：`OnlineWorldBuilder` 已使用 `Resources/Sprites/Tilesets` 做地板/墙体 tile，避免 32px tile 大面积拉伸。
3. UI：`OnlineMatchHud` 已将行动 HUD、会议/投票 overlay、任务 overlay 改为低饱和 2D UI 基准，动态按钮挂接 Kenney hover/click 反馈；任务预览按模板切换已审阅的设备图标。
4. 会议：会议席位使用职业头像，投票按钮和面板使用 `Resources/Sprites/UI/Buttons`。
5. 任务：任务终端、站点预览、小游戏板和关键任务块使用 `Resources/Sprites/UI/Buttons` 与 `Resources/Sprites/Tilesets`；任务破坏反馈已加入 LimeZu 设备盖板和证物包 sprite。
6. 音频：`AudioManager` 优先加载 `Resources/Audio/SFX/Kenney` 精选 Kenney 短音效，`Resources/Audio/SFX` 与 `Free Pack` 继续作为兜底。
7. 付费素材：LimeZu 三包完整原始文件已缓存到 `.asset_cache/purchased/limezu`；Unity 运行时只导入精选切片，避免一次性导入 9 万文件。
8. 房间道具：金融、电房、证物库、诊所、夜市、后巷、监控室、指挥点和舰内房间已加入 LimeZu room-props / Modern Office / Modern Interiors 单体 sprite，并纳入 `LimeZuRoomPropSpriteElementCount` 烟测门槛。
9. VFX：尸体视觉已加入 `Stage2 Kill VFX` 命名层，断电场景已加入 `Blackout VFX` 命名层，均纳入 EditMode + PrototypeSmoke 覆盖。

## 当前运行时资源目录快照

- LimeZu 精选 PNG：`Assets/_Project/Resources/Sprites/Tilesets/LimeZu` 当前共 60 个 PNG，包含 Interiors floors/walls/room-props、Exteriors floors/landmarks/room-props、Office floors/walls/props/room-props。
- LimeZu 运行时 manifest：`Assets/_Project/Resources/Sprites/Tilesets/LimeZu/limezu_runtime_manifest.json` 是当前权威清单；完整购买包继续只保留在 `.asset_cache/purchased/limezu`。
- Kenney 精选 SFX：`Assets/_Project/Resources/Audio/SFX/Kenney` 当前共 16 个 `.ogg`，覆盖 UI click/hover、任务完成、击杀、报告、会议、投票、破坏、通风口、胜负等主要 gameplay cue。
- Kenney 运行时 manifest：`Assets/_Project/Resources/Audio/SFX/Kenney/kenney_sfx_manifest.json` 是当前音频精选清单；完整原始 zip 继续只保留在 `.asset_cache/free/kenney`。
- UI 图标批次：`Assets/_Project/Resources/Sprites/UI/Icons` 包含 5 个破坏图标与 6 个任务图标；`reviewed_icon_manifest.json` 记录 LimeZu / Kenney CC0 来源，`ReviewedUiIconBakeTool` 可从已登记的运行时单体设备图重复烘焙。

## 历史 / 可选 3D 资源

这些资源不是当前 2D 主线采购或接入方向。除非后续明确切回 3D/2.5D，否则不要把它们作为正式地图、美术或 UI 依赖扩展。

| 类别 | 资源包 | 当前路径 | 当前状态 |
| --- | --- | --- | --- |
| Legacy3D | SimplePoly City - Low Poly Assets | `Assets/_Project/Resources/AssetStore/SimplePoly City - Low Poly Assets` | 历史 3D 原型资源 |
| Legacy3D | ModularLowpolyStreetsFree | `Assets/_Project/Resources/AssetStore/ModularLowpolyStreetsFree` | 历史 3D 原型资源 |
| Legacy3D | Quaternius ModularSciFiMegaKit | `Assets/_Project/Art/ThirdParty/Quaternius/ModularSciFiMegaKit` | 历史/可选 3D 工业件 |
| Legacy3D | Synty PolygonGeneric / Starter | `Assets/_Project/Resources/AssetStore/Synty` | 历史/可选 3D 道具和角色 |
| Legacy3D | DenysAlmaral CityPeople | `Assets/_Project/Resources/AssetStore/DenysAlmaral/CityPeople` | 历史/可选 3D 角色 |

## 已隔离资源

| 资源包 | 原路径 | 隔离路径 | 原因 | 后续处理 |
| --- | --- | --- | --- | --- |
| LowpolyStreetPack | `Assets/_Project/Resources/AssetStore/LowpolyStreetPack` | `TempAssets/Quarantined/LowpolyStreetPack` | 大量 `.meta` 缺少合法 32 位 GUID，Unity 会忽略或警告，不能作为正式表现依赖 | 只允许重新从 Unity Package Manager / Asset Store 干净导入；当前 2D 主线不需要购买或恢复 |
| Qoder 生成草稿批次 | `Assets/_Project/Resources/Sprites/{Map,MiniGames,UI,VFX/feedback}` | `Assets/_Project/Art/Review/WatermarkedRuntime` | 59 张 PNG 带可见“Qoder AI生成”水印，不能进入正式运行路径 | 只保留作构图参考；必须由无水印原创/已授权资产替换后再进入 `Resources` |

## 资源使用原则

1. 当前正式切片只按 2D / 俯视 2D 推进。
2. 先消化免费 2D 资源：角色、tileset、UI、任务站点、VFX。
3. 程序化方块只保留为缺资源兜底，不能作为第一屏主表现。
4. 新付费资源优先购买 2D modern interiors / exteriors / props，不买 3D 包。
5. 音频优先补 UI/SFX/ambience，小预算不优先买大音乐包。
6. 新资源进入项目后必须先写入本清单，再接入运行路径和烟测。

## 2026-06-08 付费 / 免费素材接入记录

- LimeZu Modern Interiors 完整包已缓存：`.asset_cache/purchased/limezu/modern-interiors/moderninteriors-win`。
- LimeZu Modern Exteriors 完整包已缓存：`.asset_cache/purchased/limezu/modern-exteriors/modernexteriors-win`。
- LimeZu Modern Office Revamped 完整包已缓存：`.asset_cache/purchased/limezu/modern-office/Modern_Office_Revamped_v1`。
- LimeZu 精选运行时切片已导入：`Assets/_Project/Resources/Sprites/Tilesets/LimeZu`，当前 60 个 PNG，详见 `limezu_runtime_manifest.json`。
- `OnlineWorldBuilder` 地板 tile 资源已改为 LimeZu Exteriors asphalt 优先，原 Harbour tile 兜底。
- Kenney Interface Sounds / Impact Sounds 原始 zip 已缓存：`.asset_cache/free/kenney`。
- Kenney 精选短音效已导入：`Assets/_Project/Resources/Audio/SFX/Kenney`，当前 16 个 `.ogg`，`AudioManager` 已改为 Kenney 优先、旧 `Resources/Audio/SFX` 兜底。

## 2026-06-09 资产化推进记录

- LimeZu Modern Exteriors 精选地标单图已导入：`Exteriors/landmarks`，包含 office sign、mailbox、truck、umbrella、table、package、air duct、door、potted plant 等 10 个 PNG。
- `OnlineWorldBuilder` 关键地标已改为真实 LimeZu sprite 主体，Smoke 门槛要求至少 12 个 LimeZu landmark sprite 使用点。
- 任务事件反馈已加入 LimeZu 破坏设备盖板和现场证物包，Smoke 门槛要求每个任务至少 2 个 LimeZu feedback sprite 使用点。
- `OnlineMatchHud` 动态按钮已挂接 `UiButtonSfx`，使用 Kenney `SFX_ButtonHover.ogg` 做悬停/选中反馈，点击继续使用 `SFX_UIClick.ogg`。
- LimeZu Modern Exteriors 房间实物道具精选导入：`Exteriors/room-props`，包含 generator、monitor、big monitor、tool box、light tower、SOS box、chair、benched table、trash can、trash pile 等 10 个 PNG。
- `OnlineWorldBuilder` 已在金融、电房、证物库、诊所、夜市、后巷落入 14 个 `房间实物 LimeZu` sprite 使用点，Smoke 门槛要求至少 14 个 LimeZu room prop sprite 使用点。
- 尸体视觉已加入 `Stage2 Kill VFX` 击杀反馈层，断电核心场景已加入 `Blackout VFX` 闪烁/视野压暗层；Smoke 门槛分别要求 4 个击杀 VFX 和 5 个黑灯 VFX 标记。
- LimeZu Modern Office Revamped 室内单体精选导入：`Office/room-props`，包含 whiteboard、chart board、server rack、printer、corner desk、dual monitor desk、CCTV camera rig、medical cart 等 8 个 PNG。
- `OnlineWorldBuilder` 已在监控室、指挥点、证物库、诊所和舰内房间新增 16 个 Modern Office `房间实物 LimeZu` sprite 使用点，Smoke 门槛提升到至少 30 个 LimeZu room prop sprite 使用点。
- LimeZu Modern Interiors 室内/生活/医疗/安防单体精选导入：`Interiors/room-props`，包含 hospital resonance machine、screen、sink、X-ray、morgue freezer、security camera、safe、grocery fridge、butcher carcass、kitchen、shopping cart、TV、locker、laser、trapdoor、ticket machine、fish sink 等 24 个 PNG。
- `OnlineWorldBuilder` 已把海关、茶餐厅、夜市、金融、电房、天台、后巷、诊所、舰内各房间和舰桥继续替换为 Modern Interiors `房间实物 LimeZu` sprite，Smoke 门槛提升到至少 75 个 LimeZu room prop sprite 使用点。

## 2026-08-02 美术审阅与 UI 收口

- 运行时美术盘点：378 张 Sprite PNG，PNG / `.meta` 一一对应，导入配置错误为 0。
- 角色覆盖：8 个职业、144 张方向/移动帧、16 张死亡/头像特殊帧；尺寸门禁通过。
- VFX 覆盖：8 组、64 帧；尺寸与运动配置门禁通过。
- UI 覆盖：9 个已审阅无水印运行时皮肤，会议、任务和通用按钮统一使用 Point Filter 与 9-slice 边框。
- 水印隔离：59 张地图、小游戏、UI、任务反馈草稿移出 `Resources`，保留到 `Art/Review/WatermarkedRuntime`；`MapArtCache` / `MinigameArtCache` 缺图时继续走现有干净素材与程序化兜底。
- 下一美术切片：优先补无水印技能/任务/破坏图标，其次补 wire / keypad / scan 三类小游戏交互图，再刷新实机会议、任务、断电截图基线。

## 2026-08-03 任务与破坏图标批次

- 新增 11 张 64×64 已审阅无水印运行时图标：5 张破坏类型、6 张任务类型；统一暗色底板、类别色边框和 Point Filter。
- 新增 `ReviewedUiIconBakeTool`，从运行时清单中已有的 LimeZu / Kenney CC0 设备单体图裁切透明边并最近邻缩放，输出可重复生成的 UI 图标，不依赖隔离区草稿。
- `UIArtCache` 已启用这批图标；任务弹窗站点预览按 CCTV、录音、电闸、车牌和通用模板切换图标，`SabotagePanel` 标准破坏按钮显示对应图标。
- 当前运行时 UI 覆盖提升到 20 张；隔离区 59 张水印草稿保持原位，不作为运行依赖。
- 下一美术切片：补齐 camera / emergency / evidence / kill / patrol / report / sabotage / vent 能力图标，再推进 wire / keypad / scan 小游戏交互素材。

## 阶段验收项

1. `OnlineMatchHud` 的行动 HUD、会议/投票、任务 overlay 都应保持 2D sprite-backed UI。
2. `Assets/_Project/Scripts`、`Assets/_Project/Editor`、`Assets/_Project/Scenes` 不应新增对 `LowpolyStreetPack` 的依赖。
3. 地图美术下一阶段不再优先扩进口资源，而是刷新关键截图基线，并做会议/任务/战术地图 UI 与角色动画帧的可感知 polish。
4. 工程下一阶段优先进入联机正确性根基：自定义消息契约、恶意 Client 回归、ChatSystem 联网化、摄像头复制 bug 和双进程实测。
5. `Gangland/Run Smoke Tests` 或 CI 编译 / EditMode / PlayMode 必须通过资源基线门禁。
