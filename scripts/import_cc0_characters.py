#!/usr/bin/env python3
"""
import_cc0_characters.py — 将CC0像素角色精灵导入Unity Resources

1. 从 organized_unity 中为每个职业×方向挑选代表帧
2. 复制到 Unity Resources/Sprites/Characters/
3. 生成职业映射配置文件
"""

import json
import os
import shutil
from pathlib import Path

PROJECT_ROOT = Path("/Users/zhugehao/projects/GanglandUndercover")
CHAR_CACHE = PROJECT_ROOT / ".asset_cache/art/characters/organized_unity/Roguelike_Characters"
DESERT_CACHE = PROJECT_ROOT / ".asset_cache/art/characters/organized_unity/Desert_Shooter"
RESOURCES_SPRITES = PROJECT_ROOT / "Assets/_Project/Resources/Sprites"
RESOURCES_CHARS = RESOURCES_SPRITES / "Characters"

# 游戏职业 → CC0角色类型映射
PROF_TO_CC0 = {
    "Inspector":       "Cop",       # 警探 → 警察
    "Forensics":       "Cop",       # 法医 → 警察（同模型）
    "Tech":            "Cop",       # 技术员 → 警察（同模型）
    "UndercoverAgent": "Triad",     # 卧底 → 三合会
    "Enforcer":        "Thug",      # 打手 → 暴徒
    "Fixer":           "Dealer",    # 清道夫 → 毒贩
    "Driver":          "Informant", # 车手 → 线人
    "Mole":            "CorpSec",   # 内鬼 → 公司保安
}

# 每方向取4帧：idle, walk1, walk2, walk3
# 每方向12-14帧 (m_0000 ~ m_0013)
# idle=0, walk均匀采样剩余帧
FRAME_INDICES = {
    "idle": 0,
    "walk_0": 4,
    "walk_1": 8,
    "walk_2": 12,
}

# 方向映射: CC0目录 → Unity命名
DIR_MAP = {
    "Down":  "Front",
    "Up":    "Back",
    "Left":  "Left",
    "Right": "Right",
}


def main():
    print("=" * 60)
    print("Gangland Undercover — CC0 角色精灵导入")
    print("=" * 60)

    RESOURCES_CHARS.mkdir(parents=True, exist_ok=True)

    imported = 0
    profession_map = {}

    for game_prof, cc0_type in PROF_TO_CC0.items():
        src_root = CHAR_CACHE / cc0_type
        if not src_root.exists():
            print(f"  ⚠️  {game_prof} → {cc0_type}: 源目录不存在")
            continue

        dst_root = RESOURCES_CHARS / game_prof
        dst_root.mkdir(parents=True, exist_ok=True)

        frames_copied = 0
        for cc0_dir, unity_dir in DIR_MAP.items():
            src_dir = src_root / cc0_dir
            dst_dir = dst_root / unity_dir
            dst_dir.mkdir(parents=True, exist_ok=True)

            if not src_dir.exists():
                continue

            # 列出所有帧文件
            all_frames = sorted(src_dir.glob("m_*.png"))
            max_frame = len(all_frames)

            if max_frame == 0:
                continue

            # 挑选代表帧（自适应：如果帧不够则用最后帧填充）
            for frame_name, idx in FRAME_INDICES.items():
                actual_idx = min(idx, max_frame - 1)
                src = all_frames[actual_idx]
                dst = dst_dir / f"{frame_name}.png"
                shutil.copy2(src, dst)
                frames_copied += 1

        imported += frames_copied
        profession_map[game_prof] = cc0_type
        print(f"  ✅ {game_prof:20s} ← {cc0_type:12s} ({frames_copied} frames)")

    # ── 沙漠射手资产（武器/NPC） ──
    print(f"\n🔫 Desert Shooter 资产:")
    desert_dst = RESOURCES_SPRITES / "DesertShooter"
    desert_dst.mkdir(parents=True, exist_ok=True)

    for sub in ["Player", "Enemies", "Weapons"]:
        src_sub = DESERT_CACHE / sub
        if src_sub.exists():
            dst_sub = desert_dst / sub
            dst_sub.mkdir(parents=True, exist_ok=True)
            pngs = sorted(src_sub.glob("*.png"))
            for png in pngs:
                shutil.copy2(png, dst_sub / png.name)
            print(f"  ✅ {sub:12s}: {len(pngs)} sprites")

    # ── 生成清单 ──
    manifest = {
        "source": "Kenney Roguelike Characters + Desert Shooter (CC0)",
        "sprite_size": "16×16",
        "frame_count": {k: v for k, v in PROF_TO_CC0.items() if v},
        "profession_map": profession_map,
    }

    manifest_path = RESOURCES_CHARS / "cc0_manifest.json"
    with open(manifest_path, 'w') as f:
        json.dump(manifest, f, indent=2)

    print(f"\n📊 共导入 {imported} 个角色帧到 Resources/Sprites/Characters/")
    print(f"📄 清单: {manifest_path}")
    print("=" * 60)


if __name__ == "__main__":
    main()
