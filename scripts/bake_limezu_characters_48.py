#!/usr/bin/env python3
"""
bake_limezu_characters_48.py — 批次 3：角色替换（LimeZu 48x48 换装）

按 art_style_lock_20261006.md（D1: 48x48 / D2: 换装方案）烘焙 8 职业运行时角色：
  Body + Eyes + Outfit + Hairstyle 四层同网格叠加（官方 CHARACTER_GENERATOR.txt 顺序）
  -> 切 idle（部件表 r1）/ walk（部件表 r3，每方向 6 帧）
  -> 输出 {Profession}/{Front,Back,Left,Right}/{idle,walk_0,walk_1,walk_2}.png + avatar.png + death.png
  -> 更新 cc0_manifest.json（结构不变，值更新为 LimeZu 来源）

已验证的 LimeZu Modern Interiors 48x48 部件表布局（Eyes 表像素级验证，2026-10-08）：
  r1: idle 单帧 4 方向 = [c0=Right, c1=Up, c2=Left, c3=Down]
  r3: walk 循环 24 帧 = c0-5 Right / c6-11 Up / c12-17 Left / c18-23 Down
  （部件表相对 Premade 成品表整体下移一行；本脚本直接操作部件表，行号已按部件表）

幂等性：仅覆盖 Resources/Sprites/Characters/ 下既有同名 PNG（GUID 不变，引用不破）。
"""

import json
import re
from pathlib import Path

from PIL import Image, ImageOps

PROJECT_ROOT = Path("/Users/zhugehao/projects/GanglandUndercover")
GEN = PROJECT_ROOT / ".asset_cache/purchased/limezu/modern-interiors/moderninteriors-win/2_Characters/Character_Generator"
OUT_ROOT = PROJECT_ROOT / "Assets/_Project/Resources/Sprites/Characters"
BACKUP_STAMP = "20261008"

CELL = 48
IDLE_ROW = 1            # 部件表 idle 行
WALK_ROW = 3            # 部件表 walk 行
# 部件表方向 -> idle 列 / walk 起始列
DIRS = {
    "Right": {"idle_col": 0, "walk_start": 0},
    "Up":    {"idle_col": 1, "walk_start": 6},
    "Left":  {"idle_col": 2, "walk_start": 12},
    "Down":  {"idle_col": 3, "walk_start": 18},
}
# Unity 输出目录名映射（路径约定硬不变量）
OUT_DIR_NAME = {"Down": "Front", "Up": "Back", "Left": "Left", "Right": "Right"}
# walk 组内取帧相位：idle 组第 0 帧为站立相，2/4 为摆动相，5 为过渡相
WALK_PHASE = {"walk_0": 2, "walk_1": 4, "walk_2": 5}

# 职业换装配置（outfit 款号经 outfit_preview_v2.png 目检选定）
# Mole 与 Inspector 完全同装 —— style_lock §3 隐藏阵营原则（Mole 外观 = Inspector）
PROFESSIONS = {
    "Inspector":      {"outfit": 19, "hair": 4,  "body": 1, "eyes": 1,
                       "accent": (45, 111, 186)},   # gangland_palette.POLICE_BLUE
    "Forensics":      {"outfit": 8,  "hair": 15, "body": 1, "eyes": 1,
                       "accent": (220, 20, 60)},    # Medic secondary（白大褂红十字）
    "Tech":           {"outfit": 16, "hair": 24, "body": 1, "eyes": 1,
                       "accent": (26, 158, 170)},   # TechExpert primary 青绿
    "UndercoverAgent": {"outfit": 28, "hair": 14, "body": 2, "eyes": 1,
                       "accent": (142, 68, 173)},   # UNDERCOVER_PURPLE（外观不带警方色）
    "Enforcer":       {"outfit": 26, "hair": 11, "body": 3, "eyes": 1,
                       "accent": (192, 57, 43)},    # GANG_RED
    "Fixer":          {"outfit": 3,  "hair": 23, "body": 4, "eyes": 1,
                       "accent": (180, 140, 60)},   # 市井棕金（palette 外无对应职业键）
    "Driver":         {"outfit": 23, "hair": 6,  "body": 5, "eyes": 1,
                       "accent": (40, 60, 80)},     # Driver primary 深蓝工装
    "Mole":           {"outfit": 19, "hair": 4,  "body": 1, "eyes": 1,
                       "accent": (149, 165, 166)},  # MOLE_GREY（仅 avatar 边框区分，外观=Inspector）
}


def find_layer(folder: str, pattern: str) -> Path:
    """在部件目录按款号找第一个变体文件。"""
    candidates = sorted((GEN / folder / "48x48").glob(pattern))
    if not candidates:
        raise FileNotFoundError(f"{folder}: 未找到 {pattern}")
    return candidates[0]


def load_cell(img: Image.Image, row: int, col: int) -> Image.Image:
    return img.crop((col * CELL, row * CELL, (col + 1) * CELL, (row + 1) * CELL))


def compose(prof: str, cfg: dict) -> dict:
    """合成四层，返回 {方向: {帧名: Image}}。"""
    body = Image.open(find_layer("Bodies", f"Body_48x48_{cfg['body']:02d}.png")).convert("RGBA")
    eyes = Image.open(find_layer("Eyes", f"Eyes_48x48_{cfg['eyes']:02d}.png")).convert("RGBA")
    outfit = Image.open(find_layer("Outfits", f"Outfit_{cfg['outfit']:02d}_48x48_*.png")).convert("RGBA")
    hair = Image.open(find_layer("Hairstyles", f"Hairstyle_{cfg['hair']:02d}_48x48_*.png")).convert("RGBA")

    frames = {}
    for direction, spec in DIRS.items():
        frames[direction] = {}
        # idle：部件表 r1 对应列
        idle = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
        for layer in (body, eyes, outfit, hair):
            idle.alpha_composite(load_cell(layer, IDLE_ROW, spec["idle_col"]))
        frames[direction]["idle"] = idle
        # walk 三相位：部件表 r3 walk 组内 2/4/5 帧
        for name, phase in WALK_PHASE.items():
            f = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
            for layer in (body, eyes, outfit, hair):
                f.alpha_composite(load_cell(layer, WALK_ROW, spec["walk_start"] + phase))
            frames[direction][name] = f
    return frames


def make_death(front_idle: Image.Image) -> Image.Image:
    """death 帧：正面 idle 旋转 90° 躺倒 + 去饱和 55%，保持 48x48 画布。"""
    d = front_idle.rotate(90, expand=False)
    alpha = d.getchannel("A")
    grey = ImageOps.grayscale(d.convert("RGB"))
    desat = ImageOps.colorize(grey, black=(40, 40, 40), white=(210, 210, 210), mid=(128, 120, 118)).convert("RGBA")
    desat.putalpha(alpha)
    return desat


def make_avatar(front_idle: Image.Image, accent: tuple) -> Image.Image:
    """avatar：正面 idle 头部区域（上半 26px）放大居中 + 2px 职业色边框，48x48。"""
    # 内容 bbox 上部为头部
    bbox = front_idle.getbbox()
    head_h = 26
    head = front_idle.crop((bbox[0], bbox[1], bbox[2], min(bbox[1] + head_h, bbox[3])))
    # 等比放大到接近 44x44 内框
    w, h = head.size
    scale = min(44 / w, 44 / h)
    nw, nh = max(1, int(w * scale)), max(1, int(h * scale))
    head = head.resize((nw, nh), Image.NEAREST)
    av = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    av.alpha_composite(head, ((CELL - nw) // 2, (CELL - nh) // 2))
    # 2px accent 边框
    px = av.load()
    for i in range(CELL):
        for t in range(2):
            for coord in ((i, t), (i, CELL - 1 - t), (t, i), (CELL - 1 - t, i)):
                px[coord] = (*accent, 255)
    return av


def bake_death_and_avatar(frames: dict, cfg: dict) -> tuple:
    death = make_death(frames["Down"]["idle"])
    avatar = make_avatar(frames["Down"]["idle"], cfg["accent"])
    return death, avatar


def main():
    print("=" * 64)
    print("Gangland Undercover — 批次 3 角色烘焙（LimeZu 48x48）")
    print("=" * 64)
    written = 0
    manifest_professions = {}
    for prof, cfg in PROFESSIONS.items():
        frames = compose(prof, cfg)
        dst = OUT_ROOT / prof
        for direction, frame_map in frames.items():
            ddir = dst / OUT_DIR_NAME[direction]
            ddir.mkdir(parents=True, exist_ok=True)
            for name, img in frame_map.items():
                img.save(ddir / f"{name}.png")
                written += 1
        death, avatar = bake_death_and_avatar(frames, cfg)
        death.save(dst / "death.png")
        avatar.save(dst / "avatar.png")
        written += 2
        manifest_professions[prof] = (
            f"LimeZu Outfit_{cfg['outfit']}/H{cfg['hair']:02d}/Body_{cfg['body']:02d}"
            f"{' (mirror of Inspector)' if prof == 'Mole' else ''}"
        )
        print(f"  ✔ {prof}: outfit#{cfg['outfit']} hair H{cfg['hair']:02d} -> {dst}")

    # 更新 manifest（结构不变：键集合保持，值更新）
    mf = OUT_ROOT / "cc0_manifest.json"
    data = json.loads(mf.read_text())
    data["source"] = "LimeZu Modern Interiors Character_Generator baked layers (Body+Eyes+Outfit+Hairstyle), licensed via purchased pack"
    data["sprite_size"] = "48x48 movement/death, 48x48 avatar"
    data["profession_map"] = manifest_professions
    mf.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n")
    print(f"  ✔ manifest 更新: {mf}")
    print(f"共写入 {written} 张 PNG（期望 144）")
    assert written == 144, f"数量异常: {written} != 144"
    print("OK 144/144")


if __name__ == "__main__":
    main()
