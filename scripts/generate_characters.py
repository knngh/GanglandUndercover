#!/usr/bin/env python3
"""Gangland Undercover 角色精灵生成器

7 职业 × 4 方向 × idle 帧 → 纯 PIL 几何绘制
几何拼接策略：身体 + 头 + 头饰 + 手持物 + 色板

依赖: pip install Pillow
用法: python generate_characters.py
"""

import os
import math
from PIL import Image, ImageDraw

# === Art Bible 色板 (from gangland_palette.py) ===
POLICE_BLUE = (45, 111, 186)
GANG_RED = (192, 57, 43)
UNDERCOVER_PURPLE = (142, 68, 173)
MOLE_GREY = (149, 165, 166)
TECH_GREEN = (39, 174, 96)
MEDIC_WHITE = (236, 240, 241)
DRIVER_ORANGE = (230, 126, 34)
SKIN_TONE = (255, 213, 170)
DARK_GREY = (52, 73, 94)
BLACK = (0, 0, 0)
WHITE = (255, 255, 255)
GOLD = (241, 196, 15)
SHOE_BROWN = (101, 67, 33)

CHAR_W, CHAR_H = 64, 64

OUT_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                       "Assets", "_Project", "Art", "2D", "Characters")

# 4 directions (down, left, right, up)
DIRECTIONS = ["d", "l", "r", "u"]

def draw_body(draw, primary_color, secondary_color=DARK_GREY):
    """绘制身体：躯干(上衣)+裤子+鞋子"""
    cx = CHAR_W // 2
    # 躯干 (上衣)
    draw.rectangle([cx-6, 22, cx+6, 38], fill=primary_color)
    draw.rectangle([cx-6, 22, cx+6, 24], fill=secondary_color)  # 衣领
    # 手臂
    draw.rectangle([cx-9, 24, cx-7, 36], fill=primary_color)
    draw.rectangle([cx+7, 24, cx+9, 36], fill=primary_color)
    # 裤子
    draw.rectangle([cx-5, 38, cx-1, 48], fill=secondary_color)
    draw.rectangle([cx+1, 38, cx+5, 48], fill=secondary_color)
    # 鞋子
    draw.rectangle([cx-6, 48, cx-2, 50], fill=SHOE_BROWN)
    draw.rectangle([cx+2, 48, cx+6, 50], fill=SHOE_BROWN)

def draw_head(draw, hair_color=BLACK):
    """绘制头部：脸+头发"""
    cx = CHAR_W // 2
    # 脸
    draw.ellipse([cx-6, 10, cx+6, 22], fill=SKIN_TONE)
    # 头发顶
    draw.rectangle([cx-6, 9, cx+6, 12], fill=hair_color)
    draw.rectangle([cx-7, 12, cx-6, 17], fill=hair_color)  # 侧发
    draw.rectangle([cx+6, 12, cx+7, 17], fill=hair_color)
    # 眼
    draw.rectangle([cx-3, 15, cx-2, 16], fill=BLACK)
    draw.rectangle([cx+2, 15, cx+3, 16], fill=BLACK)

def draw_hat(draw, hat_type, hat_color):
    """绘制头饰"""
    cx = CHAR_W // 2
    if hat_type == "police_cap":
        # 警帽
        draw.rectangle([cx-7, 5, cx+7, 11], fill=hat_color)
        draw.rectangle([cx-3, 11, cx+3, 13], fill=hat_color)
        draw.rectangle([cx-2, 7, cx+2, 9], fill=GOLD)  # 徽章
    elif hat_type == "sunglasses":
        # 墨镜
        draw.rectangle([cx-4, 13, cx-2, 15], fill=BLACK)
        draw.rectangle([cx+2, 13, cx+4, 15], fill=BLACK)
    elif hat_type == "hood":
        # 兜帽
        draw.rectangle([cx-5, 7, cx+5, 13], fill=hat_color)
        draw.rectangle([cx-5, 13, cx-3, 16], fill=hat_color)
        draw.rectangle([cx+3, 13, cx+5, 16], fill=hat_color)
    elif hat_type == "headset":
        # 耳机
        draw.rectangle([cx-7, 14, cx-6, 18], fill=hat_color)
        draw.rectangle([cx+6, 14, cx+7, 18], fill=hat_color)
        draw.rectangle([cx-7, 13, cx+7, 14], fill=hat_color)  # 头梁
    elif hat_type == "baseball_cap":
        # 棒球帽
        draw.rectangle([cx-7, 6, cx+1, 11], fill=hat_color)
        draw.rectangle([cx-7, 11, cx+5, 12], fill=hat_color)

def draw_accessory(draw, acc_type, color):
    """绘制手持/配件"""
    cx = CHAR_W // 2
    if acc_type == "badge":
        # 右胸徽章
        draw.rectangle([cx+2, 25, cx+6, 29], fill=GOLD)
    elif acc_type == "tablet":
        # 平板（左手持有）
        draw.rectangle([cx-10, 28, cx-7, 35], fill=color)
        draw.rectangle([cx-9, 30, cx-8, 33], fill=NEON_CYAN)
    elif acc_type == "med_cross":
        # 红十字标记
        draw.rectangle([cx-1, 26, cx+1, 31], fill=color)
        draw.rectangle([cx-3, 28, cx+3, 29], fill=color)
    elif acc_type == "gloves":
        # 手套高亮
        draw.rectangle([cx-9, 34, cx-7, 36], fill=color)
        draw.rectangle([cx+7, 34, cx+9, 36], fill=color)
    elif acc_type == "weapon":
        # 右臂武器
        draw.rectangle([cx+8, 28, cx+10, 34], fill=color)

NEON_CYAN = (26, 158, 170)  # for tablet screen

# === 职业定义 ===
PROFESSIONS = {
    "inspector": {
        "name": "Inspector",
        "body_color": POLICE_BLUE,
        "secondary": DARK_GREY,
        "hair": BLACK,
        "hat": ("police_cap", POLICE_BLUE),
        "accessory": ("badge", GOLD),
    },
    "enforcer": {
        "name": "Enforcer",
        "body_color": GANG_RED,
        "secondary": BLACK,
        "hair": BLACK,
        "hat": ("sunglasses", BLACK),
        "accessory": ("weapon", DARK_GREY),
    },
    "undercover": {
        "name": "Undercover",
        "body_color": UNDERCOVER_PURPLE,
        "secondary": DARK_GREY,
        "hair": DARK_GREY,
        "hat": ("hood", UNDERCOVER_PURPLE),
        "accessory": ("badge", GOLD),
    },
    "tech": {
        "name": "Tech",
        "body_color": TECH_GREEN,
        "secondary": DARK_GREY,
        "hair": BLACK,
        "hat": ("headset", BLACK),
        "accessory": ("tablet", TECH_GREEN),
    },
    "medic": {
        "name": "Medic",
        "body_color": MEDIC_WHITE,
        "secondary": DARK_GREY,
        "hair": BLACK,
        "hat": (None, None),
        "accessory": ("med_cross", GANG_RED),
    },
    "driver": {
        "name": "Driver",
        "body_color": DRIVER_ORANGE,
        "secondary": DARK_GREY,
        "hair": BLACK,
        "hat": ("baseball_cap", DRIVER_ORANGE),
        "accessory": ("gloves", WHITE),
    },
    "mole": {
        "name": "Mole",
        "body_color": MOLE_GREY,
        "secondary": DARK_GREY,
        "hair": DARK_GREY,
        "hat": ("hood", MOLE_GREY),
        "accessory": ("weapon", DARK_GREY),
    },
}

def generate_idle_frame(prof_key, prof):
    """生成单职业 idle 帧"""
    img = Image.new('RGBA', (CHAR_W, CHAR_H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # 阴影（脚底椭圆）
    draw.ellipse([CHAR_W//2-8, 51, CHAR_W//2+8, 54], fill=(0, 0, 0, 80))

    # 身体
    draw_body(draw, prof["body_color"], prof["secondary"])

    # 头部
    draw_head(draw, prof["hair"])

    # 头饰
    hat_type, hat_color = prof["hat"]
    if hat_type:
        draw_hat(draw, hat_type, hat_color)

    # 配件
    acc_type, acc_color = prof["accessory"]
    if acc_type:
        draw_accessory(draw, acc_type, acc_color)

    return img

def generate_walk_frames(idle_img, num_frames=4, amplitude=2):
    """从 idle 帧生成 walk 帧（简易版）"""
    frames = [idle_img]  # frame 0 = idle
    w, h = idle_img.size
    for i in range(1, num_frames):
        frame = Image.new('RGBA', (w, h), (0, 0, 0, 0))
        phase = 2 * math.pi * i / num_frames
        offset_x = int(amplitude * math.sin(phase))

        # 切分 + 偏移
        # 上半身 (y:0-32) 稍微左右移
        upper = idle_img.crop((0, 0, w, 32))
        frame.paste(upper, (offset_x // 2, 0), upper)

        # 左腿区域 (y:38-48, x:24-32)
        left_leg = idle_img.crop((24, 38, 32, 50))
        frame.paste(left_leg, (24 + offset_x, 38), left_leg)

        # 右腿区域 (y:38-48, x:32-40)
        right_leg = idle_img.crop((32, 38, 40, 50))
        frame.paste(right_leg, (32 - offset_x, 38), right_leg)

        frames.append(frame)
    return frames

def save_sprite_sheet(frames, path, cols=4):
    """保存 spritesheet"""
    if not frames:
        return
    fw, fh = frames[0].size
    rows = (len(frames) + cols - 1) // cols
    sheet = Image.new('RGBA', (fw * cols, fh * rows), (0, 0, 0, 0))
    for i, frame in enumerate(frames):
        row, col = i // cols, i % cols
        sheet.paste(frame, (col * fw, row * fh))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    sheet.save(path)

def generate_all_characters():
    print("=" * 60)
    print("Gangland Undercover Character Generator")
    print("=" * 60)

    total_sprites = 0
    for prof_key, prof in PROFESSIONS.items():
        print(f"\n[{prof['name']}] Generating...")
        prof_dir = os.path.join(OUT_DIR, prof_key)
        os.makedirs(prof_dir, exist_ok=True)

        # === IDLE 帧 ===
        idle_img = generate_idle_frame(prof_key, prof)
        idle_path = os.path.join(prof_dir, f"{prof_key}_idle.png")
        idle_img.save(idle_path)
        print(f"  ✓ idle: {idle_path}")

        # === 四方向 (idle 通用，只做标识文件) ===
        for d in DIRECTIONS:
            d_path = os.path.join(prof_dir, f"{prof_key}_idle_{d}.png")
            idle_img.save(d_path)

        # === WALK 帧 ===
        walk_frames = generate_walk_frames(idle_img, num_frames=4)
        walk_path = os.path.join(prof_dir, f"{prof_key}_walk.png")
        save_sprite_sheet(walk_frames, walk_path, cols=4)
        print(f"  ✓ walk sheet: {walk_path} ({len(walk_frames)} frames)")

        # 四方向 walk sheet
        for d in DIRECTIONS:
            d_walk = os.path.join(prof_dir, f"{prof_key}_walk_{d}.png")
            save_sprite_sheet(walk_frames, d_walk, cols=4)

        total_sprites += 1 + 4 + 1 + 4  # idle + 4dir idle + walk + 4dir walk

    print(f"\n{'=' * 60}")
    print(f"TOTAL: {len(PROFESSIONS)} professions, {total_sprites} sprites")
    print(f"Output: {OUT_DIR}")
    print(f"{'=' * 60}")

if __name__ == "__main__":
    generate_all_characters()
