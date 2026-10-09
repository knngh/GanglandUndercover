#!/usr/bin/env python3
"""
bake_minigame_art.py — 批次 5 前置：六类小游戏交互美术全量生成

按 MinigameArtCache.cs 的文件名约定（Assets/_Project/Scripts/Art/MinigameArtCache.cs）
生成 46 张 sprite 到 Resources/Sprites/MiniGames/，Load() 路径即文件名，零代码改动。

风格：noir-clean（与批次 4 UI 同语言）；色板全部出自 gangland_palette.py；
像素级绘制（3x5 像素数字字体，无抗锯齿依赖）；每张写 .meta（Point filter + 固定 GUID）。
"""

import uuid
from pathlib import Path

from PIL import Image, ImageDraw

PROJECT = Path("/Users/zhugehao/projects/GanglandUndercover")
OUT = PROJECT / "Assets/_Project/Resources/Sprites/MiniGames"

# ---- gangland_palette ----
BG = (20, 22, 30)             # UI background
PANEL = (30, 33, 42)          # BG+panel 感（BG 与 border 之间的层次，无新基色：BG 与 border 中间值）
BORDER = (60, 65, 75)         # panel_border
TEXT = (230, 235, 240)        # text_primary
POLICE_BLUE = (45, 111, 186)
POLICE_BLUE_DARK = (30, 80, 140)
ACTIVE = (26, 158, 170)       # state active 青绿
GREEN = (39, 174, 96)         # state complete
RED = (192, 57, 43)           # state sabotaged / GANG_RED
YELLOW = (255, 215, 0)        # highlight
DISABLED = (80, 80, 85)

META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
  seamless: 0
  fadeOut: 0
  borderMipMap: 0
  mipMapsPreserveCoverage: 0
  spriteMode: 1
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spritePixelsPerUnit: 100
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  filterMode: 0
  aniso: 1
  mipBias: 0
  wrapU: 1
  wrapV: 1
  spriteMeshType: 1
  alignment: 0
"""

# 3x5 像素数字字形
DIGITS = {
    "0": ["111", "101", "101", "101", "111"],
    "1": ["010", "110", "010", "010", "111"],
    "2": ["111", "001", "111", "100", "111"],
    "3": ["111", "001", "111", "001", "111"],
    "4": ["101", "101", "111", "001", "001"],
    "5": ["111", "100", "111", "001", "111"],
    "6": ["111", "100", "111", "101", "111"],
    "7": ["111", "001", "001", "001", "001"],
    "8": ["111", "101", "111", "101", "111"],
    "9": ["111", "101", "111", "001", "111"],
    "C": ["111", "100", "100", "100", "111"],
    "E": ["111", "100", "111", "100", "111"],
    "N": ["101", "111", "111", "101", "101"],
    "T": ["111", "010", "010", "010", "010"],
    "R": ["111", "101", "111", "110", "101"],
}


def draw_px_text(d, text, x, y, color, scale=2):
    cx = x
    for ch in text:
        glyph = DIGITS[ch]
        for gy, row in enumerate(glyph):
            for gx, bit in enumerate(row):
                if bit == "1":
                    d.rectangle([cx + gx * scale, y + gy * scale,
                                 cx + gx * scale + scale - 1, y + gy * scale + scale - 1],
                                fill=color)
        cx += 4 * scale
    return cx


def panel_bg(w, h, accent=BORDER):
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle([0, 0, w - 1, h - 1], radius=6, fill=(*BG, 245))
    d.rounded_rectangle([0, 0, w - 1, h - 1], radius=6, outline=(*accent, 255), width=2)
    # 顶部内高光
    for x in range(8, w - 8):
        d.point((x, 3), fill=(*TEXT, 40))
    # 底部内阴影
    for x in range(8, w - 8):
        d.point((x, h - 4), fill=(*(0, 0, 0), 90))
    return im


def node(color, size=16):
    im = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    m = size // 2 - 1
    d.ellipse([2, 2, size - 3, size - 3], fill=(*color, 255))
    d.ellipse([4, 4, size - 5, size - 5], outline=(*TEXT, 90))
    d.point((m - 2, 4), fill=(*TEXT, 160))
    return im


def key(w, h, label=None, color=TEXT, bg_fill=PANEL, border=BORDER, scale=3):
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle([0, 0, w - 1, h - 1], radius=4, fill=(*bg_fill, 255),
                        outline=(*border, 255), width=1)
    for x in range(5, w - 5):
        d.point((x, 2), fill=(*TEXT, 70))
    if label:
        lw = len(label) * 4 * scale
        draw_px_text(d, label, (w - lw) // 2, (h - 5 * scale) // 2, color, scale)
    return im


def save(rel: str, im: Image.Image, written: list):
    p = OUT / rel
    p.parent.mkdir(parents=True, exist_ok=True)
    im.save(p)
    meta = OUT / (rel + ".meta")
    if not meta.exists():
        meta.write_text(META.format(guid=uuid.uuid4().hex))
    written.append(rel)


def main():
    print("=" * 60)
    print("Gangland Undercover — 小游戏美术烘焙（46 张全量）")
    print("=" * 60)
    w = []

    # ── shared ──
    save("shared/panel_bg.png", panel_bg(96, 128), w)
    hdr = Image.new("RGBA", (96, 24), (0, 0, 0, 0))
    d = ImageDraw.Draw(hdr)
    d.rounded_rectangle([0, 0, 95, 23], radius=5, fill=(*POLICE_BLUE, 255),
                        outline=(*POLICE_BLUE_DARK, 255))
    save("shared/panel_header.png", hdr, w)
    for name, col in [("success_badge", GREEN), ("fail_badge", RED)]:
        im = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
        dd = ImageDraw.Draw(im)
        dd.ellipse([2, 2, 29, 29], fill=(*col, 255), outline=(*TEXT, 120))
        # 勾 / 叉
        if name == "success_badge":
            for i in range(7):
                dd.point((9 + i, 17 - i), fill=(*TEXT, 255))
                dd.point((10 + i, 17 - i), fill=(*TEXT, 255))
            for i in range(8):
                dd.point((16 + i, 10 + i), fill=(*TEXT, 255))
                dd.point((17 + i, 10 + i), fill=(*TEXT, 255))
        else:
            for i in range(8):
                dd.point((10 + i, 10 + i), fill=(*TEXT, 255))
                dd.point((22 - i, 10 + i), fill=(*TEXT, 255))
        save(f"shared/{name}.png", im, w)

    # ── wire ──
    save("wire/panel_bg.png", panel_bg(128, 128), w)
    for n, c in [("red", RED), ("blue", POLICE_BLUE), ("green", GREEN), ("yellow", YELLOW)]:
        save(f"wire/node_{n}.png", node(c), w)
    conn = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    d = ImageDraw.Draw(conn)
    d.rounded_rectangle([1, 4, 14, 11], radius=3, fill=(*BORDER, 255))
    d.rectangle([3, 6, 12, 9], fill=(*POLICE_BLUE, 255))
    save("wire/connector.png", conn, w)

    # ── keypad ──
    save("keypad/panel_bg.png", panel_bg(112, 160), w)
    for i in range(10):
        save(f"keypad/key_{i}.png", key(28, 28, str(i), TEXT), w)
    save("keypad/key_clear.png", key(28, 28, "C", RED, bg_fill=(40, 24, 26)), w)
    save("keypad/key_enter.png", key(28, 28, "E", (*TEXT,), bg_fill=POLICE_BLUE_DARK), w)
    lcd = Image.new("RGBA", (88, 28), (0, 0, 0, 0))
    d = ImageDraw.Draw(lcd)
    d.rounded_rectangle([0, 0, 87, 27], radius=3, fill=(*(8, 30, 18), 255),
                        outline=(*BORDER, 255))
    for i in range(6):
        d.point((4 + i * 14, 4), fill=(*ACTIVE, 60))
    save("keypad/lcd_frame.png", lcd, w)

    # ── scan ──
    save("scan/panel_bg.png", panel_bg(128, 128), w)
    fr = Image.new("RGBA", (96, 96), (0, 0, 0, 0))
    d = ImageDraw.Draw(fr)
    for (x0, y0, dx, dy) in [(4, 4, 1, 0), (92, 4, 0, 1), (92, 92, -1, 0), (4, 92, 0, -1)]:
        for i in range(16):
            d.point((x0 + dx * i, y0 + dy * i), fill=(*ACTIVE, 255))
    d.line([8, 8, 88, 8], fill=(*ACTIVE, 120))
    save("scan/scanner_frame.png", fr, w)
    fp = Image.new("RGBA", (40, 48), (0, 0, 0, 0))
    d = ImageDraw.Draw(fp)
    for r, a in [(18, 255), (14, 200), (10, 160), (6, 120)]:
        d.arc([20 - r, 24 - r, 20 + r, 24 + r], start=-60, end=120, fill=(*ACTIVE, a))
        d.arc([20 - r, 24 - r, 20 + r, 24 + r], start=200, end=280, fill=(*ACTIVE, a))
    save("scan/fingerprint.png", fp, w)
    card = Image.new("RGBA", (64, 40), (0, 0, 0, 0))
    d = ImageDraw.Draw(card)
    d.rounded_rectangle([0, 0, 63, 39], radius=4, fill=(*PANEL, 255),
                        outline=(*TEXT, 130))
    d.rectangle([6, 8, 18, 20], fill=(*POLICE_BLUE, 255))  # 照片区
    for i in range(3):
        d.line([24, 12 + i * 5, 56, 12 + i * 5], fill=(*DISABLED, 200))
    d.rectangle([6, 26, 18, 34], fill=(*YELLOW, 255))  # 芯片
    save("scan/id_card.png", card, w)
    sl = Image.new("RGBA", (96, 6), (0, 0, 0, 0))
    d = ImageDraw.Draw(sl)
    for y in range(6):
        d.line([4, y, 92, y], fill=(*ACTIVE, 255 - y * 35))
    save("scan/scan_line.png", sl, w)

    # ── download ──
    save("download/panel_bg.png", panel_bg(112, 96), w)
    srv = Image.new("RGBA", (32, 40), (0, 0, 0, 0))
    d = ImageDraw.Draw(srv)
    d.rounded_rectangle([0, 0, 31, 39], radius=2, fill=(*PANEL, 255),
                        outline=(*BORDER, 255))
    for row in range(4):
        y = 4 + row * 9
        d.rectangle([2, y, 29, y + 6], fill=(*BG, 255))
        led = GREEN if row < 2 else ACTIVE
        d.ellipse([4, y + 2, 6, y + 4], fill=(*led, 255))
        d.line([9, y + 3, 26, y + 3], fill=(*DISABLED, 180))
    save("download/server_icon.png", srv, w)
    pk = Image.new("RGBA", (16, 12), (0, 0, 0, 0))
    d = ImageDraw.Draw(pk)
    d.rectangle([0, 2, 15, 9], fill=(*POLICE_BLUE, 255))
    d.rectangle([3, 0, 12, 3], fill=(*ACTIVE, 255))
    d.rectangle([3, 8, 12, 11], fill=(*ACTIVE, 255))
    save("download/data_packet.png", pk, w)
    pb = Image.new("RGBA", (80, 12), (0, 0, 0, 0))
    d = ImageDraw.Draw(pb)
    d.rounded_rectangle([0, 0, 79, 11], radius=3, fill=(*BG, 255),
                        outline=(*BORDER, 255))
    d.rounded_rectangle([2, 2, 47, 9], radius=2, fill=(*GREEN, 255))
    save("download/progress_bar.png", pb, w)

    # ── memory ──
    back = Image.new("RGBA", (32, 48), (0, 0, 0, 0))
    d = ImageDraw.Draw(back)
    d.rounded_rectangle([0, 0, 31, 47], radius=4, fill=(*POLICE_BLUE_DARK, 255),
                        outline=(*BORDER, 255))
    for y in range(4, 44, 6):
        for x in range(4, 28, 6):
            d.point((x, y), fill=(*POLICE_BLUE, 200))
    save("memory/card_back.png", back, w)
    ICONS = [
        ("diamond", YELLOW), ("triangle", GREEN), ("circle", POLICE_BLUE),
        ("square", ACTIVE), ("star", YELLOW), ("cross", RED),
        ("bars", TEXT), ("ring", ACTIVE),
    ]
    for i, (shape, col) in enumerate(ICONS):
        im = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
        d = ImageDraw.Draw(im)
        d.rounded_rectangle([0, 0, 31, 31], radius=3, fill=(*PANEL, 255),
                             outline=(*BORDER, 255))
        c = (*col, 255)
        if shape == "diamond":
            d.polygon([(16, 6), (26, 16), (16, 26), (6, 16)], fill=c)
        elif shape == "triangle":
            d.polygon([(16, 6), (26, 25), (6, 25)], fill=c)
        elif shape == "circle":
            d.ellipse([7, 7, 24, 24], fill=c)
        elif shape == "square":
            d.rectangle([8, 8, 23, 23], fill=c)
        elif shape == "star":
            d.polygon([(16, 5), (19, 13), (27, 13), (21, 19), (23, 27),
                       (16, 22), (9, 27), (11, 19), (5, 13), (13, 13)], fill=c)
        elif shape == "cross":
            d.rectangle([13, 6, 18, 25], fill=c)
            d.rectangle([6, 13, 25, 18], fill=c)
        elif shape == "bars":
            for k in range(3):
                d.rectangle([8 + k * 6, 8, 11 + k * 6, 23], fill=c)
        elif shape == "ring":
            d.ellipse([7, 7, 24, 24], outline=c, width=3)
        save(f"memory/icon_{i}.png", im, w)

    # ── swipe ──
    slot = Image.new("RGBA", (96, 20), (0, 0, 0, 0))
    d = ImageDraw.Draw(slot)
    d.rounded_rectangle([0, 0, 95, 19], radius=4, fill=(*BG, 255),
                        outline=(*BORDER, 255))
    d.rectangle([8, 7, 88, 12], fill=(*(0, 0, 0), 200))
    save("swipe/card_slot.png", slot, w)
    card = Image.new("RGBA", (48, 32), (0, 0, 0, 0))
    d = ImageDraw.Draw(card)
    d.rounded_rectangle([0, 0, 47, 31], radius=3, fill=(*PANEL, 255),
                        outline=(*TEXT, 140))
    d.rectangle([4, 4, 14, 12], fill=(*POLICE_BLUE, 255))
    d.rectangle([18, 6, 42, 8], fill=(*DISABLED, 220))
    d.rectangle([18, 12, 42, 14], fill=(*DISABLED, 160))
    d.rectangle([4, 24, 10, 28], fill=(*YELLOW, 255))
    save("swipe/card.png", card, w)
    track = Image.new("RGBA", (96, 12), (0, 0, 0, 0))
    d = ImageDraw.Draw(track)
    for y in range(12):
        d.line([0, y, 95, y], fill=(*BORDER, 40 + y * 12))
    for x in range(0, 96, 6):
        d.point((x, 5), fill=(*ACTIVE, 90))
    save("swipe/track.png", track, w)

    print(f"  ✔ 共 {len(w)} 张")
    assert len(w) == 45, f"数量异常: {len(w)} != 45"
    print("OK 45/45（shared 4 / wire 6 / keypad 14 / scan 5 / download 4 / memory 9 / swipe 3）")


if __name__ == "__main__":
    main()
