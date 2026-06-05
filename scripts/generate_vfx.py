#!/usr/bin/env python3
"""Gangland Undercover VFX 特效生成器

生成 5 种破坏特效 + 击杀特效 + 通用 VFX sprite sheets
纯 PIL 程序化绘制，无外部依赖

依赖: pip install Pillow
用法: python generate_vfx.py [--all] [--list] [name]
"""

import os
import math
from PIL import Image, ImageDraw

# === Art Bible 色板 ===
NEON_CYAN = (26, 158, 170)
NEON_YELLOW = (244, 162, 54)
EMERGENCY_RED = (192, 57, 43)
POLICE_BLUE = (45, 111, 186)
GANG_RED = (192, 57, 43)
WHITE = (255, 255, 255)
BLACK = (0, 0, 0)

OUT_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                       "Assets", "_Project", "Art", "2D", "VFX")

def draw_circle(draw, cx, cy, r, color):
    draw.ellipse([cx-r, cy-r, cx+r, cy+r], fill=color)

def draw_ring(draw, cx, cy, r, width, color):
    draw.ellipse([cx-r, cy-r, cx+r, cy+r], outline=color, width=width)

def generate_spark_frame(w=64, h=64):
    """单个火花帧 - 随机方向的短线段"""
    img = Image.new('RGBA', (w, h), (0,0,0,0))
    draw = ImageDraw.Draw(img)
    cx, cy = w//2, h//2
    # 中心亮点
    draw_circle(draw, cx, cy, 2, WHITE)
    # 4条火花射线
    rays = [(0,-1), (1,-1), (1,0), (1,1), (0,1), (-1,1), (-1,0), (-1,-1)]
    for dx, dy in rays:
        for dist in range(4, 16, 3):
            px = cx + dx * dist
            py = cy + dy * dist
            if 0 < px < w and 0 < py < h:
                img.putpixel((px, py), NEON_CYAN)
    return img

def generate_blackout_effect():
    """停电特效 sprite sheet - 8帧循环：蓝色电弧脉冲"""
    frames = []
    w, h = 96, 96
    for i in range(8):
        img = Image.new('RGBA', (w, h), (0,0,0,0))
        draw = ImageDraw.Draw(img)
        cx, cy = w//2, h//2
        # 同心圆衰减
        r = 10 + i * 6
        alpha = max(0, 180 - i * 20)
        color = (*POLICE_BLUE, alpha)
        draw_circle(draw, cx, cy, r, color)
        # 电弧线
        for a in range(0, 360, 60):
            ang = math.radians(a + i * 15)
            ex = cx + int(r * 1.2 * math.cos(ang))
            ey = cy + int(r * 1.2 * math.sin(ang))
            if 0 < ex < w and 0 < ey < h:
                draw.line([(cx, cy), (ex, ey)], fill=(*NEON_CYAN, 200), width=2)
        frames.append(img)
    # 反向衰减（4-7帧是反的）
    for i in range(3, -1, -1):
        img = Image.new('RGBA', (w, h), (0,0,0,0))
        draw = ImageDraw.Draw(img)
        r = 10 + i * 6
        color = (*POLICE_BLUE, max(0, 180 - i * 20))
        draw_circle(draw, cx, cy, r, color)
        for a in range(0, 360, 60):
            ang = math.radians(a + i * 15)
            ex = cx + int(r * 1.2 * math.cos(ang))
            ey = cy + int(r * 1.2 * math.sin(ang))
            draw.line([(cx, cy), (ex, ey)], fill=(*NEON_CYAN, 200), width=2)
        frames.append(img)
    return frames

def generate_door_lock_effect():
    """锁门特效 - 红色X标记 + 锁图标"""
    frames = []
    w, h = 48, 48
    for i in range(6):
        img = Image.new('RGBA', (w, h), (0,0,0,0))
        draw = ImageDraw.Draw(img)
        # 红色X逐渐出现
        if i >= 1:
            alpha = min(255, (i - 1) * 80 + 50)
            draw.line([(8, 8), (w-8, h-8)], fill=(*EMERGENCY_RED, alpha), width=3)
        if i >= 2:
            draw.line([(w-8, 8), (8, h-8)], fill=(*EMERGENCY_RED, alpha), width=3)
        # 边框闪烁
        if i % 2 == 0:
            draw.rectangle([4, 4, w-5, h-5], outline=(*EMERGENCY_RED, 180), width=2)
        frames.append(img)
    return frames

def generate_comms_jam_effect():
    """通讯干扰特效 - 锯齿波纹 + 噪点"""
    frames = []
    w, h = 64, 64
    for i in range(8):
        img = Image.new('RGBA', (w, h), (0,0,0,0))
        draw = ImageDraw.Draw(img)
        # 水平锯齿线
        for y in range(8, h-8, 8):
            offset = (i + y//8) % 3 - 1
            for x in range(4, w-4, 2):
                py = y + (1 if (x + offset) % 6 == 0 else 0)
                if 0 < py < h:
                    img.putpixel((x, py), (*NEON_YELLOW, 180))
        # 随机噪点
        for _ in range(20):
            import random
            px = random.randint(8, w-9)
            py = random.randint(8, h-9)
            img.putpixel((px, py), (255, 255, 255, random.randint(100, 200)))
        frames.append(img)
    return frames

def generate_patrol_alert_effect():
    """巡逻警报特效 - 红色脉冲闪烁"""
    frames = []
    w, h = 64, 64
    for i in range(4):
        img = Image.new('RGBA', (w, h), (0,0,0,0))
        draw = ImageDraw.Draw(img)
        alpha = 200 if i < 2 else 80
        # 全屏红色半透明
        draw.rectangle([0, 0, w-1, h-1], fill=(*EMERGENCY_RED, alpha))
        # 感叹号
        if i % 2 == 0:
            draw.rectangle([w//2-2, 10, w//2+2, 35], fill=WHITE)
            draw.rectangle([w//2-2, 42, w//2+2, 46], fill=WHITE)
        frames.append(img)
    return frames

def generate_evidence_leak_effect():
    """证据泄露特效 - 数据粒子飘散"""
    frames = []
    w, h = 48, 48
    for i in range(8):
        img = Image.new('RGBA', (w, h), (0,0,0,0))
        draw = ImageDraw.Draw(img)
        cx, cy = w//2, h//2
        # 中心散开粒子
        for p in range(12):
            ang = math.radians(p * 30 + i * 15)
            dist = 4 + i * 4
            px = cx + int(dist * math.cos(ang))
            py = cy + int(dist * math.sin(ang))
            if 0 < px < w and 0 < py < h:
                img.putpixel((px, py), (*NEON_CYAN, max(20, 255 - i*30)))
        # 中心原点
        draw_circle(draw, cx, cy, 3, (255, 255, 255, 200 - i*20))
        frames.append(img)
    # 继续散开
    for i in range(8, 12):
        img = Image.new('RGBA', (w, h), (0,0,0,0))
        for p in range(12):
            ang = math.radians(p * 30 + i * 15)
            dist = 4 + i * 4
            px = cx + int(dist * math.cos(ang))
            py = cy + int(dist * math.sin(ang))
            if 0 < px < w and 0 < py < h:
                img.putpixel((px, py), (*NEON_CYAN, max(10, 255 - (i-4)*40)))
        frames.append(img)
    return frames

def generate_kill_effect():
    """击杀特效 - 红色冲击波"""
    frames = []
    w, h = 128, 128
    for i in range(10):
        img = Image.new('RGBA', (w, h), (0,0,0,0))
        draw = ImageDraw.Draw(img)
        cx, cy = w//2, h//2
        # 红色爆炸
        r = 5 + i * 8
        alpha = max(0, 255 - i * 25)
        color = (*EMERGENCY_RED, alpha)
        draw_circle(draw, cx, cy, r, color)
        # 白色冲击环
        if i < 6:
            ring_r = 5 + i * 9
            draw_ring(draw, cx, cy, ring_r, 3, (*WHITE, max(0, 200 - i * 40)))
        # 碎片粒子
        for p in range(8):
            ang = math.radians(p * 45 + i * 10)
            dist = 10 + i * 10 + p % 3 * 4
            px = cx + int(dist * math.cos(ang))
            py = cy + int(dist * math.sin(ang))
            if 0 < px < w and 0 < py < h:
                img.putpixel((px, py), (*EMERGENCY_RED, alpha))
        frames.append(img)
    return frames

def generate_hit_effect():
    """击中特效 - 小尺寸火花"""
    frames = []
    w, h = 32, 32
    for i in range(4):
        img = Image.new('RGBA', (w, h), (0,0,0,0))
        draw = ImageDraw.Draw(img)
        cx, cy = w//2, h//2
        # 火花星形
        r = 3 + i * 2
        draw_circle(draw, cx, cy, r, (*NEON_YELLOW, 255 - i*40))
        for a in range(0, 360, 90):
            ang = math.radians(a)
            ex = cx + int(r * 2 * math.cos(ang))
            ey = cy + int(r * 2 * math.sin(ang))
            draw.line([(cx, cy), (ex, ey)], fill=(*NEON_YELLOW, 200 - i*30), width=2)
        frames.append(img)
    return frames

def generate_emergency_light():
    """应急灯脉冲 - 全屏红光闪烁 + 灯光图标"""
    frames = []
    w, h = 48, 48
    for i in range(8):
        img = Image.new('RGBA', (w, h), (0,0,0,0))
        draw = ImageDraw.Draw(img)
        # 脉冲底光
        alpha = 180 if i % 3 == 0 else 60
        draw_circle(draw, w//2, h//2, 20, (*EMERGENCY_RED, alpha))
        # 灯光图标
        if i % 4 < 2:
            draw.ellipse([12, 8, 36, 20], fill=(*EMERGENCY_RED, 255))
            draw.rectangle([20, 20, 28, 36], fill=(*EMERGENCY_RED, 255))
        frames.append(img)
    return frames

def save_sprite_sheet(frames, output_path, cols=4):
    """保存 sprite sheet"""
    if not frames:
        return
    fw, fh = frames[0].size
    rows = (len(frames) + cols - 1) // cols
    sheet = Image.new('RGBA', (fw*cols, fh*rows), (0,0,0,0))
    for i, frame in enumerate(frames):
        row, col = i // cols, i % cols
        sheet.paste(frame, (col*fw, row*fh))
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    sheet.save(output_path)
    print(f"  ✓ Saved: {output_path} ({len(frames)} frames, {fw}×{fh})")

def save_frames(frames, prefix, out_dir):
    """保存每帧为单独 PNG"""
    os.makedirs(out_dir, exist_ok=True)
    for i, frame in enumerate(frames):
        path = os.path.join(out_dir, f"{prefix}_{i:02d}.png")
        frame.save(path)
    print(f"  ✓ {len(frames)} frames to {out_dir}/{prefix}_*.png")

def generate_all():
    print("=" * 60)
    print("Gangland Undercover VFX Generator")
    print("=" * 60)

    configs = {
        "blackout": generate_blackout_effect,
        "door_lock": generate_door_lock_effect,
        "comms_jam": generate_comms_jam_effect,
        "patrol_alert": generate_patrol_alert_effect,
        "evidence_leak": generate_evidence_leak_effect,
        "kill": generate_kill_effect,
        "hit": generate_hit_effect,
        "emergency_light": generate_emergency_light,
    }

    total_frames = 0
    for name, gen_fn in configs.items():
        print(f"\n[{name}] Generating...")
        frames = gen_fn()
        total_frames += len(frames)
        
        # Save sprite sheet
        sheet_path = os.path.join(OUT_DIR, f"vfx_{name}_sheet.png")
        save_sprite_sheet(frames, sheet_path)
        
        # Save individual frames
        frame_dir = os.path.join(OUT_DIR, name)
        save_frames(frames, name, frame_dir)

    print(f"\n{'=' * 60}")
    print(f"TOTAL: {len(configs)} effects, {total_frames} frames")
    print(f"Output: {OUT_DIR}")
    print(f"{'=' * 60}")

if __name__ == "__main__":
    import sys
    if "--list" in sys.argv:
        print("Available effects:")
        for name in ["blackout", "door_lock", "comms_jam", "patrol_alert",
                      "evidence_leak", "kill", "hit", "emergency_light"]:
            print(f"  {name}")
    else:
        generate_all()
