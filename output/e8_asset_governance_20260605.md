# E8 资源治理和导入管线

日期：2026-06-05  
基线：`Sprite2DAssetCache.cs` 全程序化生成  

## 当前状态

### 正式美术资产路径

| 目录 | 状态 | 说明 |
|------|------|------|
| `Assets/_Project/Art/2D/` | 空目录 | 正式 2D 资产目的地 |
| `Assets/_Project/Art/2D/Characters/` | 空 | 角色 sprite sheet |
| `Assets/_Project/Art/2D/Tiles/` | 空 | 地图 tileset |
| `Assets/_Project/Art/2D/UI/` | 空 | UI 组件 |
| `Assets/_Project/Art/2D/VFX/` | 空 | 特效帧 |
| `Assets/_Project/Scripts/Art/` | 6 文件 | 程序化生成脚本 |

### 程序化资产（当前主力）

所有运行时 2D 资产由 `Sprite2DAssetCache` 100% 程序化生成，零外部依赖：
- 7 职业 × 4 向 × 3 帧 + 死亡 + 头像 = 105 sprite
- 8 种地图 tile
- 6 种道具 sprite
- 4 种 VFX + 4 种 UI sprite

### 第三方资产（仅作参考）

| 来源 | 内容 | 状态 | 处理方式 |
|------|------|------|---------|
| Kenney | png/fbx/ogg | 本地存在 | 审计后选择性镜像 |
| Quaternius | fbx/glb/gltf | 本地存在 | 审计后选择性镜像 |
| AssetStore | prefab/fbx/png/wav/mat | 本地存在，已 gitignored | 不作为发布依赖 |
| Stage2/Denys/Synty | 3D 角色 | 历史路径 | 降级为概念参考 |

---

## 导入设置规范

### Sprite 导入设置（.png / .psd）

```
Texture Type:       Sprite (2D and UI)
Sprite Mode:        Single / Multiple
Pixels Per Unit:    64  (角色) / 32 (tile) / 16 (UI)
Mesh Type:          Tight
Extrude Edges:      0
Filter Mode:        Point (no filter) — 像素风格
Compression:        None — 小尺寸像素不需要压缩
Generate Mip Maps:  Off
Max Size:           2048
Format:             RGBA 32 bit
```

### Sprite Atlas 策略

```
Atlas_Characters   — 7职业角色帧 (64x64 x ~105帧) → 约 420KB
Atlas_Tiles_Harbour  — 港区 tiles (32x32 x ~80) → 约 80KB
Atlas_Tiles_Police   — 警署 tiles
Atlas_Tiles_Kowloon  — 九龙城寨 tiles
Atlas_Props      — 道具 (32x32 x ~40) → 约 40KB
Atlas_UI         — UI 组件 (16/32/64 混) → 约 160KB
Atlas_VFX        — 特效帧 (32/64 混) → 约 64KB

预估总 Atlas 内存：< 1MB（不含程序化生成）
```

### 音频导入设置

```
Load Type:          Compressed In Memory (短音效)
                    Streaming (BGM/环境声)
Compression Format: Vorbis
Quality:            70%
Sample Rate:        44100 Hz
Force To Mono:      音效 = true, BGM = false
```

---

## 命名规范

### 角色 Sprite Sheet
```
格式：chr_{profession}_{direction}_{frame}_{state}.png

profession: inspector / tech / forensics / undercover / enforcer / fixer / driver / mole
direction:  front / back / left / right
frame:      idle / walk0 / walk1 / walk2 / interact / hit
state:      alive / dead / ghost

示例：
  chr_inspector_front_idle_alive.png
  chr_enforcer_right_walk1_alive.png
  chr_fixer_front_dead.png
  chr_tech_back_ghost.png
```

### 地图 Tileset
```
格式：tile_{map}_{room}_{type}_{variant}.png

map:    harbour / police / kowloon
room:   container_yard / customs / cctv / diner / night_market / electric / back_alley / clinic / ...
type:   floor / wall / door / prop / overlay
variant: 01..99

示例：
  tile_harbour_container_yard_floor_01.png
  tile_harbour_diner_wall_01.png
  tile_police_interrogation_room_prop_01.png
```

### 道具
```
格式：prop_{name}_{state}.png

name:  crate / barrel / desk / cabinet / evidence_box / terminal / keypad / scanner / ...
state: idle / highlight / destroyed

示例：
  prop_evidence_box_idle.png
  prop_terminal_highlight.png
```

### UI
```
格式：ui_{category}_{name}_{state}.png

category: button / panel / icon / card / bar / badge
state:    normal / hover / pressed / disabled / active

示例：
  ui_button_vote_normal.png
  ui_panel_hud_bg.png
  ui_icon_task_wire.png
  ui_card_player_avatar.png
```

### VFX
```
格式：vfx_{type}_{frame}.png

type:  blood / kill / blackout / lockdown / commjam / evidence_leak / patrol_alert / skill_{name}
frame: 00..99

示例：
  vfx_blood_splatter_00.png
  vfx_skill_inspector_interrogate_00.png
```

---

## 第三方资产 License 审计清单

### Kenney 资产（需逐包记录）

| 包名 | License | 用途 | 是否发布 |
|------|---------|------|---------|
| 待审计 | CC0/CC-BY | 概念参考 | 否 |
| 待审计 | CC0/CC-BY | 概念参考 | 否 |

### Quaternius 资产

| 包名 | License | 用途 | 是否发布 |
|------|---------|------|---------|
| 待审计 | CC0 | 概念参考 | 否 |

### 审计规则

1. CC0 → 可直接使用，需保留作者署名
2. CC-BY → 需署名，检查署名位置是否合适
3. CC-BY-SA → 需署名+同方式共享 → 建议避免
4. CC-BY-NC → 禁止商业用途 → 必须排除
5. Asset Store EULA → 按具体 EULA 条款

### 发布依赖清单（最终）

所有正式发布资产必须：
- [ ] 在 `Assets/_Project/Art/` 目录下（非 ThirdParty）
- [ ] 有清晰的来源记录（作者、license、获取方式）
- [ ] 命名符合规范
- [ ] 导入设置经过检查
- [ ] 不被 `.gitignore` 忽略

---

## 构建验证清单

### Clean Checkout 构建

```bash
# 1. 确认项目不需要 ThirdParty/AssetStore 目录
rm -rf Assets/_Project/Art/ThirdParty/
rm -rf Assets/_Project/Resources/AssetStore/

# 2. 打开 Unity 项目
# 3. 检查 Console 无红色错误
# 4. 检查所有场景可加载（无 Missing Prefab/Missing Script）
# 5. 运行 PlayMode 测试
```

### 资源引用扫描

```bash
# 查找非法发布依赖
rg "ThirdParty|AssetStore" Assets/_Project/Scripts/ --type cs
# 期望：无结果（或只在注释中作为概念参考）

# 查找缺失资源引用
rg "Resources.Load" Assets/_Project/Scripts/ --type cs
# 期望：有对应的程序化 fallback
```

### 性能预算

| 指标 | 目标 | 当前 |
|------|------|------|
| 所有 Sprite Atlas 总内存 | < 2MB | ~0MB（全程序化） |
| Draw Call（游戏画面） | < 50 | 待测 |
| 场景 GameObject 数 | < 500 | 待测 |
| 启动时间 | < 5s | 待测 |
| 帧率（macOS 基准） | > 60fps | 待测 |

---

## 后续步骤

1. **本周**：程序化资产继续作为主力，开发功能不等待美术
2. **E3 启动时**：外部像素艺术家按本规范交付 tileset，直接放入对应目录
3. **E6 启动时**：UI 组件按本规范入 `Assets/_Project/Art/2D/UI/`
4. **E8 收尾时**：完成第三方 license 审计，写入 `Assets/_Project/Art/ThirdParty/LICENSE.md`
