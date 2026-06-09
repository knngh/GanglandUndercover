# Gangland Undercover - 资源目录审计表

> 审计日期: 2026-06-09 | 当前资源快照，不执行删除

## 总览

Unity 会识别任意 `Resources` 目录。当前运行时资源主要在 `Assets/_Project/Resources`，普通 `Assets/Resources` 只包含构建信息。

| 目录 | 大小 | 备注 |
|------|------|------|
| `Assets/_Project/Resources` | 105 MB | 运行时 `Resources.Load` 主目录 |
| `Assets/_Project/Audio` | 19 MB | 非 Resources 音频素材库/候选库 |
| `Assets/_Project/Art` | 290 MB | 2D UI/VFX、ThirdParty、动画等 |
| `Assets/_Project/Legacy3D` | 728 MB | 旧 3D/AssetStore 素材，需单独治理 |
| `Assets/_Project/UI` | 5.2 MB | UI 资源 |

## `Assets/_Project/Resources`

| 分类 | 当前数量/状态 | 代码路径 |
|------|---------------|----------|
| `Audio/Ambience` | 2 个 `.ogg` | `AudioManager.LoadRuntimeAudio` |
| `Audio/BGM` | 3 个 `.ogg` | `AudioManager.LoadRuntimeAudio` |
| `Audio/SFX/Kenney` | 16 个 `.ogg` + manifest | `AudioManager` 优先加载 |
| `Audio/SFX` | 16 个同名 `.wav` | Kenney `.ogg` 的 fallback，体积冗余但仍可回滚 |
| `Fonts/KenneyFuture.ttf` | 存在 | `UIStyle` |
| `Fonts/CJKPixelFallback` | 缺失 | `UIStyle` 会尝试加载但允许为空 |
| `Sprites/Characters` | 128 张角色方向/帧图 | `Sprite2DAssetCache` |
| `Sprites/Tilesets` | 84 张瓦片/地标/道具图 | `OnlineWorldBuilder`、`Sprite2DAssetCache` |
| `Sprites/Tilesets/LimeZu` | 60 张精选 Interiors/Exteriors 图 | 当前 2D 地图优先资产 |
| `Sprites/DesertShooter` | 72 张旧主题图 | 未发现代码直接引用，清理前需 Unity 引用检查 |
| `Quaternius/ModularSciFiMegaKit` | 大量 FBX/贴图 | 3D/旧构建候选 |
| `AssetStore/Free Pack` | 15 个旧 `.wav` | 旧音频素材，未确认运行时引用 |

## 已接入资源事实

- `AudioManager` 会优先从 `Audio/SFX/Kenney/SFX_*` 加载 UI/玩法 SFX，再 fallback 到 `Audio/SFX/SFX_*`。
- `CoreSystemTests` 已覆盖 Kenney 运行时 SFX 是否存在，以及 `OnlineMatchController` 的玩法音效映射。
- `OnlineMatchHud_AttachesHoverSfxToRuntimeButtons` 覆盖运行时按钮挂接 hover/click 音效的方向。
- `WorldBuilder_*` 和 `Sprite2DAssetCache_*` 测试覆盖 LimeZu 精选瓦片/道具/角色帧加载路径。

## 清理候选

| 候选 | 估计价值 | 风险 | 建议 |
|------|----------|------|------|
| `Audio/SFX/SFX_*.wav` | 16 个重复 fallback | 中 | 先确认所有平台 `.ogg` 导入稳定，再删除 |
| `Sprites/DesertShooter` | 72 张旧主题图 | 中 | 先跑引用扫描和 Unity meta 引用检查 |
| `AssetStore/Free Pack` | 15 个旧 `.wav` | 中 | 确认 AudioManager/场景/Prefab 未引用 |
| `Legacy3D` | 728 MB | 高 | 单独做资产迁移/归档计划，不和代码提交混合 |
| `Fonts/CJKPixelFallback` 缺失 | 低 | 低 | 若中文显示风险升高，补字体；否则删除无效加载路径 |

## 下一步

1. 保留本次审计为目录事实快照。
2. 另开“资源清理 manifest”切片，列出每个候选文件、引用扫描结果和回滚方式。
3. 资源删除必须独立提交，且先跑 EditMode、PlayMode、Smoke。
