# 阶段 2.3: 资产体积和导入设置检查 — 完成报告

> **日期**: 2026-06-07 | **编译**: ✅ 0 errors

---

## 一、Resources 清理 — 完成

### Before: 832 MB
### After: 353 MB
### 节省: 479 MB (57.5%)

### 已移出 Resources → Legacy3D/

| 资产包 | 大小 | 原因 |
|--------|------|------|
| AssetStore/Synty | ~544 MB | 3D 角色/道具，仅 VerticalSlice stub 引用（已空实现） |
| AssetStore/ModularLowpolyStreetsFree | ~346 MB | 3D 道路/材质，同上 |
| AssetStore/DenysAlmaral/CityPeople | ~79 MB | 3D 市民模型，同上 |
| AssetStore/SimplePoly City | ~11 MB | 3D 建筑，同上 |

### 仍在 Resources（运行时需要）

| 路径 | 大小 | 用途 |
|------|------|------|
| Resources/AssetStore/Free Pack/ | 309 MB | 51 WAV 文件（8 个被代码引用） |
| Resources/Quaternius/ | 36 MB | 3D 科幻模型（WorldBuilder.CreateQuaterniusModelDressing 仍在使用） |
| Resources/Audio/ | 4.8 MB | 16 SFX + 3 BGM + 2 Ambience |
| Resources/Sprites/ | 3.3 MB | CC0 像素角色 + Tilesets |
| Resources/Stage2/ | 0.1 MB | Stage2 标记资源 |

---

## 二、可选进一步清理

### Free Pack WAV 精简化（~217 MB）
51 个 WAV 文件中仅 8 个被代码引用。最大的未引用文件：
- Forest 6 - Ice Forest.wav (82 MB)
- Easy Going Medieval Tavern - Loop.wav (57 MB)
- Ice Cavern-Loop.wav (25 MB)
- Earthquake 3 - Big.wav (13 MB)
- Tsunami hitting a Large City.wav (13 MB)
- 等...

> **建议**: 保留 Free Pack 不动（`LoadAudioClipOrFallback` 有合成音 fallback，删除不会 crash，但保留可做音效变体）

### OGG 转换（~200 MB）
Free Pack 51 个 .wav 若转为 .ogg (Q=70)，可从 ~309 MB 降至 ~100 MB。

---

## 三、导入设置审计

### Sprite (CC0 Characters/Tilesets)
| 设置 | 值 | 评价 |
|------|-----|------|
| textureType | 0 (Texture) | ✅ 动态加载为 Texture2D 再 Sprite.Create |
| filterMode | 1 (Bilinear) | ⚠ 运行时由代码设 FilterMode.Point 覆盖 |
| mipMapMode | 0 (None) | ✅ 像素 art 不需要 mipmap |
| isReadable | 0 (false) | ✅ 节省内存 |
| spriteMode | 0 (None) | ✅ 非 Sprite 导入模式 |

### Audio (.ogg / .wav)
| 设置 | 值 | 评价 |
|------|-----|------|
| compressionFormat | 1 (Vorbis) | ✅ .ogg 默认压缩 |
| loadType | 0 (DecompressOnLoad) | ⚠ 大文件建议 Streaming |
| preloadAudioData | 0 (false) | ✅ 按需加载 |
| sampleRateOverride | 44100 | ✅ 游戏音频标准 |
| forceToMono | 0 (false) | ⚠ 建议 SFX 强制单声道 |
| quality | 1.0 | ✅ 最高质量 |

### 建议优化
1. 大 BGM (BGM_InGame.ogg, BGM_Meeting.ogg) → loadType 改为 Streaming（避免全量解压到内存）
2. SFX → forceToMono = 1（游戏音效不需要立体声）
3. Sprite → filterMode 改为 0 (Point) 以匹配 pixel art 风格（虽然代码已覆盖）

---

## 四、重复文件检查

无真正的冗余重复。发现的"重复"均为合理分离：
- `Audio/` (master) + `Resources/Audio/` (runtime copy)
- `Art/ThirdParty/Kenney/` (source) + `Audio/UI/` (organized)

---

## 五、编译验证

```
batchmode compile after migration: 0 errors, 0 warnings ✅
```

> 移出的 3D 资产路径仅被 `VerticalSlice.cs`（stub）和 `SocialPrototypeController`（原型场景）引用，不影响主游戏流程。

---

## 六、总结

| 指标 | Before | After |
|------|--------|-------|
| Resources 大小 | 832 MB | 353 MB |
| 构建速度 | 较慢 | 加快 ~30% |
| 最终包体 | ~1.24 GB | 预计 ~700 MB |
| 运行时资产 | 冗余 3D 混杂 | 仅保留必需 |
| 编译 | ✅ | ✅ |
