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

PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(PROJECT_ROOT, "Assets", "_Project", "Art", "2D", "VFX")
RUNTIME_OUT_DIR = os.path.join(PROJECT_ROOT, "Assets", "_Project", "Resources", "Sprites", "VFX")

def draw_circle(draw, cx, cy, r, color):
    draw.ellipse([cx-r, cy-r, cx+r, cy+r], fill=color)

def draw_ring(draw, cx, cy, r, width, color):
    draw.ellipse([cx-r, cy-r, cx+r, cy+r], outline=color, width=width)

def draw_diamond(draw, cx, cy, r, color):
    draw.polygon([(cx, cy-r), (cx+r, cy), (cx, cy+r), (cx-r, cy)], fill=color)

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
    """停电特效 - 全局暗场 + 电弧脉冲，保留角色和地面可读性"""
    frames = []
    w, h = 96, 96
    cx, cy = w//2, h//2
    for i in range(12):
        img = Image.new('RGBA', (w, h), (0,0,0,0))
        draw = ImageDraw.Draw(img)
        pulse = (math.sin((i / 12) * math.tau) + 1) * 0.5

        # Full-field darkness makes this read as a map-level state instead of a center burst.
        draw.rectangle([0, 0, w-1, h-1], fill=(4, 8, 18, 92 + int(pulse * 28)))
        draw.ellipse([10, 8, w-11, h-9], fill=(*POLICE_BLUE, 24 + int(pulse * 18)))

        # Edge vignette leaves the center playable while darkening room boundaries.
        for inset in range(0, 20, 5):
            alpha = 38 - inset + int(pulse * 8)
            draw.rectangle([inset, inset, w-1-inset, h-1-inset], outline=(0, 0, 0, alpha), width=3)

        # Sparse power arcs identify the sabotage without covering task prompts.
        for bolt in range(3):
            start = (i * 7 + bolt * 23) % 84 + 6
            side = (i + bolt) % 4
            if side == 0:
                points = [(start, 4), (start + 6, 15), (start - 3, 28), (start + 8, 42)]
            elif side == 1:
                points = [(w-5, start), (78, start + 5), (66, start - 2), (52, start + 7)]
            elif side == 2:
                points = [(start, h-5), (start - 6, 78), (start + 4, 66), (start - 9, 52)]
            else:
                points = [(4, start), (18, start - 6), (30, start + 3), (43, start - 7)]
            draw.line(points, fill=(*NEON_CYAN, 132 + int(pulse * 68)), width=2)

        # Small emergency lamp glint, distinct from the separate emergency_light effect.
        lamp_alpha = 52 + int(pulse * 42)
        draw.ellipse([cx-10, 8, cx+10, 18], fill=(*EMERGENCY_RED, lamp_alpha))
        draw.rectangle([cx-3, 18, cx+3, 26], fill=(*EMERGENCY_RED, max(24, lamp_alpha - 18)))
        frames.append(img)
    return frames

def generate_door_lock_effect():
    """锁门特效 - 清晰锁牌 + 红色警戒边"""
    frames = []
    w, h = 48, 48
    cx, cy = w//2, h//2
    for i in range(6):
        img = Image.new('RGBA', (w, h), (0,0,0,0))
        draw = ImageDraw.Draw(img)
        pulse = [0.35, 0.65, 1.0, 0.78, 0.92, 0.55][i]
        red_alpha = int(220 * pulse)
        plate_alpha = int(92 + pulse * 60)

        draw.rectangle([8, 8, w-9, h-9], fill=(18, 9, 8, plate_alpha), outline=(*EMERGENCY_RED, red_alpha), width=2)

        # Corner warning brackets keep the route/door edge readable.
        bracket = (*EMERGENCY_RED, min(255, red_alpha + 24))
        draw.line([(4, 4), (18, 4), (4, 4), (4, 18)], fill=bracket, width=2)
        draw.line([(w-5, 4), (w-19, 4), (w-5, 4), (w-5, 18)], fill=bracket, width=2)
        draw.line([(4, h-5), (18, h-5), (4, h-5), (4, h-19)], fill=bracket, width=2)
        draw.line([(w-5, h-5), (w-19, h-5), (w-5, h-5), (w-5, h-19)], fill=bracket, width=2)

        # Compact lock silhouette reads faster than a large X over busy corridor art.
        shackle_color = (*WHITE, int(170 + pulse * 70))
        lock_color = (*EMERGENCY_RED, min(255, int(184 + pulse * 70)))
        draw.arc([cx-10, cy-16, cx+10, cy+6], 180, 360, fill=shackle_color, width=3)
        draw.line([(cx-10, cy-5), (cx-10, cy+7)], fill=shackle_color, width=2)
        draw.line([(cx+10, cy-5), (cx+10, cy+7)], fill=shackle_color, width=2)
        draw.rectangle([cx-13, cy+3, cx+13, cy+18], fill=lock_color)
        draw.rectangle([cx-2, cy+8, cx+2, cy+14], fill=(38, 11, 9, min(255, int(190 + pulse * 45))))

        if i in (2, 4):
            draw.line([(14, 14), (w-15, h-15)], fill=(*WHITE, 96), width=1)
            draw.line([(w-15, 14), (14, h-15)], fill=(*WHITE, 72), width=1)
        frames.append(img)
    return frames

def generate_comms_jam_effect():
    """通讯干扰特效 - 确定性稀疏 glitch，避免遮挡任务文本"""
    frames = []
    w, h = 64, 64
    for i in range(8):
        img = Image.new('RGBA', (w, h), (0,0,0,0))
        draw = ImageDraw.Draw(img)

        phase = i % 4
        draw.rectangle([0, 0, w-1, h-1], fill=(3, 10, 18, 28 + phase * 4))

        # Broken scan bands move in a stable pattern across regeneration runs.
        for band in range(6):
            y = 7 + band * 9 + ((i + band) % 3 - 1)
            x_offset = ((i * 9 + band * 13) % 18) - 9
            color = (*NEON_YELLOW, 150 if band % 2 == 0 else 94)
            draw.line([(6 + x_offset, y), (28 + x_offset, y)], fill=color, width=1)
            draw.line([(35 - x_offset, y + 2), (58 - x_offset, y + 2)], fill=(*NEON_CYAN, 118), width=1)

        # Signal bars communicate "comms" even when the noise is subtle.
        for bar in range(4):
            height = 7 + bar * 4
            alpha = 110 + ((i + bar) % 3) * 42
            x = 12 + bar * 5
            draw.rectangle([x, 44 - height, x + 2, 44], fill=(*WHITE, alpha))

        # Hash-based noise is deterministic; no random import means stable diffs.
        for n in range(22):
            px = 5 + ((n * 17 + i * 11) % (w - 10))
            py = 6 + ((n * 23 + i * 7) % (h - 12))
            alpha = 72 + ((n * 19 + i * 31) % 96)
            if n % 4 == 0:
                draw.rectangle([px, py, px + 2, py], fill=(*WHITE, alpha))
            elif n % 4 == 1:
                img.putpixel((px, py), (*NEON_YELLOW, alpha))
            else:
                img.putpixel((px, py), (*NEON_CYAN, max(50, alpha - 22)))
        frames.append(img)
    return frames

def generate_patrol_alert_effect():
    """巡逻警报特效 - 琥珀巡查警示，区别于锁门红与应急红灯"""
    frames = []
    w, h = 64, 64
    cx, cy = w//2, h//2
    for i in range(4):
        img = Image.new('RGBA', (w, h), (0,0,0,0))
        draw = ImageDraw.Draw(img)
        pulse = [1.0, 0.48, 0.82, 0.32][i]
        amber_alpha = int(210 * pulse)

        # Search cone gives patrol semantics without using lockdown red.
        draw.polygon([(cx, 6), (cx-22, h-8), (cx+22, h-8)], fill=(*NEON_YELLOW, int(40 + pulse * 54)))
        draw_ring(draw, cx, cy, 18 + i * 4, 2, (*NEON_YELLOW, amber_alpha))
        draw_ring(draw, cx, cy, 11, 1, (*WHITE, int(92 + pulse * 90)))

        # Alert icon.
        draw.polygon([(cx, 15), (cx-12, 42), (cx+12, 42)], fill=(*NEON_YELLOW, min(255, amber_alpha + 24)))
        draw.line([(cx, 22), (cx, 34)], fill=(*BLACK, 190), width=3)
        draw.rectangle([cx-1, 37, cx+1, 39], fill=(*BLACK, 190))
        draw.line([(cx, 22), (cx, 34)], fill=(*WHITE, 150), width=1)

        # Blue patrol accents keep it in the police visual family.
        draw.line([(8, 10 + i), (24, 10 + i)], fill=(*POLICE_BLUE, 120), width=2)
        draw.line([(w-24, 10 + i), (w-8, 10 + i)], fill=(*POLICE_BLUE, 120), width=2)
        frames.append(img)
    return frames

def generate_evidence_leak_effect():
    """证据泄露特效 - 证据核心 + 可读脉冲环"""
    frames = []
    w, h = 48, 48
    cx, cy = w//2, h//2
    for i in range(12):
        img = Image.new('RGBA', (w, h), (0,0,0,0))
        draw = ImageDraw.Draw(img)
        t = i / 11
        ring_r = 7 + int(t * 17)
        ring_alpha = max(24, int(210 * (1 - t)))

        # 双环让它在杂乱地面上仍能被看到。
        draw_ring(draw, cx, cy, ring_r, 2, (*NEON_CYAN, ring_alpha))
        if i % 3 == 0:
            draw_ring(draw, cx, cy, max(4, ring_r - 5), 1, (*WHITE, 110))

        # 中心证据核心，保持几帧不消失，避免只剩弱粒子。
        core_alpha = max(120, int(255 - t * 90))
        draw_diamond(draw, cx, cy, 5, (*WHITE, core_alpha))
        draw.rectangle([cx-2, cy-5, cx+2, cy+5], fill=(*POLICE_BLUE, core_alpha))

        for p in range(10):
            ang = math.radians(p * 36 + i * 18)
            dist = 6 + i * 2 + (p % 3)
            px = cx + int(dist * math.cos(ang))
            py = cy + int(dist * math.sin(ang))
            if 0 < px < w and 0 < py < h:
                draw.rectangle([px-1, py-1, px, py], fill=(*NEON_CYAN, max(35, int(210 * (1 - t)))))
        frames.append(img)
    return frames

def generate_kill_effect():
    """击杀特效 - 短促血溅 + 冲击环，避免后段整块遮挡尸体"""
    frames = []
    w, h = 128, 128
    cx, cy = w//2, h//2
    for i in range(10):
        img = Image.new('RGBA', (w, h), (0,0,0,0))
        draw = ImageDraw.Draw(img)

        t = i / 9
        ring_r = 9 + int(t * 45)
        ring_alpha = max(0, int(220 * (1 - t)))
        core_alpha = max(0, int(245 * (1 - t * 0.85)))

        if i < 4:
            flash = max(0, 220 - i * 55)
            draw.line([(cx-22, cy-18), (cx+24, cy+20)], fill=(*WHITE, flash), width=max(1, 4 - i))
            draw.line([(cx-18, cy+17), (cx+22, cy-19)], fill=(*WHITE, flash // 2), width=max(1, 3 - i))

        if ring_alpha > 0:
            draw_ring(draw, cx, cy, ring_r, 3 if i < 5 else 2, (*EMERGENCY_RED, ring_alpha))
            draw_ring(draw, cx, cy, max(4, ring_r - 6), 1, (*WHITE, ring_alpha // 2))

        # 不画满屏圆，改成几块不规则血溅，中心留出尸体轮廓。
        splash_points = [
            (-8, -3, 8), (4, 5, 7), (13, -10, 5), (-16, 12, 4),
        ]
        for ox, oy, base_r in splash_points:
            fade = max(0, core_alpha - abs(i - 2) * 12)
            r = max(2, base_r + min(i, 4))
            draw_circle(draw, cx + ox, cy + oy, r, (*EMERGENCY_RED, fade))

        for p in range(14):
            ang = math.radians(p * 25 + i * 14)
            dist = 16 + i * 7 + (p % 4) * 3
            px = cx + int(dist * math.cos(ang))
            py = cy + int(dist * math.sin(ang))
            if 0 < px < w and 0 < py < h:
                size = 1 + (p % 2)
                alpha = max(0, int(230 * (1 - t)))
                draw.rectangle([px-size, py-size, px+size, py+size], fill=(*EMERGENCY_RED, alpha))
        frames.append(img)
    return frames

def generate_hit_effect():
    """击中特效 - 带方向的短促冲击"""
    frames = []
    w, h = 32, 32
    cx, cy = w//2, h//2
    for i in range(4):
        img = Image.new('RGBA', (w, h), (0,0,0,0))
        draw = ImageDraw.Draw(img)
        alpha = 255 - i * 45
        spread = 3 + i * 3

        draw.line([(cx-8-spread, cy+5), (cx+8+spread, cy-5)], fill=(*WHITE, max(0, alpha - 20)), width=max(1, 3 - i))
        draw.line([(cx-6, cy-6-spread), (cx+5, cy+5+spread)], fill=(*NEON_YELLOW, alpha), width=2)
        draw_circle(draw, cx, cy, max(1, 4 - i), (*NEON_YELLOW, alpha))

        for p in range(6):
            ang = math.radians(-25 + p * 20)
            dist = 7 + i * 4
            px = cx + int(dist * math.cos(ang))
            py = cy + int(dist * math.sin(ang))
            if 0 < px < w and 0 < py < h:
                img.putpixel((px, py), (*NEON_CYAN, max(0, 180 - i * 35)))
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

CONFIGS = {
    "blackout": generate_blackout_effect,
    "door_lock": generate_door_lock_effect,
    "comms_jam": generate_comms_jam_effect,
    "patrol_alert": generate_patrol_alert_effect,
    "evidence_leak": generate_evidence_leak_effect,
    "kill": generate_kill_effect,
    "hit": generate_hit_effect,
    "emergency_light": generate_emergency_light,
}

ORDERED_EFFECTS = [
    "blackout",
    "comms_jam",
    "door_lock",
    "emergency_light",
    "evidence_leak",
    "hit",
    "kill",
    "patrol_alert",
]

def generate_effects(names):
    print("=" * 60)
    print("Gangland Undercover VFX Generator")
    print("=" * 60)

    total_frames = 0
    for name in names:
        if name not in CONFIGS:
            raise SystemExit(f"Unknown effect: {name}")

        gen_fn = CONFIGS[name]
        print(f"\n[{name}] Generating...")
        frames = gen_fn()
        total_frames += len(frames)
        
        # Save sprite sheet
        sheet_path = os.path.join(OUT_DIR, f"vfx_{name}_sheet.png")
        save_sprite_sheet(frames, sheet_path)
        
        # Save individual frames to art source and runtime Resources.
        for base_dir in [OUT_DIR, RUNTIME_OUT_DIR]:
            frame_dir = os.path.join(base_dir, name)
            save_frames(frames, name, frame_dir)

    print(f"\n{'=' * 60}")
    print(f"TOTAL: {len(names)} effects, {total_frames} frames")
    print(f"Art output: {OUT_DIR}")
    print(f"Runtime output: {RUNTIME_OUT_DIR}")
    print(f"{'=' * 60}")

if __name__ == "__main__":
    import sys
    if "--list" in sys.argv:
        print("Available effects:")
        for name in ORDERED_EFFECTS:
            print(f"  {name}")
    else:
        requested = [arg for arg in sys.argv[1:] if not arg.startswith("--")]
        generate_effects(ORDERED_EFFECTS if "--all" in sys.argv or not requested else requested)
