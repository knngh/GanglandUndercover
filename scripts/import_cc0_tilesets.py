#!/usr/bin/env python3
"""
import_cc0_tilesets.py — 为每个地图主题挑选代表性tileset精灵

从 organized tileset 中为 harbour/police_station/kowloon 各主题
挑选代表性的地板/墙壁/道具/装饰精灵，复制到 Unity Resources。
"""

import shutil
import json
from pathlib import Path

PROJECT_ROOT = Path("/Users/zhugehao/projects/GanglandUndercover")
TILESET_CACHE = PROJECT_ROOT / ".asset_cache/art/tilesets/organized"
RESOURCES_TILES = PROJECT_ROOT / "Assets/_Project/Resources/Sprites/Tilesets"

# 主题 → 目录名映射
THEME_DIR_MAP = {
    "Harbour":           "harbour",
    "PoliceStation":     "police_station",
    "KowloonWalledCity": "kowloon_walled_city",
    "Shared":            "shared",
}

# 主题 → 精选文件
THEME_PICKS = {
    "Harbour": {
        "floors": ["cargo-bay.png", "metal-grid.png"],
        "walls": ["industrial-tech", "industrial_tiles"],
        "props": ["container.png"],
        "decorations": ["crane-hook.png", "warning-sign.png"],
    },
    "PoliceStation": {
        "floors": ["clean-room.png", "command-deck.png", "lab-floor.png"],
        "walls": ["dark-tech", "display-screen.png"],
        "props": ["evidence-locker.png"],
        "decorations": ["badge-icon.png"],
    },
    "KowloonWalledCity": {
        "floors": ["concrete-slab.png", "wooden-plank.png", "dirt-floor.png"],
        "walls": ["alien-tech.png", "residential-alley.png"],
        "props": ["street-sign.png", "lantern.png"],
        "decorations": ["neon-sign.png", "laundry-line.png"],
    },
    "Shared": {
        "floors": [],
        "walls": ["energy-shield.png", "hull-panel.png"],
        "props": ["crate-stack.png"],
        "decorations": [],
    },
}


def find_best_tile(theme_dir: Path, category: str, filename: str) -> Path | None:
    """在主题目录中查找最佳匹配tile文件"""
    cat_dir = theme_dir / category
    name_no_ext = filename.replace(".png", "")

    if not cat_dir.exists():
        return None

    # 精确匹配文件
    candidate = cat_dir / filename
    if candidate.exists() and candidate.is_file():
        return candidate

    # 匹配同名目录：取第一个PNG
    candidate_dir = cat_dir / name_no_ext
    if candidate_dir.exists() and candidate_dir.is_dir():
        pngs = sorted(candidate_dir.glob("*.png"))
        if pngs:
            return pngs[0]

    # 搜索子目录中的文件
    for sub in cat_dir.iterdir():
        if sub.is_dir():
            # 子目录中的精确文件名
            candidate = sub / filename
            if candidate.exists():
                return candidate
            # 文件名部分匹配
            for f in sub.glob("*.png"):
                if name_no_ext.lower() in f.stem.lower():
                    return f
        elif sub.is_file() and sub.suffix == ".png":
            if name_no_ext.lower() in sub.stem.lower():
                return sub

    # 兜底：任意第一个PNG
    for sub in cat_dir.iterdir():
        if sub.is_file() and sub.suffix == ".png":
            return sub
        elif sub.is_dir():
            pngs = sorted(sub.glob("*.png"))
            if pngs:
                return pngs[0]

    return None


def main():
    print("=" * 60)
    print("Gangland Undercover — CC0 Tileset 精选导入")
    print("=" * 60)

    RESOURCES_TILES.mkdir(parents=True, exist_ok=True)

    total = 0
    theme_summary = {}

    for theme, categories in THEME_PICKS.items():
        theme_cache = TILESET_CACHE / THEME_DIR_MAP[theme]
        dst_theme = RESOURCES_TILES / theme
        dst_theme.mkdir(parents=True, exist_ok=True)

        if not theme_cache.exists():
            print(f"  ⚠️  {theme}: 缓存目录不存在 ({theme_cache})")
            continue

        theme_count = 0
        theme_files = {}

        for category, filenames in categories.items():
            dst_cat = dst_theme / category
            dst_cat.mkdir(parents=True, exist_ok=True)

            for fname in filenames:
                src = find_best_tile(theme_cache, category, fname)
                if src is None:
                    print(f"  ❌ {theme}/{category}/{fname} — 未找到")
                    continue

                dst_name = src.name.replace(" ", "_")
                dst_target = dst_cat / dst_name
                shutil.copy2(src, dst_target)
                theme_count += 1
                total += 1
                theme_files.setdefault(category, []).append(dst_name)

                rel = src.relative_to(theme_cache) if theme_cache in src.parents else src.name
                print(f"  ✅ {theme}/{category}/{dst_name:30s} ← {rel}")

        theme_summary[theme] = {"count": theme_count, "files": theme_files}
        print(f"  ── {theme}: {theme_count} tiles\n")

    manifest = {
        "source": "Kenney CC0 tileset packs",
        "total_imported": total,
        "themes": theme_summary,
    }

    manifest_path = RESOURCES_TILES / "tileset_manifest.json"
    with open(manifest_path, 'w') as f:
        json.dump(manifest, f, indent=2)

    print(f"📊 共导入 {total} 个 tileset 精灵")
    print(f"📄 清单: {manifest_path}")
    print("=" * 60)


if __name__ == "__main__":
    main()
