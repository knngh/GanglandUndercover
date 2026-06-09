# Gangland Undercover — 全局 2D 美术 & 音频整改方案

> **日期**: 2026-06-07 | **修正**: 2026-06-08 | **原则**: 不改架构，不打大文件，精准手术式增强

---

# 第一部分：2D 美术整改

## 一、地图系统（最大短板 → 最大提升）

### 现状
```
地图 = 纯色矩形 + 单张 32px sprite 拉伸到 24 米
走廊 = CreateProp("走廊", pos, 6m×1m, darkGray)     纯灰色块
房间 = CreateShapeProp("房间", 32px-tile, pos, area)   32px→大面积，糊
地板 = CreateProp("暗区", 26m×16m, #080809)            死黑矩形
```

### 方案：五步地图美化

#### A1 — 走廊 Tile 重复铺贴 (P0)
**改**: `CreateCorridorSegment()` — 单张 32px 拉伸 → 多张 tile 重复铺贴
```
Before: CreateProp("走廊", center, 6×1m, gray)
After:  用 1m×0.25m 的 tile sprite 沿走廊方向重复 24 段
        每个 tile = CC0 地板砖纹理，FilterMode.Point
```
**效果**: 走廊立刻有像素地板纹理，不再是纯色块
**改量**: ~25 行

#### A2 — 房间地板 Tile 填充 (P0)
**改**: 大房间背景（`CreateFloor()` 中的"暗区"矩形）用 tiled sprite
```
Before: CreateProp("暗区", pos, 26m×16m, #080809)    纯黑色 26 米矩形
After:  拉伸 sprite 但设置 SpriteRenderer.drawMode = Tiled
        用 CC0 地板 tile 做底，tileSize = 1m×1m
```
**效果**: 整个地板区域有棋盘格纹理
**改量**: ~15 行

#### A3 — 墙壁厚度感 (P0)
**改**: 走廊和房间边缘加 0.12m 深色边框
```
CreateProp("走廊上沿", center + up, width×0.12m, darkBorder)
+ 下沿/左沿/右沿
```
**效果**: 空间有立体感，不再是平面色块漂浮
**改量**: ~20 行

#### A4 — 调色板港区化 (P1)
**改**: 全部硬编码颜色 → 统一到 3 套调色板
```
Harbour:   地 #0a0c0f 墙 #1a1e22 强调 #1e3a4a (潮湿蓝灰港区)
Police:    地 #0f0f11 墙 #1e1e22 强调 #1a1a2e (冷峻警署)
Kowloon:  地 #0d0a08 墙 #1f1814 强调 #3a1a1a (暖褐九龙城寨)
```
**效果**: 三张地图视觉可区分
**改量**: 3 个 static Color[] 数组，~20 行

#### A5 — 行动照明层 (P1)
**2026-06-08 修正**: 不再做随机高饱和霓虹光斑；改为低饱和警务行动照明。
**改**: 关键房间/封控点/主走廊放置冷光、琥珀灯带、红色封控灯
```
CreateShapeProp("行动照明 指挥车冷光洗地", pos, washScale, commandBlue)
CreateShapeProp("行动照明 封控灯带", entrance, stripScale, amber/restrictedRed)
```
**效果**: 保留夜间行动氛围，但避免廉价赛博霓虹感
**状态**: ✅ 港区主地图已落地，含 EditMode 回归

### 地图整改总结
| 项 | 效果 | 行数 |
|----|------|------|
| A1 走廊 tile | 像素地板纹理 | ~25 |
| A2 房间 tile | 地板棋盘格 | ~15 |
| A3 墙壁厚度 | 立体空间感 | ~20 |
| A4 调色板 | 三图可区分 | ~20 |
| A5 行动照明 | 夜间行动氛围 | 已落地 |
| **合计** | | **~95 行** |

---

## 二、角色视觉增强

### B1 — 脚下阴影 (P1)
**改**: 每个角色 Create 时加一个子 GameObject
```
阴影 = CreateShapeProp("Shadow", SoftCircle, playerPos-0.08z, 0.45m, black/0.35)
```
**效果**: 角色不再漂浮，有地面锚定感
**改量**: ~8 行（在角色创建处加一行）

### B2 — 淘汰/鬼魂视觉 (P1)
**改**: GhostMode 激活时
```
spriteRenderer.color = new Color(0.3f, 0.3f, 0.35f, 0.5f);  // 半透明灰紫
```
**效果**: 被淘汰角色一目了然
**改量**: ~5 行

### B3 — 角色边框色 (P2)
**改**: 根据 OnlineRole 给角色加 1px 边框
```
Police → NeonBlue outline
Gang/Mole → NeonRed outline
Undercover → NeonAmber outline
```
**效果**: 远距离也能辨认阵营
**改量**: ~10 行（在 sprite 创建时设置 outline shader/material）

---

## 三、UI 视觉增强

### C1 — 按钮按下/悬停状态 (P1)
**改**: Button 加 ColorTint transition
```
btn.colors = { normal:neutral, highlighted:bright, pressed:dark, disabled:dim }
```
**效果**: 按钮有反馈感
**改量**: ~8 行（在 CreateButton 中）

### C2 — 进度条发光 (P2)
**改**: EvidenceBar/KillCooldown 填充色用渐变
```
fillImage.color = Color.Lerp(NeonRed, NeonGreen, ratio);
```
**效果**: 冷却/进度可视化更强
**改量**: ~5 行

---

## 四、VFX 视觉增强

### D1 — 血迹从圆→泼溅精灵 (P2)
**改**: `DrawBloodPool` → 用 4 张程序化泼溅纹理随机选一
```
血迹纹理: 径向渐变 + Perlin noise → 不规则血斑
```
**效果**: 看起来像真的血，不是红色椭圆
**改量**: ~20 行（改 DrawBloodPool 方法）

### D2 — 破坏黑灯红色脉冲 (P2)
**改**: Blackout 激活时在相机上叠加红色脉动 overlay
```
Canvas overlay: RawImage + pulsing alpha (sin wave, 0.05→0.15, 2s cycle)
```
**效果**: 黑灯时屏幕有危险红色呼吸感
**改量**: ~15 行

### D3 — 任务完成粒子爆发 (P3)
**改**: 任务完成位置生成 6-8 个向上飘散的小方块
```
Color: task完成=NeonGreen, 击杀=NeonRed
Duration: 0.8s, 向上+随机横向偏移
```
**效果**: 任务完成有视觉庆祝反馈
**改量**: ~25 行

---

# 第二部分：音频整改

## 五、音频系统升级

### 现状问题
- AudioManager 所有 clip 是 `[SerializeField]`，需手动 Editor 赋值
- 同一首 BGM 循环整个对局（无分层/无变化）
- 16 个 SFX 音质未知，缺少空间感
- 无音频过渡（BGM 切换生硬）
- Free Pack 309MB 中 90% 未使用

### 方案

#### E1 — AudioManager Resources 自动加载 fallback (P0)
**改**: `Awake()` 中未赋值的 clip → `Resources.Load<AudioClip>()` 自动加载
```csharp
if (killClip == null) killClip = Resources.Load<AudioClip>("Audio/SFX/SFX_Kill");
if (bodyReportClip == null) bodyReportClip = Resources.Load<AudioClip>("Audio/SFX/SFX_BodyReport");
// ... 全部 16 个 SFX + 3 BGM + 2 Ambience
```
**效果**: 不需要 Editor 手动赋值，开箱即有声
**改量**: ~30 行

#### E2 — BGM 淡入淡出过渡 (P0)
**改**: `PlayMusic(MusicTrack track)` 方法
```csharp
// 当前 BGM volume → 0 over 0.8s, 然后切换, 新 BGM 0→target over 0.8s
StartCoroutine(FadeMusic(current, next, 0.8f));
```
**效果**: BGM 切换顺滑
**改量**: ~20 行

#### E3 — SFX 随机变体 (P1)
**效果**: 同样事件每次播放略微不同的音效变体
```
Play(SoundEffect.Footstep) → 从 4 个 footstep 变体中随机选
pitch = Random.Range(0.95f, 1.05f);  // 微小音调变化
```
**效果**: 重复声音不机械
**改量**: ~15 行

#### E4 — BGM 动态分层 (P2)
**改**: 行动 BGM 分 3 层，根据证据进度叠加
```
Layer1 (base): 始终播放 — 低调氛围打击乐
Layer2 (evidence≥50%): 叠加 — 紧迫弦乐
Layer3 (evidence≥75%): 叠加 — 警笛/警报音
```
**效果**: 对局越紧张音乐越激烈
**改量**: ~35 行（需要 3 个 AudioSource 同时播放）

#### E5 — 空间化音效 (P2)
**改**: 脚步声/报告/击杀 → `PlaySpatial(SoundEffect.Footstep, worldPosition)`
```
AudioSource.spatialBlend = 1.0f (3D)
position = worldPosition
maxDistance = 8m
```
**效果**: 脚步声有方向，能听出远处有人
**改量**: ~20 行（添加 PlaySpatial 重载）

#### E6 — Free Pack 清理 (P1)
**操作**: 保留 8 个被代码引用的 WAV，其余 43 个移出 Resources
**效果**: Resources 再省 ~200MB
**工作量**: 文件操作，~5 分钟

---

# 总览

## 美术整改

| 类别 | 项数 | 行数 | 视觉提升 |
|------|------|------|---------|
| 地图 (A1-A5) | 5 | ~95 | 🔴→🟢 最大 |
| 角色 (B1-B3) | 3 | ~23 | 🟡→🟢 |
| UI (C1-C2) | 2 | ~13 | 🟢→🟢+ |
| VFX (D1-D3) | 3 | ~60 | 🔴→🟡 |
| **美术合计** | **13** | **~190** | |

## 音频整改

| 类别 | 项数 | 行数 | 听觉提升 |
|------|------|------|---------|
| E1 Resources 自动加载 | 1 | ~30 | 🔴→🟢 |
| E2 BGM 淡入淡出 | 1 | ~20 | 🟡→🟢 |
| E3 SFX 变体 | 1 | ~15 | 🟡→🟢 |
| E4 BGM 动态分层 | 1 | ~35 | 🟡→🟢 |
| E5 空间化音效 | 1 | ~20 | 🔴→🟢 |
| E6 Free Pack 清理 | 1 | 0 (文件操作) | 体积 |
| **音频合计** | **6** | **~120** | |

## 总计: 19 项，~310 行代码，零新文件，零架构改动

---

## 实施顺序建议

```
Session 1 (地图P0): A1+A2+A3 → 地图立即可见纹理
Session 2 (音频P0): E1+E2 → 开箱即有声+BGM过渡
Session 3 (氛围):   A4+A5+B1 → 三图区分+霓虹+阴影
Session 4 (细节):   B2+B3+C1+D1+D2 → 角色/VFX/UI打磨
Session 5 (音频P1): E3+E6 → SFX变体+清理
Session 6 (最终):   C2+D3+E4+E5 → 收尾
```
