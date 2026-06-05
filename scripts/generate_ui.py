#!/usr/bin/env python3
"""Gangland Undercover UI 组件生成器

纯 PIL 几何绘制 ~50 个 UI 像素组件：
- 按钮 (normal/hover/pressed/disabled)
- 面板/框架/边框
- 进度条
- 图标 (返回/设置/确认/警告/问号)
- 角色头像框
- 职业徽章
- 开关/复选框

依赖: pip install Pillow
用法: python generate_ui.py
"""

import os
from PIL import Image, ImageDraw

# === Art Bible 色板 ===
POLICE_BLUE = (45, 111, 186)
GANG_RED = (192, 57, 43)
UNDERCOVER_PURPLE = (142, 68, 173)
MOLE_GREY = (149, 165, 166)
TECH_GREEN = (39, 174, 96)
MEDIC_WHITE = (236, 240, 241)
DRIVER_ORANGE = (230, 126, 34)
NEON_CYAN = (26, 158, 170)
HARBOUR_BG = (26, 28, 44)
WHITE = (255, 255, 255)
BLACK = (0, 0, 0)
DARK_BG = (20, 22, 36)
PANEL_BG = (30, 33, 50)
BORDER = (60, 65, 85)
TEXT_COLOR = (220, 225, 240)
ACCENT = (80, 90, 110)

OUT_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                       "Assets", "_Project", "Art", "2D", "UI")

def make_button(w, h, state, color=POLICE_BLUE, text=""):
    """像素按钮: normal/hover/pressed/disabled"""
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    if state == "normal":
        bg = color
        border = tuple(min(c+40, 255) for c in color)
    elif state == "hover":
        bg = tuple(min(c+60, 255) for c in color)
        border = tuple(min(c+80, 255) for c in color)
    elif state == "pressed":
        bg = tuple(max(c-30, 0) for c in color)
        border = tuple(min(c+20, 255) for c in color)
    else:  # disabled
        bg = (80, 80, 90)
        border = (60, 60, 70)

    # 按钮主体（3D效果边框）
    draw.rectangle([0, 0, w-1, h-1], fill=bg)  # 底部阴影
    draw.rectangle([1, 1, w-3, h-3], fill=border)  # 边框
    draw.rectangle([2, 2, w-4, h-4], fill=bg)  # 主体

    # 高光顶边
    hl = tuple(min(c+60, 255) for c in bg)
    draw.line([(3, 3), (w-5, 3)], fill=hl, width=1)

    return img

def make_panel(w, h):
    """面板/框架"""
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    draw.rectangle([0, 0, w-1, h-1], fill=PANEL_BG)
    draw.rectangle([0, 0, w-1, h-1], outline=BORDER, width=2)
    # 标题栏
    draw.rectangle([0, 0, w-1, 24], fill=DARK_BG)
    draw.line([(0, 24), (w-1, 24)], fill=BORDER, width=1)
    return img

def make_progress_bar(w, h, fill_pct=0.5, color=NEON_CYAN):
    """进度条"""
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    # 背景
    draw.rectangle([0, 0, w-1, h-1], fill=DARK_BG)
    draw.rectangle([0, 0, w-1, h-1], outline=BORDER, width=1)
    # 填充
    fill_w = int((w-4) * fill_pct)
    if fill_w > 0:
        draw.rectangle([2, 2, 2+fill_w, h-3], fill=color)
    return img

def make_icon(name):
    """小图标 16×16"""
    img = Image.new('RGBA', (16, 16), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    icons = {
        "back": lambda: [draw.line([(10,3),(4,8),(10,13)], fill=WHITE, width=2)],
        "close": lambda: [draw.line([(3,3),(13,13)], fill=WHITE, width=2), draw.line([(13,3),(3,13)], fill=WHITE, width=2)],
        "confirm": lambda: [draw.line([(3,8),(6,12),(13,4)], fill=TECH_GREEN, width=2)],
        "settings": lambda: [draw.ellipse([4,4,12,12], outline=WHITE, width=1), draw.rectangle([7,1,9,3], fill=WHITE), draw.rectangle([7,13,9,15], fill=WHITE), draw.rectangle([1,7,3,9], fill=WHITE), draw.rectangle([13,7,15,9], fill=WHITE)],
        "warning": lambda: [draw.polygon([(8,2),(14,13),(2,13)], fill=GANG_RED), draw.rectangle([7,6,9,9], fill=WHITE), draw.rectangle([7,10,9,11], fill=WHITE)],
        "question": lambda: [draw.ellipse([2,2,14,14], outline=WHITE, width=1), draw.rectangle([7,4,9,5], fill=WHITE), draw.line([(8,5),(8,9)], fill=WHITE, width=1), draw.rectangle([7,11,9,12], fill=WHITE)],
        "info": lambda: [draw.ellipse([2,2,14,14], outline=POLICE_BLUE, width=1), draw.rectangle([7,5,9,6], fill=POLICE_BLUE), draw.rectangle([7,7,9,12], fill=POLICE_BLUE)],
        "lock": lambda: [draw.rectangle([4,8,12,14], fill=WHITE), draw.rectangle([5,3,11,10], outline=WHITE, width=2)],
        "skull": lambda: [draw.ellipse([3,2,13,12], fill=WHITE), draw.rectangle([6,8,10,14], fill=WHITE), draw.rectangle([5,4,7,6], fill=BLACK), draw.rectangle([9,4,11,6], fill=BLACK)],
        "refresh": lambda: [draw.arc([3,3,13,13], 0, 270, fill=WHITE, width=2), draw.polygon([(11,3),(13,1),(13,5)], fill=WHITE)],
        "play": lambda: [draw.polygon([(4,3),(13,8),(4,13)], fill=TECH_GREEN)],
        "pause": lambda: [draw.rectangle([4,3,7,13], fill=NEON_CYAN), draw.rectangle([9,3,12,13], fill=NEON_CYAN)],
    }
    if name in icons:
        icons[name]()
    return img

def make_role_badge(role, size=24):
    """职业徽章"""
    colors = {
        "police": POLICE_BLUE,
        "gangster": GANG_RED,
        "undercover": UNDERCOVER_PURPLE,
        "mole": MOLE_GREY,
    }
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    color = colors.get(role, WHITE)
    draw.ellipse([1, 1, size-2, size-2], fill=color)
    draw.ellipse([1, 1, size-2, size-2], outline=tuple(min(c+40,255) for c in color), width=1)
    return img

def make_toggle(checked=False):
    """开关 toggle"""
    w, h = 28, 16
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    bg = TECH_GREEN if checked else DARK_BG
    draw.rounded_rectangle([0, 0, w-1, h-1], radius=8, fill=bg)
    draw.rounded_rectangle([0, 0, w-1, h-1], radius=8, outline=BORDER, width=1)
    knob_x = w-13 if checked else 2
    draw.ellipse([knob_x, 2, knob_x+11, h-3], fill=WHITE)
    return img

def make_checkbox(checked=False):
    """复选框"""
    img = Image.new('RGBA', (16, 16), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    draw.rectangle([0, 0, 15, 15], fill=DARK_BG)
    draw.rectangle([0, 0, 15, 15], outline=BORDER, width=1)
    if checked:
        draw.line([(3, 8), (6, 12), (13, 4)], fill=TECH_GREEN, width=2)
    return img

def make_tab_button(w, label, active=False):
    """标签按钮"""
    h = 28
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    bg = POLICE_BLUE if active else DARK_BG
    draw.rectangle([0, 0, w-1, h-1], fill=bg)
    draw.rectangle([0, 0, w-1, h-1], outline=BORDER, width=1)
    if active:
        draw.line([(0, h-1), (w-1, h-1)], fill=POLICE_BLUE, width=2)
    return img

def make_portrait_frame(size=48):
    """角色头像框"""
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    m = 2
    draw.rectangle([m, m, size-1-m, size-1-m], fill=DARK_BG)
    draw.rectangle([m, m, size-1-m, size-1-m], outline=BORDER, width=2)
    # 内角装饰
    draw.rectangle([m+2, m+2, m+6, m+3], fill=POLICE_BLUE)
    return img

def main():
    print("=" * 60)
    print("Gangland Undercover UI Generator")
    print("=" * 60)
    os.makedirs(OUT_DIR, exist_ok=True)

    # === 按钮 ===
    print("\n[Buttons]")
    for color_name, color_val in [("blue", POLICE_BLUE), ("red", GANG_RED), ("purple", UNDERCOVER_PURPLE)]:
        for state in ["normal", "hover", "pressed", "disabled"]:
            btn = make_button(120, 32, state, color_val)
            path = os.path.join(OUT_DIR, f"btn_{color_name}_{state}.png")
            btn.save(path)
    print("  ✓ 12 buttons (3 colors × 4 states)")

    # === 面板 ===
    print("\n[Panels]")
    for w, h, name in [(200,120,"small"), (300,200,"medium"), (400,300,"large")]:
        p = make_panel(w, h)
        p.save(os.path.join(OUT_DIR, f"panel_{name}.png"))
    print("  ✓ 3 panels")

    # === 进度条 ===
    print("\n[Progress Bars]")
    for pct in [0.0, 0.25, 0.5, 0.75, 1.0]:
        bar = make_progress_bar(160, 16, pct)
        bar.save(os.path.join(OUT_DIR, f"progress_{int(pct*100):03d}.png"))
    bar_red = make_progress_bar(160, 16, 0.5, GANG_RED)
    bar_red.save(os.path.join(OUT_DIR, f"progress_050_red.png"))
    print("  ✓ 6 progress bars")

    # === 图标 ===
    print("\n[Icons]")
    icon_names = ["back","close","confirm","settings","warning","question","info","lock","skull","refresh","play","pause"]
    for name in icon_names:
        icon = make_icon(name)
        icon.save(os.path.join(OUT_DIR, f"icon_{name}.png"))
    print(f"  ✓ {len(icon_names)} icons")

    # === 职业徽章 ===
    print("\n[Role Badges]")
    for role in ["police", "gangster", "undercover", "mole"]:
        badge = make_role_badge(role)
        badge.save(os.path.join(OUT_DIR, f"badge_{role}.png"))
    print("  ✓ 4 badges")

    # === Toggle / Checkbox ===
    print("\n[Controls]")
    for checked in [False, True]:
        toggle = make_toggle(checked)
        toggle.save(os.path.join(OUT_DIR, f"toggle_{'on' if checked else 'off'}.png"))
        cb = make_checkbox(checked)
        cb.save(os.path.join(OUT_DIR, f"checkbox_{'on' if checked else 'off'}.png"))
    print("  ✓ 2 toggles + 2 checkboxes")

    # === Tab buttons ===
    print("\n[Tabs]")
    for i, active in enumerate([True, False, False]):
        tab = make_tab_button(80, f"Tab{i+1}", active)
        tab.save(os.path.join(OUT_DIR, f"tab_{'active' if active else 'inactive'}_{i+1}.png"))
    print("  ✓ 3 tabs")

    # === Portrait frame ===
    portrait = make_portrait_frame()
    portrait.save(os.path.join(OUT_DIR, f"portrait_frame.png"))
    print("  ✓ 1 portrait frame")

    # Summary
    total = len([f for f in os.listdir(OUT_DIR) if f.endswith('.png')])
    print(f"\n{'='*60}")
    print(f"TOTAL: {total} UI components")
    print(f"Output: {OUT_DIR}")
    print(f"{'='*60}")

if __name__ == "__main__":
    main()
