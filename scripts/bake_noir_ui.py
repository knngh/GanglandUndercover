#!/usr/bin/env python3
"""
bake_noir_ui.py — 批次 4：UI 统一（noir-clean 收敛）

策略：**保留文件名与尺寸，只换像素内容**（与批次 3 角色换皮同思路）。
代码硬编码引用（UIStyle.cs / UIArtCache.cs / OnlineMatchHud.cs / 测试断言）零破坏。

色板合规：全部取自 scripts/gangland_palette.py（UI_COLORS / STATE_COLORS / 品牌色），
无新造基色；高光/暗部直接使用 palette 内 text_primary / background。

新增状态文件（hover/pressed/cooldown）仅落资产，不改 C#（编辑器占用期间不引入
无法编译验证的改动）；代码接线点记录于 output/art_roadmap_20261006.md 批次 4。

幂等：覆盖既有同名 PNG（GUID 不变）；新增文件写入时同步生成 .meta（Point filter）。
"""

import shutil
import uuid
from pathlib import Path

from PIL import Image, ImageDraw

PROJECT_ROOT = Path("/Users/zhugehao/projects/GanglandUndercover")
UI = PROJECT_ROOT / "Assets/_Project/Resources/Sprites/UI/Buttons"

# ---- gangland_palette.py（Art Bible §4/§5）----
BG = (20, 22, 30)            # UI_COLORS.background
BORDER = (60, 65, 75)        # UI_COLORS.panel_border
TEXT = (230, 235, 240)       # UI_COLORS.text_primary（用作高光）
DISABLED = (80, 80, 85)      # UI_COLORS.button_disabled
POLICE_BLUE = (45, 111, 186) # §4.1 品牌警蓝
POLICE_BLUE_DARK = (30, 80, 140)  # PROFESSION_COLORS.Inspector.secondary
ACTIVE = (26, 158, 170)      # STATE_COLORS.active 青绿
HIGHLIGHT = (255, 215, 0)    # UI_COLORS.highlight 金

META_TEMPLATE = """fileFormatVersion: 2
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


def rounded_rect(size, radius, fill, outline=None, outline_w=1):
    im = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle([0, 0, size[0] - 1, size[1] - 1], radius=radius,
                        fill=fill, outline=outline, width=outline_w)
    return im


def square_button(base_fill, border_color, top_glow=None, pressed_inset=False):
    """45x49 noir 方钮：2px 圆角、1px 边框、顶部内侧 1px 高光。"""
    im = rounded_rect((45, 49), 3, base_fill, border_color, 1)
    px = im.load()
    # 顶部内侧高光
    if top_glow:
        for x in range(4, 41):
            px[x, 2] = (*top_glow, 110)
            px[x, 3] = (*top_glow, 40)
    # 底部内侧暗线（用 BG）
    for x in range(4, 41):
        px[x, 46] = (*BG, 160)
    if pressed_inset:
        # 内陷感：外框内侧再加一圈暗框
        d = ImageDraw.Draw(im)
        d.rounded_rectangle([2, 2, 42, 46], radius=2, outline=(*BG, 200), width=1)
    return im


def round_button():
    """64x64 noir 圆钮：深底圆 + 警蓝环 + 顶部高光弧。"""
    im = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.ellipse([1, 1, 62, 62], fill=(*BG, 255), outline=(*POLICE_BLUE, 255), width=2)
    # 顶部高光弧（text_primary 半透明）
    d.arc([5, 5, 58, 58], start=200, end=340, fill=(*TEXT, 130), width=2)
    return im


def main():
    print("=" * 60)
    print("Gangland Undercover — 批次 4 UI 烘焙（noir-clean）")
    print("=" * 60)

    # 备份校验（safety-first）
    backup = PROJECT_ROOT / "output/art_batch4_ui/backup_20261008"
    if not (backup / "UI").exists():
        raise SystemExit("备份缺失，拒绝执行。先运行备份。")

    written = []

    # 1) 覆盖三个 Kenney 方钮（文件名/尺寸不变，引用零破坏）
    # beige = HUD 通用行动钮（UIStyle.cs 首选）
    square_button(BG, BORDER, top_glow=TEXT).save(UI / "buttonSquare_beige.png")
    written.append("buttonSquare_beige.png(覆盖)")
    # blue = HUD 主动作钮（OnlineMatchHud 兜底）→ 警蓝实底
    square_button(POLICE_BLUE, POLICE_BLUE_DARK, top_glow=TEXT).save(UI / "buttonSquare_blue.png")
    written.append("buttonSquare_blue.png(覆盖)")
    # grey = 次要/禁用态 → button_disabled
    square_button(DISABLED, BG, top_glow=None).save(UI / "buttonSquare_grey.png")
    written.append("buttonSquare_grey.png(覆盖)")

    # 2) 圆钮（UIArtCache.ButtonRound）
    round_button().save(UI / "button_round_gloss.png")
    written.append("button_round_gloss.png(覆盖)")

    # 3) 新增状态资产（不接线，供后续代码批次使用）
    new_files = {
        # 悬停：深底 + 青绿描边（STATE_COLORS.active）
        "buttonSquare_hover.png": square_button(BG, ACTIVE, top_glow=TEXT),
        # 按下：内陷暗框
        "buttonSquare_pressed.png": square_button(BG, BORDER, top_glow=None, pressed_inset=True),
        # 冷却：暗底 + 金描边（UI_COLORS.highlight），运行时叠加 fillAmount 遮罩
        "buttonSquare_cooldown.png": square_button((40, 42, 50), HIGHLIGHT, top_glow=None),
    }
    for name, im in new_files.items():
        p = UI / name
        if not p.exists():
            im.save(p)
            meta = UI / (name + ".meta")
            if not meta.exists():
                meta.write_text(META_TEMPLATE.format(guid=uuid.uuid4().hex))
            written.append(f"{name}(新增+meta)")
        else:
            im.save(p)
            written.append(f"{name}(覆盖)")

    for w in written:
        print(f"  ✔ {w}")
    print(f"共 {len(written)} 项。button_noir_clean 保持不动（已是 noir 基准）。")
    print("OK")


if __name__ == "__main__":
    main()
