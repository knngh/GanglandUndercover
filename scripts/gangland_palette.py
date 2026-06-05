#!/usr/bin/env python3
"""Gangland Undercover Art Bible 色板常量

基于 Art Bible v1 (2026-06-05) §4 完整配色体系
色彩空间：sRGB, 8-bit per channel
"""

# ============================================================
# §4.1 核心品牌色
# ============================================================
POLICE_BLUE = (45, 111, 186)       # #2d6fba — 警蓝，警察阵营主色
GANG_RED = (192, 57, 43)           # #c0392b — 帮红，黑帮阵营主色
UNDERCOVER_PURPLE = (142, 68, 173) # #8e44ad — 卧底紫，卧底职业主色
MOLE_GREY = (149, 165, 166)        # #95a5a6 — 内鬼灰，Mole职业主色

# ============================================================
# §4.2 职业色板
# ============================================================
PROFESSION_COLORS = {
    "Inspector": {
        "primary": POLICE_BLUE,
        "secondary": (30, 80, 140),      # 深蓝制服
        "accent": (255, 215, 0),          # 金色徽章
        "skin": (255, 213, 178),          # 肤色
    },
    "Enforcer": {
        "primary": GANG_RED,
        "secondary": (30, 30, 30),        # 黑色皮衣
        "accent": (200, 200, 200),        # 银色链子
        "skin": (255, 213, 178),
    },
    "UndercoverAgent": {
        "primary": UNDERCOVER_PURPLE,
        "secondary": (60, 60, 65),        # 深灰兜帽
        "accent": (180, 180, 185),         # 浅灰口袋
        "skin": (255, 213, 178),
    },
    "Medic": {
        "primary": (255, 255, 255),       # 白大褂
        "secondary": (220, 20, 60),        # 红十字
        "accent": (100, 180, 200),         # 浅蓝手套
        "skin": (255, 213, 178),
    },
    "TechExpert": {
        "primary": (26, 158, 170),        # 青绿 (#1a9eaa)
        "secondary": (40, 50, 60),         # 深灰工装
        "accent": (255, 200, 50),          # 黄色耳机
        "skin": (255, 213, 178),
    },
    "Driver": {
        "primary": (40, 60, 80),          # 深蓝工装
        "secondary": (139, 69, 19),        # 棕色棒球帽
        "accent": (50, 50, 50),            # 深色手套
        "skin": (255, 213, 178),
    },
    "Mole": {
        "primary": MOLE_GREY,
        "secondary": (50, 40, 40),         # 暗红内衬（暗示黑帮）
        "accent": (190, 190, 195),         # 浅灰口袋
        "skin": (255, 213, 178),
    },
}

# ============================================================
# §4.3 地图主题色
# ============================================================
MAP_THEMES = {
    "Harbour": {
        "ambient": (26, 28, 44),          # #1a1c2c 港区雨夜深蓝
        "neon_primary": (244, 162, 54),   # #f4a236 霓虹暖黄
        "neon_secondary": (26, 158, 170),  # #1a9eaa 霓虹青绿
        "floor": (30, 32, 40),            # 深灰地面
        "wall": (45, 50, 55),             # 混凝土墙
        "wet_reflection": (40, 50, 100),   # 湿地反光蓝紫
        # PIL HSV 变换参数
        "hue_shift": -0.04,               # -15°
        "sat_factor": 1.20,               # 饱和度+20%
        "bright_factor": 0.90,            # 亮度-10%
    },
    "PoliceStation": {
        "ambient": (20, 20, 32),          # #141420 警署夜间
        "neon_primary": (45, 111, 186),   # 警蓝灯光
        "neon_secondary": (255, 255, 240), # 荧光白
        "floor": (40, 42, 45),            # 瓷砖地面
        "wall": (55, 60, 70),             # 蓝灰墙壁
        "hue_shift": 0.014,               # +5°
        "sat_factor": 0.85,               # 饱和度-15%
        "bright_factor": 0.95,            # 微暗
    },
    "KowloonWalledCity": {
        "ambient": (15, 15, 26),          # #0f0f1a 城寨深夜
        "neon_primary": (255, 50, 100),   # 密集霓虹粉红
        "neon_secondary": (255, 200, 50),  # 霓虹黄
        "floor": (25, 22, 20),            # 暗湿地面
        "wall": (30, 28, 30),             # 斑驳砖墙
        "hue_shift": 0.028,               # +10°
        "sat_factor": 1.30,               # 饱和度+30%
        "bright_factor": 0.85,            # 更暗
    },
}

# ============================================================
# §4.4 状态色
# ============================================================
STATE_COLORS = {
    "idle": (128, 128, 128),            # 中性灰
    "active": (26, 158, 170),           # 青绿辉光 (#1a9eaa)
    "complete": (39, 174, 96),          # 绿色确认 (#27ae60)
    "sabotaged": (192, 57, 43),         # 红色报警 (#c0392b)
    "emergency": (231, 76, 60),         # 应急红灯
}

# ============================================================
# §4.5 UI 色板
# ============================================================
UI_COLORS = {
    "background": (20, 22, 30),          # 深色面板背景
    "panel_border": (60, 65, 75),         # 面板边框
    "text_primary": (230, 235, 240),      # 主文字色
    "text_secondary": (150, 155, 165),    # 次文字色
    "button_default": POLICE_BLUE,        # 默认按钮
    "button_danger": GANG_RED,            # 危险按钮
    "button_disabled": (80, 80, 85),      # 禁用按钮
    "highlight": (255, 215, 0),           # 高亮金色
    "separator": (80, 85, 90),           # 分割线
}

# ============================================================
# §9 光照色
# ============================================================
LIGHTING = {
    "ambient_global": (26, 28, 44),      # 全局环境光
    "neon_lamp": (244, 162, 54),          # 霓虹灯暖黄
    "police_light": (45, 111, 186),       # 警灯蓝
    "emergency_red": (192, 57, 43),       # 应急红
    "task_glow": {
        "idle": STATE_COLORS["idle"],
        "active": (26, 158, 170),
        "complete": (39, 174, 96),
        "sabotaged": (192, 57, 43),
    },
    "crt_glow": (26, 158, 170),          # CRT屏幕辉光
    "monitor_led": (192, 57, 43),        # 监控红LED
    "shadow_alpha": 0.3,                  # 角色阴影不透明度
}

# ============================================================
# 工具函数
# ============================================================
def hex_to_rgb(hex_color):
    """将 #RRGGBB 字符串转换为 (R,G,B) 元组"""
    h = hex_color.lstrip('#')
    return tuple(int(h[i:i+2], 16) for i in (0, 2, 4))

def rgb_to_hex(rgb):
    """将 (R,G,B) 元组转换为 #RRGGBB 字符串"""
    return '#{:02x}{:02x}{:02x}'.format(*rgb)

def get_profession_palette(profession):
    """获取职业完整色板"""
    return PROFESSION_COLORS.get(profession, PROFESSION_COLORS["Inspector"])

def get_map_theme(map_name):
    """获取地图主题色"""
    return MAP_THEMES.get(map_name, MAP_THEMES["Harbour"])

def get_state_color(state):
    """获取状态色"""
    return STATE_COLORS.get(state, STATE_COLORS["idle"])


# 导出全部色板供外部使用
__all__ = [
    'POLICE_BLUE', 'GANG_RED', 'UNDERCOVER_PURPLE', 'MOLE_GREY',
    'PROFESSION_COLORS', 'MAP_THEMES', 'STATE_COLORS', 'UI_COLORS',
    'LIGHTING', 'hex_to_rgb', 'rgb_to_hex',
    'get_profession_palette', 'get_map_theme', 'get_state_color',
]
