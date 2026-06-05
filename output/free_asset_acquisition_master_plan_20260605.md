# 多源免费资产采集与自动生成方案

> 日期：2026-06-05  
> 前提：无外部艺术家/音效师，单人全栈推进  
> 策略：CC0/开放许可采集 + Python PIL 像素编辑 + 程序化补全

---

## 一、资源可行性评估

### 1.1 现实判断

| 需求类别 | 能否找到现成CC0？ | 现实应对 |
|----------|:--:|---------|
| 警察/黑帮角色精灵（64×64 top-down） | ❌ 几乎不可能 | **采集通用角色底模 + PIL重着色** |
| 港区/警署/城寨 tileset（32×32） | ⚠️ 部分可用 | 采集科幻/都市 tileset + 主题色重映射 |
| UI 像素组件（按钮/面板/图标） | ✅ 大量可用 | freegamesprites + Kenney UI 直接使用 |
| VFX 像素帧（破坏/击杀特效） | ⚠️ 部分可用 | 采集通用爆炸/火花 + 程序化生成帧 |
| 核心 SFX（UI/事件/投票/结算） | ✅ 大量可用 | OpenGameArt 直接下载 |
| 环境音（雨夜/室内/霓虹） | ✅ 大量可用 | Freesound + Pixabay |
| BGM（探索/紧张/投票/胜利） | ⚠️ 需筛选 | Pixabay + OpenGameArt 筛选后循环 |
| 脚步声/门/破坏音效 | ✅ 大量可用 | OpenGameArt 直接下载 |

### 1.2 关键资源库概览

| 资源库 | 类型 | 许可证 | 规模 | 对Gangland匹配度 |
|--------|------|--------|------|:--:|
| **OpenGameArt.org** | 音效大全 | CC0 | 90+包,~5000文件 | ⭐⭐⭐⭐⭐ |
| **FreeGameSprites.com** | 像素精灵 | CC0 | 20K+文件 | ⭐⭐⭐ |
| **Kenney.nl** | 通用游戏资产 | CC0 | ~300包 | ⭐⭐⭐ |
| **Pixabay** | 音效/环境音 | CC0/免版税 | 600万+ | ⭐⭐⭐⭐⭐ |
| **Freesound.org** | 音效 | CC0/CC-BY | 50万+ | ⭐⭐⭐⭐ |
| **Itch.io (tag:pixel-art + CC0 + Free)** | 像素艺术 | CC0为主 | 海量 | ⭐⭐⭐ |
| **Sonniss GDC Packs** | 音效年包 | 免版税可商用 | ~50GB/年 | ⭐⭐⭐⭐⭐ |
| **CraftPix.net (free区)** | 像素精灵/tileset | 免费可商用 | 数十包 | ⭐⭐⭐ |

---

## 二、总体策略：三线并行

```
美术线 ── 采集CC0底模 → PIL重着色/尺寸调整 → 程序化补全缺失帧 → Unity集成
音频线 ── 批量下载CC0音效 → sox/ffmpeg格式转换 → 裁剪/循环编辑 → Unity集成
管线线 ── Python工具脚本 → 批量处理 → 命名规范 → 自动导入验证
```

### 2.1 美术线策略：采集 + PIL重着色

**核心思路**：无法找到"警察像素精灵"，但可以找到 top-down 的通用角色底模，然后通过 Python PIL 进行：
- **颜色板替换**：将底模的色板映射到 Art Bible 定义的 Police Blue / Gang Red / Undercover Purple / Mole Grey
- **头饰/细节叠加**：在底模上叠加帽子、徽章、武器等小图层
- **尺寸统一**：将所有采集素材统一到 64×64（角色）/ 32×32（tile）规格
- **帧生成**：从 idle 帧通过 PIL 变换生成 walk 帧的近似版本

**适用源**：
1. FreeGameSprites.com — 筛选 tag:characters + tag:pixel-art 的角色底模
2. OpenGameArt.org — CC0 角色 sprite sheet
3. Itch.io — CC0 top-down character packs
4. Kenney.nl — Tiny Dungeon / 1-Bit Pack 等可修改的基础造型

### 2.2 音频线策略：批量采集 + 格式转换

**核心思路**：OpenGameArt 和 Freesound 有海量 CC0 音效，可以直接匹配 80%+ 的 A1-A4 需求。需要做的是：
- **精确搜索**：按音效类别搜索 → 试听 → 筛选 → 下载
- **格式统一**：WAV/MP3 → OGG (mono 44.1kHz SFX, stereo 44.1kHz Ambience/BGM)
- **裁剪/循环**：用 ffmpeg 裁剪过长音效，制作无缝循环环境音
- **响度标准化**：用 ffmpeg loudnorm 统一响度到 -16 LUFS

### 2.3 不可采集的部分 → 程序化生成

| 类别 | 不可采集原因 | 程序化方案 |
|------|------------|-----------|
| 7职业22帧/职业的完整动画 | 无现成匹配主题的包 | PIL 从 idle 帧生成 walk 帧变体 |
| 任务站4状态视觉 | 太具体，无匹配 | Python 生成几何像素形状 + 状态色覆盖 |
| 港区电房 tileset | 无匹配 | Python PIL 绘制规则几何 tile（配电柜=矩形+细节） |
| BGM（香港警匪主题） | 无匹配 | 采集通用 tension/action BGM + 循环编排 |
| 破坏特效自定义帧 | 太具体 | 采集通用爆炸/火花 + PIL 颜色替换 |

---

## 三、美术资产：分阶段执行计划

### 阶段 P1：工具链搭建（本周，3-4h）

```
目标：Python 像素处理管线就绪

1. 安装依赖
   pip install Pillow numpy requests

2. 编写核心工具脚本 scripts/pixel_pipeline.py
   - load_sprite(path) → PIL Image
   - recolor_sprite(image, palette_map: dict) → PIL Image  
   - resize_sprite(image, target_w, target_h, scale_mode) → PIL Image
   - generate_walk_frames(idle_img, num_frames=4, offset=2) → list[Image]
   - save_sprite_sheet(frames, output_path, cols=4)
   - overlay_sprite(base_img, overlay_img, x, y) → PIL Image

3. 编写 Art Bible 色板常量 scripts/gangland_palette.py
   - POLICE_BLUE = (45, 111, 186)
   - GANG_RED = (192, 57, 43)
   - UNDERCOVER_PURPLE = (142, 68, 173)
   - MOLE_GREY = (149, 165, 166)
   - HARBOUR_NIGHT_BG = (26, 28, 44)
   - ...完整色板
```

### 阶段 P2：角色精灵采集+改造（第1-2周，8-10h）

```
目标：7职业 × 4方向 idle 帧（28帧）到位
     后续 P3 扩展到 walk 帧

采集清单：
□ 从 itch.io 搜索 "CC0 top-down character" → 下载 3-5 个底模包
□ 从 freegamesprites.com 筛选 tag:characters + pixel → 下载可用底模
□ 从 OpenGameArt 搜索 CC0 character sprite sheet → 下载

改造流程（每个职业）：
1. 选取最匹配的底模（体型/比例接近）
2. PIL recolor_sprite() 应用职业色板
3. 叠加头饰图层（警帽/兜帽/头盔等，用 PIL overlay）
4. 输出 4 方向 PNG 到 Assets/_Project/Art/2D/Characters/
5. 对照 Art Bible §8 验收

职业→底模匹配策略：
┌──────────────────┬──────────────────────────────────┐
│ 职业              │ 底模策略                          │
├──────────────────┼──────────────────────────────────┤
│ Inspector(警察)   │ 通用站立底模 + 警帽+徽章overlay    │
│ Enforcer(打手)    │ 同上底模 + 深色皮衣+墨镜overlay    │
│ Undercover(卧底)  │ 同上底模 + 兜帽overlay            │
│ Tech(技术员)      │ 同上底模 + 耳机+平板overlay        │
│ Medic(医护)       │ 同上底模 + 白大褂+十字overlay      │
│ Driver(司机)      │ 同上底模 + 棒球帽+手套overlay      │
│ Mole(内鬼)        │ 复用Undercover底模 + 暗红色调       │
└──────────────────┴──────────────────────────────────┘

PIL overlay 图层清单（需要手绘或用 PIL 绘制）：
- 警帽：8×12 深蓝色几何方块
- 徽章：6×8 金色小矩形
- 墨镜：10×4 黑色横条
- 兜帽：14×14 深灰三角轮廓
- 白大褂：从底模提取轮廓后 PIL flood fill 白色
- 棒球帽：10×6 深红半圆
- 十字标记：4×4 红色十字（PIL 像素点阵）
```

### 阶段 P3：角色动画帧扩展（第2-3周，6-8h）

```
目标：从 idle 4方向 → walk 4方向 × 4帧/方向 = 112帧

策略：不追求完美动画 → 使用 PIL 程序化位移生成简易 walk 帧

generate_walk_frames() 算法：
1. 将 idle 帧垂直切成 4 段（头/躯干/腿左/腿右）
2. 按正弦波偏移每段的 x 坐标（±2px）
3. 生成 4 帧循环，形成"原地踏步"效果
4. 每个方向独立生成

验收标准：
- 远距离（3+ tile）可识别职业轮廓
- 4 帧循环无视觉跳帧
- 色板与 idle 帧一致
```

### 阶段 P4：Tileset 采集+改造（第3-5周，12-15h）

```
目标：3 张地图完整 tileset（港区~106 + 警署~55 + 城寨~62 = ~223 tiles）

采集策略：
□ Itch.io 搜索 "CC0 pixel tileset urban/modern/sci-fi"
□ OpenGameArt 搜索 CC0 tileset city/indoor/factory
□ FreeGameSprites 筛选 tag:tilesets + pixel

改造流程：
1. 下载所有匹配的 tileset 包
2. 导入 PIL → 统一缩放到 32×32（NEAREST 插值保像素感）
3. 应用地图主题色重映射：
   - 港区：全局色相偏移 -15°，饱和度+20%，亮度-10%（港夜霓虹感）
   - 警署：全局色相偏移 +5°，饱和度-15%（冷峻蓝调感）
   - 城寨：全局色相偏移 +10°，饱和度+30%，对比度+20%（霓虹密集感）
4. 输出到 Assets/_Project/Art/2D/Tiles/{Harbour,Police,Kowloon}/

PIL 色相偏移函数：
```python
from PIL import Image, ImageEnhance
import colorsys

def apply_theme(image, hue_shift, sat_factor, bright_factor):
    """对整张 tileset 应用主题色映射"""
    pixels = image.convert('RGBA')
    data = list(pixels.getdata())
    new_data = []
    for r, g, b, a in data:
        if a == 0:
            new_data.append((r, g, b, a))
            continue
        h, s, v = colorsys.rgb_to_hsv(r/255, g/255, b/255)
        h = (h + hue_shift) % 1.0
        s = min(1.0, s * sat_factor)
        v = min(1.0, v * bright_factor)
        nr, ng, nb = colorsys.hsv_to_rgb(h, s, v)
        new_data.append((int(nr*255), int(ng*255), int(nb*255), a))
    pixels.putdata(new_data)
    return pixels
```

### 阶段 P5：任务站道具生成（第5-6周，8-10h）

```
目标：11种任务站 × 4状态（idle/active/complete/sabotaged）= 44 sprite

策略：纯 Python PIL 几何绘制（不需要采集底模）

每种任务站的 PIL 绘制规格：
├── Wire/Repair     → 矩形配电箱(48×64) + 线缆路径(16×8) + 火花粒子(8×8)
├── Keypad          → 矩形面板(32×48) + 数字按键(4×4×12) + LCD屏(24×12)
├── SwipeCard       → 读卡器(24×32) + 卡槽(16×4)
├── Scan            → 平板扫描仪(48×32) + 证物袋(16×24)
├── Download        → CRT终端(40×48) + 进度条(32×4)
├── Sort            → 档案柜(48×64) + 分拣台(32×16)
├── Memory          → 3×3灯板(36×36) + 底座(40×8)
├── Tap             → 控制台(40×32) + 节奏灯(8×8×4)
├── Calibrate       → 仪表面板(48×48) + 4旋钮(8×8×4)
├── RadarTracking   → 雷达屏(40×40) + 控制台(48×24)
└── EvidenceArchive → 证据柜(48×64) + 照片墙(40×32)

4 状态色规则：
- idle: 中性灰/金属色 (r=128,g=128,b=128)
- active: 青绿色辉光 (叠加 #1a9eaa 半透明)
- complete: 绿色确认 (叠加 #27ae60 半透明)
- sabotaged: 红色报警 (叠加 #c0392b 半透明 + 火花覆盖)

每帧输出为单独的 PNG，命名规范：
tile_task_{type}_{state}.png → 例 tile_task_wire_active.png
```

### 阶段 P6：UI 组件采集（第4-5周，4-6h）

```
目标：~88 UI 组件（按钮/面板/图标/进度条）

采集策略（相对容易，UI 素材通用性强）：
□ Kenney.nl → "UI Pack", "Input Prompts", "Interface Sounds"
□ FreeGameSprites → 筛选 tag:ui + pixel
□ Itch.io → "CC0 pixel UI pack"

改造：
- 颜色替换为 Art Bible 色板（PoliceBlue 按钮/GangRed 警告等）
- 尺寸调整为适合 Canvas 的规格
- 输出到 Assets/_Project/Art/2D/UI/
```

### 阶段 P7：VFX 精灵采集+生成（第5-6周，3-4h）

```
目标：5种破坏特效 + 击杀特效 + 其他 VFX ≈ 32 sprites

采集策略：
□ OpenGameArt → CC0 explosion/spark particles
□ FreeGameSprites → tag:vfx + pixel
□ Itch.io → CC0 pixel VFX pack

改造：
- PIL 颜色替换：通用橙色爆炸 → 蓝色电弧（停电）、红色应急灯（锁门）
- 缩放/裁剪到合适尺寸

缺失的 VFX 用 PIL 程序化生成：
- 停电脉冲波：同心圆 + alpha fade
- 通讯干扰波纹：锯齿线 + 随机噪点
- 应急灯脉冲：全屏红色半透明闪烁
```

---

## 四、音频资产：分阶段执行计划

### 阶段 Q1：核心 SFX 批量下载（第1-2周，4-6h）

```
目标：A1 的 15 个 P0 核心 SFX 全部到位

来源：OpenGameArt.org CC0 合集（已验证大量匹配）
  
下载清单 & 匹配关系：
┌─────────────────────┬────────────────────────────────────────────┐
│ 需求文件             │ OpenGameArt 匹配源                         │
├─────────────────────┼────────────────────────────────────────────┤
│ sfx_ui_click.ogg    │ 87 Clickety Clips → 选最短清脆一声         │
│ sfx_ui_confirm.ogg  │ 87 Clickety Clips / Level up, power up    │
│ sfx_ui_error.ogg    │ 3 Pop Sounds → 低频一声                     │
│ sfx_ui_notify.ogg   │ Interface beeps → 双段通知                  │
│ sfx_task_start.ogg  │ 50 RPG sound effects → 面板/交互音          │
│ sfx_task_complete.ogg│ Level up, power up → CEG上行              │
│ sfx_kill.ogg        │ 37 hits/punches → 选最有力的打击音          │
│ sfx_body_report.ogg │ Interface beeps → 警笛变体                  │
│ sfx_emergency.ogg   │ 50 CC0 Sci-Fi SFX → 警报                   │
│ sfx_meeting_start.ogg│ 30 CC0 SFX loops → drone氛围               │
│ sfx_vote_cast.ogg   │ 54 Casino sound effects → 卡牌/筹码音       │
│ sfx_player_ejected.ogg│ 37 hits/punches → 低沉打击                │
│ sfx_victory_police.ogg│ Bell Arpeggio 24 → 选上行片段             │
│ sfx_victory_gang.ogg │ 同上 → 选下行变体                          │
│ sfx_defeat.ogg      │ Demon voice Game Over → 或选低沉变体        │
└─────────────────────┴────────────────────────────────────────────┘

操作流程（每个文件）：
1. 打开对应 OpenGameArt 包页面
2. 试听 → 选最佳候选 → 下载
3. ffmpeg 转 OGG： ffmpeg -i input.wav -c:a libvorbis -ar 44100 -ac 1 output.ogg
4. 裁剪/淡入淡出： ffmpeg -i input.ogg -af "afade=t=in:d=0.01,afade=t=out:d=0.05" output.ogg
5. 响度标准化： ffmpeg -i input.ogg -af "loudnorm=I=-16:LRA=11:TP=-1.5" output.ogg
```

### 阶段 Q2：破坏 + 脚步声（第2-3周，3-4h）

```
目标：A2 全部 21 文件

破坏音效 (16文件)：
┌──────────────────┬────────────────────────────────────┐
│ 破坏类型          │ OpenGameArt 匹配源                  │
├──────────────────┼────────────────────────────────────┤
│ 停电 (3音效)     │ Machine shutting down + buzz       │
│ 锁门 (3音效)     │ Door Open, Door Close Set          │
│ 通讯干扰 (3音效) │ 50 CC0 Sci-Fi SFX → static/noise   │
│ 证据泄露 (3音效) │ 30 weird CC0 SFX → 选择异样声      │
│ 巡逻警报 (3音效) │ Seamless Energy Emission Loop      │
│ 应急灯 (1音效)   │ Interface beeps → 脉冲变体          │
└──────────────────┴────────────────────────────────────┘

脚步声 (5文件)：
□ Fantozzi's Footsteps → 石头地面
□ Steps in wood floor → 木地板
□ Freesound 补充搜索: "concrete footsteps", "metal grate footsteps"
```

### 阶段 Q3：环境音 + BGM（第3-4周，4-5h）

```
目标：A3 全部 16 文件

环境音源：Pixabay + Freesound（CC0）
- "rain city night ambient" → 港区雨夜环境音
- "police station interior ambient" → 警署室内
- "neon street ambient" → 九龙城寨
- 各截取 60-120s 循环段，ffmpeg 做无缝循环

BGM 源：Pixabay Music（免版税可商用）
搜索关键词：
- "tension cinematic" → 探索/紧张层
- "detective mystery ambient" → 探索层
- "cyberpunk dark ambient" → 城寨氛围
- "action chase tension" → 追逐/威胁层
- "suspense thriller" → 会议/投票层
- "victory orchestral short" → 胜利
- "dark victory" → 黑帮胜利

处理：ffmpeg 裁剪精华段 → 淡入淡出 → 循环适配
```

### 阶段 Q4：任务细节 + 结算变体（第4周，2-3h）

```
A4 补完（从 OpenGameArt 补充）：
- 各小游戏特有音效：使用 50 RPG sound effects / 100 CC0 SFX 系列筛选
- 结算变体：从已有 BGM 素材中裁剪变体段

所有音频文件最终处理：
□ 响度标准化到 -16 LUFS
□ SFX/步声 → mono 44.1kHz .ogg（减小体积）
□ 环境音/BGM → stereo 44.1kHz .ogg
□ 目录结构：
  Assets/_Project/Audio/SFX/Core/
  Assets/_Project/Audio/SFX/Sabotage/
  Assets/_Project/Audio/SFX/Footsteps/
  Assets/_Project/Audio/SFX/Tasks/
  Assets/_Project/Audio/Ambience/
  Assets/_Project/Audio/BGM/
```

---

## 五、自动化管线脚本

### 5.1 pixel_pipeline.py

```python
#!/usr/bin/env python3
"""Gangland Undercover 像素资产处理管线"""

from PIL import Image, ImageEnhance, ImageFilter
import colorsys
import os
import sys

# === Art Bible 色板常量 ===
PALETTE = {
    "POLICE_BLUE": (45, 111, 186),
    "GANG_RED": (192, 57, 43),
    "UNDERCOVER_PURPLE": (142, 68, 173),
    "MOLE_GREY": (149, 165, 166),
    "HARBOUR_NIGHT_BG": (26, 28, 44),
    "NEON_YELLOW": (244, 162, 54),
    "NEON_CYAN": (26, 158, 170),
    "EMERGENCY_RED": (192, 57, 43),
}

def load_sprite(path):
    """加载精灵图，保持 RGBA"""
    return Image.open(path).convert('RGBA')

def recolor_sprite(image, color_map):
    """
    将图像中的旧颜色映射到新颜色
    color_map: {(old_r, old_g, old_b): (new_r, new_g, new_b)}
    使用容差匹配
    """
    pixels = image.load()
    w, h = image.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = pixels[x, y]
            if a == 0:
                continue
            for (or_, og, ob), (nr, ng, nb) in color_map.items():
                if abs(r-or_) < 30 and abs(g-og) < 30 and abs(b-ob) < 30:
                    pixels[x, y] = (nr, ng, nb, a)
                    break
    return image

def apply_theme(image, hue_shift=0, sat_factor=1.0, bright_factor=1.0):
    """对整个图像应用主题色映射"""
    pixels = image.load()
    w, h = image.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = pixels[x, y]
            if a == 0:
                continue
            hsv_h, hsv_s, hsv_v = colorsys.rgb_to_hsv(r/255, g/255, b/255)
            hsv_h = (hsv_h + hue_shift) % 1.0
            hsv_s = min(1.0, hsv_s * sat_factor)
            hsv_v = min(1.0, hsv_v * bright_factor)
            nr, ng, nb = colorsys.hsv_to_rgb(hsv_h, hsv_s, hsv_v)
            pixels[x, y] = (int(nr*255), int(ng*255), int(nb*255), a)
    return image

def resize_sprite(image, target_w, target_h):
    """像素艺术缩放（NEAREST 保持像素感）"""
    return image.resize((target_w, target_h), Image.NEAREST)

def overlay_sprite(base, overlay, x, y):
    """将 overlay 叠加到 base 的 (x,y) 位置"""
    result = base.copy()
    result.paste(overlay, (x, y), overlay)
    return result

def generate_walk_frames(idle_img, num_frames=4, amplitude=2):
    """
    从 idle 帧生成简易 walk 帧
    策略：垂直分段 + 正弦波 x 偏移
    """
    w, h = idle_img.size
    frames = []
    # 切成 4 段：头、躯干、左腿、右腿
    section_h = h // 4
    for i in range(num_frames):
        frame = Image.new('RGBA', (w, h), (0,0,0,0))
        phase = 2 * 3.14159 * i / num_frames
        offset = int(amplitude * __import__('math').sin(phase))
        
        # 头部（不动）
        head = idle_img.crop((0, 0, w, section_h))
        frame.paste(head, (0, 0))
        
        # 躯干（微动）
        torso = idle_img.crop((0, section_h, w, section_h*2))
        frame.paste(torso, (offset//2, section_h))
        
        # 左腿（摆动）
        left_leg = idle_img.crop((0, section_h*2, w//2, section_h*3))
        frame.paste(left_leg, (offset//2, section_h*2))
        
        # 右腿（反相摆动）
        right_leg = idle_img.crop((w//2, section_h*2, w, section_h*3))
        frame.paste(right_leg, (w//2 - offset//2, section_h*2))
        
        # 脚（保持）
        feet = idle_img.crop((0, section_h*3, w, h))
        frame.paste(feet, (0, section_h*3))
        
        frames.append(frame)
    return frames

def save_sprite_sheet(frames, output_path, cols=4):
    """将帧列表保存为 sprite sheet"""
    if not frames:
        return
    fw, fh = frames[0].size
    rows = (len(frames) + cols - 1) // cols
    sheet = Image.new('RGBA', (fw*cols, fh*rows), (0,0,0,0))
    for i, frame in enumerate(frames):
        row, col = i // cols, i % cols
        sheet.paste(frame, (col*fw, row*fh))
    os.makedirs(os.path.dirname(output_path) or '.', exist_ok=True)
    sheet.save(output_path)
    print(f"✓ Saved sprite sheet: {output_path}")

def draw_circle_pixels(image, cx, cy, radius, color):
    """在图像上绘制像素圆"""
    pixels = image.load()
    for y in range(max(0,cy-radius), min(image.height,cy+radius+1)):
        for x in range(max(0,cx-radius), min(image.width,cx+radius+1)):
            if (x-cx)**2 + (y-cy)**2 <= radius**2:
                pixels[x, y] = color

def draw_rect_pixels(image, x, y, w, h, color):
    """绘制填充矩形"""
    pixels = image.load()
    for py in range(y, min(image.height, y+h)):
        for px in range(x, min(image.width, x+w)):
            pixels[px, py] = color

# === CLI 入口 ===
if __name__ == "__main__":
    # 示例用法见下方
    print("Gangland Pixel Pipeline v1.0")
    print("Usage: python pixel_pipeline.py <command> <args>")
    print("Commands: recolor, theme, resize, overlay, walk, sheet")
```

### 5.2 audio_pipeline.sh

```bash
#!/bin/bash
# Gangland Undercover 音频处理管线
# 依赖: ffmpeg, sox (可选)

AUDIO_OUT="Assets/_Project/Audio"

# 批量转 OGG + 标准化
normalize_sfx() {
    local input="$1"
    local output="$2"
    ffmpeg -i "$input" \
        -c:a libvorbis -ar 44100 -ac 1 \
        -af "afade=t=in:d=0.01,afade=t=out:d=0.03,loudnorm=I=-16:LRA=11:TP=-1.5" \
        -q:a 4 \
        "$output" -y 2>/dev/null
    echo "✓ $output"
}

# 制作无缝循环环境音
make_loop() {
    local input="$1"
    local output="$2"
    local duration="${3:-120}"
    ffmpeg -i "$input" \
        -t "$duration" \
        -c:a libvorbis -ar 44100 -ac 2 \
        -af "afade=t=in:d=2,afade=t=out:d=2,loudnorm=I=-16:LRA=11:TP=-1.5" \
        -q:a 4 \
        "$output" -y 2>/dev/null
    echo "✓ Loop: $output (${duration}s)"
}

# 裁剪音频段
trim_audio() {
    local input="$1"
    local output="$2"
    local start="${3:-0}"
    local duration="${4:-5}"
    ffmpeg -i "$input" \
        -ss "$start" -t "$duration" \
        -c:a libvorbis -ar 44100 -ac 1 \
        -q:a 4 \
        "$output" -y 2>/dev/null
    echo "✓ Trimmed: $output"
}

# 批量处理目录
process_dir() {
    local src_dir="$1"
    local dst_dir="$2"
    mkdir -p "$dst_dir"
    for f in "$src_dir"/*.{wav,mp3,flac,aiff}; do
        [ -f "$f" ] || continue
        local name=$(basename "${f%.*}")
        normalize_sfx "$f" "$dst_dir/${name}.ogg"
    done
}

echo "Gangland Audio Pipeline v1.0"
echo "Commands: normalize_sfx, make_loop, trim_audio, process_dir"
```

---

## 六、集成检查清单

### 6.1 文件就绪 → Unity Import

```
□ 所有 sprite 放入 Assets/_Project/Art/2D/ 对应子目录
□ 所有 AudioClip 放入 Assets/_Project/Audio/ 对应子目录
□ Unity 中设置 Texture Type = Sprite (2D and UI)
□ 像素精灵设置 Filter Mode = Point (no filter), Compression = None
□ AudioClip 设置 Load Type 按用途：
  - SFX: Decompress On Load (短音效)
  - 环境音/BGM: Compressed In Memory (长音频流式)
```

### 6.2 代码加载路径验证

```
□ Sprite2DAssetCache.cs → 确认 Resources.Load 路径匹配新目录结构
□ GreyboxMapBuilder.cs → 确认 tile 加载路径
□ SabotageVFX.cs → 确认 VFX sprite 加载路径
□ AudioManager.cs → 确认 SFX/Ambience/BGM 加载路径
□ TaskStationController.cs → 确认道具 sprite 加载路径
```

### 6.3 质量验证

```
□ 编译：batchmode 0 error
□ 角色：远距离可区分 7 职业轮廓
□ 地图：3 张地图 tile 无缝隙拼接
□ UI：全部按钮/面板在 Canvas 中正常渲染
□ 音频：全部 .ogg 文件可正常播放
□ 性能：无 AudioClip 内存泄漏
```

---

## 七、分阶段执行里程碑

```
┌─────────┬──────────┬──────────────────────────────┬──────────┐
│ 阶段     │ 耗时      │ 产出                          │ 关键依赖  │
├─────────┼──────────┼──────────────────────────────┼──────────┤
│ P1 工具  │ 3-4h     │ pixel_pipeline.py + palette  │ Python3  │
│ Q1 SFX  │ 4-6h     │ 15 核心 SFX .ogg             │ 网络下载  │
│ P2 角色  │ 8-10h    │ 7职业×4方向 idle (28帧)      │ P1       │
│ Q2 破坏  │ 3-4h     │ 16破坏+5步声 .ogg           │ 网络下载  │
│ P4 港区  │ 4-5h     │ 港区~106 tiles               │ P1       │
│ P6 UI   │ 4-6h     │ ~88 UI 组件                   │ 网络下载  │
│ Q3 环境  │ 4-5h     │ 8环境+8 BGM .ogg             │ 网络下载  │
│ P3 动画  │ 6-8h     │ 7职业 walk 112帧             │ P2       │
│ P5 道具  │ 8-10h    │ 44 道具 sprite               │ P1       │
│ P7 VFX  │ 3-4h     │ ~32 VFX sprite               │ P1+P5    │
│ P4 警署  │ 3-4h     │ 警署~55 tiles                │ P1+P4    │
│ P4 城寨  │ 3-4h     │ 城寨~62 tiles                │ P1+P4    │
│ Q4 收尾  │ 2-3h     │ 12任务+4结算+补充 .ogg       │ Q3       │
│ 集成     │ 3-4h     │ Unity导入验证+编译            │ All      │
├─────────┼──────────┼──────────────────────────────┼──────────┤
│ 总计     │ ~55-70h  │ ~599 美术 + ~70 音频          │          │
└─────────┴──────────┴──────────────────────────────┴──────────┘
```

### 推荐执行顺序（最大化并行）

```
Week 1-2: P1(工具) + Q1(SFX) 并行
          完成后 → P2(角色idle) + Q2(破坏) 并行
Week 2-3: P4(港区tile) + P6(UI采集) 并行
Week 3-4: Q3(环境+BGM) + P3(角色walk) 并行
Week 4-5: P5(道具生成) + P4续(警署+城寨)
Week 5-6: P7(VFX) + Q4(收尾音效)
Week 6  : 集成验证 + 编译 + 质量检查
```

---

## 八、风险与缓解

| 风险 | 概率 | 影响 | 缓解 |
|------|:----:|:----:|------|
| 找不到匹配的角色底模 | 中 | 高 | P1 工具支持纯程序化生成角色（几何拼接） |
| 下载的音效风格不统一 | 高 | 中 | Q1 阶段先出 3 样品评审，统一响度标准化 |
| PIL 简单 walk 帧观感太差 | 中 | 中 | 降级为纯 idle 帧 + 滑动位移（不生成 walk 帧） |
| tileset 拼接有缝隙 | 中 | 高 | P4 每 tile 边缘预留 1px 安全区，PIL 检查对缝 |
| BGM 找不到匹配的 | 高 | 低 | 降级为纯环境音（无 BGM），游戏仍可玩 |

---

## 九、下一步行动

**立即启动 P1（工具链）+ Q1（SFX 下载）**

```bash
# 1. 确保依赖
pip install Pillow requests

# 2. 创建管线脚本
# scripts/pixel_pipeline.py ← 本文档 §5.1
# scripts/audio_pipeline.sh ← 本文档 §5.2

# 3. 创建色板常量
# scripts/gangland_palette.py

# 4. 开始下载 SFX
# 从 OpenGameArt.org 按 Q1 清单逐项下载
```
