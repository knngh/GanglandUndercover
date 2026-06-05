#!/usr/bin/env python3
"""Gangland Undercover Tileset 生成器

3 张地图 tileset 纯 PIL 程序化生成：
- Harbour District (港区): ~80 tiles, 水泥/金属/货柜/霓虹
- Police Station (警署): ~50 tiles, 瓷砖/白墙/办公桌/审讯室
- Kowloon Walled City (城寨): ~55 tiles, 木地板/砖墙/霓虹招牌/窄巷

策略：几何绘制 + Art Bible 主题色映射
依赖: pip install Pillow
用法: python generate_tilesets.py [harbour|police|kowloon|all]
"""

import os
import math
from PIL import Image, ImageDraw

# === Art Bible 色板 ===
HARBOUR_BG = (26, 28, 44)
POLICE_BG = (20, 20, 32)
KOWLOON_BG = (15, 15, 26)
NEON_YELLOW = (244, 162, 54)
NEON_CYAN = (26, 158, 170)
NEON_PINK = (231, 76, 160)
EMERGENCY_RED = (192, 57, 43)
POLICE_BLUE = (45, 111, 186)
GANG_RED = (192, 57, 43)
WHITE = (255, 255, 255)
BLACK = (0, 0, 0)

TILE = 32
BASE = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "Assets", "_Project", "Art", "2D", "Tiles")

def new_tile(fill=None):
    img = Image.new('RGBA', (TILE, TILE), (0, 0, 0, 0))
    if fill:
        draw = ImageDraw.Draw(img)
        draw.rectangle([0, 0, TILE-1, TILE-1], fill=fill)
    return img

def make_checker(primary, secondary):
    """棋盘格地板"""
    img = new_tile()
    draw = ImageDraw.Draw(img)
    for y in range(0, TILE, 8):
        for x in range(0, TILE, 8):
            is_light = ((x//8) + (y//8)) % 2 == 0
            draw.rectangle([x, y, x+7, y+7], fill=primary if is_light else secondary)
    return img

def make_diagonal(primary, secondary):
    """斜纹地板"""
    img = new_tile()
    for y in range(TILE):
        for x in range(TILE):
            img.putpixel((x, y), primary if (x + y) % 16 < 8 else secondary)
    return img

def make_grid(primary, secondary, grid_size=8):
    """网格地板"""
    img = new_tile(primary)
    draw = ImageDraw.Draw(img)
    for i in range(0, TILE, grid_size):
        draw.line([(i, 0), (i, TILE-1)], fill=secondary, width=1)
        draw.line([(0, i), (TILE-1, i)], fill=secondary, width=1)
    return img

def make_solid(color):
    return new_tile(color)

def make_wall(color, thickness=4):
    """墙壁 tile"""
    img = new_tile()
    draw = ImageDraw.Draw(img)
    # 顶部阴影边缘
    draw.rectangle([0, 0, TILE-1, thickness], fill=color)
    return img

def make_door(bg_color, accent):
    """门 tile"""
    img = new_tile()
    draw = ImageDraw.Draw(img)
    # 门框
    draw.rectangle([2, 2, TILE-3, TILE-3], outline=accent, width=2)
    # 门板
    draw.rectangle([4, 4, TILE-5, TILE-5], fill=bg_color)
    # 门把手
    draw.rectangle([TILE-9, TILE//2-2, TILE-7, TILE//2+2], fill=accent)
    return img

def make_crate(wood_color, accent):
    """木箱 tile"""
    img = new_tile()
    draw = ImageDraw.Draw(img)
    draw.rectangle([4, 4, TILE-5, TILE-5], fill=wood_color)
    draw.rectangle([4, 4, TILE-5, 6], fill=accent)
    draw.line([(4, TILE//2), (TILE-5, TILE//2)], fill=accent, width=1)
    # 金属角
    draw.rectangle([4, 4, 7, 7], fill=accent)
    draw.rectangle([TILE-8, 4, TILE-5, 7], fill=accent)
    return img

def make_barrel(bg, color):
    """金属桶 tile"""
    img = new_tile(bg)
    draw = ImageDraw.Draw(img)
    # 桶身
    draw.rectangle([8, 6, TILE-9, TILE-7], fill=color)
    draw.rectangle([6, 8, TILE-7, TILE-9], fill=color)
    # 桶边
    hl = tuple(min(c+40, 255) for c in color)
    draw.line([(6, 6), (TILE-7, 6)], fill=hl, width=2)
    draw.line([(6, TILE-8), (TILE-7, TILE-8)], fill=hl, width=2)
    return img

def make_neon_sign(bg, neon_color):
    """霓虹招牌 tile"""
    img = new_tile(bg)
    draw = ImageDraw.Draw(img)
    # 招牌框
    draw.rectangle([4, 8, TILE-5, 24], outline=neon_color, width=2)
    # 霓虹发光
    glow = tuple(min(c+100, 255) for c in neon_color)
    draw.rectangle([5, 9, TILE-6, 23], fill=glow)
    # 文字（简化横条）
    draw.rectangle([8, 12, TILE-9, 14], fill=WHITE)
    draw.rectangle([8, 17, TILE-13, 19], fill=WHITE)
    return img

def make_desk(wood_color, accent):
    """办公桌 tile"""
    img = new_tile()
    draw = ImageDraw.Draw(img)
    # 桌面
    draw.rectangle([2, 6, TILE-3, TILE-8], fill=wood_color)
    draw.rectangle([2, 6, TILE-3, 8], fill=accent)
    # 桌腿
    draw.rectangle([3, TILE-8, 6, TILE-3], fill=accent)
    draw.rectangle([TILE-7, TILE-8, TILE-4, TILE-3], fill=accent)
    return img

def make_filing_cabinet(bg, metal_color):
    """档案柜 tile"""
    img = new_tile(bg)
    draw = ImageDraw.Draw(img)
    draw.rectangle([5, 2, TILE-6, TILE-3], fill=metal_color)
    draw.rectangle([5, TILE//2-1, TILE-6, TILE//2+1], fill=BLACK)
    draw.rectangle([5, 8, TILE-6, 10], fill=BLACK)
    # 把手
    hl = tuple(min(c+60, 255) for c in metal_color)
    draw.rectangle([TILE-10, TILE//2-3, TILE-8, TILE//2], fill=hl)
    return img

def make_vent(bg, metal_color):
    """通风口 tile"""
    img = new_tile(bg)
    draw = ImageDraw.Draw(img)
    draw.rectangle([4, 4, TILE-5, TILE-5], fill=BLACK)
    for i in range(6, TILE-6, 4):
        draw.line([(5, i), (TILE-6, i)], fill=metal_color, width=1)
    return img

def make_bamboo_scaffold(bg):
    """竹架 tile（九龙城寨）"""
    img = new_tile(bg)
    draw = ImageDraw.Draw(img)
    bamboo = (160, 120, 60)
    # 竖杆
    draw.rectangle([4, 0, 6, TILE-1], fill=bamboo)
    draw.rectangle([TILE-7, 0, TILE-5, TILE-1], fill=bamboo)
    # 横杆
    draw.rectangle([0, 10, TILE-1, 12], fill=bamboo)
    draw.rectangle([0, 20, TILE-1, 22], fill=bamboo)
    return img

def make_lantern(bg):
    """灯笼 tile（九龙城寨）"""
    img = new_tile(bg)
    draw = ImageDraw.Draw(img)
    # 线
    draw.line([(TILE//2, 0), (TILE//2, 8)], fill=BLACK, width=1)
    # 灯笼身
    draw.ellipse([TILE//2-6, 8, TILE//2+6, 22], fill=GANG_RED)
    draw.ellipse([TILE//2-4, 10, TILE//2+4, 20], fill=(255, 180, 100))  # 内光
    return img

def make_wires(bg):
    """电线/电缆 tile（九龙城寨）"""
    img = new_tile(bg)
    draw = ImageDraw.Draw(img)
    wire = (50, 50, 50)
    draw.line([(0, 4), (TILE-1, 8)], fill=wire, width=1)
    draw.line([(0, 14), (TILE-1, 12)], fill=wire, width=1)
    draw.line([(2, 22), (TILE-3, 24)], fill=wire, width=1)
    return img

def make_whiteboard(bg):
    """白板 tile（警署）"""
    img = new_tile(bg)
    draw = ImageDraw.Draw(img)
    DG = (52, 73, 94)
    draw.rectangle([3, 6, TILE-4, TILE-5], fill=WHITE)
    draw.rectangle([3, 6, TILE-4, TILE-5], outline=DG, width=1)
    # 标记笔痕迹
    draw.line([(6, 10), (TILE-6, 14)], fill=DG, width=1)
    draw.line([(8, 16), (TILE-8, 18)], fill=DG, width=1)
    return img

def make_container(bg):
    """货柜 tile（港区）"""
    img = new_tile(bg)
    draw = ImageDraw.Draw(img)
    container_color = (100, 120, 140)
    draw.rectangle([1, 1, TILE-2, TILE-2], fill=container_color)
    draw.rectangle([1, 1, TILE-2, 4], fill=(80, 100, 120))  # 顶部
    # 波纹纹理
    for y in range(6, TILE-4, 5):
        draw.line([(2, y), (TILE-3, y)], fill=(90, 110, 130), width=1)
    return img

def make_mahjong_table(bg):
    """麻将桌 tile（城寨）"""
    img = new_tile(bg)
    draw = ImageDraw.Draw(img)
    wood = (120, 70, 30)
    green = (40, 100, 40)
    draw.rectangle([3, 4, TILE-4, TILE-5], fill=wood)
    draw.rectangle([5, 6, TILE-6, TILE-7], fill=green)  # 桌面绒布
    # 牌
    for i, (dx, dy) in enumerate([(6,8),(TILE-10,8),(6,TILE-12),(TILE-10,TILE-12)]):
        draw.rectangle([dx, dy, dx+4, dy+6], fill=WHITE)
    return img

def make_water_puddle(bg):
    """水洼 tile（港区后巷）"""
    img = new_tile(bg)
    draw = ImageDraw.Draw(img)
    water = (40, 60, 100, 100)
    # 不规则形状
    draw.ellipse([6, 8, TILE-8, TILE-10], fill=water)
    draw.ellipse([4, 10, 14, 20], fill=water)
    draw.ellipse([TILE-16, 12, TILE-6, 22], fill=water)
    return img

def make_interrogation_mirror(bg):
    """审讯室单面镜"""
    img = new_tile(bg)
    draw = ImageDraw.Draw(img)
    draw.rectangle([4, 2, TILE-5, TILE-3], fill=(80, 100, 140))
    draw.rectangle([5, 3, TILE-6, TILE-4], fill=(120, 160, 200, 150))
    draw.line([(5, 3), (TILE-6, TILE-4)], fill=(200, 220, 240, 100), width=1)
    return img


# ================================================================
# 地图 tileset 定义
# ================================================================

HARBOUR_TILES = {
    # === 地板 ===
    "floor_concrete": lambda: make_solid((80, 85, 95)),
    "floor_concrete_dark": lambda: make_solid((60, 65, 75)),
    "floor_metal": lambda: make_solid((90, 95, 105)),
    "floor_metal_grid": lambda: make_grid((90, 95, 105), (60, 65, 75), 8),
    "floor_checker": lambda: make_checker((80, 85, 95), (60, 65, 75)),
    "floor_wet": lambda: make_diagonal((40, 50, 70), (30, 35, 50)),
    # === 墙壁 ===
    "wall_top": lambda: make_wall((50, 55, 70), 4),
    "wall_side": lambda: make_solid((40, 45, 60)),
    "wall_corner": lambda: make_wall((60, 65, 80), 5),
    # === 门 ===
    "door_metal": lambda: make_door((40, 45, 60), (100, 110, 120)),
    "door_wood": lambda: make_door((80, 60, 30), (120, 80, 40)),
    # === 货柜场 ===
    "container_blue": lambda: make_container(HARBOUR_BG),
    "crate_wood": lambda: make_crate((120, 80, 40), (80, 50, 20)),
    "crate_metal": lambda: make_crate((100, 105, 115), (70, 75, 85)),
    "barrel_oil": lambda: make_barrel(HARBOUR_BG, (60, 50, 40)),
    "barrel_metal": lambda: make_barrel(HARBOUR_BG, (100, 105, 115)),
    # === 后巷 ===
    "puddle": lambda: make_water_puddle(HARBOUR_BG),
    "vent_backalley": lambda: make_vent(HARBOUR_BG, (90, 95, 105)),
    # === 霓虹 ===
    "neon_yellow": lambda: make_neon_sign(HARBOUR_BG, NEON_YELLOW),
    "neon_cyan": lambda: make_neon_sign(HARBOUR_BG, NEON_CYAN),
    "neon_pink": lambda: make_neon_sign(HARBOUR_BG, NEON_PINK),
    # === 电房 ===
    "floor_electrical": lambda: make_checker((70, 75, 85), (90, 95, 105)),
    "machine_panel": lambda: make_filing_cabinet(HARBOUR_BG, (80, 85, 95)),
    # === 茶餐厅 ===
    "floor_tile_tea": lambda: make_checker((180, 170, 150), (160, 150, 130)),
    "desk_tea": lambda: make_desk((140, 100, 50), (100, 70, 30)),
    # === 额外 ===
    "wall_brick": lambda: make_diagonal((60, 55, 50), (50, 45, 40)),
    "cable_floor": lambda: make_wires(HARBOUR_BG),
    "sign_exit": lambda: make_neon_sign(HARBOUR_BG, EMERGENCY_RED),
}

POLICE_TILES = {
    # === 地板 ===
    "floor_tile_white": lambda: make_checker((220, 220, 225), (200, 200, 208)),
    "floor_tile_blue": lambda: make_checker((180, 190, 210), (160, 170, 190)),
    "floor_linoleum": lambda: make_solid((190, 195, 200)),
    # === 墙壁 ===
    "wall_white": lambda: make_wall((210, 210, 215), 3),
    "wall_blue_stripe": lambda: make_wall(POLICE_BLUE, 6),
    # === 门 ===
    "door_offic": lambda: make_door((200, 200, 205), POLICE_BLUE),
    "door_interrogation": lambda: make_door((80, 85, 90), (120, 125, 130)),
    # === 办公室 ===
    "desk_office": lambda: make_desk((160, 140, 100), (100, 80, 50)),
    "filing_cabinet": lambda: make_filing_cabinet(POLICE_BG, (140, 145, 150)),
    "whiteboard": lambda: make_whiteboard(POLICE_BG),
    # === 审讯室 ===
    "floor_interrogation": lambda: make_solid((60, 65, 70)),
    "mirror": lambda: make_interrogation_mirror(POLICE_BG),
    # === 大厅 ===
    "desk_reception": lambda: make_desk((180, 160, 120), (120, 100, 70)),
    # === 武器库 ===
    "floor_armory": lambda: make_grid((120, 125, 130), (100, 105, 110)),
    "locker": lambda: make_filing_cabinet(POLICE_BG, (100, 105, 115)),
    # === 拘留室 ===
    "floor_cell": lambda: make_solid((70, 75, 80)),
    "bars": lambda: make_grid((70, 75, 80), (40, 45, 50), 4),
}

KOWLOON_TILES = {
    # === 地板 ===
    "floor_wood": lambda: make_solid((120, 80, 45)),
    "floor_wood_dark": lambda: make_diagonal((100, 60, 30), (80, 45, 20)),
    "floor_concrete_old": lambda: make_solid((70, 65, 60)),
    "floor_tile_old": lambda: make_checker((100, 90, 80), (80, 70, 60)),
    # === 墙壁 ===
    "wall_brick_old": lambda: make_wall((90, 70, 50), 5),
    "wall_concrete": lambda: make_wall((60, 55, 50), 4),
    # === 门 ===
    "door_old_wood": lambda: make_door((80, 50, 20), (50, 30, 10)),
    "door_metal_rust": lambda: make_door((60, 55, 50), (100, 70, 40)),
    # === 霓虹 ===
    "neon_sign_red": lambda: make_neon_sign(KOWLOON_BG, GANG_RED),
    "neon_sign_blue": lambda: make_neon_sign(KOWLOON_BG, POLICE_BLUE),
    "neon_sign_pink": lambda: make_neon_sign(KOWLOON_BG, NEON_PINK),
    # === 窄巷 ===
    "bamboo_scaffold": lambda: make_bamboo_scaffold(KOWLOON_BG),
    "lantern_red": lambda: make_lantern(KOWLOON_BG),
    "wires": lambda: make_wires(KOWLOON_BG),
    "puddle_alley": lambda: make_water_puddle(KOWLOON_BG),
    # === 药材铺 ===
    "floor_herb": lambda: make_checker((140, 110, 70), (120, 90, 50)),
    "cabinet_herb": lambda: make_filing_cabinet(KOWLOON_BG, (100, 70, 40)),
    # === 麻将馆 ===
    "mahjong_table": lambda: make_mahjong_table(KOWLOON_BG),
    # === 通风/暗渠 ===
    "vent_rust": lambda: make_vent(KOWLOON_BG, (100, 70, 40)),
    # === 额外 ===
    "crate_old": lambda: make_crate((80, 50, 30), (50, 30, 20)),
    "sign_shop": lambda: make_neon_sign(KOWLOON_BG, NEON_YELLOW),
}

def generate_tileset(name, tiles, out_dir):
    print(f"\n{'─'*50}")
    print(f"[{name}] Generating {len(tiles)} tiles...")
    full_dir = os.path.join(out_dir, name)
    os.makedirs(full_dir, exist_ok=True)

    for tile_name, gen_fn in tiles.items():
        try:
            tile = gen_fn()
            path = os.path.join(full_dir, f"tile_{tile_name}.png")
            tile.save(path)
        except Exception as e:
            print(f"  ✗ {tile_name}: {e}")

    count = len([f for f in os.listdir(full_dir) if f.endswith('.png')])
    print(f"  ✓ {count}/{len(tiles)} tiles generated")
    return count

def main():
    import sys
    targets = sys.argv[1:] if len(sys.argv) > 1 else ["all"]

    if "all" in targets or "harbour" in targets:
        generate_tileset("Harbour", HARBOUR_TILES, BASE)
    if "all" in targets or "police" in targets:
        generate_tileset("Police", POLICE_TILES, BASE)
    if "all" in targets or "kowloon" in targets:
        generate_tileset("Kowloon", KOWLOON_TILES, BASE)

    # Summary
    print(f"\n{'='*60}")
    total = 0
    for d in ["Harbour", "Police", "Kowloon"]:
        dpath = os.path.join(BASE, d)
        if os.path.isdir(dpath):
            n = len([f for f in os.listdir(dpath) if f.endswith('.png')])
            print(f"  {d}: {n} tiles")
            total += n
    print(f"  TOTAL: {total} tiles")
    print(f"  Output: {BASE}")
    print(f"{'='*60}")

if __name__ == "__main__":
    main()
