#!/usr/bin/env python3
"""
bind_sfx_events.py — 从512 Retro SFX中为AudioManager每个SoundEffect挑选最佳音效

1. 读取 sfx_event_mapping.json
2. 将50个细粒度事件映射到AudioManager的17个SoundEffect枚举
3. 从每个类别中智能挑选1个最佳候选
4. 复制WAV到 Unity Resources/Audio/SFX/
5. 同时复制 Ambience/BGM 到对应位置

使用: python3 bind_sfx_events.py
"""

import json
import os
import shutil
import sys
from pathlib import Path

# ── 路径配置 ──
PROJECT_ROOT = Path("/Users/zhugehao/projects/GanglandUndercover")
SFX_ROOT = PROJECT_ROOT / ".asset_cache/sfx/unpacked/512_8bit_sfx"
SFX_PACK = SFX_ROOT / "The Essential Retro Video Game Sound Effects Collection [512 sounds] By Juhani Junkala"
MAPPING_JSON = PROJECT_ROOT / "Assets/_Project/Audio/sfx_event_mapping.json"

# Unity Resources 目标目录
RESOURCES_AUDIO = PROJECT_ROOT / "Assets/_Project/Resources/Audio"
RESOURCES_SFX = RESOURCES_AUDIO / "SFX"

# ── 事件映射：细粒度事件 → AudioManager SoundEffect ──
EVENT_TO_SFX_ENUM = {
    # UI
    "ui_click":          "UIClick",
    "ui_hover":          "ButtonHover",
    "ui_confirm":        "UIClick",        # 确认声 = click 变体
    "ui_cancel":         "UIClick",        # 取消声 = click 变体
    "ui_pause":          "UIClick",
    "ui_menu_open":      "UIClick",
    "ui_menu_close":     "UIClick",
    "ui_coin":           "UIClick",

    # 脚步
    "footstep_concrete":  "Footstep",
    "footstep_metal":     "Footstep",
    "footstep_wood":      "Footstep",

    # 击杀/死亡
    "enemy_die_human":    "Kill",
    "enemy_die_alien":    "Kill",
    "enemy_die_robot":    "Kill",
    "player_melee":       "Kill",

    # 报告/会议
    "body_report":        "BodyReport",
    "report":             "Report",
    "meeting_start":      "MeetingStart",

    # 投票/淘汰
    "vote_cast":          "VoteCast",
    "player_eliminated":  "PlayerEliminated",

    # 任务
    "task_complete":      "TaskComplete",
    "objective_complete": "TaskComplete",
    "pickup_item":        "TaskComplete",
    "pickup_weapon":      "TaskComplete",
    "pickup_ammo":        "TaskComplete",
    "pickup_health":      "TaskComplete",
    "level_up":           "TaskComplete",

    # 破坏
    "explosion_small":    "Sabotage",
    "explosion_medium":   "Sabotage",
    "explosion_large":    "Sabotage",
    "explosion_chain":    "Sabotage",

    # 胜利/失败
    "victory":            "Victory",
    "defeat":             "Defeat",
    "mission_fail":       "Defeat",

    # 紧急/警报
    "emergency":          "Emergency",
    "alarm_police":       "Emergency",
    "alarm_facility":     "Emergency",

    # 通风管道
    "door_open":          "VentOpen",
    "door_close":         "VentClose",
    "vent_open":          "VentOpen",
    "vent_close":         "VentClose",

    # 武器声音（扩展用，当前AudioManager无对应Slot）
    # 这些映射到最接近的现有槽位或跳过
    "player_shoot_pistol":  None,
    "player_shoot_shotgun": None,
    "player_shoot_rifle":   None,
    "player_shoot_smg":     None,
    "enemy_shoot":          None,
    "weapon_empty":         None,
    "weapon_reload":        None,
    "grenade_whistle":      None,
    "jump":                 None,
    "land":                 None,
    "fall":                 None,
    "climb_ladder":         None,
    "vehicle_enter":        None,
    "amb_damage_player":    None,
    "amb_damage_enemy":     None,
    "amb_impact":           None,
    "amb_weird":            None,
    "portal_teleport":      None,
    "laser_beam":           None,
    "cannon_fire":          None,
}

# ── 智能挑选规则：每个SoundEffect选1个最佳候选 ──
# 优先级顺序: 首选文件名关键字 > 备用首选
BEST_PICK_RULES = {
    # 对于有明确要求的，直接指定文件匹配
    "Kill":         ("Death Screams/Human", ["sfx_deathscream_human1.wav"]),
    "Footstep":     ("Movement/Footsteps", ["sfx_movement_footsteps1.wav"]),
    "UIClick":      ("General Sounds/Buttons", ["sfx_sounds_button1.wav"]),
    "ButtonHover":  ("General Sounds/Simple Bleeps", ["sfx_sounds_bleep1.wav"]),
    "BodyReport":   ("General Sounds/Alarms/Alarms", ["sfx_sounds_alarm1.wav"]),
    "Report":       ("General Sounds/Impacts", ["sfx_sounds_impact1.wav"]),
    "MeetingStart": ("General Sounds/Fanfares", ["sfx_sounds_fanfare1.wav"]),
    "VoteCast":     ("General Sounds/Buttons", ["sfx_sounds_button2.wav"]),
    "PlayerEliminated": ("Death Screams/Human", ["sfx_deathscream_human4.wav"]),
    "TaskComplete": ("General Sounds/Positive Sounds", ["sfx_sounds_positive1.wav"]),
    "Sabotage":     ("Explosions/Medium Length", ["sfx_exp_medium1.wav"]),
    "Victory":      ("General Sounds/Fanfares", ["sfx_sounds_fanfare3.wav"]),
    "Defeat":       ("General Sounds/Negative Sounds", ["sfx_sounds_negative1.wav"]),
    "Emergency":    ("General Sounds/Alarms/Alarms", ["sfx_sounds_alarm3.wav"]),
    "VentOpen":     ("Movement/Opening Doors", ["sfx_movement_dooropen1.wav"]),
    "VentClose":    ("Movement/Opening Doors", ["sfx_movement_doorclose1.wav"]),
}

# ── BGM 映射 ──
# BGM 需要从 100_cc0_sfx 或现有 BGM 目录中挑选
BGM_PICKS = {
    "MainMenu": None,   # 从现有 BGM/ 中选
    "InGame":   None,   # 从现有 BGM/ 中选
    "Meeting":  None,   # 从现有 BGM/ 中选
}


def load_mapping():
    """加载 sfx_event_mapping.json"""
    with open(MAPPING_JSON, 'r') as f:
        return json.load(f)


def find_file_in_pack(subdir: str, preferred_names: list) -> str | None:
    """在512 pack中按子目录+首选文件名查找WAV"""
    search_dir = SFX_PACK / subdir
    if not search_dir.exists():
        return None

    # 第一优先：精确名称匹配
    for name in preferred_names:
        candidate = search_dir / name
        if candidate.exists():
            return str(candidate)

    # 第二优先：同类文件夹中第一个WAV
    wavs = sorted(search_dir.glob("*.wav"))
    if wavs:
        return str(wavs[0])

    # 第三优先：递归搜索
    all_wavs = sorted(search_dir.rglob("*.wav"))
    if all_wavs:
        return str(all_wavs[0])

    return None


def main():
    print("=" * 60)
    print("Gangland Undercover — SFX 事件绑定脚本")
    print("=" * 60)

    # 创建目标目录
    RESOURCES_SFX.mkdir(parents=True, exist_ok=True)
    print(f"\n目标目录: {RESOURCES_SFX}")

    # 统计
    picked = {}
    missing = []
    total_wavs_copied = 0

    # 为每个 AudioManager SoundEffect 挑选最佳文件
    print("\n🎵 挑选 SFX 音效文件:")
    print("-" * 60)

    for sfx_enum, (subdir, preferred) in BEST_PICK_RULES.items():
        filepath = find_file_in_pack(subdir, preferred)
        if filepath:
            src = Path(filepath)
            # 目标文件名 = SoundEffect枚举值
            dst = RESOURCES_SFX / f"SFX_{sfx_enum}.wav"
            shutil.copy2(src, dst)
            size_kb = src.stat().st_size / 1024
            picked[sfx_enum] = str(dst)
            total_wavs_copied += 1
            print(f"  ✅ {sfx_enum:20s} ← {subdir}/{src.name:40s} ({size_kb:.0f} KB)")
        else:
            missing.append(sfx_enum)
            print(f"  ❌ {sfx_enum:20s} ← 未找到文件")

    print(f"\n📊 结果: {len(picked)}/{len(BEST_PICK_RULES)} 个 SFX 已绑定, {len(missing)} 缺失")
    print(f"   共复制 {total_wavs_copied} 个 WAV 到 Resources/Audio/SFX/")

    # ── Ambience ──
    print("\n🌊 环境音 (Ambience):")
    ambience_dir = PROJECT_ROOT / "Assets/_Project/Audio/Ambience"
    ambience_ogg = sorted(ambience_dir.glob("*.ogg"))
    if ambience_ogg:
        # 复制最好的环境音到 Resources
        RESOURCES_AMBIENCE = RESOURCES_AUDIO / "Ambience"
        RESOURCES_AMBIENCE.mkdir(parents=True, exist_ok=True)
        for amb in ambience_ogg[:2]:  # 复制前2个
            dst = RESOURCES_AMBIENCE / amb.name
            shutil.copy2(amb, dst)
            print(f"  ✅ {amb.name} ({amb.stat().st_size/1024:.0f} KB)")

    # ── BGM ──
    print("\n🎼 背景音乐 (BGM):")
    bgm_dir = PROJECT_ROOT / "Assets/_Project/Audio/BGM"
    bgm_files = sorted(bgm_dir.glob("*.ogg")) + sorted(bgm_dir.glob("*.wav"))
    if bgm_files:
        RESOURCES_BGM = RESOURCES_AUDIO / "BGM"
        RESOURCES_BGM.mkdir(parents=True, exist_ok=True)
        # 选3个最好的BGM
        bgm_map = {
            "MainMenu": bgm_files[0] if len(bgm_files) > 0 else None,
            "InGame":   bgm_files[1] if len(bgm_files) > 1 else None,
            "Meeting":  bgm_files[2] if len(bgm_files) > 2 else None,
        }
        for track, src in bgm_map.items():
            if src and src.exists():
                dst = RESOURCES_BGM / f"BGM_{track}{src.suffix}"
                shutil.copy2(src, dst)
                print(f"  ✅ {track:12s} ← {src.name:30s} ({src.stat().st_size/1024:.0f} KB)")
            else:
                print(f"  ❌ {track:12s} ← 未找到文件")

    # ── 生成清单 ──
    manifest_path = PROJECT_ROOT / "Assets/_Project/Audio/SFX_BINDING_MANIFEST.md"
    with open(manifest_path, 'w') as f:
        f.write("# SFX 事件绑定清单\n\n")
        f.write(f"> 生成时间: 2026-06-05\n")
        f.write(f"> 来源: Juhani Junkala 512 Retro SFX (CC0)\n")
        f.write(f"> 已绑定: {len(picked)}/{len(BEST_PICK_RULES)} 个 SFX 事件\n\n")
        f.write("## SoundEffect → WAV 映射\n\n")
        f.write("| SoundEffect | 源文件 | 目标路径 |\n")
        f.write("|-------------|--------|----------|\n")
        for sfx, dst_path in picked.items():
            src_rel = Path(dst_path).name
            f.write(f"| {sfx} | {src_rel} | Resources/Audio/SFX/ |\n")
        if missing:
            f.write(f"\n### ⚠️ 缺失事件 ({len(missing)}个)\n")
            for m in missing:
                f.write(f"- {m}\n")

    print(f"\n📄 清单已生成: {manifest_path}")

    # ── 输出摘要 ──
    print("\n" + "=" * 60)
    print("✅ 完成！下一步:")
    print("  1. 打开 Unity → Assets/_Project/Resources/Audio/SFX/ 会自动导入 WAV")
    print("  2. AudioManager.cs 已修改为自动从 Resources.Load 回退加载")
    print("  3. 如果 Inspector 已赋值 AudioClip，Inspector 优先级更高")
    print("=" * 60)


if __name__ == "__main__":
    main()
