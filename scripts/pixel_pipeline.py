#!/usr/bin/env python3
"""Gangland Undercover 像素资产处理管线

功能：
  recolor   — 颜色板替换（色块映射）
  theme     — 地图主题色应用（HSV全局变换）
  resize    — 批量缩放到指定尺寸（NEAREST保持像素感）
  overlay   — 图层叠加（头饰/徽章等）
  walk      — 从idle帧生成walk动画帧
  sheet     — 多帧合并为sprite sheet
  rect      — 绘制纯色矩形像素块
  circle    — 绘制纯色圆形像素块
  taskprop  — 生成任务站道具sprite
  batchprocess — 批量处理整个目录

依赖: pip install Pillow
"""

import os
import sys
import math
import argparse
from PIL import Image, ImageEnhance
import colorsys

# 添加同级目录到 path 以导入色板
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gangland_palette import (
    PROFESSION_COLORS, MAP_THEMES, STATE_COLORS,
    get_profession_palette, get_map_theme, get_state_color,
)

# ============================================================
# 核心函数
# ============================================================

def load_sprite(path):
    """加载精灵图，保持 RGBA 模式"""
    img = Image.open(path)
    if img.mode != 'RGBA':
        img = img.convert('RGBA')
    return img


def recolor_sprite(image, color_map, tolerance=30):
    """
    颜色板替换：将图像中匹配的颜色替换为目标颜色
    
    color_map: {(from_r, from_g, from_b): (to_r, to_g, to_b)}
    tolerance: 颜色匹配容差（0-255）
    """
    pixels = image.load()
    w, h = image.size
    changed = 0
    for y in range(h):
        for x in range(w):
            r, g, b, a = pixels[x, y]
            if a == 0:
                continue
            for (fr, fg, fb), (tr, tg, tb) in color_map.items():
                if (abs(r - fr) <= tolerance and 
                    abs(g - fg) <= tolerance and 
                    abs(b - fb) <= tolerance):
                    pixels[x, y] = (tr, tg, tb, a)
                    changed += 1
                    break
    return image, changed


def apply_theme(image, hue_shift=0, sat_factor=1.0, bright_factor=1.0):
    """HSV 全局变换：色相偏移、饱和度、亮度"""
    pixels = image.load()
    w, h = image.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = pixels[x, y]
            if a == 0:
                continue
            h, s, v = colorsys.rgb_to_hsv(r/255.0, g/255.0, b/255.0)
            h = (h + hue_shift) % 1.0
            s = min(1.0, max(0.0, s * sat_factor))
            v = min(1.0, max(0.0, v * bright_factor))
            nr, ng, nb = colorsys.hsv_to_rgb(h, s, v)
            pixels[x, y] = (int(nr*255), int(ng*255), int(nb*255), a)
    return image


def resize_sprite(image, target_w, target_h):
    """像素艺术缩放：NEAREST 插值保持像素块感"""
    return image.resize((target_w, target_h), Image.NEAREST)


def overlay_sprite(base, overlay, x, y):
    """将 overlay 叠加到 base 的 (x,y) 位置，使用 alpha 通道"""
    result = base.copy()
    result.paste(overlay, (x, y), overlay if overlay.mode == 'RGBA' else None)
    return result


def generate_walk_frames(idle_img, num_frames=4, amplitude=2):
    """
    从 idle 帧生成简易 walk 动画帧
    策略：垂直切3段（头+躯干/左腿/右腿），正弦波 x 偏移
    """
    w, h = idle_img.size
    head_h = h // 3           # 头+躯干
    leg_h = h - head_h        # 腿部总高
    
    frames = []
    for i in range(num_frames):
        frame = Image.new('RGBA', (w, h), (0, 0, 0, 0))
        phase = 2 * math.pi * i / num_frames
        
        # 头部+躯干：微动（上下弹跳）
        body_offset_y = int(amplitude * 0.3 * math.sin(phase * 2))
        body_offset_x = int(amplitude * 0.5 * math.sin(phase))
        body = idle_img.crop((0, 0, w, head_h))
        frame.paste(body, (body_offset_x, body_offset_y), body)
        
        # 左腿：正弦摆动
        left_offset = int(amplitude * math.sin(phase))
        left_leg = idle_img.crop((0, head_h, w // 2, h))
        frame.paste(left_leg, (left_offset, head_h), left_leg)
        
        # 右腿：反相摆动
        right_offset = int(amplitude * math.sin(phase + math.pi))
        right_leg = idle_img.crop((w // 2, head_h, w, h))
        frame.paste(right_leg, (w // 2 + right_offset, head_h), right_leg)
        
        frames.append(frame)
    return frames


def save_sprite_sheet(frames, output_path, cols=4):
    """多帧合并为 sprite sheet"""
    if not frames:
        print("  ⚠ No frames to save")
        return
    fw, fh = frames[0].size
    rows = (len(frames) + cols - 1) // cols
    sheet = Image.new('RGBA', (fw * cols, fh * rows), (0, 0, 0, 0))
    for i, frame in enumerate(frames):
        row, col = i // cols, i % cols
        sheet.paste(frame, (col * fw, row * fh))
    os.makedirs(os.path.dirname(output_path) or '.', exist_ok=True)
    sheet.save(output_path)
    print(f"  ✓ Saved sprite sheet ({len(frames)} frames): {output_path}")


def draw_rect(image, x, y, w, h, color):
    """在图像上绘制填充矩形"""
    pixels = image.load()
    for py in range(y, min(image.height, y + h)):
        for px in range(x, min(image.width, x + w)):
            pixels[px, py] = color


def draw_circle(image, cx, cy, radius, color):
    """在图像上绘制填充圆形"""
    pixels = image.load()
    r2 = radius * radius
    for py in range(max(0, cy - radius), min(image.height, cy + radius + 1)):
        for px in range(max(0, cx - radius), min(image.width, cx + radius + 1)):
            if (px - cx) ** 2 + (py - cy) ** 2 <= r2:
                pixels[px, py] = color


def draw_line(image, x1, y1, x2, y2, color, thickness=1):
    """Bresenham 画线"""
    pixels = image.load()
    dx = abs(x2 - x1)
    dy = abs(y2 - y1)
    sx = 1 if x1 < x2 else -1
    sy = 1 if y1 < y2 else -1
    err = dx - dy
    cx, cy = x1, y1
    while True:
        for t in range(-thickness//2, thickness//2 + 1):
            px, py = cx + t, cy + t
            if 0 <= px < image.width and 0 <= py < image.height:
                pixels[px, py] = color
        if cx == x2 and cy == y2:
            break
        e2 = 2 * err
        if e2 > -dy:
            err -= dy
            cx += sx
        if e2 < dx:
            err += dx
            cy += sy


# ============================================================
# 任务站道具生成
# ============================================================

TASK_PROP_SPECS = {
    "wire": {
        "name": "Wire/Repair 配电箱",
        "size": (48, 64),
        "draw": lambda img, state: _draw_wire(img, state),
    },
    "keypad": {
        "name": "Keypad 数字键盘",
        "size": (32, 48),
        "draw": lambda img, state: _draw_keypad(img, state),
    },
    "swipecard": {
        "name": "SwipeCard 读卡器",
        "size": (32, 40),
        "draw": lambda img, state: _draw_swipecard(img, state),
    },
    "scan": {
        "name": "Scan 平板扫描仪",
        "size": (48, 36),
        "draw": lambda img, state: _draw_scan(img, state),
    },
    "download": {
        "name": "Download CRT终端",
        "size": (48, 56),
        "draw": lambda img, state: _draw_download(img, state),
    },
    "sort": {
        "name": "Sort 档案分拣",
        "size": (48, 64),
        "draw": lambda img, state: _draw_sort(img, state),
    },
    "memory": {
        "name": "Memory 记忆灯板",
        "size": (40, 48),
        "draw": lambda img, state: _draw_memory(img, state),
    },
    "tap": {
        "name": "Tap 按钮控制台",
        "size": (44, 36),
        "draw": lambda img, state: _draw_tap(img, state),
    },
    "calibrate": {
        "name": "Calibrate 仪表校准",
        "size": (52, 52),
        "draw": lambda img, state: _draw_calibrate(img, state),
    },
    "radar": {
        "name": "RadarTracking 雷达追踪",
        "size": (48, 56),
        "draw": lambda img, state: _draw_radar(img, state),
    },
    "evidence": {
        "name": "EvidenceArchive 证据档案",
        "size": (48, 64),
        "draw": lambda img, state: _draw_evidence(img, state),
    },
}

def _get_glow(state):
    """获取状态辉光色"""
    return get_state_color(state)


def _draw_wire(img, state):
    """配电箱：金属矩形 + 线缆 + 火花"""
    color = _get_glow(state)
    # 箱体
    draw_rect(img, 4, 8, 40, 48, (60, 65, 70, 255))
    draw_rect(img, 6, 10, 36, 44, (80, 85, 90, 255))
    # 指示灯
    draw_circle(img, 24, 16, 3, color + (200,))
    # 线缆
    draw_line(img, 44, 20, 48, 36, (40, 45, 50, 255), 2)
    draw_line(img, 44, 44, 48, 60, (40, 45, 50, 255), 2)
    if state == "active":
        # 火花粒子
        for ox, oy in [(38, 18), (42, 24), (36, 30)]:
            draw_circle(img, ox, oy, 2, (244, 162, 54, 200))
    if state == "sabotaged":
        for ox, oy in [(38, 18), (42, 24), (36, 30), (20, 50), (30, 55)]:
            draw_circle(img, ox, oy, 2, (192, 57, 43, 200))


def _draw_keypad(img, state):
    """壁挂数字键盘"""
    color = _get_glow(state)
    draw_rect(img, 2, 2, 28, 44, (50, 55, 60, 255))
    draw_rect(img, 4, 4, 24, 40, (70, 75, 80, 255))
    # LCD屏
    draw_rect(img, 6, 6, 20, 10, (40, 50, 55, 255))
    draw_rect(img, 8, 8, 16, 6, (26, 158, 170, 150) if state == "active" else (30, 40, 45, 255))
    # 数字键 4×3
    for row in range(4):
        for col in range(3):
            dx, dy = 6 + col * 7, 18 + row * 6
            draw_rect(img, dx, dy, 5, 4, (90, 95, 100, 255))
    if state == "sabotaged":
        draw_rect(img, 8, 8, 16, 6, (192, 57, 43, 180))


def _draw_swipecard(img, state):
    """门禁读卡器"""
    color = _get_glow(state)
    draw_rect(img, 4, 2, 24, 36, (55, 60, 65, 255))
    draw_rect(img, 6, 4, 20, 10, (40, 45, 50, 255))
    # LED指示灯
    draw_circle(img, 10, 8, 2, color + (255,))
    draw_circle(img, 22, 8, 2, (39, 174, 96, 255) if state == "complete" else (100, 105, 110, 255))
    # 刷卡槽
    draw_rect(img, 8, 20, 16, 4, (30, 35, 40, 255))
    if state == "active":
        draw_rect(img, 5, 0, 22, 2, (26, 158, 170, 180))


def _draw_scan(img, state):
    """平板扫描仪"""
    draw_rect(img, 0, 8, 48, 24, (60, 65, 70, 255))
    draw_rect(img, 2, 10, 44, 20, (50, 55, 60, 255))
    draw_rect(img, 4, 12, 40, 12, (80, 85, 90, 255))
    if state == "active":
        draw_line(img, 4, 18, 44, 18, (26, 158, 170, 150), 2)
    # 证物袋（右侧）
    draw_rect(img, 30, 2, 14, 6, (200, 190, 170, 200))
    draw_line(img, 37, 5, 37, 8, (150, 140, 120, 200), 1)


def _draw_download(img, state):
    """CRT终端"""
    color = _get_glow(state)
    # 显示器
    draw_rect(img, 6, 4, 36, 32, (40, 45, 50, 255))
    draw_rect(img, 8, 6, 32, 28, (30, 35, 40, 255))
    if state == "active":
        draw_rect(img, 10, 8, 28, 2, (26, 158, 170, 200))
        draw_rect(img, 10, 14, 20, 2, (26, 158, 170, 150))
        draw_rect(img, 10, 20, 15, 2, (26, 158, 170, 100))
    # 底座
    draw_rect(img, 10, 36, 28, 16, (60, 65, 70, 255))
    draw_rect(img, 12, 38, 24, 12, (70, 75, 80, 255))
    # 进度条
    pct = {"idle": 0, "active": 50, "complete": 100, "sabotaged": 20}.get(state, 0)
    bar_w = int(28 * pct / 100)
    draw_rect(img, 10, 50, 28, 3, (50, 55, 60, 255))
    draw_rect(img, 10, 50, bar_w, 3, (39, 174, 96, 255) if state == "complete" else color + (200,))


def _draw_sort(img, state):
    """档案分拣台"""
    color = _get_glow(state)
    # 档案柜
    draw_rect(img, 2, 4, 18, 56, (80, 70, 55, 255))
    for i in range(4):
        draw_rect(img, 4, 6 + i * 13, 14, 10, (100, 90, 70, 255))
    # 分拣台面
    draw_rect(img, 22, 28, 24, 16, (90, 85, 80, 255))
    draw_rect(img, 23, 29, 22, 14, (100, 95, 90, 255))
    # 文件夹
    if state in ("active", "complete"):
        draw_rect(img, 26, 24, 8, 6, (200, 180, 140, 200))
        draw_rect(img, 36, 22, 8, 6, (180, 200, 160, 200))
    if state == "sabotaged":
        draw_rect(img, 2, 4, 18, 56, (192, 57, 43, 100))


def _draw_memory(img, state):
    """3×3 记忆灯板"""
    color = _get_glow(state)
    # 底座
    draw_rect(img, 4, 36, 32, 8, (60, 65, 70, 255))
    # 面板
    draw_rect(img, 4, 4, 32, 34, (50, 55, 60, 255))
    draw_rect(img, 5, 5, 30, 32, (40, 45, 50, 255))
    # 3×3 灯
    lights = [(0,0), (0,1), (0,2), (1,0), (1,1), (1,2), (2,0), (2,1), (2,2)]
    import random
    random.seed(42)
    for i, (lx, ly) in enumerate(lights):
        px, py = 9 + lx * 9, 9 + ly * 9
        lit = state == "active" and random.random() > 0.5
        if lit or state == "complete":
            draw_circle(img, px, py, 3, (244, 162, 54, 255))
        else:
            draw_circle(img, px, py, 3, (60, 65, 70, 255))


def _draw_tap(img, state):
    """按钮控制台 + 节奏灯"""
    color = _get_glow(state)
    draw_rect(img, 2, 4, 40, 24, (50, 55, 60, 255))
    # 4个大按钮
    for i in range(4):
        bx, by = 4 + i * 10, 6
        btn_color = color + (200,) if state == "active" else (80, 85, 90, 255)
        draw_rect(img, bx, by, 8, 8, btn_color)
        draw_rect(img, bx + 1, by + 1, 6, 6, (60, 65, 70, 255))
    # 节奏灯
    for i in range(4):
        draw_circle(img, 6 + i * 10, 22, 2, (100, 105, 110, 255))
    if state == "active":
        draw_circle(img, 16, 22, 2, (244, 162, 54, 255))
    elif state == "complete":
        for i in range(4):
            draw_circle(img, 6 + i * 10, 22, 2, (39, 174, 96, 255))


def _draw_calibrate(img, state):
    """4仪表校准面板"""
    color = _get_glow(state)
    draw_rect(img, 2, 2, 48, 48, (55, 60, 65, 255))
    # 4个仪表
    for idx, (mx, my) in enumerate([(6, 6), (30, 6), (6, 30), (30, 30)]):
        draw_circle(img, mx + 10, my + 10, 10, (70, 75, 80, 255))
        draw_circle(img, mx + 10, my + 10, 8, (90, 95, 100, 255))
        # 指针
        import random; random.seed(idx)
        angle = random.uniform(0, 360) if state == "active" else (30 if state == "complete" else 0)
        px = mx + 10 + int(6 * math.cos(math.radians(angle)))
        py = my + 10 + int(6 * math.sin(math.radians(angle)))
        draw_line(img, mx + 10, my + 10, px, py, color + (255,), 1)
        # 旋钮
        draw_circle(img, mx + 10, my + 10, 2, (60, 65, 70, 255))
    if state == "complete":
        for mx, my in [(6, 6), (30, 6), (6, 30), (30, 30)]:
            draw_circle(img, mx + 10, my + 10, 8, (39, 174, 96, 50))


def _draw_radar(img, state):
    """雷达追踪控制台"""
    color = _get_glow(state)
    # 雷达屏
    draw_rect(img, 4, 4, 40, 34, (40, 50, 55, 255))
    draw_circle(img, 24, 21, 16, (30, 40, 45, 255))
    draw_circle(img, 24, 21, 14, (20, 30, 35, 255))
    # 扫描线
    if state == "active":
        for a in range(0, 360, 10):
            px = 24 + int(12 * math.cos(math.radians(a)))
            py = 21 + int(12 * math.sin(math.radians(a)))
            draw_line(img, 24, 21, px, py, (26, 158, 170, 40), 1)
    # 控制台
    draw_rect(img, 8, 40, 32, 12, (60, 65, 70, 255))
    for i in range(3):
        draw_rect(img, 12 + i * 10, 42, 6, 8, (80, 85, 90, 255))
    if state == "sabotaged":
        draw_rect(img, 4, 4, 40, 34, (192, 57, 43, 80))


def _draw_evidence(img, state):
    """证据档案柜 + 照片墙"""
    color = _get_glow(state)
    # 档案柜
    draw_rect(img, 2, 12, 20, 48, (80, 70, 50, 255))
    for i in range(3):
        draw_rect(img, 4, 14 + i * 15, 16, 12, (100, 90, 65, 255))
    # 照片墙
    draw_rect(img, 24, 4, 22, 32, (180, 175, 160, 255))
    # 照片
    for px, py in [(26, 6), (38, 6), (26, 18), (38, 18), (32, 12)]:
        draw_rect(img, px, py, 8, 6, (200, 195, 180, 255))
        draw_line(img, px + 4, py + 6, px + 4, py + 8, (150, 145, 130, 200), 1)
    if state == "sabotaged":
        draw_rect(img, 24, 4, 22, 32, (192, 57, 43, 100))


def generate_task_prop(prop_type, state, output_dir):
    """生成单个任务站道具 sprite"""
    spec = TASK_PROP_SPECS.get(prop_type)
    if not spec:
        print(f"  ✗ Unknown prop type: {prop_type}")
        return None
    
    w, h = spec["size"]
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    spec["draw"](img, state)
    
    filename = f"tile_task_{prop_type}_{state}.png"
    path = os.path.join(output_dir, filename)
    os.makedirs(output_dir, exist_ok=True)
    img.save(path)
    print(f"  ✓ {spec['name']} [{state}]: {filename}")
    return path


# ============================================================
# 批量处理
# ============================================================

def batch_process_directory(src_dir, dst_dir, operation, **kwargs):
    """批处理目录中所有 PNG"""
    os.makedirs(dst_dir, exist_ok=True)
    count = 0
    for fname in sorted(os.listdir(src_dir)):
        if not fname.lower().endswith('.png'):
            continue
        src_path = os.path.join(src_dir, fname)
        dst_path = os.path.join(dst_dir, fname)
        try:
            img = load_sprite(src_path)
            if operation == 'theme':
                img = apply_theme(img, **kwargs)
            elif operation == 'resize':
                img = resize_sprite(img, kwargs.get('w', 32), kwargs.get('h', 32))
            img.save(dst_path)
            count += 1
        except Exception as e:
            print(f"  ✗ Failed {fname}: {e}")
    print(f"  Batch done: {count} files processed")


# ============================================================
# CLI 入口
# ============================================================

def main():
    parser = argparse.ArgumentParser(
        description='Gangland Undercover 像素资产处理管线 v1.0',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
示例:
  # 单张图片主题色应用（港区）
  python pixel_pipeline.py theme tile_harbour_raw.png -m Harbour -o tile_harbour_out.png
  
  # 批量地图主题色
  python pixel_pipeline.py batch-theme raw_tiles/ -m Harbour -o themed_tiles/
  
  # 生成所有任务站道具
  python pixel_pipeline.py gen-all-props --output-dir Assets/_Project/Art/2D/Props/TaskStations/
  
  # 生成 walk 帧
  python pixel_pipeline.py walk chr_idle.png -n 4 -a 3 -o chr_walk.png
        """
    )
    sub = parser.add_subparsers(dest='command', help='子命令')
    
    # theme - 主题色应用
    p_theme = sub.add_parser('theme', help='应用地图主题色')
    p_theme.add_argument('input', help='输入图片路径')
    p_theme.add_argument('-m', '--map', choices=['Harbour', 'PoliceStation', 'KowloonWalledCity'],
                         default='Harbour', help='地图主题')
    p_theme.add_argument('-o', '--output', required=True, help='输出路径')
    
    # batch-theme - 批量主题色
    p_btheme = sub.add_parser('batch-theme', help='批量应用地图主题色')
    p_btheme.add_argument('input_dir', help='输入目录')
    p_btheme.add_argument('-m', '--map', choices=['Harbour', 'PoliceStation', 'KowloonWalledCity'],
                          default='Harbour')
    p_btheme.add_argument('-o', '--output-dir', required=True, help='输出目录')
    
    # walk - 生成 walk 帧
    p_walk = sub.add_parser('walk', help='从 idle 帧生成 walk 动画')
    p_walk.add_argument('input', help='idle 帧 PNG')
    p_walk.add_argument('-n', '--frames', type=int, default=4, help='帧数')
    p_walk.add_argument('-a', '--amplitude', type=int, default=2, help='摆动幅度(px)')
    p_walk.add_argument('-o', '--output', required=True, help='输出路径')
    
    # gen-all-props - 生成全部任务站道具
    p_props = sub.add_parser('gen-all-props', help='生成全部11种任务站4状态道具')
    p_props.add_argument('-o', '--output-dir', required=True, help='输出目录')
    
    # resize - 缩放
    p_resize = sub.add_parser('resize', help='缩放图片')
    p_resize.add_argument('input', help='输入路径')
    p_resize.add_argument('-W', '--width', type=int, required=True)
    p_resize.add_argument('-H', '--height', type=int, required=True)
    p_resize.add_argument('-o', '--output', required=True)
    
    # recolor - 颜色替换
    p_recolor = sub.add_parser('recolor', help='颜色板替换')
    p_recolor.add_argument('input', help='输入路径')
    p_recolor.add_argument('-p', '--profession', choices=list(PROFESSION_COLORS.keys()),
                           help='使用职业色板')
    p_recolor.add_argument('-o', '--output', required=True)
    
    args = parser.parse_args()
    
    if args.command == 'theme':
        theme = get_map_theme(args.map)
        img = load_sprite(args.input)
        img = apply_theme(img, theme['hue_shift'], theme['sat_factor'], theme['bright_factor'])
        os.makedirs(os.path.dirname(args.output) or '.', exist_ok=True)
        img.save(args.output)
        print(f"✓ Theme applied: {args.output}")
    
    elif args.command == 'batch-theme':
        theme = get_map_theme(args.map)
        batch_process_directory(args.input_dir, args.output_dir, 'theme',
                                hue_shift=theme['hue_shift'],
                                sat_factor=theme['sat_factor'],
                                bright_factor=theme['bright_factor'])
    
    elif args.command == 'walk':
        img = load_sprite(args.input)
        frames = generate_walk_frames(img, args.frames, args.amplitude)
        save_sprite_sheet(frames, args.output, cols=args.frames)
    
    elif args.command == 'gen-all-props':
        for prop_type in TASK_PROP_SPECS:
            for state in ['idle', 'active', 'complete', 'sabotaged']:
                generate_task_prop(prop_type, state, args.output_dir)
        print(f"\n✓ All 44 task prop sprites generated in: {args.output_dir}")
    
    elif args.command == 'resize':
        img = load_sprite(args.input)
        img = resize_sprite(img, args.width, args.height)
        os.makedirs(os.path.dirname(args.output) or '.', exist_ok=True)
        img.save(args.output)
        print(f"✓ Resized: {args.output}")
    
    elif args.command == 'recolor':
        img = load_sprite(args.input)
        if args.profession:
            palette = get_profession_palette(args.profession)
            # 简单映射：用 primary+secondary+accent 作为目标色
            # 实际使用时需配合底模的源色
            print(f"  Palette for {args.profession}: {palette}")
        img.save(args.output)
        print(f"✓ Saved: {args.output}")
    
    else:
        parser.print_help()


if __name__ == '__main__':
    main()
